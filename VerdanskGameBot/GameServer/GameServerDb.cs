using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;

namespace VerdanskGameBot.GameServer
{
    internal class GameServerDb : DbContext
    {
        internal DbSet<GameServerModel> GameServers { get; set; }

        public GameServerDb(DbContextOptions<GameServerDb> options) : base(options)
        {
        }

        public GameServerDb() : base()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            try
            {
                var connstr = Program.BotConfig["ConnectionString"];
                switch (Enum.Parse<DbProviders>(Program.BotConfig["DbProvider"]))
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
            }
            catch (Exception ex)
            {
                Program.Log.Fatal(ex, "Database connection invalid. Please check in config file.");
                Environment.Exit(-(int)ExitCodes.ConnStringInvalid);
                return;
            }

            base.OnConfiguring(optionsBuilder);
        }

        //protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        //{
        //    configurationBuilder
        //        .Properties<DateTimeOffset>()
        //        .HaveConversion<DateTimeToLongConverter>();

        //    base.ConfigureConventions(configurationBuilder);
        //}
    }
    class DateTimeToLongConverter : ValueConverter<DateTimeOffset, long>
    {
        public DateTimeToLongConverter()
            : base(
                toDb => toDb.ToUnixTimeSeconds(),
                fromDb => DateTimeOffset.FromUnixTimeSeconds(fromDb)
                  )
        {
        }
    }
}
