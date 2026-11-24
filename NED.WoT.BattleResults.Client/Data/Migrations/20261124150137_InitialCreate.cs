using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NED.WoT.BattleResults.Client.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LobbyTeams",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                LobbyKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                TeamName = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LobbyTeams", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LobbyTeams_LobbyKey",
            table: "LobbyTeams",
            column: "LobbyKey",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "LobbyTeams");
    }
}
