using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WatchSphere.API.Data;

#nullable disable

namespace WatchSphere.API.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260614000000_StandardizePlannedStatus")]
    public partial class StandardizePlannedStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE UserLibraries SET Status = 'Planned' WHERE Status = 'PlanToWatch'");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE UserLibraries SET Status = 'PlanToWatch' WHERE Status = 'Planned'");
        }
    }
}
