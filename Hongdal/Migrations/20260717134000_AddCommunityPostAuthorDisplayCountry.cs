using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260717134000_AddCommunityPostAuthorDisplayCountry")]
public sealed class AddCommunityPostAuthorDisplayCountry : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AuthorDisplayCountryCode",
            table: "platform_community_posts",
            type: "varchar(2)",
            maxLength: 2,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "AuthorDisplayCountryName",
            table: "platform_community_posts",
            type: "varchar(80)",
            maxLength: 80,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<bool>(
            name: "IsAuthorDisplayCountryPublic",
            table: "platform_community_posts",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AuthorDisplayCountryCode",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(
            name: "AuthorDisplayCountryName",
            table: "platform_community_posts");

        migrationBuilder.DropColumn(
            name: "IsAuthorDisplayCountryPublic",
            table: "platform_community_posts");
    }
}
