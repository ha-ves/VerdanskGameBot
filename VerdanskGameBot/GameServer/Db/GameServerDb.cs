using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
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
    public abstract class GameServerDb : DbContext
    {
        internal DbSet<GameServerModel> GameServers { get; set; }

        public GameServerDb(DbContextOptions options) : base(options)
        {
        }

        public GameServerDb() : base()
        {
        }

        public static GameServerDb GetContext()
        {
            switch (Enum.Parse<DbProviders>(Program.BotConfig["DbProvider"]))
            {
                case DbProviders.SQLite:
                    return null;
                case DbProviders.MySql:
                    return new GameServerMySqlDb();
                case DbProviders.SqlServer:
                    return new GameServerSqlServerDb();
                case DbProviders.PostgreSql:
                    return null;
                // Not used yet, should be used when watching hundreds of gameservers
                //case DbProviders.InMemory:
                //    optionsBuilder.UseInMemoryDatabase("GameServers");
                //    break;
                default:
                    return null;
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (optionsBuilder.IsConfigured)
                return;

            //try
            //{
                var connstr = Program.BotConfig["ConnectionString"];
                var dbprovider = Enum.Parse<DbProviders>(Program.BotConfig["DbProvider"]);
                //var dbasm = Assembly.LoadFrom(Enum.GetName(dbprovider) + "Db.dll").FullName;
                switch (dbprovider)
                {
                    case DbProviders.SQLite:
                        optionsBuilder.UseSqlite(connstr);
                        break;
                    case DbProviders.MySql:
                        optionsBuilder.UseMySql(connstr, ServerVersion.AutoDetect(connstr)/*, oa => oa.MigrationsAssembly(dbasm)*/);
                        break;
                    case DbProviders.SqlServer:
                        optionsBuilder.UseSqlServer(connstr/*, oa => oa.MigrationsAssembly(dbasm)*/);
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
            //}
            //catch (Exception ex)
            //{
            //    Program.Log.Fatal(ex, "Database connection invalid. Please check in config file.");
            //    Environment.Exit(-(int)ExitCodes.ConnStringInvalid);
            //    return;
            //}

            base.OnConfiguring(optionsBuilder);
        }

        //protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        //{
        //    configurationBuilder
        //        .Properties<DateTimeOffset>()
        //        .HaveConversion<DateTimeOffsetToDateTimeConverter>();

        //    base.ConfigureConventions(configurationBuilder);
        //}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GameServerModel>()
                .HasKey(gs => new { gs.Id, gs.ServerName })
                .IsClustered();

            base.OnModelCreating(modelBuilder);
        }
    }

    internal class GameServerSqlServerDb : GameServerDb
    {
        public GameServerSqlServerDb(DbContextOptions options) : base(options)
        {
        }

        public GameServerSqlServerDb() : base()
        {
        }
    }

    internal class GameServerMySqlDb : GameServerDb
    {
        public GameServerMySqlDb(DbContextOptions options) : base(options)
        {
        }

        public GameServerMySqlDb() : base()
        {
        }
    }
}
