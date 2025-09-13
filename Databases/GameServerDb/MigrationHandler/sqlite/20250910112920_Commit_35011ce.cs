using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerdanskGameBot.GameServer.Db.MigrationHandler.sqlite
{
    /// <inheritdoc />
    internal partial class Commit_35011ce(MigrationGuildHex guildHex) : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: $"gameserver_{guildHex}_history",
                columns: table => new
                {
                    HistoryId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Operation = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    ChangedBy = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ChangedAtUTCTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Remarks = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false),
                    GameType = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastOnlineUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameLink = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GameLinkRemarks = table.Column<string>(type: "TEXT", nullable: true),
                    Players = table.Column<byte>(type: "INTEGER", nullable: true),
                    MaxPlayers = table.Column<byte>(type: "INTEGER", nullable: true),
                    AddedBy = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AddedSinceUTCTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ThreadId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IP = table.Column<string>(type: "TEXT", nullable: false),
                    GamePort = table.Column<ushort>(type: "INTEGER", nullable: false),
                    LastModifiedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    LastModifiedSinceUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdateIntervalHMSTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    NextUpdateAtUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    LastUpdateUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey($"PK_gameserver_{guildHex}_history", x => x.HistoryId);
                });

            migrationBuilder.CreateTable(
                name: $"gameservers_{guildHex}",
                columns: table => new
                {
                    ServerId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameType = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    ServerName = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    IsOnline = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastOnlineUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    IsHidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameLink = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    GameLinkRemarks = table.Column<string>(type: "TEXT", nullable: true),
                    Players = table.Column<byte>(type: "INTEGER", nullable: true),
                    MaxPlayers = table.Column<byte>(type: "INTEGER", nullable: true),
                    AddedBy = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AddedSinceUTCTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    ChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    MessageId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ThreadId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IP = table.Column<string>(type: "TEXT", nullable: false),
                    GamePort = table.Column<ushort>(type: "INTEGER", nullable: false),
                    LastModifiedBy = table.Column<ulong>(type: "INTEGER", nullable: true),
                    LastModifiedSinceUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    UpdateIntervalHMSTicks = table.Column<long>(type: "INTEGER", nullable: false),
                    NextUpdateAtUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    LastUpdateUTCTicks = table.Column<long>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey($"PK_gameservers_{guildHex}", x => x.ServerId);
                });

            migrationBuilder.CreateIndex(
                name: $"IX_history_ChangedAt_{guildHex}",
                table: $"gameserver_{guildHex}_history",
                column: "ChangedAtUTCTicks");

            migrationBuilder.CreateIndex(
                name: $"IX_history_Server_Changed_{guildHex}",
                table: $"gameserver_{guildHex}_history",
                columns: new[] { "ServerId", "ChangedAtUTCTicks" });

            migrationBuilder.CreateIndex(
                name: $"IX_history_ServerId_{guildHex}",
                table: $"gameserver_{guildHex}_history",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: $"IX_ServerName_{guildHex}",
                table: $"gameservers_{guildHex}",
                column: "ServerName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: $"gameserver_{guildHex}_history");

            migrationBuilder.DropTable(
                name: $"gameservers_{guildHex}");
        }
    }
}
