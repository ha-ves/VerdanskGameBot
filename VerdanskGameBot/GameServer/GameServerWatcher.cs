using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using HtmlAgilityPack;
using Jering.Javascript.NodeJS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using VerdanskGameBot.Commands.GameServer;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;
using Timer = System.Timers.Timer;

namespace VerdanskGameBot.GameServer
{
    internal static class GameServerWatcher
    {
        private static Logger? _logger;

        private static DiscordSocketClient? _botClient;
        private static readonly ConcurrentDictionary<ulong, (string name, CancellationTokenSource cancelToken)> 
            _watchedGuilds = [];
        private static InteractionService? _botInteractSvc;
        private static DbContextOptions<GameServerDb>? _dbOptions;

        private static GamedigGamesJsonDocument? _supportGames;
        private static SteamRedirectPage? _steamRedir;

        private const int _descMaxLength = 200;

        private static Timer? _queueTimer;

        private static readonly SemaphoreSlim 
            _elapsedLock = new(1, 1),
            _startStopLock = new(1, 1);
        private static bool _isStarted = false;
        private static CancellationTokenSource? _stopToken;
        private static TimeSpan _queryTimeout;
        private static Stream? _queryStream;
        private static readonly ConcurrentDictionary<ulong, ConcurrentDictionary<ulong, (Task task, DateTimeOffset since)>>
            _pendingQuery = [];

        /// <summary>
        /// Starts the Game Server Watcher asynchronously.
        /// <br/><br/>
        /// Required services:
        /// <list type="bullet">
        ///     <item><see cref="DiscordSocketClient"/> - <description>Used for Discord server interaction and message handling</description></item>
        ///     <item><see cref="InteractionService"/> - <description>Handles Discord bot command interactions</description></item>
        ///     <item><see cref="DbContextOptions"/>&lt;<see cref="GameServerDb"/>&gt; - <description>Database configuration for game server storage</description></item>
        ///     <item><see cref="LogFactory"/> - <description>Optional service for logging functionality</description></item>
        /// </list>
        /// </summary>
        /// <param name="svcs">The <see cref="IServiceProvider"/> used to resolve required dependencies.</param>
        /// <param name="token">An optional <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task"/> that represents the asynchronous operation.</returns>
        public static async Task StartAsync(IServiceProvider svcs,
            TimeSpan interval, CancellationToken token = default)
        {
            await _startStopLock.WaitAsync(token);
            await _elapsedLock.WaitAsync(token);

            if (_isStarted)
            {
                _logger?.Trace("Game Server Watcher is static instance and already started.");
                return;
            }

            _isStarted = true;

            _logger?.Trace("Starting Game Server Watcher...");

            _stopToken = new();

            try
            {
                _botClient = svcs.GetRequiredService<DiscordSocketClient>();
                _botInteractSvc = svcs.GetRequiredService<InteractionService>();
                _dbOptions = svcs.GetRequiredService<DbContextOptions<GameServerDb>>();

                _queryTimeout = TimeSpan.FromSeconds(5);
                _ = GameBotApp.TryGetRes("query.js", out _queryStream);

                _supportGames = svcs.GetRequiredService<GamedigGamesJsonDocument>();
                _steamRedir = svcs.GetRequiredService<SteamRedirectPage>();

                _logger = svcs.GetService<LogFactory>()?.GetCurrentClassLogger();

                var interactModule = await _botInteractSvc.AddModuleAsync<GameServerInteraction>(svcs);
                GameServerInteraction.RegisterModals(_botInteractSvc);

                var inittasks = _botClient.Guilds.AsParallel().WithCancellation(token)
                    .Select(async guild =>
                    {
                        token.ThrowIfCancellationRequested();

                        _logger?.ConditionalTrace($"Initializing game server watcher for guild '{guild.Id}' ({guild.Name})...");

                        await EnsureDatabaseAsync(guild.Id, _dbOptions, token);

                        _watchedGuilds[guild.Id] =
                            (guild.Name, CancellationTokenSource.CreateLinkedTokenSource(_stopToken.Token));
                        _pendingQuery[guild.Id] = [];

                        await _botInteractSvc!.AddModulesToGuildAsync(guild, false, interactModule);
                    });

                await Task.WhenAll(inittasks);

                _logger?.Info($"Watching {_watchedGuilds.Count} discord servers.");

                _queueTimer = new Timer(interval); // 5 minutes
                _queueTimer.Elapsed += QueueTimer_Elapsed;
                _queueTimer.Start();

                _logger?.Info("Game Server Watcher started.");
            }
            finally 
            {
                _elapsedLock.Release();

                await Task.Run(() => QueueTimer_Elapsed(_botClient, new ElapsedEventArgs(DateTime.Now)), token);

                _startStopLock.Release();
            }
        }

