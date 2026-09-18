using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialEventProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "재무대사예외",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StableId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    재무사건StableId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    예외Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    요약Code = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    감지일시Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    해결일시Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_재무대사예외", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "재무사건",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StableId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본Event유형 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본StableId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본Revision = table.Column<long>(type: "bigint", nullable: false),
                    재무영향ProfileStableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    재무의미Code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    재무영향유형Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    금액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    통화Code = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    업무발생일시Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    현금이동일시Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    지급기일Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    상대역할Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    증빙Hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    투영상태Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_재무사건", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "관리계정전기",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    재무사건Id = table.Column<long>(type: "bigint", nullable: false),
                    LineNumber = table.Column<int>(type: "int", nullable: false),
                    관리계정StableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    전기방향Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    금액 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    통화Code = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    매핑Revision = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    역분개대상StableId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_관리계정전기", x => x.Id);
                    table.ForeignKey(
                        name: "FK_관리계정전기_재무사건_재무사건Id",
                        column: x => x.재무사건Id,
                        principalTable: "재무사건",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "재무사건증빙",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    재무사건Id = table.Column<long>(type: "bigint", nullable: false),
                    증빙유형Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본참조 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본Revision = table.Column<long>(type: "bigint", nullable: false),
                    내용Hash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_재무사건증빙", x => x.Id);
                    table.ForeignKey(
                        name: "FK_재무사건증빙_재무사건_재무사건Id",
                        column: x => x.재무사건Id,
                        principalTable: "재무사건",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_관리계정전기_관리계정StableId_통화Code",
                table: "관리계정전기",
                columns: new[] { "관리계정StableId", "통화Code" });

            migrationBuilder.CreateIndex(
                name: "IX_관리계정전기_재무사건Id_LineNumber",
                table: "관리계정전기",
                columns: new[] { "재무사건Id", "LineNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_재무대사예외_상태Code_감지일시Utc",
                table: "재무대사예외",
                columns: new[] { "상태Code", "감지일시Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_재무대사예외_StableId",
                table: "재무대사예외",
                column: "StableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_재무사건_원본Event유형_원본StableId_원본Revision_재무의미Code",
                table: "재무사건",
                columns: new[] { "원본Event유형", "원본StableId", "원본Revision", "재무의미Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_재무사건_통화Code_업무발생일시Utc",
                table: "재무사건",
                columns: new[] { "통화Code", "업무발생일시Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_재무사건_StableId",
                table: "재무사건",
                column: "StableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_재무사건증빙_재무사건Id_증빙유형Code_원본참조",
                table: "재무사건증빙",
                columns: new[] { "재무사건Id", "증빙유형Code", "원본참조" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "관리계정전기");

            migrationBuilder.DropTable(
                name: "재무대사예외");

            migrationBuilder.DropTable(
                name: "재무사건증빙");

            migrationBuilder.DropTable(
                name: "재무사건");
        }
    }
}
