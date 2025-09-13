using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    internal static class Helper
    {
        internal static string ClockEmoji(int hour, int roundedMinutes)
            => (hour, roundedMinutes) switch
            {
                (0, 0) => "🕛",
                (0, 30) => "🕧",
                (1, 0) => "🕐",
                (1, 30) => "🕜",
                (2, 0) => "🕑",
                (2, 30) => "🕝",
                (3, 0) => "🕒",
                (3, 30) => "🕞",
                (4, 0) => "🕓",
                (4, 30) => "🕟",
                (5, 0) => "🕔",
                (5, 30) => "🕠",
                (6, 0) => "🕕",
                (6, 30) => "🕡",
                (7, 0) => "🕖",
                (7, 30) => "🕢",
                (8, 0) => "🕗",
                (8, 30) => "🕣",
                (9, 0) => "🕘",
                (9, 30) => "🕤",
                (10, 0) => "🕙",
                (10, 30) => "🕥",
                (11, 0) => "🕚",
                (11, 30) => "🕦",
                _ => "🕛"
            };
    }
}
