using Discord;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Jering.Javascript.NodeJS;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using VerdanskGameBot.Commands.GameServer;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.MigrationHandler;

namespace VerdanskGameBot
{
    public class GameBotApp
    {
        public string? Version { get; private set; }
        
        private readonly LogFactory? _logFactory;
        private readonly Logger? _logger;
        private readonly bool _traceEnabled;
        private readonly string[]? _startArgs;

        private Logger? _botLogger;

        private IConfigurationRoot? _botCfg;
        private DiscordSocketClient? _botClient;
        private InteractionService? _botInteractSvc;

        private Timer? _clearIntegrationStateTimer;
        private bool _isFirstReady = true;

        private DbContextOptions<GameServerDb>? _gsDbOpts;

        private ServiceProvider? _appSvc;
        private JsonDocument? _gamedigGames;

        public GameBotApp() { }

        public GameBotApp(LogFactory logFactory, bool traceLog = false, string[]? args = null)
        {
            _logFactory = logFactory;
            _logger = logFactory.GetCurrentClassLogger();
            _traceEnabled = traceLog;
            _startArgs = args;
        }

        public async Task StartAsync(CancellationToken cancelToken = default)
        {
            #region Pre-Init Debugging

#if DEBUG

#endif

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Banner

            var startstr = new StringBuilder();
            startstr.Append("=====[ Starting Verdansk GameBot ");

            var assembly = GetType().Assembly;
            var verinfo = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion;

            var revplus = verinfo.LastIndexOf('+');
            if (revplus < 0)
                startstr.Append(verinfo);
            else
            {
                startstr.Append(verinfo[..revplus]);
            }
            startstr.Append(" ]=====");

            _logger?.Info(startstr.ToString());

            #endregion

            #region Loading Bot Configuration

            _logger?.Trace("Loading Configuration...");

            var appSvc = new ServiceCollection();

            try
            {
                _ = TryGetRes("BotConfig.json", out var botcfg);

                if (!File.Exists("BotConfig.json"))
                {
                    _logger?.Trace("No BotConfig.json file found. Creating one...");

                    using var file = File.Create("BotConfig.json");
                    await botcfg!.CopyToAsync(file, cancelToken);

                    _logger?.Trace("Created \"BotConfig.json\" file with default values.");
                }

                _botCfg = new ConfigurationBuilder()
                    .AddJsonFile("BotConfig.json")
                    .AddEnvironmentVariables()
                    .Build();

                botcfg!.Seek(0, SeekOrigin.Begin);
                var defaultCfg = new ConfigurationBuilder()
                    .AddJsonStream(botcfg!)
                    .Build();

                var missing = defaultCfg.GetChildren().ExceptBy(_botCfg.GetChildren().Select(m => m.Key), c => c.Key)
                                .Select(c => $"[{c.Key}](default: \"{c.Value}\")");

                if (missing.Any())
                {
                    var err = new KeyNotFoundException($"Configuration missing mandatory key(s):" +
                        Environment.NewLine + string.Join(Environment.NewLine, missing));
                    throw err;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ex.Data[nameof(ExitCode)] = ExitCode.BotConfigInvalid;
                _logger?.Error(ex, "Configuration is not valid. Please check if Environment Variable is overriding. " +
                    "(Please delete \"BotConfig.json\" if you want to reset.)");
                throw;
            }
            _logger?.Trace("Loaded Configuration.");

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Loading NodeJS

            _logger?.Trace("Loading NodeJS ...");
            try
            {
                var nodejsvc = new ServiceCollection();

                if (_logFactory is not null && _traceEnabled)
                    nodejsvc.AddNLog(_logFactory, $"[{nameof(StaticNodeJSService)}] >> ");

                nodejsvc.AddNodeJS();

                var packagedNodeJSPath = assembly.GetCustomAttribute<AssemblyNodeJSAttribute>()?.Path;
                if (!string.IsNullOrWhiteSpace(packagedNodeJSPath))
                    nodejsvc.Configure<NodeJSProcessOptions>(opt =>
                        opt.ExecutablePath = Path.GetRelativePath(
                                Directory.GetCurrentDirectory(), packagedNodeJSPath));
                
                StaticNodeJSService.SetServices(nodejsvc);

                var nodejs = await StaticNodeJSService.InvokeFromStringAsync<string>(
                    @"module.exports = (callback) => callback(null, process.versions);",
                    cancellationToken: cancelToken);
                _logger?.Info("Using NodeJS version " 
                    + JsonDocument.Parse(nodejs ?? string.Empty).RootElement.GetProperty("node"));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exc)
            {
                exc.Data[nameof(ExitCode)] = ExitCode.NodeJSNotAvail;
                _logger?.Fatal(exc, "Can not start because NodeJS is not available. " +
                    "Please get from official release. (https://nodejs.org/en/download/current/)");
                throw;
            }
            _logger?.Trace("Done loading NodeJS.");

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Loading gamedig

            _logger?.Trace("Loading gamedig ...");

            var gamedigver = await EnsureGamedigInstallAsync(cancelToken);

            _logger?.Info($"Using node-gamedig version {gamedigver}");

            try
            {
                _gamedigGames = await StaticNodeJSService.InvokeFromStringAsync<JsonDocument>(
                    @"module.exports = (callback) => callback(null, require('gamedig').games);",
                    cancellationToken: cancelToken);

                appSvc.AddSingleton(new GamedigGamesJsonDocument(_gamedigGames!));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ex.Data[nameof(ExitCode)] = ExitCode.GamedigMissing;
                _logger?.Error(ex, "Failed to load { gamedig } games.");
                throw;
            }

            _logger?.Trace("Done loading gamedig.");

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Loading Discord Bot

            _logger?.Trace("Loading Discord Bot ...");

            var botlog = _logger?.IsTraceEnabled ?? false ? LogSeverity.Debug
                        : _logger?.IsDebugEnabled ?? false ? LogSeverity.Verbose
                        : LogSeverity.Info;

            _botClient = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = botlog,
                DefaultRetryMode = RetryMode.RetryTimeouts,
                GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents),
                MaxWaitBetweenGuildAvailablesBeforeReady = (int)TimeSpan.FromSeconds(60).TotalMilliseconds
            });

