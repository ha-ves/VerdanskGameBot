using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.GameServer.Db;

namespace VerdanskGameBot.GameServer
{
    internal static class GameServerWatcher
    {
        public static async Task<GameServerModel> QueryGameServerAsync(GameServerModel gameServer)
        {
            using(var db = new GameServerDb())
            {
                var indb = db.GameServers.First();
                var gsstate = db.Entry(gameServer).State;
                Debugger.Break();
            }

            return gameServer;
        }
    }
}
