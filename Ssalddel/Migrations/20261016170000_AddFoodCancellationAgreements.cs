using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

public sealed partial class AddFoodCancellationAgreements : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "음식주문취소합의",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                합의StableId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                음식주문Id = table.Column<long>(type: "bigint", nullable: false),
                주문번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                주문판본 = table.Column<long>(type: "bigint", nullable: false),
                주문상태 = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                주문자부담액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                음식점부담액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                비용근거참조 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                주문자동의 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                음식점동의 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                영수증Json = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                취소반영시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_음식주문취소합의", x => x.Id);
                table.ForeignKey("FK_음식주문취소합의_음식주문_음식주문Id", x => x.음식주문Id, "음식주문", "id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_음식주문취소합의_합의StableId", "음식주문취소합의", "합의StableId", unique: true);
        migrationBuilder.CreateIndex("IX_음식주문취소합의_음식주문Id_CreatedAtUtc", "음식주문취소합의", new[] { "음식주문Id", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("음식주문취소합의");
}
