using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    internal class GameServerContextFactory : IDesignTimeDbContextFactory<GameBotDb>
    {
        public GameBotDb CreateDbContext(string[] args)
        {
            GameBotDb db = null;

            var config = new ConfigurationBuilder().AddCommandLine(args).Build();
            var optionsBuilder = new DbContextOptionsBuilder();

            optionsBuilder
                .LogTo((evid, lvl) => evid.Id >= RelationalEventId.MigrateUsingConnection.Id
                                    && evid.Id <= RelationalEventId.ColumnOrderIgnoredWarning.Id,
                evt => Program.Log.Trace(evt.ToString()));

            var connstr = config["ConnStr"];
            try
            {
                switch (Enum.Parse<DbTypes>(config["DbType"]))
                {
                    case DbTypes.SQLite:
                        optionsBuilder.UseSqlite(connstr);
                        break;
                    case DbTypes.MySql:
                        optionsBuilder.UseMySql(connstr, ServerVersion.AutoDetect(connstr));
                        db = new GameServerMySqlDb(optionsBuilder.Options);
                        break;
                    case DbTypes.SqlServer:
                        optionsBuilder.UseSqlServer(connstr);
                        db = new GameServerSqlServerDb(optionsBuilder.Options);
                        break;
                    case DbTypes.PostgreSql:
                        optionsBuilder.UseNpgsql(connstr);
                        db = new GameServerPostgreSqlDb(optionsBuilder.Options);
                        break;
                    default:
                        throw new ArgumentException("Database Type (--DbType) not choosen");
                }
            }
            catch (Exception e)
            {
                Program.Log.Warn(e);
            }

            return db;
        }
    }

    internal class GameServerPostgreSqlDb : GameBotDb
    {
        public GameServerPostgreSqlDb(DbContextOptions options) : base(options)
        {
        }
    }

    internal class GameServerSqlServerDb : GameBotDb
    {
        public GameServerSqlServerDb(DbContextOptions options) : base(options)
        {
        }
    }

    internal class GameServerMySqlDb : GameBotDb
    {
        public GameServerMySqlDb(DbContextOptions options) : base(options)
        {
        }
    }
}
