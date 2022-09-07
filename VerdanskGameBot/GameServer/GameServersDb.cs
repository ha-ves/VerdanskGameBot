using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VerdanskGameBot.GameServer
{
    internal class GameServersDb : DbContext
    {
        internal DbSet<GameServerModel> GameServers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=gameservers.db;");

            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GameServerModel>()
                .ToTable("GameServers");

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var prop in entity.GetProperties())
                {
                    if (prop.ClrType == typeof(DateTimeOffset))
                        prop.SetValueConverter(new ValueConverter<DateTimeOffset, long>(
                            toDb => toDb.ToUnixTimeSeconds(),
                            fromDb => DateTimeOffset.FromUnixTimeSeconds(fromDb)));
                    else if (prop.ClrType == typeof(TimeSpan))
                        prop.SetValueConverter(new ValueConverter<TimeSpan, ulong>(
                            toDb => (ulong)toDb.TotalMilliseconds,
                            fromDb => TimeSpan.FromMilliseconds(fromDb)));
                }
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}
