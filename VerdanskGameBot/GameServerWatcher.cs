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
    class GameServerWatcher
    {
        static Dictionary<GameServerModel, Timer> Watchers;
        static Timer GlobalTimer;

        internal GameServerWatcher()
        {
            Task.Run(async () =>
            {
                Program.Log.Debug("Starting Game Server Watchers.");

                Watchers = new Dictionary<GameServerModel, Timer>();

                using (var db = new GameServersDb())
                {
                    await db.GameServers.ForEachAsync(gameserver =>
                    {
                        var timetoupdate = (gameserver.LastUpdate + gameserver.UpdateInterval) - DateTimeOffset.Now;
                        Watchers.Add(gameserver, new Timer(callback: WatcherTimer_Elapsed, state: gameserver,
                            dueTime: timetoupdate <= TimeSpan.Zero ? TimeSpan.Zero : timetoupdate, period: gameserver.UpdateInterval));
                    });
                }

                Program.Log.Debug("Game server watchers Started.");

                GlobalTimer = new Timer(dueTime: TimeSpan.Zero, period: TimeSpan.FromMinutes(15), state: null, callback: (obj) =>
                {
                    Program.Log.Info($"Currently watching {Watchers.Count} game servers.");
                });
            });
        }

        static async void WatcherTimer_Elapsed(object gameserver)
        {
            var server = gameserver as GameServerModel;
            server.LastUpdate = DateTimeOffset.Now;

            PingReply pong;
            for (int i = 0; i < 4; i++) { await new Ping().SendPingAsync(server.IP, 1000); }
            pong = await new Ping().SendPingAsync(server.IP, 1000);

            if (pong.Status != IPStatus.Success && server.IsOnline)
            {
                server.IsOnline = false;
                server.Config = "Server is not reachable from the internet";

                return;
            }

            var rcon = RconClient.Create(server.IP.ToString(), server.RconPort);
            try
            {
                await rcon.ConnectAsync();
                if (!await rcon.AuthenticateAsync(server.RconPass))
                {
                    server.Config = "Failed authenticating to RCON server";
                }
            }
            catch (SocketException sockexc)
            {
                server.Config = sockexc.Message;
            }

            server.IsOnline = true;
            server.LastOnline = DateTimeOffset.Now;
            server.Config = "Server should be online and available to join";

            using (var db = new GameServersDb())
            {
                db.Update(server);
                await db.SaveChangesAsync();
            }

            await Program.Current.CmdSvc.GameServerUpdate(server);
        }

        internal static async Task<Task<GameServerModel>> Add(SocketModal modal, IMessage placeholder)
        {
            var customid = CustomID.Deserialize(modal.Data.CustomId);

            string host_ip = modal.Data.Components.First(cmp => cmp.CustomId == "host_ip").Value;
            IPAddress ip;
            ushort rconport, gameport;

            Program.Log.Debug($"Resolving hostname \"{host_ip}\" ...");
            try
            {
                ip = (await new LookupClient(new[]{ NameServer.Cloudflare, NameServer.Cloudflare2 }).QueryAsync(host_ip, QueryType.A)).Answers.AddressRecords().First().Address;
                gameport = ushort.Parse(modal.Data.Components.First(cmp => cmp.CustomId == "game_port").Value);
                rconport = ushort.Parse(modal.Data.Components.First(cmp => cmp.CustomId == "rcon_port").Value);
            }
            catch (InvalidOperationException invopexc)
            {
                Program.Log.Debug(invopexc, $"Can't get IP Address of {host_ip}.");

                return (Task<GameServerModel>)Task.FromException(invopexc);
            }
            catch (FormatException formatexc)
            {
                Program.Log.Debug(formatexc, $"Game/RCON port is not in the correct format.");

                return (Task<GameServerModel>)Task.FromException(formatexc);
            }
            catch(Exception exc)
            {
                Program.Log.Error(exc);

                return (Task<GameServerModel>)Task.FromException(exc);
            }
            
            Program.Log.Debug($"Resolved hostname \"{host_ip}\" having IP : {ip}");

            var gameserver = new GameServerModel
            {
                ServerName = customid.Options["servername"] as string,
                IsOnline = false,
                LastOnline = DateTimeOffset.UnixEpoch,
                AddedBy = modal.User.Id,
                ChannelId = modal.Channel.Id,
                MessageId = placeholder.Id,
                IP = ip,
                GamePort = gameport,
                RconPort = rconport,
                RconPass = modal.Data.Components.First(cmp => cmp.CustomId == "rcon_pass").Value,
                AddedSince = DateTimeOffset.Now,
                LastUpdate = DateTimeOffset.UnixEpoch,
                UpdateInterval = TimeSpan.FromSeconds(5),
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
                    await db.AddAsync(gameserver);
                    await db.SaveChangesAsync();
                }
                catch (Exception exc)
                {
                    Program.Log.Error(exc);
                    return (Task<GameServerModel>)Task.FromException(exc);
                }
                Program.Log.Debug($"Added a game server to database.");
            }

            Watchers.Add(gameserver, new Timer(callback: WatcherTimer_Elapsed, state: gameserver, dueTime: TimeSpan.Zero, period: gameserver.UpdateInterval));
            GlobalTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(15));

            return Task.FromResult(gameserver);
        }

        internal static async Task<bool> RemoveAsync(GameServerModel theserver)
        {
            var disposi = Watchers[theserver].DisposeAsync();
            await disposi;
            if (disposi.IsCompletedSuccessfully) return true;
            else return false;
        }
    }
}
