using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.Commands.GameServer
{
    [Group(CmdSlash.Prefix, CmdSlash.ServerDesc)]
    public partial class GameServerInteraction
    {
        internal static Dictionary<Guid, (DateTime since, object? obj)> _state = [];
        internal static readonly TimeSpan _stateTimeout = TimeSpan.FromMinutes(15);

        private readonly DbContextOptions<GameServerDb> _gsDbOpts = gsDbOpts;
        private readonly GamedigGamesJsonDocument _supportGames = supportGames;

        private static Logger? _logger;

        internal const byte CustomIdMaxLength = 100;
        internal const char CustomIdSeparator = ',';
        internal const string CustomIdSuffixRegex = @"\(([0-9a-fA-F]{32})\)$";

        #region Slash Commands

        internal static partial class CmdSlash
        {
            internal const string Prefix = "gameserver";
            internal const string ServerDesc = "Game server commands.";

            #region Add Server

            internal static class Add
            {
                internal const string Cmd = "add";
                internal const string Desc = "Add and show the server status info to this channel.";

                internal const string AddModal = "addmodal";
                internal const string AddModalRegex = "^" + AddModal + CustomIdSuffixRegex;

                internal const string ReAddBtn = "readdbtn";
                internal const string ReAddBtnRegex = "^" + ReAddBtn + CustomIdSuffixRegex;
            }

            #endregion

            #region Edit Server

            /// <summary>
            /// Description: <i>Change a server status info of this discord guild/server.</i><br/>
            /// See Also: <seealso cref="ChangeDesc"/>
            /// </summary>
            internal static class Edit
            {
                internal const string Cmd = "edit";
                internal const string Desc = "Edit a game server entry of this discord guild/server.";

                internal const string EditBtn = "editbtn";
                internal const string EditBtnRegex = "^" + EditBtn + CustomIdSuffixRegex;

                internal const string EditModal = "editmodal";
                internal const string EditModalRegex = "^" + EditModal + CustomIdSuffixRegex;

                internal const string ReEditBtn = "reeditbtn";
                internal const string ReEditBtnRegex = "^" + ReEditBtn + CustomIdSuffixRegex;
            }

            #endregion

            #region Refresh Server

            internal static class Refresh
            {
                internal const string Cmd = "refresh";
                internal const string Desc = "Refresh a server status info of this discord guild/server.";

            }

            #endregion

            #region List Servers

            internal static class List
            {
                internal const string Cmd = "list";
                internal const string Desc = "Lists all game servers added to watch list.";

                internal const int ChunkSize = 10;
                internal const string ListSuffixRegex = @"\(([-0-9]{1,})\)";

                internal const string NavBtns = "lsnavsbtn";
                internal const string NavBtnsRegex = "^" + NavBtns + ListSuffixRegex + CustomIdSuffixRegex;

                internal const string RefreshBtn = "lsrfshbtn";
                internal const string RefreshBtnRegex = "^" + RefreshBtn + CustomIdSuffixRegex;
            }

            #endregion

            /// <summary>
            /// Description: <i>Move the server status info to this channel.</i><br/>
            /// See Also: <seealso cref="MovehereDesc"/>
            /// </summary>
            internal const string Movehere = "movehere";
            internal const string MovehereDesc = "Move the server status info to this channel.";

            /// <summary>
            /// Description: <i>Remove a server status info from this discord guild/server.</i><br/>
            /// See Also: <seealso cref="RemoveDesc"/>
            /// </summary>
            internal const string Remove = "remove";
            internal const string RemoveDesc = "Remove a server status info from this discord guild/server.";
        }

        internal static partial class CmdSlashParams
        {
            internal static class ServerName
            {
                internal const string Name = "name";
                internal const string Desc = "Private server name defined by you (Admin).";

                internal const int MaxLength = 32;
            }

            internal static class MessageLink
            {
                internal const string Name = "message_link";
                internal const string Desc = "The Discord message link of the game server that was already added.";
            }
        }

        #endregion
    }
}
