using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261003190000_AddCargoFareQuoteEvidence")]
public sealed class AddCargoFareQuoteEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "expected_distance_km", table: "운임구성", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "per_km_rate", table: "운임구성", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "minimum_fare", table: "운임구성", type: "decimal(18,2)", precision: 18, scale: 2, nullable: true);
        migrationBuilder.AddColumn<string>(name: "distance_basis", table: "운임구성", type: "varchar(80)", maxLength: 80, nullable: true).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<string>(name: "rate_source", table: "운임구성", type: "varchar(160)", maxLength: 160, nullable: true).Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var column in new[] { "expected_distance_km", "per_km_rate", "minimum_fare", "distance_basis", "rate_source" })
            migrationBuilder.DropColumn(name: column, table: "운임구성");
    }
}
