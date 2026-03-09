using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace ClanWarReminder.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260309183000_AddClanWarHistory")]
    public partial class AddClanWarHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "clan_war_weeks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClanTag = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClanName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WarKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_war_weeks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "clan_war_week_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClanWarWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerTag = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BattlesPlayed = table.Column<int>(type: "integer", nullable: false),
                    MaxBattles = table.Column<int>(type: "integer", nullable: false),
                    TotalContribution = table.Column<int>(type: "integer", nullable: false),
                    AverageContributionPerBattle = table.Column<double>(type: "double precision", nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clan_war_week_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_clan_war_week_members_clan_war_weeks_ClanWarWeekId",
                        column: x => x.ClanWarWeekId,
                        principalTable: "clan_war_weeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_clan_war_week_members_ClanWarWeekId_PlayerTag",
                table: "clan_war_week_members",
                columns: new[] { "ClanWarWeekId", "PlayerTag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clan_war_weeks_ClanTag_WarKey",
                table: "clan_war_weeks",
                columns: new[] { "ClanTag", "WarKey" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clan_war_week_members");

            migrationBuilder.DropTable(
                name: "clan_war_weeks");
        }
    }
}
