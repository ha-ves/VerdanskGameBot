using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;

namespace VerdanskGameBot.GameServer
{
    internal class GameServerContextFactory : IDesignTimeDbContextFactory<GameServerDb>
    {
        public GameServerDb CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder().AddCommandLine(args).Build();
            var optionsBuilder = new DbContextOptionsBuilder<GameServerDb>();

            var connstr = config["ConnStr"];
            switch (Enum.Parse<DbProviders>(config["DbType"]))
            {
                case DbProviders.SQLite:
                    optionsBuilder.UseSqlite(connstr);
                    break;
                case DbProviders.MySql:
                    optionsBuilder.UseMySql(connstr, ServerVersion.AutoDetect(connstr));
                    break;
                case DbProviders.PostgreSql:
                    optionsBuilder.UseNpgsql(connstr);
                    break;
                // Not used yet, should be used when watching hundreds of gameservers
                //case DbProviders.InMemory:
                //    optionsBuilder.UseInMemoryDatabase("GameServers");
                //    break;
                default:
                    break;
            }

            return new GameServerDb(optionsBuilder.Options);
        }
    }
}
