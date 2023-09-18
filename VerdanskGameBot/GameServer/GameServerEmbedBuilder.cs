using Discord;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.GameServer
{
    internal class GameServerEmbedBuilder : EmbedBuilder
    {
        internal GameServerEmbedBuilder(GameServerModel server)
        {
            var title = ReplaceIfNullOrEmpty(server.DisplayName, string.Empty);
            var desc = Environment.NewLine
                       + ReplaceIfNullOrEmpty(server.Description, string.Empty)
                       + Environment.NewLine;
            var img = ReplaceIfNullOrEmpty(server.ImageUrl, string.Empty);

            WithTitle(title);
            WithDescription(desc);
            WithImageUrl(img);

            PopulateFields(server);
        }

        internal GameServerEmbedBuilder(GameServerModel server, IEmbed embed)
        {
            var title = ReplaceIfNullOrEmpty(server.DisplayName, embed.Title);
            var desc = Environment.NewLine
                       + ReplaceIfNullOrEmpty(server.Description, embed.Description)
                       + Environment.NewLine;
            var img = ReplaceIfNullOrEmpty(server.ImageUrl, embed.Image.Value.Url);

            WithTitle(title);
            WithDescription(desc);
            WithImageUrl(img);

            PopulateFields(server);
        }

        private void PopulateFields(GameServerModel server)
        {
            var rand = new Random((int)(DateTimeOffset.Now - server.AddedSince).Ticks);
            WithColor(new Color(rand.Next(255), rand.Next(255), rand.Next(255)));

            var isonlinestr = server.IsOnline ? ":green_circle: Online" : ":red_circle: Offline";
            var lastonlinetimestr = "Last Online : " + (server.LastOnline is not null ? $"<t:{server.LastOnline.Value.ToUnixTimeSeconds()}:R>" : "Never");
            AddField(isonlinestr, (!server.IsOnline ? lastonlinetimestr : "") + Environment.NewLine, true);

            AddField("IP Address", server.IP.ToString(), true);
            AddField("Game Port", server.GamePort.ToString(), true);

            var joinserver = ReplaceIfNullOrEmpty(server.GameLink, "--Server don't provide join link.--");
            AddField("Join This Server", joinserver, true);

            AddField("Players", $"{server.Players}/{server.MaxPlayers}", true);

            AddField("NOTE", ReplaceIfNullOrEmpty(server.Note, "-") + Environment.NewLine);

            WithFooter($"Last checked ->");
            WithCurrentTimestamp();
        }

        private string ReplaceIfNullOrEmpty(string str1, string str2)
        {
            return !string.IsNullOrEmpty(str1) ? str1 : str2;
        }
    }
}