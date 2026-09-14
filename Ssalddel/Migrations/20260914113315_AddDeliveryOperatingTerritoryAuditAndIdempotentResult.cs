using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryOperatingTerritoryAuditAndIdempotentResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor_user_stable_id",
                table: "delivery_operating_territory_command_receipts",
                type: "varchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "result_json",
                table: "delivery_operating_territory_command_receipts",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "actor_user_stable_id",
                table: "delivery_operating_territory_change_outbox",
                type: "varchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_user_stable_id",
                table: "delivery_operating_territory_command_receipts");

            migrationBuilder.DropColumn(
                name: "result_json",
                table: "delivery_operating_territory_command_receipts");

            migrationBuilder.DropColumn(
                name: "actor_user_stable_id",
                table: "delivery_operating_territory_change_outbox");
        }
    }
}
