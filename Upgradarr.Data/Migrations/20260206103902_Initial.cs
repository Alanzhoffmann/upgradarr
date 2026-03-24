using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Upgradarr.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackedDownloads",
                columns: table => new
                {
                    DownloadId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    RemoveAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Added = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemScores = table.Column<string>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackedDownloads", x => x.DownloadId);
                }
            );

            migrationBuilder.CreateTable(
                name: "UpgradeStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false).Annotation("Sqlite:Autoincrement", true),
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    ItemType = table.Column<int>(type: "INTEGER", nullable: false),
                    ParentSeriesId = table.Column<int>(type: "INTEGER", nullable: true),
                    SeasonNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    SearchState = table.Column<int>(type: "INTEGER", nullable: false),
                    IsMonitored = table.Column<bool>(type: "INTEGER", nullable: false),
                    QueuePosition = table.Column<int>(type: "INTEGER", nullable: false),
                    ReleaseDate = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsMissing = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UpgradeStates", x => x.Id);
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TrackedDownloads");

            migrationBuilder.DropTable(name: "UpgradeStates");
        }
    }
}
