using Discord;
using Discord.Rest;
using Discord.WebSocket;
using HtmlAgilityPack;
using Jering.Javascript.NodeJS;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.GameServer
{
    internal static class GameServerWatcher
    {
        private static bool _isStarted = false;
        private static Dictionary<int, Timer> _watchers { get; set; } = new();

        public static async Task StartAsync(IEnumerable<IGuild> guilds)
        {
            Program.Log.Trace("Loading Game Server Watcher module ...");

            try
            {
                foreach (var guild in guilds)
                {
                    await EnsureTableCreatedAsync(guild);

                    using (var db = new GameServerDbReadOnly(guild))
                    {
                        await db.GameServers.ForEachAsync(gs =>
                        {
                            var duetime = TimeSpan.Zero;

                            if (gs.LastUpdate.HasValue)
                                if (gs.LastUpdate.Value + gs.UpdateInterval > DateTimeOffset.UtcNow)
                                    duetime = gs.LastUpdate.Value + gs.UpdateInterval - DateTimeOffset.UtcNow;

                            var tim = new Timer(Watcher_Elapsed, gs, duetime, gs.UpdateInterval);
                            _watchers.Add(gs.ServerId, tim);
                        });
                    }

                    _isStarted = true;
                }

                Program.Log.Info("Game Server Watcher module loaded successfully.");
            }
            catch (Exception)
            {
                Program.Log.Error("Game Server Watcher module is not loaded, see previous messages for details.");
            }
        }

        private static async Task EnsureTableCreatedAsync(IGuild guild)
        {
            Program.Log.Trace("Checking database for guild " + guild.Name + " ...");

            using (var db = new GameBotDb(guild))
            {
                await db.Database.EnsureCreatedAsync();

                var tablesquery = string.Empty;
                if (db.Database.IsSqlite())
                    tablesquery += "SELECT name AS Value FROM sqlite_schema where type='table'";
                else if (db.Database.IsSqlServer())
                    tablesquery += $"SELECT [name] AS [Value] FROM {db.Database.GetDbConnection().Database}.sys.tables";
                else if (db.Database.IsMySql())
                    tablesquery += $"SELECT TABLE_NAME AS Value FROM information_schema.tables ";
                else if (db.Database.IsNpgsql())
                    tablesquery += $"";

                Program.Log.Trace($"Checking gameservers tables ...");

                var reqTbs = db.Model.GetEntityTypes().Select(t => t.GetTableName()).Distinct();
                var exstTbs = db.Database.SqlQueryRaw<string>(tablesquery);

                foreach (var tb in reqTbs.Except(exstTbs))
                {
                    Program.Log.Trace(guild.Name + "'s table is not in database, creating table ...");
                    var crt = db.Database.GenerateCreateScript();
                    foreach (var script in crt.Split("\r\nGO\r\n\r\n\r\n").Except(new[] { string.Empty }))
                    {
                        await db.Database.ExecuteSqlRawAsync(script);
                    } 
                    
                }

                Program.Log.Trace("All gameservers tables checked.");
            }

            Program.Log.Trace("Database available.");
        }

        private static async void Watcher_Elapsed(object gameServer)
        {
            var gs = gameServer as GameServerModel;

            var ch = await Program.BotClient.GetChannelAsync(gs.ChannelId) as SocketTextChannel;
            var msg = ch.ModifyMessageAsync(gs.MessageId, msgprop =>
            {
                Debugger.Break();
            });
        }

        /// <summary>
        /// Query the game server using <see href="https://github.com/gamedig/node-gamedig">node-gamedig</see>.
        /// </summary>
        /// <param name="gameServer">The <see cref="GameServerModel"/> to query.</param>
        /// <returns></returns>
        private static async Task<GameServerModel> QueryGameServerAsync(GameServerModel gameServer)
        {
            var js = await StaticNodeJSService.InvokeFromStreamAsync<string>(
            Program.GetRes("query.js"),
            args: new[]
            {
                gameServer.IP.ToString(),
                gameServer.GameType == "valve" ? "" : gameServer.GameType,
                gameServer.GamePort.ToString(),
                /*maxAttempts*/ "3",
                /*timeoutAllAttempts*/ TimeSpan.FromSeconds(30).TotalMilliseconds.ToString("#")
            });

            var gsjs = JsonDocument.Parse(js);

            gameServer.IsOnline = false;

            if (gsjs.RootElement.GetArrayLength() > 0
                && string.IsNullOrEmpty(gsjs.RootElement.GetProperty("error").GetString()))
            {
                gameServer.IsOnline = true;
                gameServer.LastOnline = DateTimeOffset.Now;

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

                        var descfull = doc.SelectSingleNode("//div[@class='game_description_snippet']").InnerText;
                        var descexcerpt =

                        gameServer.Description = descfull.Substring(0, descfull.LastIndexOf(' ', 0, 200))
                            + $"...{Environment.NewLine}" + page;
                    }
            }

            return gameServer;
        }

        public static Task UpdateWatcherAsync(GameServerModel gs)
        {
            _watchers.Add(gs.ServerId, new Timer(Watcher_Elapsed, gs, TimeSpan.Zero, gs.UpdateInterval));
            return Task.CompletedTask;
        }

        public static async Task StopAsync()
        {
            foreach (var item in _watchers)
            {
                item.Value.Change(Timeout.Infinite, Timeout.Infinite);
                await item.Value.DisposeAsync();
            }
            _watchers.Clear();

            _isStarted = false;
        }
    }
}
