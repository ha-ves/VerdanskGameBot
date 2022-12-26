using Discord;
using Discord.WebSocket;
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
using VerdanskGameBot.Command;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.GameServer
{
    internal class GameServerWatcher
    {
        private static TimeSpan WatchTimeout = TimeSpan.FromSeconds(15);

        private static Dictionary<string, Tuple<Timer, ManualResetEvent>> Watchers;
        private static Timer GlobalTimer;

        private static Dictionary<string, Mutex> UpdateMutexes;
        private static bool IsPaused = false;

        #region Watcher Methods

        // Run on Bot thread (SHOULD NOT BLOCK)
        internal static void StartWatcher() { try { Task.Run(() => QueryGameServersToWatch(), Program.ExitCancel.Token); } catch { return; } }

        private static void QueryGameServersToWatch()
        {
            Program.Log.Debug("Starting Game Server Watchers.");

            if (!File.Exists("gameservers.db"))
            {
                Program.Log.Debug("No gameservers sqlite database file found. Creating one...");
                var file = File.Create("gameservers.db");
                Program.GetRes("gameservers.db").CopyTo(file);
                file.Close();
                Program.Log.Debug("Created gameservers sqlite database file with default values.");
            }

            Watchers = new Dictionary<string, Tuple<Timer, ManualResetEvent>>();
            UpdateMutexes = new Dictionary<string, Mutex>();

            using (var db = GameServerDb.GetContext())
            {
                foreach (var gameServer in db.GameServers)
                {
                    AddToWatcher(gameServer);
                }
            }

            Program.Log.Debug("Game server watchers Started.");

            GlobalTimer = new Timer(dueTime: TimeSpan.Zero, period: TimeSpan.FromHours(1), state: null, callback: (obj) =>
            {
                if (Program.BotClient.ConnectionState == ConnectionState.Disconnected)
                {
                    Program.Log.Fatal("Bot disconnected from Discord and can't reconnect. Exiting.");
                    Environment.Exit(-(int)ExitCodes.DiscordDisconnect);
                }

                Program.Log.Info($"Currently watching {Watchers.Count} game servers.");
            });
        }

        private static void AddToWatcher(GameServerModel gameServer)
        {
            var timetoupdate = gameServer.LastUpdate + gameServer.UpdateInterval - DateTimeOffset.Now;
            UpdateMutexes.Add(gameServer.ServerName, new Mutex(false));
            Watchers.Add(gameServer.ServerName, new Tuple<Timer, ManualResetEvent>(new Timer(callback: WatcherTimer_Elapsed, state: new Tuple<string, TimeSpan>(gameServer.ServerName, gameServer.UpdateInterval),
                dueTime: timetoupdate <= TimeSpan.Zero ? TimeSpan.Zero : timetoupdate, period: gameServer.UpdateInterval), new ManualResetEvent(false)));
            Program.Log.Trace($"Started watcher for game server {{ {gameServer.ServerName} }} | next update in {timetoupdate.TotalSeconds} s.");
        }

        #endregion

        #region GameServerUpdate

        private static void WatcherTimer_Elapsed(object tupel) { try { Task.Run(() => UpdateGameServerStatus(tupel as Tuple<string, TimeSpan>), Program.ExitCancel.Token); } catch { return; } }

        private static void UpdateGameServerStatus(Tuple<string, TimeSpan> tupel)
        {
            if (!UpdateMutexes[tupel.Item1].WaitOne(tupel.Item2)) return;

            Program.Log.Trace($"Updating game server {{ {tupel.Item1} }}");

            GameServerModel gameserver;

            using (var db = GameServerDb.GetContext())
                gameserver = db.GameServers.First(gs => gs.ServerName == tupel.Item1);

            gameserver.LastUpdate = DateTimeOffset.Now;

            JsonDocument gamedig;
            try
            {
                var attempts = 3;
                gamedig = JsonDocument.Parse(StaticNodeJSService.InvokeFromStreamAsync<string>(
                    Program.GetRes("query.js"),
                    args: new string[]
                    {
                        /* in case you're running the bot in the same system or internal NAT
                         * ex: i run the bot in a public facing VPS that acts as router with NAT,
                         * the game servers use VPN to connect to the VPS and get public ip with port forwarding
                         * 
                         * ex architecture:
                         * ~~~~~~~~~~~~~~~~~INTERNET~~~~~~~~~~~~~~~~
                         * ----------------Public IP----------------
                         * --------------------|--------------------
                         * -------------------VPS-------------------
                         * ------------NAT & Port Forward-----------
                         * -------------------=|=-------------------
                         * -------v=======Internal VPN======v-------
                         * ------=|=----------=|=----------=|=------
                         * --GameServer1--GameServer2--GameServer3--
                         */
                        NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                            intf => intf.GetIPProperties().UnicastAddresses.FirstOrDefault(
                                ipinfo => ipinfo.Address.Equals(gameserver.IP)) != null) == null ? gameserver.IP.ToString() : Program.LocalIP.ToString(),
                        gameserver.GameType == "valve" ? "przomboid" : gameserver.GameType,
                        gameserver.GamePort.ToString(),
                        attempts.ToString(),
                        (WatchTimeout.TotalMilliseconds / attempts).ToString("#")
                    }).Result);
            }
            catch (Exception exc)
            {
                Program.Log.Trace(exc, $"Failed updating game server with name {{ {gameserver.ServerName} }}{Environment.NewLine}" +
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
                        $"...{Environment.NewLine}" + page;
                }
            }

            using (var db = GameServerDb.GetContext())
            {
                db.Update(gameserver);
                db.SaveChanges();
            }

            CommandService.UpdateGameServerStatusMessage(gameserver);

            Watchers[gameserver.ServerName].Item2.Set();
            UpdateMutexes[gameserver.ServerName].ReleaseMutex();

            Program.Log.Trace($"Game server with name {{ {gameserver.ServerName} }} updated.");
        }

        internal static bool PauseUpdate(GameServerModel gameServer)
        {
            if (IsPaused) return true;

            Watchers[gameServer.ServerName].Item1.Change(Timeout.Infinite, Timeout.Infinite);

            if (UpdateMutexes[gameServer.ServerName].WaitOne(WatchTimeout))
            {
                IsPaused = true;
                return true;
            }
            else return false;
        }

        internal static void ResumeUpdate(GameServerModel gameServer)
        {
            if (!IsPaused) return;

            UpdateMutexes[gameServer.ServerName].ReleaseMutex();

            var timetoupdate = gameServer.LastUpdate + gameServer.UpdateInterval - DateTimeOffset.Now;

            Watchers[gameServer.ServerName].Item1.Change(dueTime: timetoupdate <= TimeSpan.Zero ? TimeSpan.Zero : timetoupdate, period: gameServer.UpdateInterval);

        }

        #endregion

        #region Main Methods

        internal static bool RefreshWatcher(GameServerModel gameServer)
        {
            Watchers[gameServer.ServerName].Item2.Reset();
            if (Watchers[gameServer.ServerName].Item1.Change(TimeSpan.Zero, gameServer.UpdateInterval))
                if (Watchers[gameServer.ServerName].Item2.WaitOne(WatchTimeout))
                    return true;
                else
                    return false;
            else
                return false;
        }

        internal static Task<GameServerModel> ChangeGameServer(SocketModal modal)
        {
            var customid = CustomID.Deserialize(modal.Data.CustomId);

            string servername = customid.Options[CustomIDs.ServernameOption];

            string gametype = modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.GameType.ToString("d")).Value;
            string host_ip = modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.HostIPPort.ToString("d")).Value.Split(':')[0];
            string notes = modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.Note.ToString("d")).Value;

            GameServerModel theserver; IPAddress ip; ushort gameport; int update_interval;

            using (var db = GameServerDb.GetContext())
                theserver = db.GameServers.FirstOrDefault(srv => srv.ServerName == servername);

            PauseUpdate(theserver);

            Program.Log.Debug($"Resolving hostname \"{host_ip}\" ...");
            try
            {
                ParseHostIPGamePort(modal, host_ip, out ip, out gameport, out update_interval);
            }
            catch (SocketException invopexc)
            {
                Program.Log.Debug(invopexc, $"Can't get IP Address of {{ {host_ip} }}.");

                return Task.FromException<GameServerModel>(invopexc);
            }
            catch (GamePortFormatException gpformatexc)
            {
                Program.Log.Debug(gpformatexc, $"Game port is not in the correct format.");

                return Task.FromException<GameServerModel>(gpformatexc);
            }
            catch (UpdateIntervalFormatException gpformatexc)
            {
                Program.Log.Debug(gpformatexc, $"Update interval is not in the correct format.");

                return Task.FromException<GameServerModel>(gpformatexc);
            }
            catch (Exception exc)
            {
                Program.Log.Error(exc);

                return Task.FromException<GameServerModel>(exc);
            }

            Program.Log.Debug($"Resolved hostname \"{host_ip}\" having IP : {ip}");

            theserver.GameType = gametype;
            theserver.IsOnline = false;
            theserver.IP = ip;
            theserver.GamePort = gameport;
            theserver.UpdateInterval = TimeSpan.FromMinutes(update_interval);
            theserver.LastOnline = DateTimeOffset.UnixEpoch;
            theserver.Note = notes;

            using (var db = GameServerDb.GetContext())
            {
                try
                {
                    db.Update(theserver);
                    db.SaveChanges();
                }
                catch (Exception exc)
                {
                    Program.Log.Error(exc);
                    return Task.FromException<GameServerModel>(exc);
                }
                Program.Log.Debug($"Updated a game server in database.");
            }

            ResumeUpdate(theserver);
            Watchers[theserver.ServerName].Item1.Change(TimeSpan.Zero, theserver.UpdateInterval);

            return Task.FromResult(theserver);
        }

        internal static Task<GameServerModel> AddGameServer(SocketModal modal, IMessage placeholder)
        {
            var customid = CustomID.Deserialize(modal.Data.CustomId);

            string gametype = modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.GameType.ToString("d")).Value;
            string host_ip = modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.HostIPPort.ToString("d")).Value.Split(':')[0];
            string notes = modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.Note.ToString("d")).Value;

            IPAddress ip; ushort gameport; int update_interval;

            Program.Log.Debug($"Resolving hostname \"{host_ip}\" ...");
            try
            {
                ParseHostIPGamePort(modal, host_ip, out ip, out gameport, out update_interval);
            }
            catch (SocketException sockexc)
            {
                Program.Log.Debug(sockexc, $"Can't get IP Address of {{ {host_ip} }}.");

                return Task.FromException<GameServerModel>(sockexc);
            }
            catch (GamePortFormatException gpformatexc)
            {
                Program.Log.Debug(gpformatexc, $"Game port is not in the correct format.");

                return Task.FromException<GameServerModel>(gpformatexc);
            }
            catch (UpdateIntervalFormatException gpformatexc)
            {
                Program.Log.Debug(gpformatexc, $"Update interval is not in the correct format.");

                return Task.FromException<GameServerModel>(gpformatexc);
            }
            catch (Exception exc)
            {
                Program.Log.Error(exc);

                return Task.FromException<GameServerModel>(exc);
            }

            Program.Log.Debug($"Resolved hostname \"{host_ip}\" having IP : {ip}");

            using (var db = GameServerDb.GetContext())
                if (db.GameServers.Any(server => server.IP == ip && server.GamePort == gameport))
                {
                    var existexc = new AlreadyExistException();
                    Program.Log.Debug(existexc, "Server with the same Public IP and Port exist. Can't add the same server to watch list more than one instance.");

                    return (Task<GameServerModel>)Task.FromException(existexc);
                }

            var gameserver = new GameServerModel
            {
                ServerName = customid.Options[CustomIDs.ServernameOption],
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
                UpdateInterval = TimeSpan.FromMinutes(update_interval),
                Note = notes,
            };

            using (var db = GameServerDb.GetContext())
            {
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

            UpdateGameServerStatus(new Tuple<string, TimeSpan>(gameserver.ServerName, gameserver.UpdateInterval));

            AddToWatcher(gameserver);

            GlobalTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(15));

            return Task.FromResult(gameserver);
        }

        internal static void RemoveWatcher(GameServerModel theserver) => Watchers[theserver.ServerName].Item1.Dispose();

        /// <summary>
        /// Parses the GameServer Input data
        /// </summary>
        /// <param name="modal">source discord modal</param>
        /// <param name="host_ip">gameserver host/ip</param>
        /// <param name="ip">resulting IP</param>
        /// <param name="gameport">resulting gameport</param>
        /// <param name="update_interval">resulting watcher interval</param>
        /// <exception cref="GamePortFormatException"></exception>
        /// <exception cref="UpdateIntervalFormatException">Thrown when update interval time is invalid</exception>
        private static void ParseHostIPGamePort(SocketModal modal, string host_ip, out IPAddress ip, out ushort gameport, out int update_interval)
        {
            if (!IPAddress.TryParse(host_ip, out ip))
                ip = Dns.GetHostAddresses(host_ip).First();
            
            if (!ushort.TryParse(modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.HostIPPort.ToString("d")).Value.Split(':')[1], out gameport)) throw new GamePortFormatException();
            if (!int.TryParse(modal.Data.Components.First(cmp => cmp.CustomId == CustomIDs.UpdateInterval.ToString("d")).Value, out update_interval)) throw new UpdateIntervalFormatException();
        }

        public static async void Dispose()
        {
            if (GlobalTimer != null)
            {
                await GlobalTimer.DisposeAsync();
                GlobalTimer = null;
            }

            if (Watchers != null)
            {
                foreach (var tim in Watchers.Values)
                {
                    await tim.Item1.DisposeAsync();
                    tim.Item2.Dispose();
                }
                Watchers.Clear();
                Watchers = null;
            }

            if (UpdateMutexes != null)
            {
                foreach (var mutex in UpdateMutexes)
                    mutex.Value.Dispose();
                UpdateMutexes.Clear();
                UpdateMutexes = null;
            }
        }

        #endregion
    }
}
