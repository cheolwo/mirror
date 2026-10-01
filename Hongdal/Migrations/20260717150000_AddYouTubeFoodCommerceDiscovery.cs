using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260717150000_AddYouTubeFoodCommerceDiscovery")]
public sealed class AddYouTubeFoodCommerceDiscovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "channel_handle",
            table: "youtube_watched_channels",
            type: "varchar(100)",
            maxLength: 100,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "country_code",
            table: "youtube_watched_channels",
            type: "varchar(2)",
            maxLength: 2,
            nullable: false,
            defaultValue: "ZZ")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "default_language_code",
            table: "youtube_watched_channels",
            type: "varchar(10)",
            maxLength: 10,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "food_category_codes",
            table: "youtube_watched_channels",
            type: "varchar(300)",
            maxLength: 300,
            nullable: false,
            defaultValue: "")
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<int>(
            name: "import_discovery_score",
            table: "youtube_watched_channels",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<bool>(
            name: "is_food_channel",
            table: "youtube_watched_channels",
            type: "tinyint(1)",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<int>(
            name: "purchase_discovery_score",
            table: "youtube_watched_channels",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "research_note",
            table: "youtube_watched_channels",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<string>(
            name: "research_source_url",
            table: "youtube_watched_channels",
            type: "varchar(1000)",
            maxLength: 1000,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<DateTime>(
            name: "research_verified_at_utc",
            table: "youtube_watched_channels",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "youtube_video_product_candidates",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                youtube_channel_video_id = table.Column<long>(type: "bigint", nullable: false),
                product_key = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                product_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                brand_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                origin_country_code = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                hs_code_candidate = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                temperature_code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                logistics_mode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                candidate_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                video_timestamp_seconds = table.Column<int>(type: "int", nullable: true),
                discovery_evidence = table.Column<string>(type: "text", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                extraction_method = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                review_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                sponsorship_disclosure_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                allowed_intent_types = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                official_purchase_url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                review_note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                reviewer_user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                reviewed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_youtube_video_product_candidates", x => x.Id);
                table.ForeignKey(
                    name: "FK_youtube_video_product_candidates_youtube_channel_videos_yout~",
                    column: x => x.youtube_channel_video_id,
                    principalTable: "youtube_channel_videos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_youtube_watched_channels_is_food_channel_purchase_discovery_~",
            table: "youtube_watched_channels",
            columns: new[] { "is_food_channel", "purchase_discovery_score", "import_discovery_score" });

        migrationBuilder.CreateIndex(
            name: "IX_youtube_watched_channels_country_active_sync",
            table: "youtube_watched_channels",
            columns: new[] { "country_code", "is_active", "last_synced_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_youtube_video_product_candidates_candidate_type_review_status",
            table: "youtube_video_product_candidates",
            columns: new[] { "candidate_type", "review_status" });

        migrationBuilder.CreateIndex(
            name: "IX_youtube_video_product_candidates_review_status_updated_at_utc",
            table: "youtube_video_product_candidates",
            columns: new[] { "review_status", "updated_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_youtube_video_product_candidates_youtube_channel_video_id_pr~",
            table: "youtube_video_product_candidates",
            columns: new[] { "youtube_channel_video_id", "product_key" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "youtube_video_product_candidates");

        migrationBuilder.DropIndex(
            name: "IX_youtube_watched_channels_is_food_channel_purchase_discovery_~",
            table: "youtube_watched_channels");

        migrationBuilder.DropIndex(
            name: "IX_youtube_watched_channels_country_active_sync",
            table: "youtube_watched_channels");

        migrationBuilder.DropColumn(name: "channel_handle", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "country_code", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "default_language_code", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "food_category_codes", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "import_discovery_score", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "is_food_channel", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "purchase_discovery_score", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "research_note", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "research_source_url", table: "youtube_watched_channels");
        migrationBuilder.DropColumn(name: "research_verified_at_utc", table: "youtube_watched_channels");
    }
}
