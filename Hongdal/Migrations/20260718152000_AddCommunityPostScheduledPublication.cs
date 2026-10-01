using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260718152000_AddCommunityPostScheduledPublication")]
public sealed class AddCommunityPostScheduledPublication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "PublicationAttemptCount",
            table: "platform_community_posts",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTime>(
            name: "PublicationClaimedAtUtc",
            table: "platform_community_posts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicationLastError",
            table: "platform_community_posts",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<DateTime>(
            name: "PublicationNextAttemptAtUtc",
            table: "platform_community_posts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PublicationStatusCode",
            table: "platform_community_posts",
            type: "varchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "published")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<DateTime>(
            name: "PublishedAtUtc",
            table: "platform_community_posts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ScheduledPublishAtUtc",
            table: "platform_community_posts",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.Sql(
            "UPDATE `platform_community_posts` " +
            "SET `PublishedAtUtc` = `CreatedAtUtc` " +
            "WHERE `PublishedAtUtc` IS NULL;");

        migrationBuilder.CreateIndex(
            name: "IX_platform_community_posts_publication_due",
            table: "platform_community_posts",
            columns:
            [
                "PublicationStatusCode",
                "PublicationNextAttemptAtUtc",
                "PublicationClaimedAtUtc"
            ]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_platform_community_posts_publication_due",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(name: "PublicationAttemptCount", table: "platform_community_posts");
        migrationBuilder.DropColumn(name: "PublicationClaimedAtUtc", table: "platform_community_posts");
        migrationBuilder.DropColumn(name: "PublicationLastError", table: "platform_community_posts");
        migrationBuilder.DropColumn(name: "PublicationNextAttemptAtUtc", table: "platform_community_posts");
        migrationBuilder.DropColumn(name: "PublicationStatusCode", table: "platform_community_posts");
        migrationBuilder.DropColumn(name: "PublishedAtUtc", table: "platform_community_posts");
        migrationBuilder.DropColumn(name: "ScheduledPublishAtUtc", table: "platform_community_posts");
    }
}
