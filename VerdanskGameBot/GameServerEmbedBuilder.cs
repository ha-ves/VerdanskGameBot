﻿using Discord;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VerdanskGameBot
{
    internal class GameServerEmbedBuilder : EmbedBuilder
    {
        internal GameServerEmbedBuilder(GameServerModel server, IEmbed embed = null)
        {
            WithTitle(string.IsNullOrEmpty(server.DisplayName) ? (string.IsNullOrEmpty(embed.Title) ? "--Untitled Game Server--" : embed.Title) : server.DisplayName);
            WithDescription(string.IsNullOrEmpty(server.Description) ? (string.IsNullOrEmpty(embed.Description) ? "--Game server has no description, could be any game server available out there.--" : embed.Description) : ($"⠀{Environment.NewLine}" + server.Description + $"{Environment.NewLine}⠀"));
            WithThumbnailUrl(string.IsNullOrEmpty(server.ImageUrl) ? (!embed.Thumbnail.HasValue ? "https://cdn.discordapp.com/icons/790540532714831882/7449dcd6aded699bdbdec4718f66b6c8.webp" : embed.Thumbnail.ToString()) : server.ImageUrl);
            
            var rand = new Random((int)(DateTimeOffset.Now - server.AddedSince).Ticks);
            WithColor(new Color(rand.Next(255), rand.Next(255), rand.Next(255)));

            AddField(server.IsOnline ? ":green_circle: Online" : ":red_circle: Offline", (server.IsOnline ? "Server is Online" : $"Last online : " +
                (server.LastOnline > server.AddedSince ? server.LastOnline.ToString("MMM dd yyyy hh:mm tt") + $"{Environment.NewLine}*({GetLastTime(server.LastOnline)})*" : "Never")) + $"{Environment.NewLine}⠀", true);
            AddField("IP Address", server.IP.ToString(), true);
            AddField("Game Port", server.GamePort.ToString(), true);
            
            AddField("CLICK TO JOIN SERVER", (string.IsNullOrEmpty(server.GameLink) ? "--Server don't provide game link.--" : server.GameLink) + $"{Environment.NewLine}⠀", true);

            AddField("Players", $"{server.Players}/{server.MaxPlayers}", true);

            AddField("NOTE", (string.IsNullOrEmpty(server.Note) ? "--Empty--" : server.Note) + $"{Environment.NewLine}⠀");

            WithFooter($"Last checked ->");
            WithCurrentTimestamp();
        }

        private static string GetLastTime(DateTimeOffset lasttime)
        {
            var period = Period.Between(LocalDateTime.FromDateTime(lasttime.LocalDateTime), LocalDateTime.FromDateTime(DateTime.Now), PeriodUnits.AllUnits);

            var str = "";

            str += period.Years > 0 ? period.Years + " Years " : "";

            str += period.Months > 0 ? period.Months + " Months " : "";

            str += period.Weeks > 0 ? period.Weeks + " Weeks " : "";

            if(period.Years > 0)
                return str + "ago.";

            str += period.Days > 0 ? period.Days + " Days " : "";

            if (period.Months > 0)
                return str + "ago.";

            str += period.Hours > 0 ? period.Hours + " Hours " : "";

            if (period.Weeks > 0)
                return str + "ago.";

            str += period.Minutes > 0 ? period.Minutes + " Minutes " : "";

            if (str == "")
                return "a few seconds ago.";
            else
                return str + "ago.";
        }
    }
}