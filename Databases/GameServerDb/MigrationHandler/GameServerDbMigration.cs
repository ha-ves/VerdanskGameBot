using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace VerdanskGameBot.GameServer.Db.MigrationHandler
{
    public sealed class GameServerDbMigration : IMigrationsAssembly
    {
        private readonly IServiceProvider _svp;
        private readonly IMigrationsIdGenerator _idGenerator;
        private static ILogger<GameServerDbMigration>? _logger;

        public required IReadOnlyDictionary<string, TypeInfo> Migrations { get; init; }
        public ModelSnapshot? ModelSnapshot { get; init; }
        public Assembly Assembly { get; init; }

        public GameServerDbMigration(
            IServiceProvider svp,
            ICurrentDbContext ctx,
            IMigrationsIdGenerator idGen,
            ILogger<GameServerDbMigration>? logger = null)
        {
            _logger = logger;

            _svp = svp;
            _idGenerator = idGen;
            var dbCtx = ctx.Context;
            var ctxType = dbCtx.GetType();

            if (dbCtx is GameServerReadOnlyDb)
            {
                throw new NotSupportedException("Migrations are disabled for read-only context.");
            }

            Assembly = typeof(GameServerDbMigration).Assembly;

            var migs = Assembly.DefinedTypes.Where(t => t.IsAssignableTo(typeof(Migration)))
                .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType == ctxType)
                .Where(t => t.Namespace!.EndsWith(dbCtx.Database.IsMySql() ? ".mysql" :
                                      dbCtx.Database.IsSqlServer() ? ".sqlserver" :
                                      dbCtx.Database.IsNpgsql() ? ".postgresql" :
                                      dbCtx.Database.IsSqlite() ? ".sqlite" :
                                      throw new NotSupportedException("Unsupported database provider for migrations.")))
                .Select(t => new { Type = t.GetTypeInfo(), MigrationAttr = t.GetCustomAttribute<MigrationAttribute>() })
                .Where(x =>
                {
                    if (x.MigrationAttr?.Id is null)
                    {
                        _logger?.LogTrace("Migration type [{name}] missing MigrationAttribute for Id.", x.Type.Name);
                        return false;
                    }
                    return true;
                })
                .OrderBy(x => x.MigrationAttr!.Id);

            Migrations = migs.ToDictionary(t => t.MigrationAttr!.Id, t => t.Type);

            _logger?.LogTrace("Discovered migrations: [{keys}]", string.Join(", ", Migrations.Keys));

            var snapshots = Assembly.DefinedTypes.Where(t => t.IsAssignableTo(typeof(ModelSnapshot)))
                .Where(t => t.Namespace!.EndsWith(dbCtx.Database.IsMySql() ? ".mysql" :
                                      dbCtx.Database.IsSqlServer() ? ".sqlserver" :
                                      dbCtx.Database.IsNpgsql() ? ".postgresql" :
                                      dbCtx.Database.IsSqlite() ? ".sqlite" :
                                      throw new NotSupportedException("Unsupported database provider for migrations.")))
                .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType == ctxType);

            _logger?.LogTrace("Discovered model snapshots: [{names}]", string.Join(", ", snapshots.Select(s => s.Name)));

            var snapshot = snapshots.FirstOrDefault();
            if (snapshot is null)
            {
                _logger?.LogTrace("No ModelSnapshot class with DbContextAttribute of [{name}] found in the assembly.",
                    ctxType.Name);
            }
            else
            {
                if (snapshots.Count() > 1) _logger?.LogWarning("Multiple ModelSnapshot classes found for DbContext [{ctxname}]. " +
                    "Using the first one found: [{snapname}]", ctxType.Name, snapshot!.GetType().Name);

                ModelSnapshot = (ModelSnapshot?)ActivatorUtilities.CreateInstance(_svp, snapshot.AsType());
            }
        }

        public Migration CreateMigration(TypeInfo migrationClass, string activeProvider)
        {
            var mig = (Migration?)ActivatorUtilities.CreateInstance(_svp, migrationClass.AsType());

            if (mig is null)
            {
                var ex = new InvalidOperationException($"Failed to create instance of Migration [{migrationClass.Name}]");
                _logger?.LogError(ex, "Failed to create Migration.");
                throw ex;
            }

            _logger?.LogDebug("Migrating '{prvd}' database to [{name}]", 
                activeProvider.Split('.', StringSplitOptions.RemoveEmptyEntries).Last(), migrationClass.Name);

            return mig;
        }

        public string? FindMigrationId(string nameOrId)
            => Migrations.Keys
                .Where(
                    _idGenerator.IsValidId(nameOrId)
                        // ReSharper disable once ImplicitlyCapturedClosure
                        ? id => string.Equals(id, nameOrId, StringComparison.OrdinalIgnoreCase)
                        : id => string.Equals(_idGenerator.GetName(id), nameOrId, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
    }
}
