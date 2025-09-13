using Discord;
using Discord.Interactions;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Hosting;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;

namespace VerdanskGameBot.Commands.GameServer
{
    public partial class GameServerInteraction
    {
        #region Adding Server '/gameserver add'

        [SlashCommand(CmdSlash.Add.Cmd, CmdSlash.Add.Desc)]
        public async Task ServerAddCmd_Exec(
            [Summary(CmdSlashParams.ServerName.Name, CmdSlashParams.ServerName.Desc)]
            [MaxLength(CmdSlashParams.ServerName.MaxLength)]
            string servername)
        {
            string logprefix = $"User [{Context.User}] invoked '{CmdSlash.Prefix} {CmdSlash.Add.Cmd} {servername}'";

            if (!await ValidateSlashCmdAsync(logprefix)) return;

            servername = servername.Normalize().ToLowerInvariant();

            _logger?.ConditionalDebug($"{logprefix} for guild [{Context.Guild}]({Context.Guild.Id}) ...");

            if (char.IsAsciiLetter(servername[0]) && !servername.All(char.IsAsciiLetterOrDigit))
            {
                _logger?.ConditionalTrace($"Invalid servername: '{servername}'");
                await RespondAsync($"***servername*** : `{servername}` is invalid." +
                    $"Only alphanumerics(a-z,0-9) are allowed and FIRST character MUST NOT be a number.", ephemeral: true);
                return;
            }

            using var db = new GameServerReadOnlyDb(Context.Guild.Id, _gsDbOpts);
            try
            {
                if (await db.GameServers.AnyAsync(gs => gs.ServerName == servername))
                {
                    _logger?.ConditionalTrace($"Server `{servername}` already exists in watch list for guild ({Context.Guild.Id:X2}).");
                    await RespondAsync($"Failed to add game server because{Environment.NewLine}" +
                        $"servername : `{servername}`{Environment.NewLine}" +
                        $"already exist in watch list.", ephemeral: true);
                    return;
                }
            }
            catch (Exception e)
            {
                _logger?.ConditionalTrace(new DbNotSupportedException(Context.Guild.Id, e), 
                    $"Failed to add game server for guild [{Context.Guild}].");
                await RespondAsync("Failed to add game server. The bot encountered a problem for this discord server.", ephemeral: true);
                return;
            }

            var guid = Guid.NewGuid(); _state[guid] = (DateTime.Now, (servername, (object?)null, false));

            _logger?.Trace($"Sending modal to [{Context.User}] for adding game server '{servername}' for guild [{Context.Guild}] ...");

            await Context.Interaction.RespondWithModalAsync<ServerInputModal>(
                GetCustomId(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Add.AddModal, guid), 
                modifyModal: modal =>
                {
                    modal.Title += $"New Game Server `{servername}`";
                });
        }

        [ModalInteraction(CmdSlash.Add.AddModalRegex, TreatAsRegex = true)]
        public async Task ServerAddModal_Exec(string guidstr, ServerInputModal modal)
        {
            string logprefix = $"User [{Context.User}] submitted '{CmdSlash.Add.AddModal}'";

            if (!ValidateState(guidstr, out var guid, out var state, s => s is (string, ServerInputModal, bool)))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            _logger?.Trace($"Adding game server '{modal.Name}' for discord server [{Context.Guild.Name}] ...");

            (var retryembed, var retrybtns) = 
                GetRetryBuilders(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Add.ReAddBtn, modal, guid);

            var invalidstr = new StringBuilder();
            invalidstr.AppendLine("***(Try Again only valid for 15 minutes.)***");

            (var servername, _, var retry) = ((string, object, bool))state.obj!;

            _state[guid] = (DateTime.Now, (servername, modal, true));

            if (!await ValidateInputModalAsync(modal, invalidstr))
            {
                await FollowupAsync(ephemeral: true, components: retrybtns.Build(),
                        embed: retryembed.WithDescription(invalidstr.ToString()).Build());
                return;
            }

            var gs = new GameServerModel
            {
                AddedBy = Context.User.Id,
                AddedSince = DateTimeOffset.Now,
                ChannelId = Context.Channel.Id,
            };

            SetServerValues(modal, gs);

            if (!string.IsNullOrWhiteSpace(modal.Note))
                gs.Note = modal.Note?.Trim();

            var placeholder = await Context.Channel.SendMessageAsync(embed: new GameServerEmbedBuilder(gs).Build());
            var thread = await (Context.Channel as SocketTextChannel)!
                .CreateThreadAsync("Discuss this game server", message: placeholder, invitable: true);

            gs.ChannelId = Context.Channel.Id;
            gs.MessageId = placeholder.Id;
            gs.ThreadId = thread.Id;
            gs.NextUpdateAt = DateTimeOffset.UtcNow;

            try
            {
                using var db = new GameServerDb(Context.Guild.Id, _gsDbOpts);
                db.SetUserContext(Context.User.Id, Context.Interaction.FormatUserContext(CmdSlash.Add.AddModal));
                await db.AddAsync(gs);
                await db.SaveChangesAsync();

                if (retry)
                    await Context.Interaction.DeleteOriginalResponseAsync();

                _logger?.Debug($"User @{Context.User.Id} added game server {{ {gs.ServerName} }} to watch list for guild ${Context.Guild.Id}.");
                await FollowupAsync($"{Context.User.Mention} Added a game server to watch list on this channel ({placeholder.GetJumpUrl()}).", ephemeral: true);

                await GameServerWatcher.TriggerAsync();

                // TODO: announce added game server to news/broadcast/announcement channel
                //Context.Guild.TextChannels.First(tc => tc.GetChannelType() == ChannelType.News).SendMessageAsync;
            }
            catch (Exception exc)
            {
                _logger?.Trace(exc, "Failed to save game server to database.");
                invalidstr.AppendLine("Failed to save game server to database.");
                await FollowupAsync(ephemeral: true, components: retrybtns.Build(),
                    embed: retryembed.WithDescription(invalidstr.ToString()).Build());

                await thread.DeleteAsync();
                await placeholder.DeleteAsync();

                return;
            }
        }

