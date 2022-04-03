using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class FixProps : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "config",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "rtt",
                table: "GameServers");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "config",
                table: "GameServers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<ushort>(
                name: "rtt",
                table: "GameServers",
                type: "INTEGER",
                nullable: false,
                defaultValue: (ushort)0);
        }
    }
}
