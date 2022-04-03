﻿using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;
using System.Security;

namespace VerdanskGameBot
{
    public class GameServerModel
    {
        /// <summary>
        /// Primary Key
        /// </summary>
        [Key, Column("id", Order = 0)]
        public ulong Id { get; set; }

        /// <summary>
        /// Private server name (by Admin)
        /// </summary>
        [Required, Column("name"), MaxLength(22)]
        public string ServerName { get; set; }
        /// <summary>
        /// Display Name to show on watch list
        /// </summary>
        [Column("display_name"), MaxLength(100)]
        public string DisplayName { get; set; }

        /// <summary>
        /// Description of game server
        /// </summary>
        [Column("desc"), MaxLength(200)]
        public string Description { get; set; }

        /// <summary>
        /// Image URL to show on watch list
        /// </summary>
        [Column("img_url"), MaxLength(200)]
        public string ImageUrl { get; set; }

        /// <summary>
        /// Is the server online?
        /// </summary>
        [NotMapped]
        public bool IsOnline { get; set; }
        /// <summary>
        /// Last time the server is online
        /// </summary>
        [Required, Column("last_online_time", TypeName = "INTEGER")]
        public DateTimeOffset LastOnline { get; set; }

        /// <summary>
        /// Link to game
        /// </summary>
        [Column("game_link"), MaxLength(100)]
        public string GameLink { get; set; }

        /// <summary>
        /// Round-trip-time
        /// </summary>
        [NotMapped]
        public ushort RTT { get; set; }

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
        [Required, Column("added_by")]
        public ulong AddedBy { get; set; }
        /// <summary>
        /// Channel where to show the watch list
        /// </summary>
        [Required, Column("chan_id")]
        public ulong ChannelId { get; set; }
        /// <summary>
        /// Message to show the watch list
        /// </summary>
        [Required, Column("msg_id")]
        public ulong MessageId { get; set; }

        /// <summary>
        /// IP Address of the game server
        /// </summary>
        [Required, Column("game_ip")]
        public IPAddress IP { get; set; }

        /// <summary>
        /// Port to join the game server
        /// </summary>
        [Required, Column("game_port")]
        public ushort GamePort { get; set; }

        /// <summary>
        /// RCON IP
        /// </summary>
        [Required, Column("rcon_ip")]
        public IPAddress RconIP { get; set; }

        /// <summary>
        /// Port to administer the server (RCON)
        /// </summary>
        [Required, Column("rcon_port")]
        public ushort RconPort { get; set; }

        /// <summary>
        /// Plain-text password for RCON
        /// </summary>
        [Required, Column("rcon_pass")]
        public string RconPass { get; set; }

        /// <summary>
        /// Date the game server added
        /// </summary>
        [Required, Column("added_since", TypeName = "INTEGER")]
        public DateTimeOffset AddedSince { get; set; }
        /// <summary>
        /// Last time the game server checked
        /// </summary>
        [Required, Column("last_update", TypeName = "INTEGER")]
        public DateTimeOffset LastUpdate { get; set; }
        /// <summary>
        /// How often to check the game server
        /// </summary>
        [Required, Column("update_interval", TypeName = "INTEGER")]
        public TimeSpan UpdateInterval { get; set; }

        /// <summary>
        /// Extended configuration needs
        /// </summary>
        [NotMapped]
        public string ErrMsg { get; set; }

        /// <summary>
        /// Note about the game server
        /// </summary>
        [Column("note")]
        public string Note { get; set; }
    }
}
