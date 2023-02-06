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
using HtmlAgilityPack;
using Jering.Javascript.NodeJS;
using System.Net.Http;
using System.Text.Json;

namespace VerdanskGameBot.Commands.GameServer
{
    internal partial class GameServerInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        /// <summary>
        /// Respond the interaction with try add server again message.
        /// </summary>
        /// <param name="modal">The original details form <see cref="AddServerModal"/>.</param>
        /// <param name="servername">The servername to try again.</param>
        /// <param name="invalid">Try again description.</param>
        /// <returns></returns>
        private Task<IUserMessage> RespondWithAddServerTryAgainAsync(AddServerModal modal, string servername, string invalid)
        {
            return FollowupAsync(ephemeral: true,
                embed: new EmbedBuilder()
                    .WithTitle($"Failed to add '{servername}'")
                    .WithDescription(invalid)
                    .AddField("Game Type", modal.GameType, true)
                    .AddField("Hostname / IP : Game Port", modal.HostnameIPPort, true)
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
                .WithButton(
                    label: "Check DNS Propagation", style: ButtonStyle.Link,
                    emote: Emoji.Parse(":globe_with_meridians:"),
                    url: "https://letmegooglethat.com/?q=dns+propagation+check")
                .Build());
        }

        /// <summary>
        /// Register a new <see cref="GameServerModel"/> to database for the respective <paramref name="guild"/> / discord server.
        /// </summary>
        /// <param name="servername">Private servername by admin.</param>
        /// <param name="modal">The details form <see cref="AddServerModal"/>.</param>
        /// <param name="guild">The guild to register to.</param>
        /// <returns></returns>
        private Task<GameServerModel> RegisterGameServerAsync(string servername, AddServerModal modal, IGuild guild)
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
            catch (Exception)
            {
                return Task.FromException<GameServerModel>(new GameServerHostnameIPPortInvalidException(modal.HostnameIPPort));
            }

            TimeSpan updateInt;

            try
            {
                updateInt = TimeSpan.FromMinutes(byte.Parse(modal.UpdateInterval));
            }
            catch (Exception)
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
                using (var db = new GameBotDb(Context.Guild))
                {
                    db.Add(gs);
                    //db.SaveChanges();
                }
            }
            catch (Exception)
            {
                return Task.FromException<GameServerModel>(new GameServerDbAddException());
            }

            return Task.FromResult(gs);
        }

        private async Task RespondWithWatcherMessageAsync(GameServerModel reggs)
        {
            Program.Log.Debug($"User @{Context.User.Id} added game server {{ {reggs.ServerName} }} to watch list for guild ${Context.Guild.Id}.");
            await FollowupAsync($"{Context.User.Mention} Added a game server to watch list on this channel ({MentionUtils.MentionChannel(Context.Channel.Id)}).");

            var placeholder = await Context.Channel.SendMessageAsync(embed: new GameServerEmbedBuilder(reggs).Build());
            var thread = await (Context.Channel as SocketTextChannel)
                .CreateThreadAsync(reggs.DisplayName ?? "Discuss this game server", message: placeholder, invitable: true);

            reggs.ChannelId = Context.Channel.Id;
            reggs.MessageId = placeholder.Id;
            reggs.ThreadId = thread.Id;
        }
    }
}
