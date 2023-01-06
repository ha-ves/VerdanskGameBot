using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;

namespace VerdanskGameBot.Command
{
    internal class AddServerModalBuilder : ModalBuilder
    {
        internal AddServerModalBuilder(string servername)
        {
            WithTitle($"Add \"{servername}\" to watch list");
            WithCustomId(new CustomID
            {
                Source = CustomIDs.AddServerSource,
                Options = new Dictionary<CustomIDs, string> { { CustomIDs.ServernameOption, servername } }
            }.Serialize());

            AddTextInput(new TextInputBuilder()
            {
                CustomId = CustomIDs.GameType.ToString("d"),
                Label = "Game Type",
                Placeholder = "i.e. valve",
                Required = true,
                Style = TextInputStyle.Short
            });

            AddTextInput(new TextInputBuilder()
            {
                CustomId = CustomIDs.HostIPPort.ToString("d"),
                Label = "Hostname/Public IP & Port",
                Placeholder = "i.e. refuge.verdansk.net:5555 -or- 127.1.2.3:12727",
                Required = true,
                Style = TextInputStyle.Short
            });

            AddTextInput(new TextInputBuilder
            {
                CustomId = CustomIDs.UpdateInterval.ToString("d"),
                Label = "Watch Update Interval (in Minutes)",
                Placeholder = "i.e. 15",
                Required = true,
                Style = TextInputStyle.Short
            });

            AddTextInput(new TextInputBuilder
            {
                CustomId = CustomIDs.Note.ToString("d"),
                Label = "Note",
                Required = false,
                Style = TextInputStyle.Paragraph
            });
        }
    }
}