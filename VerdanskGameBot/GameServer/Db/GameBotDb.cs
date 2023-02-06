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
using Discord;
using Discord.WebSocket;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace VerdanskGameBot.GameServer.Db
{
    public class GameServerDbReadOnly : GameBotDb
    {
        public GameServerDbReadOnly() : base ((IGuild)null)
        {
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        }

        public GameServerDbReadOnly(IGuild guild) : base(guild) { }
    }

    public class GameBotDb : DbContext
    {
        internal static SqliteConnection _sqliteconn;

        internal IGuild Guild;

        internal DbSet<GameServerModel> GameServers { get; set; }

        public GameBotDb(IGuild guild) : base() => Guild = guild;

        public GameBotDb(DbContextOptions options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
            {
                base.OnConfiguring(optionsBuilder);
                return;
            }

            var connstr = Program.BotConfig["ConnectionString"];
            var dbprovider = Enum.Parse<DbTypes>(Program.BotConfig["DbProvider"]);

            try
            {
                //optionsBuilder.UseNLog();
                optionsBuilder.EnableSensitiveDataLogging();
                optionsBuilder.LogTo((evtid, lvl) => lvl == Microsoft.Extensions.Logging.LogLevel.Information,
                    log => Program.Log.Trace($"<{log.LogLevel}> {log.EventId.Id} / {log.EventId.Name} | " + log.ToString()));

                switch (dbprovider)
                {
                    case DbTypes.SQLite:
                        optionsBuilder.UseSqlite(connstr);
                        break;
                    case DbTypes.MySql:
                        optionsBuilder.UseMySql(connstr, ServerVersion.AutoDetect(connstr));
                        break;
                    case DbTypes.SqlServer:
                        optionsBuilder.UseSqlServer(connstr);
                        break;
                    case DbTypes.PostgreSql:
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
            modelBuilder.Entity<GameServerModel>(gs =>
            {
                gs.Property(p => p.LastOnline)
                .HasDefaultValue(DateTimeOffset.MinValue);

                gs.Property(p => p.GameLink)
                    .IsRequired(false);
                gs.Property(p => p.LastModifiedBy)
                    .IsRequired(false);
                gs.Property(p => p.LastModifiedSince)
                    .IsRequired(false);
                gs.Property(p => p.LastUpdate)
                    .IsRequired(false);
                gs.Property(p => p.Note)
                    .IsRequired(false);
                gs.Property(p => p.Remarks)
                    .IsRequired(false);
                
                var guild = (Guild != null ? Guild.Id.ToString("x2") : "Default").Normalize();
                gs.ToTable("gameservers_" + guild);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
