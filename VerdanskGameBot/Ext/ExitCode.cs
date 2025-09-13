using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    public enum ExitCode
    {
        OK = 0,

        #region Internal mechanics errors

        NodeJSNotAvail = 501,
        GamedigMissing,
        BotLoginFailed,
        BotNoGuild,
        DiscordDisconnect,

        #endregion

        #region Configuration Errors

        CmdArgsInvalid = 551,
        NLogConfigInvalid,
        GamedigConfigInvalid,
        BotConfigInvalid,
        BotTokenInvalid,
        ConnStringInvalid,
        DbConfigInvalid,

        #endregion
    }
}