            _botInteractSvc = new InteractionService(_botClient, new InteractionServiceConfig()
            {
                LogLevel = botlog, DefaultRunMode = RunMode.Sync, ThrowOnError = true,
                InteractionCustomIdDelimiters = [GameServerInteraction.CustomIdSeparator, ','],
            });
            
            if (_logger is not null && _logFactory is not null)
            {
                _botLogger = _logFactory.GetLogger(_botClient.GetType().Name);

                _botInteractSvc.Log += msg => BotLogAsync(_botLogger, msg);

                _botInteractSvc.InteractionExecuted += async (a, b, c) =>
                {
                    if (!c.IsSuccess && a is not null) await BotLogAsync(_botLogger,
                        new LogMessage(LogSeverity.Error, a.Module.Name, 
                        $"{a.MethodName} Failed. {c.Error}{c.ErrorReason}",
                        (c as ExecuteResult?)?.Exception?.InnerException));
                };

                _botClient.Log += msg => BotLogAsync(_botLogger, msg);
                _botClient.LoggedIn += () => BotLogAsync(_botLogger,
                    new LogMessage(LogSeverity.Info, nameof(GameBotApp), "LoggedIn."));
            }

            _botClient.Ready += () => Task.Run(() =>
            {
                if (_isFirstReady)
                {
                    OnBotReady().ConfigureAwait(false);
                    _isFirstReady = false;
                }
            });

            appSvc.AddSingleton(_botClient)
                  .AddSingleton(_botInteractSvc);

            _logger?.Trace("Done loading Discord Bot.");

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Loading Database

            _logger?.Trace("Loading Database ...");