        public static async Task StopAsync(CancellationToken token = default)
        {
            await _startStopLock.WaitAsync(token);
            await _elapsedLock.WaitAsync(token);

            if (!_isStarted)
            {
                _logger?.Trace("Game Server Watcher is not started yet.");
                _startStopLock.Release();
                return;
            }

            _logger?.Info("Stopping Game Server Watcher...");

            _queueTimer?.Stop();
            _queueTimer?.Dispose();
            _queueTimer = null;

            _stopToken?.Cancel();

            var wait = await Task.WhenAny(Task.WhenAll(_pendingQuery.SelectMany(kvp => kvp.Value.Select(k => k.Value.task))),
                                            Task.Delay(Timeout.Infinite, token));

            _isStarted = false;

            _stopToken?.Dispose();
            _stopToken = null;

            _pendingQuery.Clear();
            _watchedGuilds.Clear();
            _botClient = null;

            _logger?.Info("Game Server Watcher stopped.");
            _logger = null;

            _elapsedLock.Release();
            _startStopLock.Release();
        }

        private static void QueueTimer_Elapsed(object? sender, ElapsedEventArgs e)
        {
            var locked = !_elapsedLock.Wait(0);

            if (locked)
            {
                _logger?.ConditionalTrace("Game server watcher queue-er is already running, skipping this invocation.");
                return;
            }

            if (_watchedGuilds is null)
            {
                _logger!.Debug("No guilds available, skipping game server check.");
                _elapsedLock.Release();
                return;
            }

            if (_stopToken?.IsCancellationRequested ?? false)
            {
                _logger!.Debug("Game server watcher is stopping, skipping game server check.");
                _elapsedLock.Release();
                return;
            }

            _logger!.Trace("Checking game servers ...");

            var now = e.SignalTime;

            var pretoken = _watchedGuilds.AsParallel();

            if (_stopToken is not null)
                pretoken = pretoken.WithCancellation(_stopToken.Token);

            var needQuerying = pretoken.SelectMany(kvp =>
                {
                    var guildCancel = kvp.Value.cancelToken;
                    if (guildCancel.IsCancellationRequested)
                    {
                        _logger?.ConditionalTrace($"Guild '{kvp.Key:x2}' is no longer being watched, skipping.");
                        return [];
                    }

                    using var readDb = new GameServerReadOnlyDb(kvp.Key, _dbOptions);
                    var res = readDb.GameServers?
                        .Where(gs => (gs.NextUpdateAt ?? 
                            ((gs.LastUpdate ?? gs.LastModifiedSince) + gs.UpdateInterval) ?? gs.AddedSince) 
                            <= now)
                        .Select(gs => new { gs.ServerId, guildId = kvp.Key, cancelToken = guildCancel.Token })
                        .ToArray() ?? []
                        ;

                    if (guildCancel.IsCancellationRequested)
                    {
                        _logger?.ConditionalTrace($"Guild '{kvp.Key:x2}' is no longer being watched, skipping.");
                        return [];
                    }

                    return res;
                });

            var pendings = _pendingQuery.SelectMany(k => k.Value.Select(kvp => (kvp.Key, k.Key)));
            var toQuery = needQuerying.ExceptBy(pendings, a => ((ulong)a.ServerId, a.guildId)).ToList();

            toQuery.AsParallel().ForAll(a =>
                {
                    _pendingQuery[a.guildId][(ulong)a.ServerId] = (UpdateGameServerAsync((ulong)a.ServerId, a.guildId, a.cancelToken), now);
                });

            _logger.Info($"Queued {toQuery.Count} game servers across {toQuery.CountBy(a => a.guildId).Count()} discord servers for querying.");

            _elapsedLock.Release();
        }

        private static async Task UpdateGameServerAsync(ulong gsId, ulong guild, CancellationToken token = default)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                GameServerModel querygs;
                using var editdb = new GameServerDb(guild, _dbOptions);
                using var transact = await editdb.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted, token);
                
