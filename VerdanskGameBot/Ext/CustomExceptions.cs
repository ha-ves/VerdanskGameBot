using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.Ext
{
    internal class AppExitException : Exception { }

    /// <summary>
    /// Thrown when Object already exist
    /// </summary>
    class AlreadyExistException : ApplicationException { }

    /// <summary>
    /// Thrown when gameserver port is not in valid format
    /// </summary>
    class GamePortFormatException : ApplicationException { }

    /// <summary>
    /// Thrown when gameserver watcher update interval time is not in valid format
    /// </summary>
    class UpdateIntervalFormatException : ApplicationException { }

}
