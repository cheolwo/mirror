using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261013190000_AddMartCustomerReceipt")]
public sealed class AddMartCustomerReceipt : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(name: "customer_receipt_evidence_json", table: "마트배송시도", type: "longtext", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "customer_receipt_evidence_json", table: "마트배송시도");
}
