using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260717120000_AddInboundSupplierCode")]
public sealed class AddInboundSupplierCode : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "공급처코드",
            table: "입고요청",
            type: "varchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "공급처코드",
            table: "입고요청");
    }
}
