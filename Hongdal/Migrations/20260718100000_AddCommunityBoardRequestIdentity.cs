using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260718100000_AddCommunityBoardRequestIdentity")]
public sealed class AddCommunityBoardRequestIdentity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestedByUserId",
            table: "platform_community_board_requests",
            type: "varchar(450)",
            maxLength: 450,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "ReviewedByUserId",
            table: "platform_community_board_requests",
            type: "varchar(450)",
            maxLength: 450,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_community_board_requests_requester_status",
            table: "platform_community_board_requests",
            columns: ["RequestedByUserId", "Status", "IsDeleted", "CreatedAtUtc"]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_community_board_requests_requester_status",
            table: "platform_community_board_requests");

        migrationBuilder.DropColumn(
            name: "RequestedByUserId",
            table: "platform_community_board_requests");

        migrationBuilder.DropColumn(
            name: "ReviewedByUserId",
            table: "platform_community_board_requests");
    }
}
