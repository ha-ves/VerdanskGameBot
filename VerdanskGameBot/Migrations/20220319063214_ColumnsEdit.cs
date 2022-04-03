using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class ColumnsEdit : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "rcon",
                table: "GameServers",
                newName: "rcon_port");

            migrationBuilder.RenameColumn(
                name: "port",
                table: "GameServers",
                newName: "game_port");

            migrationBuilder.RenameColumn(
                name: "pass",
                table: "GameServers",
                newName: "rcon_pass");

            migrationBuilder.RenameColumn(
                name: "ip",
                table: "GameServers",
                newName: "game_ip");

            migrationBuilder.RenameColumn(
                name: "RconIP",
                table: "GameServers",
                newName: "rcon_ip");

            migrationBuilder.AlterColumn<string>(
                name: "rcon_ip",
                table: "GameServers",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "rcon_port",
                table: "GameServers",
                newName: "rcon");

            migrationBuilder.RenameColumn(
                name: "rcon_pass",
                table: "GameServers",
                newName: "pass");

            migrationBuilder.RenameColumn(
                name: "rcon_ip",
                table: "GameServers",
                newName: "RconIP");

            migrationBuilder.RenameColumn(
                name: "game_port",
                table: "GameServers",
                newName: "port");

            migrationBuilder.RenameColumn(
                name: "game_ip",
                table: "GameServers",
                newName: "ip");

            migrationBuilder.AlterColumn<string>(
                name: "RconIP",
                table: "GameServers",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
