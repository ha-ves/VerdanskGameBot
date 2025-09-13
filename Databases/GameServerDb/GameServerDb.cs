using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VerdanskGameBot.GameServer.Db.MigrationHandler;
using VerdanskGameBot.GameServer.Db.Models;

namespace VerdanskGameBot.GameServer.Db
{
    public class GameServerReadOnlyDb
        (ulong guild, DbContextOptions? options = null) 
        : GameServerDb(guild, options ?? new DbContextOptionsBuilder().Options)
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);

            base.OnConfiguring(optionsBuilder);
        }
    }

    public class GameServerDb : DbContext
    {
        protected readonly ulong? _guildId;
        private readonly ILogger<GameServerDb>? _logger;

        // User context for tracking who made changes
        private ulong? _userId;
        private string? _userContext;

        public GameServerDb(ulong guild, DbContextOptions? options = null) 
            : base(options ?? new DbContextOptionsBuilder().Options)
        {
            _guildId = guild;
            GuildHex = Convert.ToBase64String(BitConverter.GetBytes(guild))
                .Replace('+', '_')
                .Replace('/', '_')
                .TrimEnd('='); ;
        }

        // default, self, testing, etc.
        internal GameServerDb(DbContextOptions? options = null)
            : base(options ?? new DbContextOptionsBuilder().Options) { }

        public ulong? GuildId => _guildId;
        public string? GuildHex { get; init; }

        public DbSet<GameServerModel> GameServers => Set<GameServerModel>();
        private DbSet<GameServerHistory> GameServerHistories => Set<GameServerHistory>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configBuilder)
        {
            GameServerModel.AddConventions(configBuilder);
            GameServerHistory.AddConventions(configBuilder);

            base.ConfigureConventions(configBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var guild = (GuildHex ?? "Default").Normalize();
#if DEBUG
            if (EF.IsDesignTime)
            {
                guild = $"{{{MigrationGuildHex.VarName}}}";
            }
#endif
            modelBuilder.Entity<GameServerModel>(gs =>
            {
                gs.Configure();
                gs.ToTable("gameservers_" + guild);
                gs.HasIndex(p => p.ServerName, "IX_ServerName_" + guild)
                    .IsUnique(true);
            });
            modelBuilder.Entity<GameServerHistory>(gs =>
            {
                gs.Configure();
                gs.ToTable("gameserver_" + guild + "_history");
                gs.HasIndex(p => p.ServerId, "IX_history_ServerId_" + guild);
                gs.HasIndex(p => p.ChangedAt, "IX_history_ChangedAt_" + guild);
                gs.HasIndex(p => new { p.ServerId, p.ChangedAt }, "IX_history_Server_Changed_" + guild);
            });

            base.OnModelCreating(modelBuilder);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await ProcessChangesAsync(cancellationToken);
            return await base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            return SaveChangesAsync().GetAwaiter().GetResult();
        }

        private async Task ProcessChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await GameServerHistories!.AddRangeAsync(ChangeTracker.Entries<GameServerModel>()
                    .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                    .Select(e =>
                    {
                        var history = new GameServerHistory
                        {
                            Operation = e.State switch
                            {
                                EntityState.Deleted => "DELETE",
                                EntityState.Modified => "UPDATE",
                                EntityState.Added => "INSERT",
                                _ => "UNKNOWN"
                            },
                            ChangedBy = _userId,
                            ChangedAt = DateTimeOffset.UtcNow,
                            Action = _userContext!
                        };
                        Entry(history).CurrentValues.SetValues(e.Entity);

                        return history;
                    }),
                    cancellationToken
                );
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to process changes");
            }
        }

        /// <summary>
        /// Set the user context for auditing purposes
        /// </summary>
        public void SetUserContext(ulong userId, string userContext)
        {
            _userId = userId;
            _userContext = userContext;
        }

        /// <summary>
        /// Clear the user context
        /// </summary>
        public void ClearUserContext()
        {
            _userId = null;
            _userContext = null;
        }

        /// <summary>
        /// Get audit trail for a specific game server
        /// </summary>
        public IQueryable<GameServerHistory> GetHistory(int serverId)
            => GameServerHistories!
                .Where(h => h.ServerId == serverId)
                .OrderByDescending(h => h.ChangedAt);

        public static async Task CanConnectAsync
            (DbContextOptions<GameServerDb> gsDbOpts)
        {
            using var db = new GameServerDb(gsDbOpts);
            if (!await db.Database.CanConnectAsync())
                throw new Exception("Cannot connect to the database.");
        }
    }
}
