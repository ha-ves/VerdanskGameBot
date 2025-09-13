using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;

namespace VerdanskGameBot.Commands.GameServer
{
    public partial class GameServerInteraction
    {
        #region Refresh server status '/gameserver refresh'

        [SlashCommand(CmdSlash.Refresh.Cmd, CmdSlash.Refresh.Desc)]
        public async Task ServerRefreshCmd_Exec(
            [Summary(CmdSlashParams.ServerName.Name, CmdSlashParams.ServerName.Desc)]
            [MaxLength(CmdSlashParams.ServerName.MaxLength)]
            string? servername = null,
            [Summary(CmdSlashParams.MessageLink.Name, CmdSlashParams.MessageLink.Desc)]
            string? messageUrl = null
            )
        {
            string logprefix = $"User [{Context.User}] invoked '{CmdSlash.Prefix} {CmdSlash.Refresh.Cmd}'";

            if (!await ValidateSlashCmdAsync(logprefix)
                || !await ValidateEntryParamsAsync(servername, messageUrl, logprefix)) return;

            await DeferAsync(ephemeral: true);

            var foundServer = await GetExistingServerAsync(logprefix, messageUrl, servername);
            if (foundServer is null) return;

            try
            {
                using var editDb = new GameServerDb(Context.Guild.Id, _gsDbOpts);
                using var transact = await editDb.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

                foundServer.NextUpdateAt = DateTimeOffset.UtcNow;
                editDb.Update(foundServer);
                editDb.SetUserContext(Context.User.Id, Context.Interaction.FormatUserContext(CmdSlash.Refresh.Cmd));

                await editDb.SaveChangesAsync();
                await transact.CommitAsync();

                await GameServerWatcher.TriggerAsync();

                var watchMsg = await Context.Guild.GetTextChannel(foundServer.ChannelId).GetMessageAsync(foundServer.MessageId);
                await FollowupAsync($"Game server `{foundServer.ServerName}` at {watchMsg.GetJumpUrl()} queued for refresh.");
            }
            catch (Exception ex)
            {
                _logger?.ConditionalTrace(ex, $"{logprefix} - Failed to update game server '{foundServer.ServerName}' in database!");
                await FollowupAsync($"❌ Failed to queue game server `{foundServer.ServerName}` for refresh. " +
                    $"Please try again later or contact bot admin.");
                return;
            }
        }

        #endregion
    }
}
