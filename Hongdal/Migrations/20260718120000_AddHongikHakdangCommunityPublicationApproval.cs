using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260718120000_AddHongikHakdangCommunityPublicationApproval")]
public sealed class AddHongikHakdangCommunityPublicationApproval : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_community_publication_approved",
            table: "hongik_hakdang_cards",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateIndex(
            name: "IX_hh_cards_community_publication",
            table: "hongik_hakdang_cards",
            columns: ["is_community_publication_approved", "is_active", "is_admin_enabled"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_hh_cards_community_publication",
            table: "hongik_hakdang_cards");

        migrationBuilder.DropColumn(
            name: "is_community_publication_approved",
            table: "hongik_hakdang_cards");
    }
}
