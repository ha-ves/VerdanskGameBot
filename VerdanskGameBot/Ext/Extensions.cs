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
    }
}
