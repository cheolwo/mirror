using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class SyncTransportWeatherSurchargeAndAbnormalIncident : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "기사기상할증액",
                table: "음식운영정책",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "기사기상할증정책판본",
                table: "음식운영정책",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "기사기상할증활성화여부",
                table: "음식운영정책",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "driver_base_distance_payout",
                table: "운송실행투영",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "driver_expected_payout",
                table: "운송실행투영",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "driver_offer_priced_at_utc",
                table: "운송실행투영",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "driver_offer_pricing_revision",
                table: "운송실행투영",
                type: "varchar(180)",
                maxLength: 180,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "driver_weather_surcharge",
                table: "운송실행투영",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "driver_weather_surcharge_applied",
                table: "운송실행투영",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pickup_weather_code",
                table: "운송실행투영",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pickup_weather_evidence_status",
                table: "운송실행투영",
                type: "varchar(80)",
                maxLength: 80,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "pickup_weather_observed_at_utc",
                table: "운송실행투영",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pickup_weather_payload_hash",
                table: "운송실행투영",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "pickup_weather_source",
                table: "운송실행투영",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "비정상운송사건",
                columns: table => new
                {
                    사건StableId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    운송Id = table.Column<long>(type: "bigint", nullable: false),
                    운송의뢰Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    사건유형Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본예외Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단계Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    현재담당Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    보류전정산상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    정산보류적용여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    증빙참조있음 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    최초신고시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    최근신고시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    해결결과Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_비정상운송사건", x => x.사건StableId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_비정상운송사건_운송_상태",
                table: "비정상운송사건",
                columns: new[] { "운송Id", "상태Code" });

            migrationBuilder.CreateIndex(
                name: "IX_비정상운송사건_의뢰_상태_갱신",
                table: "비정상운송사건",
                columns: new[] { "운송의뢰Id", "상태Code", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "비정상운송사건");

            migrationBuilder.DropColumn(
                name: "기사기상할증액",
                table: "음식운영정책");

            migrationBuilder.DropColumn(
                name: "기사기상할증정책판본",
                table: "음식운영정책");

            migrationBuilder.DropColumn(
                name: "기사기상할증활성화여부",
                table: "음식운영정책");

            migrationBuilder.DropColumn(
                name: "driver_base_distance_payout",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "driver_expected_payout",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "driver_offer_priced_at_utc",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "driver_offer_pricing_revision",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "driver_weather_surcharge",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "driver_weather_surcharge_applied",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "pickup_weather_code",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "pickup_weather_evidence_status",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "pickup_weather_observed_at_utc",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "pickup_weather_payload_hash",
                table: "운송실행투영");

            migrationBuilder.DropColumn(
                name: "pickup_weather_source",
                table: "운송실행투영");
        }
    }
}
