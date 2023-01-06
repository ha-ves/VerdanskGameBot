using Discord;
using Discord.WebSocket;
using System.Collections.Generic;

namespace VerdanskGameBot.Command
{
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
    }

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

    internal static class ServerParam
    {
        internal const string Servername = "name";
    }

    internal static class ServerParamDesc
    {
        internal const string Servername = "Private server name defined by you (Admin).";
    }

    internal partial class CommandService
    {
        internal static SlashCommandOptionBuilder Servername = new()
        {
            Name = ServerParam.Servername,
            Description = ServerParamDesc.Servername,
            Type = ApplicationCommandOptionType.String,
            IsRequired = true,
        };

        
        internal static List<SlashCommandProperties> Commands = new()
        {
            //game servers command
            new SlashCommandBuilder
            {
                Name = ServerCmd.Server,
                Description = ServerCmd.ServerDesc,

                Options = new List<SlashCommandOptionBuilder>
                {
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.List,
                        Description = ServerCmd.ListDesc,
                        Type = ApplicationCommandOptionType.SubCommand
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Add,
                        Description = ServerCmd.AddDesc,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Movehere,
                        Description = ServerCmd.MovehereDesc,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Remove,
                        Description = ServerCmd.RemoveDesc,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Change,
                        Description = ServerCmd.ChangeDesc,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Refresh,
                        Description = ServerCmd.RefreshDesc,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    }
                }
            }.Build()
        };
    }
}