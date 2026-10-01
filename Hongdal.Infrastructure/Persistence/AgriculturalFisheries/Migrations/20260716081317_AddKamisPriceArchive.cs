using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hongdal.Infrastructure.Persistence.AgriculturalFisheries.Migrations
{
    /// <inheritdoc />
    public partial class AddKamisPriceArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agri_kamis_price_collection_runs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RunKey = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusCode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LatestSurveyDate = table.Column<DateOnly>(type: "date", nullable: true),
                    QuerySummary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FetchedCount = table.Column<int>(type: "int", nullable: false),
                    InsertedCount = table.Column<int>(type: "int", nullable: false),
                    UpdatedCount = table.Column<int>(type: "int", nullable: false),
                    ExistingCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agri_kamis_price_collection_runs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "agri_kamis_price_observations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FirstCollectionRunId = table.Column<long>(type: "bigint", nullable: false),
                    RecordKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductClassCode = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProductClassName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoryCode = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoryName = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryCode = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CountryName = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SurveyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ItemCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KindName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    KindCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RankName = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RankCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Unit = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceRaw = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    PreviousDayLabel = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousDayPriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    OneWeekAgoLabel = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OneWeekAgoPriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    TwoWeeksAgoLabel = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TwoWeeksAgoPriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    OneMonthAgoLabel = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OneMonthAgoPriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    OneYearAgoLabel = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OneYearAgoPriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    NormalYearLabel = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NormalYearPriceKrw = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    IsPriceMissing = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SourceUrl = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FirstCollectedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agri_kamis_price_observations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agri_kamis_price_observations_agri_kamis_price_collection_ru~",
                        column: x => x.FirstCollectionRunId,
                        principalTable: "agri_kamis_price_collection_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_collection_runs_RequestedDate",
                table: "agri_kamis_price_collection_runs",
                column: "RequestedDate");

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_collection_runs_RunKey",
                table: "agri_kamis_price_collection_runs",
                column: "RunKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_collection_runs_StatusCode_StartedAtUtc",
                table: "agri_kamis_price_collection_runs",
                columns: new[] { "StatusCode", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_FirstCollectionRunId",
                table: "agri_kamis_price_observations",
                column: "FirstCollectionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_ItemName_SurveyDate",
                table: "agri_kamis_price_observations",
                columns: new[] { "ItemName", "SurveyDate" });

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_LastSeenAtUtc",
                table: "agri_kamis_price_observations",
                column: "LastSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_RecordKey",
                table: "agri_kamis_price_observations",
                column: "RecordKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agri_kamis_price_observations_SurveyDate_ProductClassCode_Ca~",
                table: "agri_kamis_price_observations",
                columns: new[] { "SurveyDate", "ProductClassCode", "CategoryCode", "ItemCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agri_kamis_price_observations");

            migrationBuilder.DropTable(
                name: "agri_kamis_price_collection_runs");
        }
    }
}
