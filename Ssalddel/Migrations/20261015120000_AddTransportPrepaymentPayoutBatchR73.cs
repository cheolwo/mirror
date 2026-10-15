using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportPrepaymentPayoutBatchR73 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deposit_webhook_verification_hash",
                table: "결제",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "preparation_evidence_json",
                table: "결제",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "기사운송대금정산배치",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    revision = table.Column<long>(type: "bigint", nullable: false),
                    driver_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    execution_mode_code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    policy_revision = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    schedule_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    period_start_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    period_end_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    period_basis_code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payout_scheduled_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    idempotency_key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    content_digest = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status_code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    approved_total_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ready_total_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ready_count = table.Column<int>(type: "int", nullable: false),
                    held_count = table.Column<int>(type: "int", nullable: false),
                    created_by = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_기사운송대금정산배치", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "기사운송대금정산배치행",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    payout_request_id = table.Column<long>(type: "bigint", nullable: false),
                    transport_id = table.Column<long>(type: "bigint", nullable: false),
                    request_id = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    frozen_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    status_code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hold_reason_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    hold_message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_validated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_기사운송대금정산배치행", x => x.id);
                    table.ForeignKey(
                        name: "FK_기사운송대금정산배치행_기사운송대금정산배치_batch_id",
                        column: x => x.batch_id,
                        principalTable: "기사운송대금정산배치",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_기사운송대금정산배치행_기사운송대금지급요청_payout_request_id",
                        column: x => x.payout_request_id,
                        principalTable: "기사운송대금지급요청",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_기사운송대금정산배치_driver_id_period_start_utc",
                table: "기사운송대금정산배치",
                columns: new[] { "driver_id", "period_start_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_기사운송대금정산배치_idempotency_key",
                table: "기사운송대금정산배치",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_기사운송대금정산배치행_batch_id",
                table: "기사운송대금정산배치행",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_기사운송대금정산배치행_payout_request_id",
                table: "기사운송대금정산배치행",
                column: "payout_request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "기사운송대금정산배치행");

            migrationBuilder.DropTable(
                name: "기사운송대금정산배치");

            migrationBuilder.DropColumn(
                name: "deposit_webhook_verification_hash",
                table: "결제");

            migrationBuilder.DropColumn(
                name: "preparation_evidence_json",
                table: "결제");

        }
    }
}
