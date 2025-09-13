using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;
using VerdanskGameBot.GameServer.Db;
using VerdanskGameBot.GameServer.Db.Models;

namespace VerdanskGameBot.GameServer
{
    internal class GameServerEmbedBuilder : EmbedBuilder
    {
        internal GameServerEmbedBuilder(GameServerModel server)
        {
            Title = server.DisplayName ?? "New Game Server.";
            Description = Environment.NewLine
                        + server.Description ?? "A new game server is being added."
                        + Environment.NewLine;

            ImageUrl = ThumbnailUrl = server.ImageUrl;

            Color = new Color(Random.Shared.Next(255), Random.Shared.Next(255), Random.Shared.Next(255));

            var isonlinestr = server.IsOnline ? ":green_circle: Online" : ":red_circle: Offline";
            var lastonlinetimestr = "Last Online : " + (server.LastOnline is not null ?
                $"<t:{server.LastOnline.Value.ToUnixTimeSeconds()}:R>" : "Never");

            AddField(isonlinestr, (!server.IsOnline ? lastonlinetimestr : '.') + Environment.NewLine, true);

            AddField("IP Address", server.IP?.ToString() ?? "N/A", true);
            AddField("Game Port", server.GamePort == 0 ? "N/A" : server.GamePort.ToString(), true);

            AddField("Join This Server", server.GameLink ?? "-- Server doesn't provide join link. --", true);

            AddField("Players", $"{server.Players}/{server.MaxPlayers}", true);

            AddField("Notes", (string.IsNullOrWhiteSpace(server.Note) ? '-' : server.Note) + Environment.NewLine);

            var now = DateTimeOffset.UtcNow;
            var clockUnicode = Helper.ClockEmoji(now.Hour % 12, (now.Minute / 30) * 30);

            WithFooter($"Last checked -> {clockUnicode}");

            WithCurrentTimestamp();
        }
    }
}