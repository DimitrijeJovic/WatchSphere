using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WatchSphere.API.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalCatalogueLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AniListId",
                table: "Media",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExternalRating",
                table: "Media",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalVotes",
                table: "Media",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImdbId",
                table: "Media",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "KitsuId",
                table: "Media",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedAt",
                table: "Media",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MalId",
                table: "Media",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalTitle",
                table: "Media",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Media",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "TmdbId",
                table: "Media",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrailerUrl",
                table: "Media",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Media_AniListId",
                table: "Media",
                column: "AniListId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_KitsuId",
                table: "Media",
                column: "KitsuId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_MalId",
                table: "Media",
                column: "MalId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Media_TmdbId",
                table: "Media",
                column: "TmdbId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Media_AniListId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_Media_KitsuId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_Media_MalId",
                table: "Media");

            migrationBuilder.DropIndex(
                name: "IX_Media_TmdbId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "AniListId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ExternalRating",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ExternalVotes",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ImdbId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "KitsuId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "LastSyncedAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "MalId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "OriginalTitle",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "TmdbId",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "TrailerUrl",
                table: "Media");
        }
    }
}
