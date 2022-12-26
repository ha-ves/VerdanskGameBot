using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Converters;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;

namespace VerdanskGameBot.GameServer
{
    [Table("GameServers")]
    public class GameServerModel
    {
        /// <summary>
        /// Primary Keyw3
        /// </summary>
        public ulong Id { get; set; }

        /// <summary>
        /// Game Type
        /// </summary>
        [Required, MaxLength(22)]
        public string GameType { get; set; }

        /// <summary>
        /// Private server name (by Admin)
        /// </summary>
        [Required, MaxLength(22)]
        public string ServerName { get; set; }
        /// <summary>
        /// Display Name to show on watch list
        /// </summary>
        [NotMapped]
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of game server
        /// </summary>
        [NotMapped]
        public string Description { get; set; }

        /// <summary>
        /// Image URL to show on watch list
        /// </summary>
        [NotMapped]
        public string ImageUrl { get; set; }

        /// <summary>
        /// Is the server online?
        /// </summary>
        [NotMapped]
        public bool IsOnline { get; set; } = false;
        /// <summary>
        /// Last time the server is online
        /// </summary>
        [Column("LastOnlineUTC")]
        public DateTimeOffset LastOnline { get; set; }

        /// <summary>
        /// Link to game
        /// </summary>
        [MaxLength(100)]
        public string GameLink { get; set; } = "";

        /// <summary>
        /// Current number of players in game server
        /// </summary>
        [NotMapped]
        public byte Players { get; set; }

        /// <summary>
        /// Max players
        /// </summary>
        [NotMapped]
        public byte MaxPlayers { get; set; }

        /// <summary>
        /// Who added the server
        /// </summary>
        [Required]
        public ulong AddedBy { get; set; }
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
        /// IP Address of the game server
        /// </summary>
        [Required]
        public IPAddress IP { get; set; }
        /// <summary>
        /// Port to join the game server
        /// </summary>
        [Required]
        public ushort GamePort { get; set; }

        /// <summary>
        /// Date the game server added
        /// </summary>
        [Required, Column("AddedSinceUTC")]
        public DateTimeOffset AddedSince { get; set; }
        /// <summary>
        /// Last time the game server checked
        /// </summary>
        [Required, Column("LastUpdateUTC")]
        public DateTimeOffset LastUpdate { get; set; }
        /// <summary>
        /// How often to check the game server
        /// </summary>
        [Required]
        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Extended configuration needs
        /// </summary>
        [NotMapped]
        public string ErrMsg { get; set; }

        /// <summary>
        /// Note about the game server
        /// </summary>
        [Column("note")]
        public string Note { get; set; } = "";
    }
}
