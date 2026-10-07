using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;
namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261006120000_AddNeighborhoodDispatchChoice")]
public sealed class AddNeighborhoodDispatchChoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("neighborhood_dispatch_mode", "운송실행투영", type: "varchar(30)", maxLength: 30, nullable: true);
        migrationBuilder.AddColumn<long>("neighborhood_dispatch_revision", "운송실행투영", type: "bigint", nullable: false, defaultValue: 0L);
        migrationBuilder.AddColumn<bool>("neighborhood_dispatch_ready", "운송실행투영", type: "tinyint(1)", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<string>("neighborhood_collaboration_id", "운송실행투영", type: "varchar(100)", maxLength: 100, nullable: true);
        migrationBuilder.AddColumn<long>("neighborhood_terms_revision", "운송실행투영", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>("neighborhood_registration_key", "shipper_requests", type: "varchar(64)", maxLength: 64, nullable: true);
        migrationBuilder.CreateIndex("ux_neighborhood_registration_key", "shipper_requests", "neighborhood_registration_key", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ux_neighborhood_registration_key", "shipper_requests");
        migrationBuilder.DropColumn("neighborhood_registration_key", "shipper_requests");
        foreach (var name in new[] { "neighborhood_dispatch_mode", "neighborhood_dispatch_revision", "neighborhood_dispatch_ready", "neighborhood_collaboration_id", "neighborhood_terms_revision" })
            migrationBuilder.DropColumn(name, "운송실행투영");
    }
}
