using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    internal class GameServerEmbedBuilder : EmbedBuilder
    {
        internal Task<GameServerEmbedBuilder> Get(GameServerModel server)
        {
            WithTitle(server.DisplayName);
            WithDescription(server.Description);
            if (server.ImageUrl != "")
                WithImageUrl(server.ImageUrl);
            else
                WithImageUrl("");

            AddField(server.IsOnline ? ":green_circle: Online" : ":red_circle: Offline", server.IsOnline ? "Server is Online" : $"Last online : "
                    + (server.LastOnline > DateTimeOffset.MinValue ?
                    server.LastOnline.ToString() + $"*({GetLastTime(DateTimeOffset.Now - server.LastOnline)})*\r\n" : "Never")
                );
            AddField("IP Address", server.IP.ToString(), true);
            AddField("Game Port", server.GamePort.ToString(), true);

            WithFooter($"Last checked {GetLastTime(DateTimeOffset.Now - server.LastUpdate)}\r\n");
            WithCurrentTimestamp();

            return Task.FromResult(this);
        }

        private string GetLastTime(TimeSpan time)
        {
            var str = "";
            str += time.Days > 0 ? $"{time.Days} days " : "";
            str += time.Hours > 0 ? $"{time.Hours} hours " : "";
            str += time.Minutes > 0 ? $"{time.Minutes} minutes " : "";

            if (str == "")
                return "a few seconds ago.";
            else
                return str + "ago.";
        }
    }
}