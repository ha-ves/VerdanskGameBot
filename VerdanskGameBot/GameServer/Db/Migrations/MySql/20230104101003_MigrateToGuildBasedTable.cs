using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerdanskGameBot.Migrations.MySql
{
    public partial class MigrateToGuildBasedTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GameServers",
                table: "GameServers");

            migrationBuilder.RenameTable(
                name: "GameServers",
                newName: "gameservers_Default");

            migrationBuilder.AddPrimaryKey(
                name: "PK_gameservers_Default",
                table: "gameservers_Default",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_gameservers_Default",
                table: "gameservers_Default");

            migrationBuilder.RenameTable(
                name: "gameservers_Default",
                newName: "GameServers");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GameServers",
                table: "GameServers",
                column: "ServerId");
        }
    }
}
