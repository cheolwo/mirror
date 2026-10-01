using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260715013000_AddHongikHakdangAdminActivation")]
public sealed class AddHongikHakdangAdminActivation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_admin_enabled",
            table: "hongik_hakdang_card_collections",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "is_admin_enabled",
            table: "hongik_hakdang_cards",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_admin_enabled",
            table: "hongik_hakdang_card_collections");

        migrationBuilder.DropColumn(
            name: "is_admin_enabled",
            table: "hongik_hakdang_cards");
    }
}
