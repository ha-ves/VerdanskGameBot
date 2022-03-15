using Microsoft.EntityFrameworkCore.Migrations;

namespace VerdanskGameBot.Migrations
{
    public partial class Awal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 22, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    desc = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    img_url = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    is_online = table.Column<bool>(type: "INTEGER", nullable: false),
                    last_online_time = table.Column<long>(type: "INTEGER", nullable: false),
                    rtt = table.Column<ushort>(type: "INTEGER", nullable: false),
                    added_by = table.Column<ulong>(type: "INTEGER", nullable: false),
                    chan_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    msg_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ip = table.Column<string>(type: "TEXT", nullable: false),
                    port = table.Column<ushort>(type: "INTEGER", nullable: false),
                    rcon = table.Column<ushort>(type: "INTEGER", nullable: false),
                    pass = table.Column<string>(type: "TEXT", nullable: false),
                    added_since = table.Column<long>(type: "INTEGER", nullable: false),
                    last_update = table.Column<long>(type: "INTEGER", nullable: false),
                    update_interval = table.Column<ulong>(type: "INTEGER", nullable: false),
                    config = table.Column<string>(type: "TEXT", nullable: true)
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
