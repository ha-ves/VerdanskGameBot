using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class New : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    type = table.Column<string>(type: "TEXT", maxLength: 22, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    last_online_time = table.Column<long>(type: "INTEGER", nullable: false),
                    game_link = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    added_by = table.Column<ulong>(type: "INTEGER", nullable: false),
                    chan_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    msg_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    game_ip = table.Column<string>(type: "TEXT", nullable: false),
                    game_port = table.Column<ushort>(type: "INTEGER", nullable: false),
                    added_since = table.Column<long>(type: "INTEGER", nullable: false),
                    last_update = table.Column<long>(type: "INTEGER", nullable: false),
                    update_interval = table.Column<ulong>(type: "INTEGER", nullable: false),
                    note = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameServers");
        }
    }
}
