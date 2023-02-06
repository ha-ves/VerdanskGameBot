using Discord;
using Discord.Interactions;
using Discord.Interactions.Builders;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.Commands.GameServer
{
    [Group(ServerCmd.Server, ServerCmd.ServerDesc)]
    internal partial class GameServerInteraction : InteractionModuleBase<SocketInteractionContext>
    {
        #region Adding Server '/server add'

        [SlashCommand(ServerCmd.Add, ServerCmd.AddDesc)]
        internal async Task ServerAddCmd_Exec(
            [Summary(ServerParam.Servername, ServerParamDesc.Servername)]
            [MaxLength(22)]
            string servername)
        {
            servername = servername.Normalize().ToLowerInvariant();

            if (!await Task.Run(async () =>
            {
                if (!servername.All(ch => char.IsLetter(ch)))
                {
                    await RespondAsync($"***servername*** : `{servername}` is invalid." +
                        $"Only letters are allowed.", ephemeral: true);

                    return false;
                }

                using (var db = new GameBotDb(Context.Guild))
                {
                    if (db.GameServers.Any(gs => gs.ServerName == servername))
                    {
                        await RespondAsync($"Failed to add game server because{Environment.NewLine}" +
                            $"servername : `{servername}`{Environment.NewLine}" +
                            $"already exist in watch list.", ephemeral: true);

                        return false;
                    }
                }

                return true;
            }))
                return;

            // TODO: Use encrypted servername when included in customid
            //var srvname_enc

            await Context.Interaction.RespondWithModalAsync<AddServerModal>(
                ServerCmd.Server + ',' + ServerCmd.AddModal + '^' + servername,
                modifyModal: modal =>
                {
                    modal.Title += $"'{servername}'";
                });
        }

        [ModalInteraction(ServerCmd.AddModal, TreatAsRegex = true)]
        internal async Task ServerAddModal_Exec(AddServerModal modal)
        {
            Program.Log.Trace($"User @{Context.User.Id} adding a game server to watch list for guild ${Context.Guild.Id} ...");
            await DeferAsync(ephemeral: true);

            var servername = (Context.Interaction as SocketModal).Data.CustomId.Split('^').Last();

            var reggs = RegisterNewGameServer(servername, modal, Context.Guild);

            if (!reggs.IsCompletedSuccessfully)
            {
                var invalid = " is INVALID.";

                switch (reggs.Exception.GetBaseException())
                {
                    case GameServerGameTypeInvalidException:
                        invalid = "Game Type Not Supported. Try using other game type with the same protocol.";
                        break;
                    case GameServerHostnameIPPortInvalidException:
                        invalid = "Hostname / IP : Game Port" + invalid;
                        break;
                    case GameServerUpdateIntervalInvalidException:
                        invalid = "Update Interval" + invalid;
                        break;
                    default:
                        invalid = "Sumthing wong, i can feel it. Please try again.";
                        break;
                }

                await RespondWithAddServerTryAgainAsync(modal, servername, invalid);

                return;
            }

            await RespondWithWatcherMessageAsync(reggs.Result);

            //(Context.Channel as SocketTextChannel).CreateThreadAsync()

        }

        #region Re Adding Server 'try again button'

        [ComponentInteraction(ServerCmd.ReAddBtn, TreatAsRegex = true)]
        public async Task ReAddServerBtn_Exec()
        {
            var embed = (Context.Interaction as SocketMessageComponent).Message.Embeds.First();
            var servername = embed.Title.Substring("Failed to add ".Length).Trim('\'');

            await RespondWithModalAsync(new AddServerModalBuilder(servername,
                embed.Fields[0].Value,
                embed.Fields[1].Value,
                embed.Fields[2].Value,
                string.IsNullOrEmpty(embed.Fields[3].Value.Trim('-')) ? string.Empty : embed.Fields[3].Value.Trim('-')
                ).Build());
        }

        #endregion

        #endregion
    }
}
