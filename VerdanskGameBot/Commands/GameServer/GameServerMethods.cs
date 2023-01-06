using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer;
using Discord.Interactions;

namespace VerdanskGameBot.Commands.GameServer
{
    internal partial class GameServerInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        private async Task RespondWithAddServerTryAgainAsync(AddServerModal modal, string servername, string invalid)
        {
            await FollowupAsync(ephemeral: true,
                embed: new EmbedBuilder()
                    .WithTitle($"Failed to add '{servername}'")
                    .WithDescription(invalid)
                    .AddField("Game Type", modal.GameType, true)
                    .AddField("Hostname / IP", modal.HostnameIPPort, true)
                    .AddField("Update Interval", modal.UpdateInterval, true)
                    .AddField("Note", string.IsNullOrEmpty(modal.Note) ? '-' : modal.Note, false)
                .Build(),
                components: new ComponentBuilder()
                .WithButton(
                    label: "Try Again",
                    customId: ServerCmd.Server + ',' + ServerCmd.ReAddBtn + '^' + servername,
                    style: ButtonStyle.Primary, emote: Emoji.Parse(":repeat:"))
                .WithButton(
                    label: "Supported Games List", style: ButtonStyle.Link,
                    emote: Emoji.Parse(":globe_with_meridians:"),
                    url: "https://github.com/gamedig/node-gamedig/tree/70ec2a45a7#supported")
                .Build());
        }

        private Task<GameServerModel> RegisterNewGameServer(string servername, AddServerModal modal, SocketGuild guild)
        {
            var gametype = modal.GameType.Normalize().ToLowerInvariant();
            if (!GameTypeHelper.IsSupported(gametype))
                return Task.FromException<GameServerModel>(new GameServerGameTypeInvalidException(modal.GameType));

            IPAddress serverip;
            ushort gameport;

            try
            {
                serverip = (Dns.GetHostAddressesAsync(modal.HostnameIPPort.Split(':').First())).Result.First();
                gameport = ushort.Parse(modal.HostnameIPPort.Split(':').Last());
            }
            catch (Exception ex)
            {
                return Task.FromException<GameServerModel>(new GameServerHostnameIPPortInvalidException(modal.HostnameIPPort));
            }

            TimeSpan updateInt;

            try
            {
                updateInt = TimeSpan.FromMinutes(byte.Parse(modal.UpdateInterval));
            }
            catch (Exception ex)
            {
                return Task.FromException<GameServerModel>(new GameServerUpdateIntervalInvalidException(modal.UpdateInterval));
            }

            var gs = new GameServerModel
            {
                GameType = gametype,
                ServerName = servername,
                AddedBy = Context.User.Id,
                AddedSince = DateTimeOffset.Now,
                ChannelId = Context.Channel.Id,
                IP = serverip,
                GamePort = gameport,
                UpdateInterval = updateInt,
                Note = modal.Note
            };

            try
            {
                using (var db = new GameServerDb()) db.Add(gs);
            }
            catch (Exception ex)
            {
                return Task.FromException<GameServerModel>(new GameServerDbAddException());
            }

            return Task.FromResult(gs);
        }
    }
}