            try
            {
                var connstr = _botCfg!["ConnectionString"]!;
                var dbprovider = Enum.Parse<DbTypes>(_botCfg["DbProvider"]!, true);

                _logger?.Debug($"Using {Enum.GetName(dbprovider)} database");

                var dbconfig = new DbContextOptionsBuilder<GameServerDb>();
                var dbSvc = new ServiceCollection().AddMigrationServices();

                if (_logFactory is not null && _logger is not null && _traceEnabled)
                {
                    dbconfig.EnableDetailedErrors();
                    Action<ILoggingBuilder> configure = b => b
                        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
#if DEBUG
                    dbconfig.EnableSensitiveDataLogging();
                    configure = b => b
                        .SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
#endif
                    //dbSvc.AddNLog(_logFactory, $"[{nameof(GameServerDb)}] <> ", configure);
                }

                switch (dbprovider)
                {
                    case DbTypes.SQLite:
                        dbconfig.UseSqlite(connstr);
                        dbSvc.AddEntityFrameworkSqlite();
                        break;
                    case DbTypes.MySql:
                        var serverVersion = ServerVersion.AutoDetect(connstr);
                        dbconfig.UseMySql(connstr, serverVersion);
                        dbSvc.AddEntityFrameworkMySql();
                        break;
                    case DbTypes.SqlServer:
                        dbconfig.UseSqlServer(connstr);
                        dbSvc.AddEntityFrameworkSqlServer();
                        break;
                    case DbTypes.PostgreSql:
                        dbconfig.UseNpgsql(connstr);
                        dbSvc.AddEntityFrameworkNpgsql();
                        break;
                    default:
                        throw new NotSupportedException($"Database provider {dbprovider} is not supported.");
                }

                _gsDbOpts = dbconfig.UseInternalServiceProvider(dbSvc.BuildServiceProvider()).Options;

                // Validate connection before storing options
                await GameServerDb.CanConnectAsync(_gsDbOpts);
                _logger?.Debug("Database connection validated successfully");

                appSvc.AddSingleton(_gsDbOpts!);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e)
            {
                e.Data[nameof(ExitCode)] = ExitCode.DbConfigInvalid;
                _logger?.Error(e, "Database configuration failed. Please check and provide valid database configuration.");
                throw;
            }

            _logger?.Trace("Done loading Database.");

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Loading GameServer settings

            var steamRedir = new SteamRedirectPage(new Uri(_botCfg!["SteamRedirectPage"]!));

            appSvc.AddSingleton(steamRedir);

            #endregion

            cancelToken.ThrowIfCancellationRequested();

            #region Pre-Start Debugging

#if DEBUG

#endif

            #endregion

            #region Starting Discord Bot

            _logger?.Info("Starting Discord Bot ...");

            if (_logFactory is not null) appSvc.AddSingleton(_logFactory);

            _appSvc = appSvc.BuildServiceProvider();

            _botClient.InteractionCreated += (x) =>
            {
                _logger?.ConditionalTrace($"Received interaction {x.Type} ({x.Id}) from ({x.User.Id})");
                var ctx = new SocketInteractionContext(_botClient, x);
                return Task.Run(() => _botInteractSvc.ExecuteCommandAsync(ctx, _appSvc).ConfigureAwait(false));
            };

            var token = _botCfg["BotToken"];
            try {
                TokenUtils.ValidateToken(TokenType.Bot, token);
            }
            catch (Exception ex)
            {
                ex.Data[nameof(ExitCode)] = ExitCode.BotTokenInvalid;
                _logger?.Error(ex, "Bot token is not valid. Please check your BotToken in configuration.");
                throw;
            }

            await _botClient.LoginAsync(TokenType.Bot, token);
            cancelToken.ThrowIfCancellationRequested();

