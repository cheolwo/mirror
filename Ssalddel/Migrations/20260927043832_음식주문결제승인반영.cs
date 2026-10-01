using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class 음식주문결제승인반영 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payment_approval_id",
                table: "음식주문",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "payment_approved_amount",
                table: "음식주문",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_approved_at_utc",
                table: "음식주문",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_approved_currency",
                table: "음식주문",
                type: "varchar(3)",
                maxLength: 3,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_음식주문_payment_approval_id",
                table: "음식주문",
                column: "payment_approval_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_음식주문_payment_approval_id",
                table: "음식주문");

            migrationBuilder.DropColumn(
                name: "payment_approval_id",
                table: "음식주문");

            migrationBuilder.DropColumn(
                name: "payment_approved_amount",
                table: "음식주문");

            migrationBuilder.DropColumn(
                name: "payment_approved_at_utc",
                table: "음식주문");

            migrationBuilder.DropColumn(
                name: "payment_approved_currency",
                table: "음식주문");

        }
    }
}
