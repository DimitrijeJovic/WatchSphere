using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WatchSphere.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "ImdbRating",
                table: "Media",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImdbRatingUpdatedAt",
                table: "Media",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImdbVotes",
                table: "Media",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImdbRating",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ImdbRatingUpdatedAt",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ImdbVotes",
                table: "Media");
        }
    }
}
