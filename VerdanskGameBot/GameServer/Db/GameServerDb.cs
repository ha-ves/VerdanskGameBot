using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using NLog.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog;
using NLog.Fluent;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VerdanskGameBot.Ext;

namespace VerdanskGameBot.GameServer.Db
{
    public class GameServerDb : DbContext
    {
        internal DbSet<GameServerModel> GameServers { get; set; }

        public GameServerDb(DbContextOptions options) : base(options)
        {
        }

        public GameServerDb() : base()
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            var connstr = Program.BotConfig["ConnectionString"];
            var dbprovider = Enum.Parse<DbProviders>(Program.BotConfig["DbProvider"]);

            try
            {
                optionsBuilder.UseNLog();

            switch (dbprovider)
            {
                case DbProviders.SQLite:
                    optionsBuilder.UseSqlite(connstr);
                    break;
                case DbProviders.MySql:
                    optionsBuilder.UseMySql(connstr, ServerVersion.AutoDetect(connstr));
                    break;
                case DbProviders.SqlServer:
                    optionsBuilder.UseSqlServer(connstr);
                    break;
                case DbProviders.PostgreSql:
                    optionsBuilder.UseNpgsql(connstr);
                    break;
                default:
                    break;
            }
            }
            catch (Exception e)
            {
                Program.Log.Warn(e);
            }

            base.OnConfiguring(optionsBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configBuilder)
        {
            configBuilder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToDateTimeConverter>();

            configBuilder.Properties<ulong>()
                .HaveConversion<string>();

            base.ConfigureConventions(configBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var gs = modelBuilder.Entity<GameServerModel>();

            gs.Property(x => x.LastOnline)
                .HasDefaultValue(DateTimeOffset.MinValue);

            gs.Property(x => x.GameLink)
                .IsRequired(false);
            gs.Property(x => x.LastModifiedBy)
                .IsRequired(false);
            gs.Property(x => x.LastModifiedSince)
                .IsRequired(false);
            gs.Property(x => x.LastUpdate)
                .IsRequired(false);
            gs.Property(x => x.Note)
                .IsRequired(false);
            gs.Property(x => x.Remarks)
                .IsRequired(false);

            base.OnModelCreating(modelBuilder);
        }
    }
}
