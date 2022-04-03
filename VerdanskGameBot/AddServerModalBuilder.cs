using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    internal class AddServerModalBuilder : ModalBuilder
    {
        internal AddServerModalBuilder(string servername)
        {
            WithTitle($"Add \"{servername}\" to watch list");
            WithCustomId(new CustomID
            {
                Source = "addserver",
                Options = new Dictionary<string, object> { { "servername", servername + " " } }
            }.Serialize());
            AddComponents(new List<IMessageComponent>
            {
                new TextInputBuilder()
                {
                    CustomId = "host_ip",
                    Label = "Hostname/Public IP Address",
                    Placeholder = "i.e. refuge.verdansk.org -or- 8.8.8.8",
                    Required = true,
                    Style = TextInputStyle.Short
                }.Build()
            }, 0);
            AddComponents(new List<IMessageComponent>
            {
                new TextInputBuilder
                {
                    CustomId = "game_port",
                    Label = "Port to join server (Game)",
                    Placeholder = "i.e. 12727",
                    Required = true,
                    Style = TextInputStyle.Short
                }.Build()
            }, 0);
            AddComponents(new List<IMessageComponent>
            {
                new TextInputBuilder
                {
                    CustomId = "rcon_port",
                    Label = "Port to check server (RCON)",
                    Placeholder = "i.e. 27015",
                    Required = true,
                    Style = TextInputStyle.Short
                }.Build()
            }, 0);
            AddComponents(new List<IMessageComponent>
            {
                new TextInputBuilder
                {
                    CustomId = "rcon_pass",
                    Label = "Password to check server (RCON)",
                    Placeholder = "i.e. yousux",
                    Required = true,
                    Style = TextInputStyle.Short
                }.Build()
            }, 0);
        }
    }
}