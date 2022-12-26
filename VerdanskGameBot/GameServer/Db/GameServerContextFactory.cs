using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;

namespace VerdanskGameBot.GameServer.Db
{
    internal class GameServerContextFactory : IDesignTimeDbContextFactory<GameServerDb>
    {
        public GameServerDb CreateDbContext(string[] args)
        {
            GameServerDb db = null;

            var config = new ConfigurationBuilder().AddCommandLine(args).Build();
            var optionsBuilder = new DbContextOptionsBuilder();

            var connstr = config["ConnStr"];
            switch (Enum.Parse<DbProviders>(config["DbType"]))
            {
                case DbProviders.SQLite:
                    optionsBuilder.UseSqlite(connstr);
                    break;
                case DbProviders.MySql:
                    optionsBuilder.UseMySql(connstr, ServerVersion.AutoDetect(connstr));
                    db = new GameServerMySqlDb(optionsBuilder.Options);
                    break;
                case DbProviders.SqlServer:
                    optionsBuilder.UseSqlServer(connstr);
                    db = new GameServerSqlServerDb(optionsBuilder.Options);
                    break;
                case DbProviders.PostgreSql:
                    optionsBuilder.UseNpgsql(connstr);
                    break;
                // Not used yet, should be used when watching hundreds of gameservers
                //case DbProviders.InMemory:
                //    optionsBuilder.UseInMemoryDatabase("GameServers");
                //    break;
                default:
                    throw new ArgumentException("Database Type (--DbType) not choosen");
            }

            return db;
        }
    }
}