                try
                {
                    querygs = await editdb.GameServers.FirstOrDefaultAsync(gs => (ulong)gs.ServerId == gsId, token)
                                    ?? throw new KeyNotFoundException(
                                            $"Game server with ID '{gsId}' not found in discord server '{guild:x2}'.");
                }
                catch (Exception e)
                {
                    var ex = DbNotSupported(guild, e);
                    throw ex;
                }

                var botReq = RequestOptions.Default;
                botReq.CancelToken = token;

                token.ThrowIfCancellationRequested();

                if (await _botClient!.GetChannelAsync(querygs.ChannelId, botReq) is not SocketTextChannel ch)
                    throw new InvalidOperationException(
                        $"Channel with ID '{querygs.ChannelId}' not found in discord server '{guild:x2}'.");

                token.ThrowIfCancellationRequested();

                var queriedServer = await QueryGameServerAsync(querygs);

                token.ThrowIfCancellationRequested();

                queriedServer.NextUpdateAt = null;
                editdb.Entry(querygs).CurrentValues.SetValues(queriedServer);
                
                editdb.SetUserContext(_botClient.CurrentUser.Id, 
                    _botClient.CurrentUser.FormatUserContext("Watcher: Automatic Update"));
                await editdb.SaveChangesAsync(token);

                token.ThrowIfCancellationRequested();

                await transact.CommitAsync(token);

                var msg = await ch.ModifyMessageAsync(queriedServer.MessageId, options: botReq,
                    func: msgprop => msgprop.Embed = new GameServerEmbedBuilder(queriedServer).Build());

