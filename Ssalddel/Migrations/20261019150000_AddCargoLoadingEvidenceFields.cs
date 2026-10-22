using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261019150000_AddCargoLoadingEvidenceFields")]
public sealed partial class AddCargoLoadingEvidenceFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "cargo_input_volume_cbm", table: "shipper_requests", type: "decimal(65,30)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "cargo_volume_evidence_code", table: "shipper_requests", type: "varchar(80)", maxLength: 80, nullable: true).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<string>(name: "cargo_loading_calculation_revision", table: "shipper_requests", type: "varchar(80)", maxLength: 80, nullable: true).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<bool>(name: "cargo_floor_rotation_allowed", table: "shipper_requests", type: "tinyint(1)", nullable: true);
        migrationBuilder.AddColumn<bool>(name: "cargo_stacking_allowed", table: "shipper_requests", type: "tinyint(1)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "cargo_input_volume_cbm", "cargo_volume_evidence_code", "cargo_loading_calculation_revision", "cargo_floor_rotation_allowed", "cargo_stacking_allowed" })
            migrationBuilder.DropColumn(name: column, table: "shipper_requests");
    }
}
