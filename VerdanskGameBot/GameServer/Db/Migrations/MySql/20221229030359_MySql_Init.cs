using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerdanskGameBot.Migrations.MySql
{
    public partial class MySql_Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    ServerId = table.Column<string>(type: "varchar(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GameType = table.Column<string>(type: "varchar(22)", maxLength: 22, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ServerName = table.Column<string>(type: "varchar(22)", maxLength: 22, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastOnlineUTC = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GameLink = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AddedBy = table.Column<string>(type: "varchar(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ChannelId = table.Column<string>(type: "varchar(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageId = table.Column<string>(type: "varchar(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IP = table.Column<string>(type: "varchar(45)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GamePort = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    AddedSinceUTC = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastUpdateUTC = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdateInterval = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    note = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.ServerId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
