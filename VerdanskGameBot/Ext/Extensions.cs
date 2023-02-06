using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VerdanskGameBot.GameServer;

namespace VerdanskGameBot.Ext
{
    static partial class Extensions
    {
        /// <summary>
        /// Use NLog logger for EF Core logging
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static DbContextOptionsBuilder UseNLog(this DbContextOptionsBuilder builder)
        {
            builder
                .LogTo((eventid, lvl) => true,
                evt =>
                {
                    switch (evt.LogLevel)
                    {
                        case Microsoft.Extensions.Logging.LogLevel.Trace:
                            Program.Log.Trace($"{evt.EventId.Id} / {evt.EventId.Name} | " + evt.ToString());
                            break;
                        case Microsoft.Extensions.Logging.LogLevel.Debug:
                            Program.Log.Debug($"{evt.EventId.Id} / {evt.EventId.Name} | " + evt.ToString());
                            break;
                        case Microsoft.Extensions.Logging.LogLevel.Information:
                            Program.Log.Info($"{evt.EventId.Id} / {evt.EventId.Name} | " + evt.ToString());
                            break;
                        case Microsoft.Extensions.Logging.LogLevel.Warning:
                            Program.Log.Warn($"{evt.EventId.Id} / {evt.EventId.Name} | " + evt.ToString());
                            break;
                        case Microsoft.Extensions.Logging.LogLevel.Error:
                            Program.Log.Error($"{evt.EventId.Id} / {evt.EventId.Name} | " + evt.ToString());
                            break;
                        case Microsoft.Extensions.Logging.LogLevel.Critical:
                            Program.Log.Fatal($"{evt.EventId.Id} / {evt.EventId.Name} | " + evt.ToString());
                            break;
                        default:
                            break;
                    }
                });

            return builder;
        }

        /// <summary>
        /// Replace the properties value of this <see cref="GameServerModel"/> with a new updated one.
        /// </summary>
        /// <param name="srcModel">The old <see cref="GameServerModel"/> to replace</param>
        /// <param name="dstModel">The new <see cref="GameServerModel"/> to replace with</param>
        /// <returns></returns>
        public static GameServerModel ReplaceQueried(this GameServerModel srcModel, GameServerModel dstModel)
        {
            srcModel.GameType = dstModel.GameType;
            srcModel.DisplayName = dstModel.DisplayName;
            srcModel.Description = dstModel.Description;
            srcModel.ImageUrl = dstModel.ImageUrl;
            srcModel.IsOnline = dstModel.IsOnline;
            srcModel.LastOnline = dstModel.LastOnline;
            srcModel.GameLink = dstModel.GameLink;
            srcModel.Players = dstModel.Players;
            srcModel.MaxPlayers = dstModel.MaxPlayers;
            srcModel.ChannelId = dstModel.ChannelId;
            srcModel.MessageId = dstModel.MessageId;
            srcModel.ThreadId = dstModel.ThreadId;
            srcModel.IP = dstModel.IP;
            srcModel.GamePort = dstModel.GamePort;
            srcModel.LastModifiedBy = dstModel.LastModifiedBy;
            srcModel.LastModifiedSince = dstModel.LastModifiedSince;
            srcModel.UpdateInterval = dstModel.UpdateInterval;
            srcModel.LastUpdate = dstModel.LastUpdate;
            srcModel.Note = dstModel.Note;
            srcModel.Remarks = dstModel.Remarks;

            return srcModel;
        }
    }
}
