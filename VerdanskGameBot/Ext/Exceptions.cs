using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    /// <summary>
    /// Thrown when hostname/ip address/game port is invalid
    /// </summary>
    internal class GameServerHostnameIPPortInvalidException : ApplicationException
    {
        public GameServerHostnameIPPortInvalidException(object message) : base($"Invalid format for entry '{message}'.") => Object = message;
        public object Object { get; private set; }
    }

    /// <summary>
    /// Thrown when update interval value is invalid
    /// </summary>
    internal class GameServerUpdateIntervalInvalidException : ApplicationException
    {
        public GameServerUpdateIntervalInvalidException(object message) : base($"Invalid format for entry '{message}'.") => Object = message;
        public object Object { get; private set; }
    }

    /// <summary>
    /// Thrown when the game type is invalid
    /// </summary>
    internal class GameServerGameTypeInvalidException : ApplicationException
    {
        public GameServerGameTypeInvalidException(object message) : base($"Invalid format for entry '{message}'.") => Object = message;
        public object Object { get; private set; }
    }

    internal class GameServerDbAddException : ApplicationException
    { public GameServerDbAddException() { } }
}
