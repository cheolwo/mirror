using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261003103000_AddFoodOrderDriverSettlement")]
public sealed class AddFoodOrderDriverSettlement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "음식주문기사정산",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                정산StableId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                음식주문Id = table.Column<long>(type: "bigint", nullable: false),
                주문번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                음식점명 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                배달시도Id = table.Column<long>(type: "bigint", nullable: false),
                배달시도StableId = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                운송Id = table.Column<long>(type: "bigint", nullable: false),
                기사Id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                세전대금 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                요금정책판본 = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                요금계산근거Json = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                공제액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                수령액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                공제근거참조 = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                공제근거범위Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                정산상태Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                지급상태Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                보류사유 = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                실행모드Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                전달완료시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                수령확인시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Revision = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_음식주문기사정산", x => x.Id);
                table.ForeignKey("FK_음식주문기사정산_음식주문_음식주문Id", x => x.음식주문Id, "음식주문", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_음식주문기사정산_음식배달시도_배달시도Id", x => x.배달시도Id, "음식배달시도", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_음식주문기사정산_운송실행투영_운송Id", x => x.운송Id, "운송실행투영", "id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_음식주문기사정산_정산StableId", "음식주문기사정산", "정산StableId", unique: true);
        migrationBuilder.CreateIndex("IX_음식주문기사정산_주문번호", "음식주문기사정산", "주문번호", unique: true);
        migrationBuilder.CreateIndex("IX_음식주문기사정산_배달시도Id", "음식주문기사정산", "배달시도Id", unique: true);
        migrationBuilder.CreateIndex("IX_음식주문기사정산_기사Id_전달완료시각Utc", "음식주문기사정산", new[] { "기사Id", "전달완료시각Utc" });
        migrationBuilder.CreateIndex("IX_음식주문기사정산_음식주문Id", "음식주문기사정산", "음식주문Id");
        migrationBuilder.CreateIndex("IX_음식주문기사정산_운송Id", "음식주문기사정산", "운송Id");
        migrationBuilder.CreateTable(
            name: "음식주문기사지급검증",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                정산Id = table.Column<long>(type: "bigint", nullable: false),
                지급StableId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                멱등키 = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                확인세전대금 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                확인공제액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                모의수령액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                공제근거참조 = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                결과Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                검증관리자Id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                검증시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_음식주문기사지급검증", x => x.Id);
                table.ForeignKey("FK_음식주문기사지급검증_음식주문기사정산_정산Id", x => x.정산Id, "음식주문기사정산", "Id", onDelete: ReferentialAction.Restrict);
            }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_음식주문기사지급검증_지급StableId", "음식주문기사지급검증", "지급StableId", unique: true);
        migrationBuilder.CreateIndex("IX_음식주문기사지급검증_멱등키", "음식주문기사지급검증", "멱등키", unique: true);
        migrationBuilder.CreateIndex("IX_음식주문기사지급검증_정산Id", "음식주문기사지급검증", "정산Id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("음식주문기사지급검증");
        migrationBuilder.DropTable("음식주문기사정산");
    }
}
