using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddNeighborhoodMicroLogisticsHubs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "생활권물류거점",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    StableId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    신청가원장Id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    신청자UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    관리담당자UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    공간StableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    공간유형Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    생활권Key = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    대략위치Label = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    정확위치보호참조 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    소유자동의 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    소유자동의시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    소유자동의철회시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    관리주체동의 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    관리주체동의시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    관리주체동의철회시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    플랫폼승인 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    플랫폼승인시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    현장확인시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    단기보관가능 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    기사인계가능 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    주문자수령가능 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    상온밀봉품만허용 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    최대동시보관건수 = table.Column<int>(type: "int", nullable: false),
                    현재예약건수 = table.Column<int>(type: "int", nullable: false),
                    최대총중량Kg = table.Column<decimal>(type: "decimal(12,3)", nullable: false),
                    최대보관시간분 = table.Column<int>(type: "int", nullable: false),
                    입고가능시간창 = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    수령가능시간창 = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    완료건당고정보상 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    보상통화Code = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태사유 = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    연결창고Id = table.Column<long>(type: "bigint", nullable: true),
                    실운영허용 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StatusChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_생활권물류거점", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "생활권물류거점보상기록",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    거점Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    업무StableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    멱등성Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    금액 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    통화Code = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    실제지급대상 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    실행모드Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_생활권물류거점보상기록", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "생활권물류거점용량예약",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    거점Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    업무StableId = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    멱등성Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    중량Kg = table.Column<decimal>(type: "decimal(12,3)", nullable: false),
                    보관시간분 = table.Column<int>(type: "int", nullable: false),
                    상태Code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_생활권물류거점용량예약", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점_생활권Key_상태Code",
                table: "생활권물류거점",
                columns: new[] { "생활권Key", "상태Code" });

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점_신청가원장Id",
                table: "생활권물류거점",
                column: "신청가원장Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점_StableId",
                table: "생활권물류거점",
                column: "StableId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점보상기록_거점Id_멱등성Key",
                table: "생활권물류거점보상기록",
                columns: new[] { "거점Id", "멱등성Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점보상기록_업무StableId",
                table: "생활권물류거점보상기록",
                column: "업무StableId");

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점용량예약_거점Id_멱등성Key",
                table: "생활권물류거점용량예약",
                columns: new[] { "거점Id", "멱등성Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_생활권물류거점용량예약_거점Id_상태Code",
                table: "생활권물류거점용량예약",
                columns: new[] { "거점Id", "상태Code" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "생활권물류거점");

            migrationBuilder.DropTable(
                name: "생활권물류거점보상기록");

            migrationBuilder.DropTable(
                name: "생활권물류거점용량예약");
        }
    }
}
