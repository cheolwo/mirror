using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260717141000_AddCommunityPostMomentumPromotion")]
public sealed class AddCommunityPostMomentumPromotion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CommunityMomentumCode",
            table: "platform_community_posts",
            type: "varchar(40)",
            maxLength: 40,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "CommunityMomentumMessage",
            table: "platform_community_posts",
            type: "varchar(240)",
            maxLength: 240,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<int>(
            name: "CommunityMomentumRoleParticipantCount",
            table: "platform_community_posts",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "CommunityMomentumUpdatedAtUtc",
            table: "platform_community_posts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsCommunityMomentumPromoted",
            table: "platform_community_posts",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CommunityMomentumCode",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(
            name: "CommunityMomentumMessage",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(
            name: "CommunityMomentumRoleParticipantCount",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(
            name: "CommunityMomentumUpdatedAtUtc",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(
            name: "IsCommunityMomentumPromoted",
            table: "platform_community_posts");
    }
}
