using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class ServerCheckOverhaul : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "desc",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "rcon_ip",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "rcon_pass",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "rcon_port",
                table: "GameServers");

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "GameServers",
                type: "TEXT",
                maxLength: 22,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                table: "GameServers");

            migrationBuilder.AddColumn<string>(
                name: "desc",
                table: "GameServers",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "GameServers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rcon_ip",
                table: "GameServers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "rcon_pass",
                table: "GameServers",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<ushort>(
                name: "rcon_port",
                table: "GameServers",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);
        }
    }
}