        #region Re Adding Server 'try again button'

        [ComponentInteraction(CmdSlash.Add.ReAddBtnRegex, TreatAsRegex = true)]
        public async Task ReAddServerBtn_Exec(string guidstr)
        {
            if (!ValidateState(guidstr, out var guid, out var state,
                    s => s is ServerInputModal m && !string.IsNullOrWhiteSpace(m.Name)))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            var addmodal = (state.obj as ServerInputModal)!;
            await RespondWithModalAsync<ServerInputModal>(
                GetCustomId(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Add.AddModal, guid),
                modifyModal: modal =>
                {
                    modal.Title += $"New Game Server `{addmodal.Name}`";
                    modal.UpdateTextInput(
                        ServerInputModal.GameTypeId, addmodal.GameType);
                    modal.UpdateTextInput(
                        ServerInputModal.HostIPId, addmodal.HostIP);
                    modal.UpdateTextInput(
                        ServerInputModal.GamePortId, addmodal.GamePort);
                    modal.UpdateTextInput(
                        ServerInputModal.UpdateIntId, addmodal.UpdateMin);
                    modal.UpdateTextInput(
                        ServerInputModal.NoteId, addmodal.Note);
                });
        }

        #endregion

        #endregion

        #region Modals

        public class ServerInputModal : IModal
        {
            public string Title => "📝";

            public string? Name { get; set; }

            [RequiredInput]
            [InputLabel("Game Type")]
            [ModalTextInput(GameTypeId,
                TextInputStyle.Short,
                "Visit https://github.com/gamedig/node-gamedig#supported")]
            public string? GameType { get; set; }

            [RequiredInput]
            [InputLabel("Hostname/IP Address")]
            [ModalTextInput(HostIPId,
                TextInputStyle.Short,
                "i.e. refuge.verdansk.net -or- 127.1.2.3")]
            public string? HostIP { get; set; }
            public IPAddress[]? IPs { get; set; }

            [RequiredInput(false)]
            [InputLabel("Game Port")]
            [ModalTextInput(GamePortId,
                TextInputStyle.Short,
                "i.e. 1 to 65535")]
            public string? GamePort { get; set; }
            public ushort GamePortNum;

            [RequiredInput]
            [InputLabel("Update Interval")]
            [ModalTextInput(UpdateIntId,
                TextInputStyle.Short,
                "Watch Update Interval (in minutes, min. 1, max. 99)",
                minLength: 1, maxLength: 2)]
            public string? UpdateMin { get; set; }
            public byte UpdateMinNum;

            [RequiredInput(false)]
            [InputLabel("Note (Will be Shown)")]
            [ModalTextInput(NoteId,
                TextInputStyle.Paragraph,
                "i.e Discord-wide specified password: 1234. Markdown supported.")]
            public string? Note { get; set; }


            internal const string GameTypeId = "gametype";
            internal const string HostIPId = "hostip";
            internal const string GamePortId = "gameport";
            internal const string UpdateIntId = "updateint";
            internal const string NoteId = "note";
        }

        #endregion
    }
}