                _pendingQuery[guild].Remove(gsId, out _);
            }
            catch (OperationCanceledException )
            {
                _logger?.Trace($"Update for game server [{gsId}] in '{guild:x2}' was cancelled.");
            }
            catch (Exception ex)
            {
                _logger?.Trace(ex, $"Error querying game server [{gsId}] in '{guild:x2}'.");
            }
        }

        private static async Task EnsureDatabaseAsync(ulong guild, DbContextOptions<GameServerDb> dbOptions, 
            CancellationToken token = default)
        {
            _logger?.ConditionalTrace($"Checking database for discord server '{guild:x2}'...");

            try
            {
                using var db = new GameServerDb(guild, dbOptions);

                await db.Database.MigrateAsync(token);

                _logger?.ConditionalTrace($"Discord server '{guild:x2}' Database available.");
            }
            catch (Exception ex)
            {
                var e = DbNotSupported(guild, ex);
                _logger?.Error(e, $"Failed to ensure database for discord server '{guild:x2}'.");
                throw e;
            }
        }

        private static DbNotSupportedException DbNotSupported(ulong guildId, Exception? ex = null)
            => new(guildId, ex);

        /// <summary>
        /// Query the game server using <see href="https://github.com/gamedig/node-gamedig">node-gamedig</see>.
        /// </summary>
        /// <param name="gameServer">The <see cref="GameServerModel"/> to query.</param>
        /// <returns></returns>
        private static async Task<GameServerModel> QueryGameServerAsync(GameServerModel gameServer)
        {
            var attempts = Math.Min(3, Math.Max(1, Math.Floor(gameServer.UpdateInterval / _queryTimeout)));

            JsonDocument? gsjs = null;
            try
            {
                var js = await StaticNodeJSService.InvokeFromStreamAsync<string>(() => _queryStream!,
                    cacheIdentifier: "query.js", args: [
                        gameServer.IP!.ToString(),
                    gameServer.GameType,
                    gameServer.GamePort,
                    /*maxAttempts*/ attempts,
                    /*timeoutAllAttempts*/ (int)_queryTimeout.TotalMilliseconds
                    ]);

                gsjs = JsonDocument.Parse(js!);
            }
            catch (Exception) { }

            var now = DateTimeOffset.Now;
            gameServer.IsOnline = false;
            gameServer.LastUpdate = now;

            if (gsjs is not null && !gsjs.RootElement.TryGetProperty("error", out var err))
            {
                gameServer.IsOnline = true;
                gameServer.LastOnline = now;

                if (_supportGames!.RootElement.GetProperty(gameServer.GameType!).GetProperty("options")
                    .GetProperty("protocol").GetString()!.Equals("valve", StringComparison.OrdinalIgnoreCase))
                {
                    var uriBuilder = new UriBuilder(_steamRedir!.Url);
                    var query = HttpUtility.ParseQueryString(uriBuilder.Query);
                    query["ip"] = gameServer.IP!.ToString();
                    query["port"] = gameServer.GamePort.ToString();
                    uriBuilder.Query = query.ToString();

                    gameServer.GameLink = $"[Click to join via Steam]({uriBuilder.Uri})";
                    gameServer.GameLinkRemarks = $"*(Link hosted on GitHub.{Environment.NewLine}[GitHub Privacy Statement.]"
                        + $"(https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement))*";
                }

                gameServer.DisplayName = gsjs.RootElement.GetProperty("name").GetString();
                gameServer.Players = (byte)gsjs.RootElement.GetProperty("players").GetArrayLength();
                gameServer.MaxPlayers = gsjs.RootElement.GetProperty("maxplayers").GetByte();

                if (gsjs.RootElement.TryGetProperty("raw", out var RawProperties))
                    using (var http = new HttpClient())
                    {
                        var appid = RawProperties.GetProperty("appId").GetInt32();

                        gameServer.ImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appid}/header.jpg";
                        var page = $"https://store.steampowered.com/app/{appid}/";

                        var doc = new HtmlWeb().Load(page).DocumentNode;
                        var descfull = doc.SelectSingleNode("//div[@class='game_description_snippet']")
                                            .InnerText.FlattenNormalized(_descMaxLength, true, "...");

                        gameServer.Description = !string.IsNullOrWhiteSpace(descfull) ? 
                            descfull + Environment.NewLine + page
                            : "*No description available.*";
                    }
            }

            return gameServer;
        }

        public static Task TriggerAsync()
            => Task.Run(() => QueueTimer_Elapsed(typeof(GameServerInteraction), 
                new ElapsedEventArgs(DateTime.Now)));

        internal static async Task AddGuildAsync(IGuild guild, CancellationToken token = default)
        {
            await _startStopLock.WaitAsync(token);
            await _elapsedLock.WaitAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                _logger?.Trace($"Adding discord server '{guild.Id:x2}' to game server watcher.");

                if (_watchedGuilds.TryGetValue(guild.Id, out var exist))
                {
                    _logger?.Trace($"Discord server '{guild.Id:x2}' is already being watched.");

                    if (exist.name.Equals(guild.Name, StringComparison.Ordinal)) return;

                    _logger?.Warn($"Guild '{guild.Id:x2}' name mismatch: '{exist.name}' vs '{guild.Name}', will use latter.");

                    _watchedGuilds[guild.Id] = (guild.Name, _watchedGuilds[guild.Id].cancelToken);
                }
                else
                {
                    await EnsureDatabaseAsync(guild.Id, _dbOptions!, token);

                    _watchedGuilds[guild.Id] = 
                        (guild.Name, CancellationTokenSource.CreateLinkedTokenSource(_stopToken?.Token ?? CancellationToken.None));
                }

                _logger?.Info($"Added '{guild.Name}' to game server watcher.");
            }
            finally 
            {
                _elapsedLock.Release();

                await Task.Run(() => QueueTimer_Elapsed(_botClient, new ElapsedEventArgs(DateTime.Now)), token);

                _startStopLock.Release();
            }
}

        internal static async Task RemoveGuildAsync(IGuild guild, CancellationToken token = default)
        {
            await _startStopLock.WaitAsync(token);
            await _elapsedLock.WaitAsync(token);

            try
            {
                token.ThrowIfCancellationRequested();

                if (!_watchedGuilds.ContainsKey(guild.Id))
                {
                    _logger?.Trace($"Discord server '{guild.Id:x2}' is not being watched.");
                    return;
                }

                _logger?.Trace($"Removing discord server '{guild.Id:x2}' from game server watcher.");

                _watchedGuilds.Remove(guild.Id, out var removedGuild);

                var tasks = _pendingQuery[guild.Id].Select(kvp =>
                    {
                        _pendingQuery[guild.Id].Remove(kvp.Key, out var value);

                        _logger?.ConditionalTrace($"Cancelling pending query task (for {DateTime.Now - value.since}) of game server '{kvp.Key}' in discord server ({guild.Id:x2}'.");

                        return value.task;
                    });

                removedGuild.cancelToken.Cancel();

                var wait = await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(Timeout.Infinite, token));

                _logger?.Debug($"{tasks.Count()} pending game server queries for discord server '{guild.Id:x2}' have been cancelled.");

                _logger?.Info($"Removed '{guild.Name}' from game server watcher.");
            }
            finally
            {
                _elapsedLock.Release();

                await Task.Run(() => QueueTimer_Elapsed(_botClient, new ElapsedEventArgs(DateTime.Now)), token);

                _startStopLock.Release();
            }
        }
    }
}
