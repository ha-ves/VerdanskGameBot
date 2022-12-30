using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerdanskGameBot.Migrations.SqlServer
{
    public partial class Test : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    ServerId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    GameType = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: false),
                    ServerName = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: false),
                    LastOnlineUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GameLink = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddedBy = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    AddedSinceUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChannelId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    IP = table.Column<string>(type: "nvarchar(45)", nullable: false),
                    GamePort = table.Column<int>(type: "int", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(64)", nullable: false),
                    LastModifiedSinceUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdateInterval = table.Column<TimeSpan>(type: "time", nullable: false),
                    LastUpdateUTC = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.ServerId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServerName",
                table: "GameServers",
                column: "ServerName",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServers");
        }
    }
}
