using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace VerdanskGameBot.GameServer.Db.MigrationHandler
{
    public static partial class MigrationExtensions
    {
        public static IServiceCollection AddMigrationServices(this IServiceCollection services)
         => services.AddScoped<IMigrationsAssembly, GameServerDbMigration>()
                    .AddSingleton<IModelCacheKeyFactory, PerGuildModelCacheKeyFactory>()
                    .AddScoped(HistoryRepositoryFactory.Create)
                    .AddScoped<MigrationGuildHex>();
    }
}
