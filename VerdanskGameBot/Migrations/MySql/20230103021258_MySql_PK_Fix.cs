using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VerdanskGameBot.Migrations.MySql
{
    public partial class MySql_PK_Fix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "note",
                table: "GameServers",
                newName: "Note");

            migrationBuilder.RenameColumn(
                name: "UpdateInterval",
                table: "GameServers",
                newName: "UpdateIntervalHMS");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateUTC",
                table: "GameServers",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastOnlineUTC",
                table: "GameServers",
                type: "datetime(6)",
                nullable: true,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<int>(
                name: "ServerId",
                table: "GameServers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)")
                .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "GameServers",
                type: "varchar(64)",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedSinceUTC",
                table: "GameServers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "GameServers",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "LastModifiedSinceUTC",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "GameServers");

            migrationBuilder.RenameColumn(
                name: "Note",
                table: "GameServers",
                newName: "note");

            migrationBuilder.RenameColumn(
                name: "UpdateIntervalHMS",
                table: "GameServers",
                newName: "UpdateInterval");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastUpdateUTC",
                table: "GameServers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastOnlineUTC",
                table: "GameServers",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true,
                oldDefaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AlterColumn<string>(
                name: "ServerId",
                table: "GameServers",
                type: "varchar(64)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
        }
    }
}
