using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;

namespace VerdanskGameBot.Commands.GameServer
{
    internal partial class GameServerInteraction
    {
        #region Slash Commands

        internal static partial class ServerCmd
        {
            /// <summary>
            /// Description: <i>Game server commands.</i><br/>
            /// See Also: <seealso cref="ServerCmd.ServerDesc"/>
            /// </summary>
            internal const string Server = "server";

            /// <summary>
            /// Description: <i>Lists all game servers added to watch list.</i><br/>
            /// See Also: <seealso cref="ServerCmd.ListDesc"/>
            /// </summary>
            internal const string List = "list";
            /// <summary>
            /// Description: <i>Add and show the server status info to this channel.</i><br/>
            /// See Also: <seealso cref="ServerCmd.AddDesc"/>
            /// </summary>
            internal const string Add = "add";
            /// <summary>
            /// Description: <i>Move the server status info to this channel.</i><br/>
            /// See Also: <seealso cref="ServerCmd.MovehereDesc"/>
            /// </summary>
            internal const string Movehere = "movehere";
            /// <summary>
            /// Description: <i>Remove a server status info from this discord guild/server.</i><br/>
            /// See Also: <seealso cref="ServerCmd.RemoveDesc"/>
            /// </summary>
            internal const string Remove = "remove";
            /// <summary>
            /// Description: <i>Change a server status info of this discord guild/server.</i><br/>
            /// See Also: <seealso cref="ServerCmd.ChangeDesc"/>
            /// </summary>
            internal const string Change = "change";
            /// <summary>
            /// Description: <i>Refresh a server status info of this discord guild/server.</i><br/>
            /// See Also: <seealso cref="ServerCmd.RefreshDesc"/>
            /// </summary>
            internal const string Refresh = "refresh";
            /// <summary>
            /// Description: <i>Refresh a server status info of this discord guild/server.</i><br/>
            /// See Also: <seealso cref="ServerCmd.AddModal"/>
            /// </summary>
            internal const string AddModal = "addmodal";
            internal const string ReAddBtn = "readdbtn";
        }

        #region Description

        internal static partial class ServerCmd
        {
            /// <summary>
            /// Description for <see cref="ServerCmd.Server"/>
            /// </summary>
            internal const string ServerDesc = "Game server commands.";

            /// <summary>
            /// Description for <see cref="ServerCmd.List"/>
            /// </summary>
            internal const string ListDesc = "Lists all game servers added to watch list.";
            /// <summary>
            /// Description for <see cref="ServerCmd.Add"/>
            /// </summary>
            internal const string AddDesc = "Add and show the server status info to this channel.";
            /// <summary>
            /// Description for <see cref="ServerCmd.Movehere"/>
            /// </summary>
            internal const string MovehereDesc = "Move the server status info to this channel.";
            /// <summary>
            /// Description for <see cref="ServerCmd.Remove"/>
            /// </summary>
            internal const string RemoveDesc = "Remove a server status info from this discord guild/server.";
            /// <summary>
            /// Description for <see cref="ServerCmd.Change"/>
            /// </summary>
            internal const string ChangeDesc = "Change a server status info of this discord guild/server.";
            /// <summary>
            /// Description for <see cref="ServerCmd.Refresh"/>
            /// </summary>
            internal const string RefreshDesc = "Refresh a server status info of this discord guild/server.";
        }

        #endregion

        #endregion

        internal static class ServerParam
        {
            internal const string Servername = "name";
        }

        internal static class ServerParamDesc
        {
            internal const string Servername = "Private server name defined by you (Admin).";
        }

        internal class AddServerModalBuilder : ModalBuilder
        {
            public AddServerModalBuilder(string servername, string gametype = "", string hostnameip = "", string updateint = "", string note = "")
            {
                WithTitle(servername);
                WithCustomId(ServerCmd.Server + ',' + ServerCmd.AddModal + '^' + servername);
                AddTextInput("Game Type", "gametype", TextInputStyle.Short,
                    "Visit https://github.com/gamedig/node-gamedig#supported",
                    minLength: 2, maxLength: 31, value: string.IsNullOrEmpty(gametype) ? null : gametype, required: true);
                AddTextInput("Hostname/IP Address & Port", "hostnameip", TextInputStyle.Short,
                    "i.e. refuge.verdansk.net:5555 -or- 127.1.2.3:12727",
                    value: string.IsNullOrEmpty(hostnameip) ? null : hostnameip, required: true);
                AddTextInput("Update Interval", "updateint", TextInputStyle.Short,
                    "Watch Update Interval (in Minutes, min. 1, max. 99)",
                    minLength: 1, maxLength: 2, value: string.IsNullOrEmpty(updateint) ? null : updateint, required: true);
                AddTextInput("Note", "note", TextInputStyle.Paragraph,
                    "Notes i.e 'password: 1234'", value: string.IsNullOrEmpty(note) ? null : note, required: false);
            }
        }

        internal class AddServerModal : IModal
        {
            public string Title => "Add new Server : ";

            [RequiredInput]
            [InputLabel("Game Type")]
            [ModalTextInput("gametype",
                TextInputStyle.Short,
                "Visit https://github.com/gamedig/node-gamedig#supported",
                minLength: 2, maxLength: 31)]
            public string GameType { get; set; }

            [RequiredInput]
            [InputLabel("Hostname/IP Address & Port")]
            [ModalTextInput("hostnameip",
                TextInputStyle.Short,
                "i.e. refuge.verdansk.net:5555 -or- 127.1.2.3:12727")]
            public string HostnameIPPort { get; set; }

            [RequiredInput]
            [InputLabel("Update Interval (minutes)")]
            [ModalTextInput("updateint",
                TextInputStyle.Short,
                "Watch Update Interval (in Minutes, min. 1, max. 99)",
                minLength: 1, maxLength: 2)]
            public string UpdateInterval { get; set; }

            [RequiredInput(false)]
            [InputLabel("Note")]
            [ModalTextInput("note",
                TextInputStyle.Paragraph,
                "Notes i.e 'password: 1234'")]
            public string Note { get; set; }
        }
    }
}
