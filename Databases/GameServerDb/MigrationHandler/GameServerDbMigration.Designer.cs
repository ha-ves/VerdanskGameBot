using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore.Sqlite.Migrations.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;
using Pomelo.EntityFrameworkCore.MySql.Migrations.Internal;
using System;

namespace VerdanskGameBot.GameServer.Db.MigrationHandler
{
    internal class GameServerDbDesignTimeServices : IDesignTimeServices
    {
        public void ConfigureDesignTimeServices(IServiceCollection services)
         => services.AddSingleton<ICSharpHelper, IntplStringCSharpHelper>()
                    .AddSingleton<IMigrationsCodeGenerator, PerGuildMigrationsGenerator>()
                    .AddMigrationServices();
    }

    internal class GameServerDbDesignFactory : IDesignTimeDbContextFactory<GameServerDb>
    {
        public GameServerDb CreateDbContext(string[] args)
        {
            var connstr = Environment.GetEnvironmentVariable("CONN_STRING");
            var db = Environment.GetEnvironmentVariable("DB_PROVIDER");
            var dbprovider = Enum.Parse<DbTypes>(db!, true);

            var dbconfig = new DbContextOptionsBuilder<GameServerDb>();
            var dbSvc = new ServiceCollection().AddMigrationServices();

            switch (dbprovider)
            {
                case DbTypes.SQLite:
                    dbconfig.UseSqlite(connstr);
                    dbSvc.AddEntityFrameworkSqlite();
                    break;
                case DbTypes.MySql:
                    var serverVersion = ServerVersion.AutoDetect(connstr);
                    dbconfig.UseMySql(connstr, serverVersion);
                    dbSvc.AddEntityFrameworkMySql();
                    break;
                case DbTypes.SqlServer:
                    dbconfig.UseSqlServer(connstr);
                    dbSvc.AddEntityFrameworkSqlServer();
                    break;
                case DbTypes.PostgreSql:
                    dbconfig.UseNpgsql(connstr);
                    dbSvc.AddEntityFrameworkNpgsql();
                    break;
                default:
                    throw new NotSupportedException($"Database provider {dbprovider} is not supported.");
            }

            var opts = dbconfig.UseInternalServiceProvider(dbSvc.BuildServiceProvider()).Options;

            Console.WriteLine($"Configured design-time DbContext for GameServerDb with provider {dbprovider}.");

            return new GameServerDb(opts);
        }
    }

    internal class PerGuildModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime)
            => (designTime, context.GetType(), MigrationGuildHex.GetString((GameServerDb)context));
    }

    internal static class HistoryRepositoryFactory
    {
        public static IHistoryRepository Create(IServiceProvider svc)
        {
            var dbContext = svc.GetRequiredService<ICurrentDbContext>().Context;
            var dependencies = svc.GetRequiredService<HistoryRepositoryDependencies>();

            return dbContext.Database.IsMySql() ? new PerGuildMySqlMigrationHistory(dependencies) :
                   dbContext.Database.IsSqlServer() ? new PerGuildSqlServerMigrationHistory(dependencies) :
                   dbContext.Database.IsNpgsql() ? new PerGuildPostgreSqlMigrationHistory(dependencies) :
                   dbContext.Database.IsSqlite() ? new PerGuildSQLiteMigrationHistory(dependencies) :
                   throw new NotSupportedException("Unsupported database provider for history repository.");
        }

        internal static string HistoryTableName(ICurrentDbContext db) 
            => $"__EFMigrationsHistory_{new MigrationGuildHex(db)}";
    }

    #region History tables

    internal sealed class PerGuildSqlServerMigrationHistory
        (HistoryRepositoryDependencies dependencies) : SqlServerHistoryRepository(dependencies)
    {
        protected override string TableName => HistoryRepositoryFactory
            .HistoryTableName(Dependencies.CurrentContext);
    }

    internal sealed class PerGuildPostgreSqlMigrationHistory
        (HistoryRepositoryDependencies dependencies) : NpgsqlHistoryRepository(dependencies)
    {
        protected override string TableName => HistoryRepositoryFactory
            .HistoryTableName(Dependencies.CurrentContext);
    }

    internal sealed class PerGuildMySqlMigrationHistory
        (HistoryRepositoryDependencies dependencies) : MySqlHistoryRepository(dependencies)
    {
        protected override string TableName => HistoryRepositoryFactory
            .HistoryTableName(Dependencies.CurrentContext);
    }

    internal sealed class PerGuildSQLiteMigrationHistory
        (HistoryRepositoryDependencies dependencies) : SqliteHistoryRepository(dependencies)
    {
        protected override string TableName => HistoryRepositoryFactory
            .HistoryTableName(Dependencies.CurrentContext);
    }

    #endregion
}
