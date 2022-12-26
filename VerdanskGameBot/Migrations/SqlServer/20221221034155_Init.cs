using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerdanskGameBot.Migrations.SqlServer
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ServerName = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: false),
                    GameType = table.Column<string>(type: "nvarchar(22)", maxLength: 22, nullable: false),
                    LastOnlineUTC = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GameLink = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddedBy = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "decimal(20,0)", nullable: false),
                    IP = table.Column<string>(type: "nvarchar(45)", nullable: false),
                    GamePort = table.Column<int>(type: "int", nullable: false),
                    AddedSinceUTC = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastUpdateUTC = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdateInterval = table.Column<TimeSpan>(type: "time", nullable: false),
                    note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => new { x.Id, x.ServerName })
                        .Annotation("SqlServer:Clustered", true);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServers");
        }
    }
}
