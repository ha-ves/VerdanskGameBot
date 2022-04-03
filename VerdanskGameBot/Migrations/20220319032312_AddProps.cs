using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class AddProps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RconIP",
                table: "GameServers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "GameServers",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RconIP",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "note",
                table: "GameServers");
        }
    }
}
