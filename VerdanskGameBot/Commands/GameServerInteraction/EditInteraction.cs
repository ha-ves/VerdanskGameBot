using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Build.Tasks;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;
using static VerdanskGameBot.Commands.GameServer.GameServerInteraction.CmdSlashParams;

namespace VerdanskGameBot.Commands.GameServer
{
    public partial class GameServerInteraction
    {
        #region Edit server '/gameserver edit'

        [SlashCommand(CmdSlash.Edit.Cmd, CmdSlash.Edit.Desc)]
        public async Task ServerEditCmd_Exec(
            [Summary(CmdSlashParams.ServerName.Name, CmdSlashParams.ServerName.Desc)]
            [MaxLength(CmdSlashParams.ServerName.MaxLength)]
            string? servername = null,
            [Summary(CmdSlashParams.MessageLink.Name, CmdSlashParams.MessageLink.Desc)]
            string? messageUrl = null
            )
        {
            string logprefix = $"User [{Context.User}] invoked '{CmdSlash.Prefix} {CmdSlash.Edit.Cmd}'";

            if (!await ValidateSlashCmdAsync(logprefix)
                || !await ValidateEntryParamsAsync(servername, messageUrl, logprefix)) return;

            await DeferAsync(ephemeral: true);

            var foundServer = await GetExistingServerAsync(logprefix, messageUrl, servername);
            if (foundServer is null) return;

            var guid = Guid.NewGuid(); _state[guid] = (DateTime.Now, (foundServer, (ServerInputModal?)null, false));

            var editBtn = new ComponentBuilder().WithButton("Edit Server", style: ButtonStyle.Primary,
                customId: GetCustomId(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Edit.EditBtn, guid));

            await FollowupAsync($"Click the button below to continue editing the game server `{foundServer.ServerName}`.",
                ephemeral: true, components: editBtn.Build());
        }

        [ComponentInteraction(CmdSlash.Edit.EditBtnRegex, TreatAsRegex = true)]
        public async Task ServerEditBtn_Exec(string guidstr)
        {
            if (!ValidateState(guidstr, out var guid, out var state, s => s is (GameServerModel, ServerInputModal, bool)))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            (var gs, _, var retry) = ((GameServerModel, ServerInputModal, bool))state.obj!;

            _logger?.Trace($"Sending modal to [{Context.User}] for editing game server '{gs.ServerName}' for guild [{Context.Guild}] ...");

            await RespondWithModalAsync<ServerInputModal>(
                GetCustomId(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Edit.EditModal, guid),
                modifyModal: modal =>
                {
                    modal.Title += $"Editing Game Server `{gs.ServerName}`";
                    modal.UpdateTextInput(
                        ServerInputModal.GameTypeId, gs.GameType);
                    modal.UpdateTextInput(
                        ServerInputModal.HostIPId, gs.IP);
                    modal.UpdateTextInput(
                        ServerInputModal.GamePortId, gs.GamePort);
                    modal.UpdateTextInput(
                        ServerInputModal.UpdateIntId, (int)gs.UpdateInterval.TotalMinutes);
                    modal.UpdateTextInput(
                        ServerInputModal.NoteId, gs.Note);
                });
        }

        [ModalInteraction(CmdSlash.Edit.EditModalRegex, TreatAsRegex = true)]
        internal async Task ServerEditModal_Exec(string guidstr, ServerInputModal modal)
        {
            string logprefix = $"User [{Context.User}] submitted '{CmdSlash.Edit.EditModal}'";

            if (!ValidateState(guidstr, out var guid, out var state, s => s is (GameServerModel, ServerInputModal, bool)))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            await DeferAsync(ephemeral: true);

            (var gs, _, var retry) = ((GameServerModel, ServerInputModal, bool))state.obj!;
            modal.Name = gs.ServerName;

            _logger?.Trace($"Editing game server '{modal.Name}' for discord server [{Context.Guild.Name}] ...");

            (var retryembed, var retrybtns) = 
                GetRetryBuilders(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Edit.ReEditBtn, modal, guid);

            var invalidstr = new StringBuilder();
            invalidstr.AppendLine("***(Try Again only valid for 15 minutes.)***");

            _state[guid] = (DateTime.Now, (gs, modal, true));

            if (!await ValidateInputModalAsync(modal, invalidstr))
            {
                await FollowupAsync(ephemeral: true, components: retrybtns.Build(),
                        embed: retryembed.WithDescription(invalidstr.ToString()).Build());
                return;
            }

            SetServerValues(modal, gs);
            ResetQueried(gs);

            gs.LastModifiedBy = Context.User.Id;
            gs.LastModifiedSince = DateTimeOffset.UtcNow;

            try
            {
                using var editDb = new GameServerDb(Context.Guild.Id, _gsDbOpts);
                using var transact = await editDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

                gs.NextUpdateAt = DateTimeOffset.UtcNow;
                editDb.Update(gs);
                editDb.SetUserContext(Context.User.Id, Context.Interaction.FormatUserContext(CmdSlash.Edit.Cmd));

                await editDb.SaveChangesAsync();
                await transact.CommitAsync();

                _logger?.Debug($"User @{Context.User.Id} edited game server {{ {gs.ServerName} }} for guild ${Context.Guild.Id}.");
                var watchMsg = await Context.Guild.GetTextChannel(gs.ChannelId).GetMessageAsync(gs.MessageId);
                await FollowupAsync($"{Context.User.Mention} Edited a game server to watch list on this channel ({watchMsg.GetJumpUrl()}).", ephemeral: true);

                await GameServerWatcher.TriggerAsync();

                await FollowupAsync($"Game server `{gs.ServerName}` at {watchMsg.GetJumpUrl()} queued for refresh.", ephemeral: true);
            }
            catch (Exception exc)
            {
                _logger?.Trace(exc, "Failed to save game server to database.");
                invalidstr.AppendLine("Failed to save game server to database.");
                await FollowupAsync(ephemeral: true, components: retrybtns.Build(),
                    embed: retryembed.WithDescription(invalidstr.ToString()).Build());

                return;
            }
        }

        #region Re Editing Server 'try again button'

        [ComponentInteraction(CmdSlash.Edit.ReEditBtnRegex, TreatAsRegex = true)]
        public async Task ReEditServerBtn_Exec(string guidstr)
        {
            if (!ValidateState(guidstr, out var guid, out var state, s => s is (GameServerModel, ServerInputModal, bool)))
            {
                await RespondAsync("Invalid data / Interaction expired. Please try again from the beginning.", ephemeral: true);
                return;
            }

            (var gs, var modal, var retry) = ((GameServerModel, ServerInputModal, bool))state.obj!;

            await base.RespondWithModalAsync<ServerInputModal>(
                GetCustomId(CmdSlash.Prefix + CustomIdSeparator + CmdSlash.Edit.EditModal, guid),
                modifyModal: ml =>
                {
                    ml.Title += $"Changing Game Server `{modal.Name}`";
                    ml.UpdateTextInput(
                     ServerInputModal.GameTypeId, modal.GameType);
                    ml.UpdateTextInput(
                     ServerInputModal.HostIPId, modal.HostIP);
                    ml.UpdateTextInput(
                     ServerInputModal.GamePortId, modal.GamePort);
                    ml.UpdateTextInput(
                     ServerInputModal.UpdateIntId, modal.UpdateMin);
                    ml.UpdateTextInput(
                        ServerInputModal.NoteId, modal.Note);
                });
        }

        #endregion

        #endregion
    }
}
