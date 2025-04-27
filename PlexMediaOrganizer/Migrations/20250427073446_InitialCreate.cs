using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlexMediaOrganizer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TmdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    ImdbId = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    TvdbId = table.Column<int>(type: "INTEGER", nullable: true),
                    Season = table.Column<int>(type: "INTEGER", nullable: true),
                    Episode = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeTitle = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    DateAdded = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessedFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourcePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    DestinationPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MediaItemId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessedFiles_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_ImdbId",
                table: "MediaItems",
                column: "ImdbId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_Title_Year",
                table: "MediaItems",
                columns: new[] { "Title", "Year" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_TmdbId",
                table: "MediaItems",
                column: "TmdbId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_TvdbId",
                table: "MediaItems",
                column: "TvdbId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_FileHash",
                table: "ProcessedFiles",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_MediaItemId",
                table: "ProcessedFiles",
                column: "MediaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedFiles_SourcePath",
                table: "ProcessedFiles",
                column: "SourcePath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedFiles");

            migrationBuilder.DropTable(
                name: "MediaItems");
        }
    }
}
