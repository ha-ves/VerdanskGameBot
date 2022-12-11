using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    public enum ExitCodes
    {
        OK = 0,
        ErrCode = 500,
        
        #region Internal mechanics errors

        NodeJSNotAvail,
        GamedigMissing,
        BotLoginFailed,
        BotNoGuild,
        DiscordDisconnect,

        #endregion

        #region Configuration Errors

        BotConfigInvalid,
        BotTokenInvalid,
        ConnStringInvalid,
        DbConfigInvalid,

        #endregion
    }
}
