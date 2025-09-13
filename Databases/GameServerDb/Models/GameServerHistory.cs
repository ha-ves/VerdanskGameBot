using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VerdanskGameBot.GameServer.Db.Models
{
    /// <summary>
    /// Historical snapshots of GameServerModel for tracking changes over time
    /// </summary>s
    public class GameServerHistory : GameServerBase
    {
        public long HistoryId { get; set; }

        /// <summary>
        /// Database operation (Insert, Update, Delete)
        /// </summary>
        [Required, MaxLength(10)]
        public string? Operation { get; set; }
        
        /// <summary>
        /// Who made this change
        /// </summary>
        [Required]
        public ulong? ChangedBy { get; set; }

        /// <summary>
        /// When this change occurred
        /// </summary>
        [Required, Column("ChangedAtUTCTicks")]
        public DateTimeOffset ChangedAt { get; set; }

        /// <summary>
        /// Action performed. (200 characters max)
        /// </summary>
        [Required, MaxLength(200)]
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// The remarks or notes associated with the history of the gameserver. 
        /// (4000 characters max)
        /// </summary>
        [MaxLength(4000)]
        public string? Remarks { get; set; }

        internal static ModelConfigurationBuilder AddConventions
            (ModelConfigurationBuilder builder)
        {
            builder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
            return builder;
        }
    }

    public static class GameServerHistoryExtensions
    {
        public static H Configure<H>(this H gs)
            where H : EntityTypeBuilder<GameServerHistory>
        {
            gs.HasKey(p => p.HistoryId);
            return gs;
        }
    }
}