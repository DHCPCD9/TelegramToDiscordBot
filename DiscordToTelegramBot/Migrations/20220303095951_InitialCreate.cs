using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DiscordToTelegramBot.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    telegram_id = table.Column<int>(type: "INTEGER", nullable: false),
                    telegram_chanel_id = table.Column<long>(type: "INTEGER", nullable: false),
                    discord_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    discord_channel_id = table.Column<ulong>(type: "INTEGER", nullable: false),
                    discord_thread_id = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages");
        }
    }
}