            var oneGuild = _botCfg["GuildId"];
            var guildIdOK = ulong.TryParse(oneGuild, out var og) && og > 0;
            var guild = guildIdOK ? await _botClient.Rest.GetGuildAsync(og) : null;
            if ((_startArgs?.Contains("--one-guild") ?? false) && (!guildIdOK || guild is null))
            {
                var ex = new ConfigurationErrorsException("Bot is started with --one-guild but \"GuildId\" value is invalid or guild not found. " +
                    "Please check GuildId in configuration.");
                ex.Data[nameof(ExitCode)] = ExitCode.BotConfigInvalid;
                _logger?.Error(ex, "");
                throw ex;
            }

#if DEBUG && DEV
            await _botClient.SetCustomStatusAsync("Running pre-release version");
            cancelToken.ThrowIfCancellationRequested();
#endif
            await _botClient.StartAsync();

            #endregion
        }

        private async Task OnBotReady()
        {
            #region  OnBotReady Debugging

#if DEBUG

#endif

            #endregion

            _botClient!.GuildAvailable += (guild) =>
            {
                _logger?.Debug($"Guild {guild.Name} ({guild.Id}) is available.");
                return Task.Run(() => GameServerWatcher.AddGuildAsync(guild).ConfigureAwait(false));
            };
            _botClient!.GuildUnavailable += (guild) => 
            {
                _logger?.Debug($"Guild {guild.Name} ({guild.Id}) is unavailable.");
                 return Task.Run(() => GameServerWatcher.RemoveGuildAsync(guild).ConfigureAwait(false));
            };

            await GameServerWatcher.StartAsync(_appSvc!, TimeSpan.FromMinutes(5));

            if (_logFactory is not null) GameServerInteraction.RegisterLogger(_logFactory);

            _clearIntegrationStateTimer = new Timer(
                _ =>
                {
                    _logger?.Info("Clearing in-memory state...");
                    var removed = GameServerInteraction.RemoveStates(a => 
                        a.since + TimeSpan.FromMinutes(15) <= DateTime.Now);
                    _logger?.Info($"Cleared {removed?.Length ?? 0} stale states.");
                },
                null, TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));

            _logger?.Info("=====[ Verdansk GameBot Started ]=====");
        }

        public async Task StopAsync()
        {
            _logger?.Info("=== Stopping Verdansk GameBot ===");

            await GameServerWatcher.StopAsync();

            StaticNodeJSService.DisposeServiceProvider();

            if (_botInteractSvc is not null)
                await Task.Run(_botInteractSvc.Dispose);

            if (_botClient != null)
            {
                await _botClient.StopAsync();
                await _botClient.DisposeAsync();
            }

            _logger?.Info("=====[ Verdansk GameBot Stopped ]=====");
        }

        private async Task<string?> EnsureGamedigInstallAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            var npmPath = Environment.GetEnvironmentVariable("NPM_PATH")
#if BUILD_WINDOWS
                ?? Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, "npm.cmd")).FirstOrDefault(File.Exists);
#else
                ?? Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator)
                    .Select(p => Path.Combine(p, "npm")).FirstOrDefault(File.Exists);
