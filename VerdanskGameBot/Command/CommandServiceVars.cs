using Discord;
using Discord.WebSocket;
using System.Collections.Generic;

namespace VerdanskGameBot.Command
{
    internal static class ServerCmd
    {
        /// <summary>
        /// Description: <i>Game server commands.</i><br/>
        /// See Also: <seealso cref="ServerDesc.Server"/>
        /// </summary>
        internal const string Server = "server";

        /// <summary>
        /// Description: <i>Lists all game servers added to watch list.</i><br/>
        /// See Also: <seealso cref="ServerDesc.List"/>
        /// </summary>
        internal const string List = "list";
        /// <summary>
        /// Description: <i>Add and show the server status info to this channel.</i><br/>
        /// See Also: <seealso cref="ServerDesc.Add"/>
        /// </summary>
        internal const string Add = "add";
        /// <summary>
        /// Description: <i>Move the server status info to this channel.</i><br/>
        /// See Also: <seealso cref="ServerDesc.Movehere"/>
        /// </summary>
        internal const string Movehere = "movehere";
        /// <summary>
        /// Description: <i>Remove a server status info from this discord guild/server.</i><br/>
        /// See Also: <seealso cref="ServerDesc.Remove"/>
        /// </summary>
        internal const string Remove = "remove";
        /// <summary>
        /// Description: <i>Change a server status info of this discord guild/server.</i><br/>
        /// See Also: <seealso cref="ServerDesc.Change"/>
        /// </summary>
        internal const string Change = "change";
        /// <summary>
        /// Description: <i>Refresh a server status info of this discord guild/server.</i><br/>
        /// See Also: <seealso cref="ServerDesc.Refresh"/>
        /// </summary>
        internal const string Refresh = "refresh";
    }

    internal static class ServerDesc
    {
        /// <summary>
        /// Description for <see cref="ServerCmd.Server"/>
        /// </summary>
        internal const string Server = "Game server commands.";

        /// <summary>
        /// Description for <see cref="ServerCmd.List"/>
        /// </summary>
        internal const string List = "Lists all game servers added to watch list.";
        /// <summary>
        /// Description for <see cref="ServerCmd.Add"/>
        /// </summary>
        internal const string Add = "Add and show the server status info to this channel.";
        /// <summary>
        /// Description for <see cref="ServerCmd.Movehere"/>
        /// </summary>
        internal const string Movehere = "Move the server status info to this channel.";
        /// <summary>
        /// Description for <see cref="ServerCmd.Remove"/>
        /// </summary>
        internal const string Remove = "Remove a server status info from this discord guild/server.";
        /// <summary>
        /// Description for <see cref="ServerCmd.Change"/>
        /// </summary>
        internal const string Change = "Change a server status info of this discord guild/server.";
        /// <summary>
        /// Description for <see cref="ServerCmd.Refresh"/>
        /// </summary>
        internal const string Refresh = "Refresh a server status info of this discord guild/server.";
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
                Description = ServerDesc.Server,

                Options = new List<SlashCommandOptionBuilder>
                {
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.List,
                        Description = ServerDesc.List,
                        Type = ApplicationCommandOptionType.SubCommand
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Add,
                        Description = ServerDesc.Add,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Movehere,
                        Description = ServerDesc.Movehere,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Remove,
                        Description = ServerDesc.Remove,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Change,
                        Description = ServerDesc.Change,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Refresh,
                        Description = ServerDesc.Refresh,
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    }
                }
            }.Build()
        };
    }
}