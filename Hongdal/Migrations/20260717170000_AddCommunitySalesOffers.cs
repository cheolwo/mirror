using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260717170000_AddCommunitySalesOffers")]
public sealed class AddCommunitySalesOffers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SalesOfferJson",
            table: "platform_community_posts",
            type: "longtext",
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SalesOfferJson",
            table: "platform_community_posts");
    }
}
