using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Infrastructure.Persistence.PublicData.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrativeDongDioramaDisplayLedgers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "regional_business_display_claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ClaimStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublicBusinessRecordId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AdministrativeRegionStableId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SemanticPlaceStableId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedDisplayName = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CategoryCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestedByUserStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StatusCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    ReviewedByUserStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewReasonCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regional_business_display_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regional_business_display_claims_public_licensed_business_re~",
                        column: x => x.PublicBusinessRecordId,
                        principalTable: "public_licensed_business_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "regional_diorama_sponsorship_campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CampaignStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BusinessDisplayClaimId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BadgeText = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DetailCardText = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    StatusCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    ReviewedAtUtc = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    RequestedByUserStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewedByUserStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewReasonCode = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regional_diorama_sponsorship_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_regional_diorama_sponsorship_campaigns_regional_business_dis~",
                        column: x => x.BusinessDisplayClaimId,
                        principalTable: "regional_business_display_claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_regional_business_display_claims_AdministrativeRegionStableI~",
                table: "regional_business_display_claims",
                columns: new[] { "AdministrativeRegionStableId", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_regional_business_display_claims_ClaimStableId",
                table: "regional_business_display_claims",
                column: "ClaimStableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_regional_business_display_claims_PublicBusinessRecordId_Stat~",
                table: "regional_business_display_claims",
                columns: new[] { "PublicBusinessRecordId", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_regional_diorama_sponsorship_campaigns_BusinessDisplayClaimI~",
                table: "regional_diorama_sponsorship_campaigns",
                columns: new[] { "BusinessDisplayClaimId", "StatusCode", "StartsAtUtc", "EndsAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_regional_diorama_sponsorship_campaigns_CampaignStableId",
                table: "regional_diorama_sponsorship_campaigns",
                column: "CampaignStableId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "regional_diorama_sponsorship_campaigns");

            migrationBuilder.DropTable(
                name: "regional_business_display_claims");
        }
    }
}
