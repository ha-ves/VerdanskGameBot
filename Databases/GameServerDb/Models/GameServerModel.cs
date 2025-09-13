using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace VerdanskGameBot.GameServer.Db.Models
{
    public abstract class GameServerBase
    {
        /// <summary>
        /// Primary Key
        /// </summary>
        public long ServerId { get; set; }

        /// <summary>
        /// Game Type
        /// </summary>
        [Required, MaxLength(22)]
        public string? GameType { get; set; }

        /// <summary>
        /// Private server name (by Admin)
        /// </summary>
        [Required, MaxLength(22)]
        public string? ServerName { get; set; }

        /// <summary>
        /// Display Name to show on watch list
        /// </summary>
        [MaxLength(100)]
        public string? DisplayName { get; set; }

        /// <summary>
        /// Description of game server
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Image URL to show on watch list
        /// </summary>
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Is the server online?
        /// </summary>
        public bool IsOnline { get; set; } = false;

        /// <summary>
        /// Last time the server is online
        /// </summary>
        [Column("LastOnlineUTCTicks")]
        public DateTimeOffset? LastOnline { get; set; }

        /// <summary>
        /// Force the server to be offline (for maintenance)
        /// </summary>
        public bool IsHidden { get; set; } = false;

        /// <summary>
        /// Link to game <br/>
        /// Is using format [Text to show](steam://IP.addr.ess:port)
        /// </summary>
        [MaxLength(120)]
        public string? GameLink { get; set; }

        /// <summary>
        /// Additional remarks or notes about the game link.
        /// </summary>
        public string? GameLinkRemarks { get; set; }

        /// <summary>
        /// Current number of players in game server
        /// </summary>
        public byte? Players { get; set; }

        /// <summary>
        /// Max players
        /// </summary>
        public byte? MaxPlayers { get; set; }

        /// <summary>
        /// Who added the server
        /// </summary>
        [Required]
        public ulong AddedBy { get; set; }

        /// <summary>
        /// Date the game server added
        /// </summary>
        [Required, Column("AddedSinceUTCTicks")]
        public DateTimeOffset AddedSince { get; set; }

        /// <summary>
        /// Channel where to show the watch list
        /// </summary>
        [Required]
        public ulong ChannelId { get; set; }

        /// <summary>
        /// Message to show the watch list
        /// </summary>
        [Required]
        public ulong MessageId { get; set; }

        /// <summary>
        /// Discussion thread for the game server watch list
        /// </summary>
        [Required]
        public ulong ThreadId { get; set; }

        /// <summary>
        /// IP Address of the game server
        /// </summary>
        [Required]
        public IPAddress? IP { get; set; }

        /// <summary>
        /// Port to join the game server
        /// </summary>
        [Required]
        public ushort GamePort { get; set; }

        /// <summary>
        /// Last user to edit the game server entry
        /// </summary>
        public ulong? LastModifiedBy { get; set; }

        /// <summary>
        /// Last time someone edited the game server entry
        /// </summary>
        [Column("LastModifiedSinceUTCTicks")]
        public DateTimeOffset? LastModifiedSince { get; set; }

        /// <summary>
        /// How often to check the game server
        /// </summary>
        [Required, Column("UpdateIntervalHMSTicks")]
        public TimeSpan UpdateInterval { get; set; }

        /// <summary>
        /// Next scheduled time to force update the game server status
        /// </summary>
        [Column("NextUpdateAtUTCTicks")]
        public DateTimeOffset? NextUpdateAt { get; set; }

        /// <summary>
        /// Last time the game server checked
        /// </summary>
        [Column("LastUpdateUTCTicks")]
        public DateTimeOffset? LastUpdate { get; set; }

        /// <summary>
        /// Note about the game server
        /// </summary>
        public string? Note { get; set; }
    }

    // because ef core doesnt like when deriving haskey in entity building ...
    public class GameServerModel : GameServerBase
    {
        public static ModelConfigurationBuilder AddConventions
            (ModelConfigurationBuilder builder)
        {
            builder.Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
            builder.Properties<TimeSpan>()
                .HaveConversion<TimeSpanToTicksConverter>();
            return builder;
        }
    }

    public static class GameServerModelExtensions
    {
        public static M Configure<M>(this M gs)
            where M : EntityTypeBuilder<GameServerModel>
        {
            gs.HasKey(p => p.ServerId);
            return gs;
        }
    }
}