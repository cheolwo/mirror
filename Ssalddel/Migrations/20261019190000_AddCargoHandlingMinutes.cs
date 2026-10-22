using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261019190000_AddCargoHandlingMinutes")]
public sealed partial class AddCargoHandlingMinutes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "pickup_handling_minutes", table: "shipper_requests", type: "decimal(65,30)", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "dropoff_handling_minutes", table: "shipper_requests", type: "decimal(65,30)", nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "pickup_handling_minutes", table: "shipper_requests");
        migrationBuilder.DropColumn(name: "dropoff_handling_minutes", table: "shipper_requests");
    }
}
