using Discord;
using Discord.WebSocket;
using DnsClient;
using RconSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    internal class GameServerWatcher
    {
        private static Dictionary<GameServerModel, Timer> Watchers;
        private static Timer GlobalTimer;

        // Run on Bot thread (SHOULD NOT BLOCK)
        internal static void StartWatcher() { try { Task.Run(() => QueryGameServersToWatch(), Program.ExitCancel.Token); } catch { return; } }

        private static void QueryGameServersToWatch()
        {
            Program.Log.Debug("Starting Game Server Watchers.");

            Watchers = new Dictionary<GameServerModel, Timer>();

            using (var db = new GameServersDb())
            {
                Parallel.ForEach(db.GameServers, gameServer =>
                {
                    var timetoupdate = (gameServer.LastUpdate + gameServer.UpdateInterval) - DateTimeOffset.Now;
                    Watchers.Add(gameServer, new Timer(callback: WatcherTimer_Elapsed, state: gameServer,
                        dueTime: timetoupdate <= TimeSpan.Zero ? TimeSpan.Zero : timetoupdate, period: gameServer.UpdateInterval));
                    Program.Log.Trace($"Started watcher for game server {{ {gameServer.ServerName} }}.");
                });
            }

            Program.Log.Debug("Game server watchers Started.");

            GlobalTimer = new Timer(dueTime: TimeSpan.Zero, period: TimeSpan.FromMinutes(15), state: null, callback: (obj) =>
            {
                Program.Log.Info($"Currently watching {Watchers.Count} game servers.");
            });
        }

        #region GameServerUpdate

        private static void WatcherTimer_Elapsed(object gameserver) { try { Task.Run(() => UpdateGameServerStatus(gameserver as GameServerModel), Program.ExitCancel.Token); } catch { return; } }

        private static void UpdateGameServerStatus(GameServerModel gameserver)
        {
#if DEBUG
            Program.Log.Trace($"Updating game server {gameserver.ServerName}");
#endif
            using (var db = new GameServersDb())
                gameserver = db.GameServers.First(gs => gs.Id == gameserver.Id);

            gameserver.LastUpdate = DateTimeOffset.Now;

            var pong = new Ping().Send(gameserver.RconIP, 1000);
            for (int i = 0; i < 10; i++)
            {
                if (pong.Status == IPStatus.Success)
                {
                    gameserver.RTT = (ushort)(pong.RoundtripTime * 2);
                    break;
                }
                pong = new Ping().Send(gameserver.RconIP, 1000);
            }

            if (pong.Status != IPStatus.Success && gameserver.IsOnline)
            {
                gameserver.IsOnline = false;
                gameserver.ErrMsg = "Server is not reachable from the internet.";
            }
            else
            {
                try
                {
                    var rcon = RconClient.Create(gameserver.RconIP.ToString(), gameserver.RconPort);

                    if (!rcon.ConnectAsync().IsCompletedSuccessfully)
                    {
                        gameserver.IsOnline = false;
                        gameserver.ErrMsg = "Can't connect to RCON";
                    }
                    else
                    {
                        if (!rcon.AuthenticateAsync(gameserver.RconPass).Result)
                            gameserver.ErrMsg = "Failed authenticating to RCON server";
                        else
                        {
                            var res = rcon.ExecuteCommandAsync("showoptions").Result;
                            gameserver.MaxPlayers = byte.Parse(res.Substring(res.IndexOf("MaxPlayers=") + 11, 2));

                            res = rcon.ExecuteCommandAsync("players").Result;
                            gameserver.Players = byte.Parse(res.Substring(res.IndexOf("Players connected") + 18, 4).Trim(':'), System.Globalization.NumberStyles.AllowParentheses);
                        }

                        rcon.Disconnect();

                        gameserver.IsOnline = true;
                        gameserver.LastOnline = DateTimeOffset.Now;
                        gameserver.ErrMsg = "";
                    }
                }
                catch (Exception ex)
                {
                    gameserver.IsOnline = false;
                    gameserver.ErrMsg = "Failed to get server informations. (RCON)";
                    Program.Log.Trace(ex.Message);
                }
            }

#if DEBUG
            Program.Log.Trace(string.IsNullOrEmpty(gameserver.ErrMsg) ? "Server is Online and Available to join." : gameserver.ErrMsg);
#endif

            using (var db = new GameServersDb())
            {
                db.Update(gameserver);
                db.SaveChanges();
            }

            CommandService.UpdateGameServerStatusMessage(Program.BotClient, gameserver);  
        }

        #endregion

        #region Main Methods

        internal static Task<GameServerModel> AddGameServer(SocketModal modal, IMessage placeholder)
        {
            var customid = CustomID.Deserialize(modal.Data.CustomId);

            string host_ip = modal.Data.Components.First(cmp => cmp.CustomId == "host_ip").Value;
            IPAddress ip;
            ushort rconport, gameport;

            Program.Log.Debug($"Resolving hostname \"{host_ip}\" ...");
            try
            {
                ip = new LookupClient(new[]{ NameServer.Cloudflare, NameServer.Cloudflare2 }).QueryAsync(host_ip, QueryType.A).Result.Answers.AddressRecords().FirstOrDefault().Address;
                gameport = ushort.Parse(modal.Data.Components.FirstOrDefault(cmp => cmp.CustomId == "game_port").Value);
                rconport = ushort.Parse(modal.Data.Components.FirstOrDefault(cmp => cmp.CustomId == "rcon_port").Value);
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
                IsOnline = false,
                LastOnline = DateTimeOffset.UnixEpoch,
                AddedBy = modal.User.Id,
                ChannelId = modal.Channel.Id,
                MessageId = placeholder.Id,
                IP = ip,
                GamePort = gameport,
                RconIP = ip,
                RconPort = rconport,
                RconPass = modal.Data.Components.First(cmp => cmp.CustomId == "rcon_pass").Value,
                AddedSince = DateTimeOffset.Now,
                LastUpdate = DateTimeOffset.UnixEpoch,
                UpdateInterval = TimeSpan.FromSeconds(10),
            };

            using (var db = new GameServersDb())
            {
                if (db.GameServers.Any(server => server.IP == ip && server.RconPort == rconport))
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

            Watchers.Add(gameserver, new Timer(callback: WatcherTimer_Elapsed, state: gameserver, dueTime: TimeSpan.Zero, period: gameserver.UpdateInterval));
            GlobalTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(15));

            return Task.FromResult(gameserver);
        }

        internal static void RemoveWatcher(GameServerModel theserver) => Watchers[theserver].Dispose();

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
