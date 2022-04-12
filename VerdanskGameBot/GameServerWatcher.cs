using Discord;
using Discord.WebSocket;
using DnsClient;
using HtmlAgilityPack;
using Jering.Javascript.NodeJS;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    internal class GameServerWatcher
    {
        private static Dictionary<string, Timer> Watchers;
        private static Timer GlobalTimer;

        private static Mutex UpdateMutex = new Mutex(false);

        // Run on Bot thread (SHOULD NOT BLOCK)
        internal static void StartWatcher() { try { Task.Run(() => QueryGameServersToWatch(), Program.ExitCancel.Token); } catch { return; } }

        private static void QueryGameServersToWatch()
        {
            Program.Log.Debug("Starting Game Server Watchers.");

            if (!File.Exists("gameservers.db"))
            {
                Program.Log.Debug("No gameservers sqlite database file found. Creating one...");
                var file = File.Create("gameservers.db");
                Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(GameServerWatcher), "gameservers.db").CopyToAsync(file).Wait();
                file.Close();
                Program.Log.Debug("Created gameservers sqlite database file with default values.");
            }

            Watchers = new Dictionary<string, Timer>();

            using (var db = new GameServersDb())
            {
                Parallel.ForEach(db.GameServers, gameServer =>
                {
                    var timetoupdate = (gameServer.LastUpdate + gameServer.UpdateInterval) - DateTimeOffset.Now;
                    Watchers.Add(gameServer.ServerName, new Timer(callback: WatcherTimer_Elapsed, state: gameServer,
                        dueTime: timetoupdate <= TimeSpan.Zero ? TimeSpan.Zero : timetoupdate, period: gameServer.UpdateInterval));
                    Program.Log.Trace($"Started watcher for game server {{ {gameServer.ServerName} }} | next update in {timetoupdate.TotalSeconds} s.");
                });
            }

            Program.Log.Debug("Game server watchers Started.");

            GlobalTimer = new Timer(dueTime: TimeSpan.Zero, period: TimeSpan.FromHours(1), state: null, callback: (obj) =>
            {
                if (Program.BotClient.ConnectionState == ConnectionState.Disconnected)
                    Environment.Exit(-500);

                Program.Log.Info($"Currently watching {Watchers.Count} game servers.");
            });
        }

        #region GameServerUpdate

        private static void WatcherTimer_Elapsed(object gameserver) { try { Task.Run(() => UpdateGameServerStatus(gameserver as GameServerModel), Program.ExitCancel.Token); } catch { return; } }

        private static void UpdateGameServerStatus(GameServerModel gameserver)
        {
            if(!UpdateMutex.WaitOne(0)) return;

            Program.Log.Trace($"Updating game server {{ {gameserver.ServerName} }}");

            using (var db = new GameServersDb())
                gameserver = db.GameServers.First(gs => gs.Id == gameserver.Id);

            gameserver.LastUpdate = DateTimeOffset.Now;

            JsonDocument gamedig;
            try
            {
                gamedig = JsonDocument.Parse(StaticNodeJSService.InvokeFromStreamAsync<string>(
                    Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(GameServerWatcher), "query.js"),
                    args: new string[] { gameserver.GameType, gameserver.GamePort.ToString() }).Result);
            }
            catch (Exception exc)
            {
                Program.Log.Trace(exc, $"Failed updating game server with name {{ {gameserver.ServerName} }}\r\n" +
                    $"Exception : {exc}");
                return;
            }

            JsonElement check;
            if (!gamedig.RootElement.TryGetProperty("name", out check))
                gameserver.IsOnline = false;
            else
            {
                gameserver.IsOnline = true;
                gameserver.LastOnline = DateTimeOffset.Now;

                gameserver.DisplayName = gamedig.RootElement.GetProperty("name").GetString();
                gameserver.Players = (byte)gamedig.RootElement.GetProperty("players").GetArrayLength();
                gameserver.MaxPlayers = gamedig.RootElement.GetProperty("maxplayers").GetByte();

                using (var http = new HttpClient())
                {
                    var appid = gamedig.RootElement.GetProperty("raw").GetProperty("appId").GetInt32();

                    gameserver.ImageUrl = $"https://cdn.akamai.steamstatic.com/steam/apps/{appid}/header.jpg";
                    var page = $"https://store.steampowered.com/app/{appid}/";

                    var doc = new HtmlWeb().Load(page);

                    gameserver.Description = doc.DocumentNode.SelectSingleNode("//div[@class='game_description_snippet']").InnerText.Substring(0, 200).Trim() +
                        "...\n" + page;
                }
            }
            
            using (var db = new GameServersDb())
            {
                db.Update(gameserver);
                db.SaveChanges();
            }

            CommandService.UpdateGameServerStatusMessage(Program.BotClient, gameserver);

            UpdateMutex.ReleaseMutex();

            Program.Log.Trace($"Game server with name {{ {gameserver.ServerName} }} updated.");
        }

        internal static bool PauseUpdate(GameServerModel gameServer)
        {
            Watchers[gameServer.ServerName].Change(Timeout.Infinite, Timeout.Infinite);

            if (UpdateMutex.WaitOne(3000)) return true;
            else return false;
        }

        internal static void ResumeUpdate(GameServerModel gameServer)
        {
            UpdateMutex.ReleaseMutex();

            var timetoupdate = (gameServer.LastUpdate + gameServer.UpdateInterval) - DateTimeOffset.Now;

            Watchers[gameServer.ServerName].Change(dueTime: timetoupdate <= TimeSpan.Zero ? TimeSpan.Zero : timetoupdate, period: gameServer.UpdateInterval);
        }

        #endregion

        #region Main Methods

        internal static Task<GameServerModel> AddGameServer(SocketModal modal, IMessage placeholder)
        {
            var customid = CustomID.Deserialize(modal.Data.CustomId);

            string gametype = modal.Data.Components.First(cmp => cmp.CustomId == "gametype").Value;
            string host_ip = modal.Data.Components.First(cmp => cmp.CustomId == "host_ip").Value;
            IPAddress ip; ushort gameport;

            Program.Log.Debug($"Resolving hostname \"{host_ip}\" ...");
            try
            {
                ip = new LookupClient(new[]{ NameServer.Cloudflare, NameServer.Cloudflare2 }).QueryAsync(host_ip, QueryType.A).Result.Answers.AddressRecords().FirstOrDefault().Address;
                gameport = ushort.Parse(modal.Data.Components.FirstOrDefault(cmp => cmp.CustomId == "game_port").Value);
            }
            catch (InvalidOperationException invopexc)
            {
                Program.Log.Debug(invopexc, $"Can't get IP Address of {{ {host_ip} }}.");

                return Task.FromException<GameServerModel>(invopexc);
            }
            catch (FormatException formatexc)
            {
                Program.Log.Debug(formatexc, $"Game/RCON port is not in the correct format.");

                return Task.FromException<GameServerModel>(formatexc);
            }
            catch(Exception exc)
            {
                Program.Log.Error(exc);

                return Task.FromException<GameServerModel>(exc);
            }
            
            Program.Log.Debug($"Resolved hostname \"{host_ip}\" having IP : {ip}");

            var gameserver = new GameServerModel
            {
                ServerName = (customid.Options["servername"] as string).Trim(),
                GameType = gametype,
                IsOnline = false,
                LastOnline = DateTimeOffset.UnixEpoch,
                AddedBy = modal.User.Id,
                ChannelId = modal.Channel.Id,
                MessageId = placeholder.Id,
                IP = ip,
                GamePort = gameport,
                GameLink = $"steam://connect/{ip}:{gameport}",
                AddedSince = DateTimeOffset.Now,
                LastUpdate = DateTimeOffset.UnixEpoch,
                UpdateInterval = TimeSpan.FromSeconds(10),
            };

            using (var db = new GameServersDb())
            {
                if (db.GameServers.Any(server => server.IP == ip && server.GamePort == gameport))
                {
                    var existexc = new AlreadyExistException();
                    Program.Log.Debug(existexc, "Server with the same Public IP and Port exist. Can't add the same server to watch list more than one instance.");

                    return (Task<GameServerModel>)Task.FromException(existexc);
                }

                try
                {
                    db.Add(gameserver);
                    db.SaveChanges();
                }
                catch (Exception exc)
                {
                    Program.Log.Error(exc);
                    return Task.FromException<GameServerModel>(exc);
                }
                Program.Log.Debug($"Added a game server to database.");
            }

            Watchers.Add(gameserver.ServerName, new Timer(callback: WatcherTimer_Elapsed, state: gameserver, dueTime: TimeSpan.Zero, period: gameserver.UpdateInterval));
            GlobalTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(15));

            return Task.FromResult(gameserver);
        }

        internal static void RemoveWatcher(GameServerModel theserver) => Watchers[theserver.ServerName].Dispose();

        public static async void Dispose()
        {
            if(GlobalTimer != null)
                await GlobalTimer.DisposeAsync();

            if(Watchers != null)
                foreach (var tim in Watchers.Values)
                {
                    await tim.DisposeAsync();
                }
        }

        #endregion
    }
}
