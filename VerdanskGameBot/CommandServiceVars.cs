using Discord;
using Discord.WebSocket;
using System.Collections.Generic;

namespace VerdanskGameBot
{
    internal partial class CommandService
    {
        internal static SlashCommandOptionBuilder Servername = new()
        {
            Name = "servername",
            Description = "Private server name defined by you (Admin).",
            Type = ApplicationCommandOptionType.String,
            IsRequired = true,
        };

        internal static List<SlashCommandBuilder> Commands = new()
        {
            //game servers command
            new SlashCommandBuilder
            {
                Name = "server",
                Description = "Game server commands.",

                Options = new List<SlashCommandOptionBuilder>
                {
                    new SlashCommandOptionBuilder
                    {
                        Name = "list",
                        Description = "Lists all game servers added to watch list.",
                        Type = ApplicationCommandOptionType.SubCommand
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = "add",
                        Description = "Add and show the server status info to this channel.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = "movehere",
                        Description = "Move the server status info to this channel.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    },
                    new SlashCommandOptionBuilder
                    {
                        Name = "remove",
                        Description = "Remove a server status info from this discord guild/server.",
                        Type = ApplicationCommandOptionType.SubCommand,
                        Options = new List<SlashCommandOptionBuilder> { Servername }
                    }
                }
            },

        };
    }
}