#endif

            var modulePaths = await StaticNodeJSService.InvokeFromStringAsync<IEnumerable<string>>(
                @"module.exports = (callback) => callback(null, module.paths);", cancellationToken: token);

            token.ThrowIfCancellationRequested();

            var gamedigPkgPath = modulePaths is null ? null : await modulePaths.ToAsyncEnumerable()
                                    .Select(p => Path.Combine(p, "gamedig", "package.json"))
                                    .FirstOrDefaultAsync(File.Exists, token);

            var newInstall = false;
            if (gamedigPkgPath is null)
            {
                token.ThrowIfCancellationRequested();

                _logger?.Debug("{ node-gamedig } not available, trying to install...");

                try
                {
                    if (npmPath is null)
                    {
                        _logger?.Error("NPM not found in PATH. Please ensure NPM is installed and available in PATH.");
                        throw new InvalidOperationException("NPM not found in PATH.");
                    }

                    await RunProcessAsync(npmPath, "install gamedig", _logFactory?.GetLogger("NPM"), token);

                    newInstall = true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    ex.Data[nameof(ExitCode)] = ExitCode.GamedigMissing;
                    _logger?.Error(ex, "Failed to install { gamedig }.");
                    throw;
                }

                return null;
            }

            token.ThrowIfCancellationRequested();

            using var doc = await JsonDocument.ParseAsync(File.OpenRead(gamedigPkgPath), cancellationToken: token);
            var ver = doc.RootElement.GetProperty("version").GetString();

            _logger?.Trace($"Current gamedig version: {ver}.");

            if (!newInstall)
            {
                token.ThrowIfCancellationRequested();

                _logger?.Trace("Checking gamedig update...");

                try
                {
                    if (npmPath is null)
                    {
                        _logger?.Warn("NPM not found in PATH. Please ensure NPM is installed and available in PATH.");
                        throw new InvalidOperationException("NPM not found in PATH.");
                    }

                    _logger?.Trace($"Using NPM at {npmPath}");

                    await RunProcessAsync(npmPath, "update gamedig", _logFactory?.GetLogger("NPM"), token);

                    modulePaths = await StaticNodeJSService.InvokeFromStringAsync<IEnumerable<string>>(
                        @"module.exports = (callback) => callback(null, module.paths);", cancellationToken: token);

                    gamedigPkgPath = modulePaths is null ? null : await modulePaths.ToAsyncEnumerable()
                                        .Select(p => Path.Combine(p, "gamedig", "package.json"))
                                        .FirstOrDefaultAsync(File.Exists, token);

                    if (gamedigPkgPath is null)
                    { throw new FileNotFoundException("updated 'gamedig/package.json' not found."); }

                    using var js = await JsonDocument.ParseAsync(File.OpenRead(gamedigPkgPath), cancellationToken: token);
                    var newver = js.RootElement.GetProperty("version").GetString();

                    if (ver != newver) _logger?.Info($"Updated {{ gamedig }} to version {newver}.");

                    return newver;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger?.Warn(ex, "Failed to update { gamedig }. Continuing with current version.");
                }
            }

            return ver;
        }

        private Task RunProcessAsync(string exec, string argstr, Logger? logger = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            var info = new ProcessStartInfo
            {
                FileName = exec,
                Arguments = argstr,
            };
            if (logger is not null)
            {
                info.CreateNoWindow = true;
                info.UseShellExecute = false;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.StandardErrorEncoding = System.Text.Encoding.UTF8;
                info.StandardOutputEncoding = System.Text.Encoding.UTF8;
            }
            var proc = new Process { StartInfo = info, };

            if (logger is not null)
            {
                void OutputToLog(object sender, DataReceivedEventArgs e)
                { if (e.Data is not null) logger?.Debug(e.Data); }

                proc.OutputDataReceived += OutputToLog;
                proc.ErrorDataReceived += OutputToLog;
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
            }
            else proc.Start();

            if (token.IsCancellationRequested && !proc.HasExited) proc.Kill(true);

            return proc.WaitForExitAsync(token);
        }

        private static Task BotLogAsync(Logger _logger, LogMessage msg)
            => Task.Run(() => _logger?.Log(msg.Severity switch
                {
                    LogSeverity.Critical => NLog.LogLevel.Fatal,
                    LogSeverity.Error => NLog.LogLevel.Error,
                    LogSeverity.Warning => NLog.LogLevel.Warn,
                    LogSeverity.Info => NLog.LogLevel.Info,
                    LogSeverity.Verbose => NLog.LogLevel.Debug,
                    LogSeverity.Debug => NLog.LogLevel.Trace,
                    _ => NLog.LogLevel.Off
                },
                msg.Exception, "<{0}> {1}", msg.Source, msg.Message)
            );

        internal static bool TryGetRes(string resName, out Stream? stream)
        {
            stream = typeof(GameBotApp).Assembly.GetManifestResourceStream(resName);

            return stream is not null;
        }

    }
}
