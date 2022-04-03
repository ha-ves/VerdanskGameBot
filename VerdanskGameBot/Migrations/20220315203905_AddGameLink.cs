using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class AddGameLink : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "game_link",
                table: "GameServers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "game_link",
                table: "GameServers");
        }
    }
}
