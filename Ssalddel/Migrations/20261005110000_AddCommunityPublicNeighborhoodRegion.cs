using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261005110000_AddCommunityPublicNeighborhoodRegion")]
public sealed class AddCommunityPublicNeighborhoodRegion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
                name: "PublicNeighborhoodRegionKey", table: "platform_community_posts",
                type: "varchar(80)", maxLength: 80, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex(
            name: "IX_platform_community_posts_public_neighborhood", table: "platform_community_posts",
            columns: ["WorkflowTag", "PublicNeighborhoodRegionKey", "IsDeleted", "PublicationStatusCode"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_platform_community_posts_public_neighborhood", "platform_community_posts");
        migrationBuilder.DropColumn("PublicNeighborhoodRegionKey", "platform_community_posts");
    }
}
