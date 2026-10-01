using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using 홍달.Data;

#nullable disable

namespace Hongdal.Migrations;

[DbContext(typeof(HongdalContext))]
[Migration("20260717180000_AddCommunityPostTranslations")]
public sealed class AddCommunityPostTranslations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "OriginalLanguageCode",
            table: "platform_community_posts",
            type: "varchar(16)",
            maxLength: 16,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "platform_community_post_translations",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                PostId = table.Column<long>(type: "bigint", nullable: false),
                SourceLanguageCode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TargetLanguageCode = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                SourceContentHash = table.Column<string>(type: "char(64)", fixedLength: true, maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TranslatedTitle = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                TranslatedBody = table.Column<string>(type: "longtext", nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Provider = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ProviderModelVersion = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                IsHumanReviewed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_platform_community_post_translations", x => x.Id);
                table.ForeignKey(
                    name: "FK_platform_community_post_translations_posts",
                    column: x => x.PostId,
                    principalTable: "platform_community_posts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_community_post_translation_post_created",
            table: "platform_community_post_translations",
            columns: new[] { "PostId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "UX_community_post_translation_content",
            table: "platform_community_post_translations",
            columns: new[] { "PostId", "TargetLanguageCode", "SourceContentHash" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "platform_community_post_translations");
        migrationBuilder.DropColumn(name: "OriginalLanguageCode", table: "platform_community_posts");
    }
}
