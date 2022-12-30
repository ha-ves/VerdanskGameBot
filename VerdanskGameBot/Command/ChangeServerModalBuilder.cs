using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer;

namespace VerdanskGameBot.Command
{
    internal class ChangeServerModalBuilder : ModalBuilder
    {
        internal ChangeServerModalBuilder(GameServer.GameServerModel gameserver)
        {
            WithTitle($"Change \"{gameserver.ServerName}\" game server");
            WithCustomId(new CustomID
            {
                Source = CustomIDs.ChangeServerSource,
                Options = new Dictionary<CustomIDs, string> { { CustomIDs.ServernameOption, gameserver.ServerName } }
            }.Serialize());

            AddTextInput(new TextInputBuilder()
            {
                CustomId = CustomIDs.GameType.ToString("d"),
                Label = "Game Type",
                Placeholder = "i.e. przomboid",
                Required = true,
                Style = TextInputStyle.Short,
                Value = gameserver.GameType
            });

            AddTextInput(new TextInputBuilder()
            {
                CustomId = CustomIDs.HostIPPort.ToString("d"),
                Label = "Hostname/Public IP & Port",
                Placeholder = "i.e. refuge.verdansk.net:5555 -or- 127.1.2.3:12727",
                Required = true,
                Style = TextInputStyle.Short,
                Value = $"{gameserver.IP}:{gameserver.GamePort}"
            });

            AddTextInput(new TextInputBuilder
            {
                CustomId = CustomIDs.UpdateInterval.ToString("d"),
                Label = "Watch Update Interval (in Minutes)",
                Placeholder = "i.e. 15",
                Required = true,
                Style = TextInputStyle.Short,
                Value = gameserver.UpdateInterval.TotalMinutes.ToString()
            });

            AddTextInput(new TextInputBuilder
            {
                CustomId = CustomIDs.Note.ToString("d"),
                Label = "Note",
                Required = false,
                Style = TextInputStyle.Paragraph,
                Value = gameserver.Note
            });
        }
    }
}
