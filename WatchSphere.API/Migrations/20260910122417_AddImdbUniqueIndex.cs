using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WatchSphere.API.Migrations
{
    /// <inheritdoc />
    public partial class AddImdbUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Media_ImdbId",
                table: "Media",
                column: "ImdbId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Media_ImdbId",
                table: "Media");
        }
    }
}
