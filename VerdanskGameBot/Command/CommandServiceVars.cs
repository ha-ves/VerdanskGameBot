using Discord;
using Discord.WebSocket;
using System.Collections.Generic;

namespace VerdanskGameBot.Command
{
    internal static class ServerCmd
    {
        /// <summary>
        /// Description: <i>Game server commands.</i>
        /// </summary>
        internal const string Server = "server";

        /// <summary>
        /// Description: <i>Lists all game servers added to watch list.</i>
        /// </summary>
        internal const string List = "list";
        /// <summary>
        /// Description: <i>Add and show the server status info to this channel.</i>
        /// </summary>
        internal const string Add = "add";
        /// <summary>
        /// Description: <i>Move the server status info to this channel.</i>
        /// </summary>
        internal const string Movehere = "movehere";
        /// <summary>
        /// Description: <i>Remove a server status info from this discord guild/server.</i>
        /// </summary>
        internal const string Remove = "remove";
        /// <summary>
        /// Description: <i>Change a server status info of this discord guild/server.</i>
        /// </summary>
        internal const string Change = "change";
        /// <summary>
        /// Description: <i>Refresh a server status info of this discord guild/server.</i>
        /// </summary>
        internal const string Refresh = "refresh";
    }

    internal partial class CommandService
    {
        internal static SlashCommandOptionBuilder Servername = new()
        {
            Name = "servername",
            Description = "Private server name defined by you (Admin).",
            Type = ApplicationCommandOptionType.String,
            IsRequired = true,
        };

        
        internal static List<SlashCommandProperties> Commands = new()
        {
            //game servers command
            new SlashCommandBuilder
            {
                Name = ServerCmd.Server,
                Description = "Game server commands.",

                Options = new List<SlashCommandOptionBuilder>
                {
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.List,
                        Description = "Lists all game servers added to watch list.",
                        Type = ApplicationCommandOptionType.SubCommand
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Add,
                        Description = "Add and show the server status info to this channel.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Movehere,
                        Description = "Move the server status info to this channel.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Remove,
                        Description = "Remove a server status info from this discord guild/server.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Change,
                        Description = "Change a server status info of this discord guild/server.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = ServerCmd.Refresh,
                        Description = "Refresh a server status info of this discord guild/server.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    }
                }
            }.Build()
        };
    }
}