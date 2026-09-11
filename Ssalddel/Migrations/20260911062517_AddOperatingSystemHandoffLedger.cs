using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatingSystemHandoffLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "운영체제업무인계",
                columns: table => new
                {
                    인계StableId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    생성멱등Key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    출발운영체제Id = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    도착운영체제Id = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    현재책임운영체제Id = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    출발업무유형Code = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    출발업무StableId = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    출발업무Revision = table.Column<long>(type: "bigint", nullable: false),
                    도착업무StableId = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    인계계약Code = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    인계계약Revision = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    최소상태사본Json = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    공개범위Code = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    마지막응답요청Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    마지막응답결정Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    응답사유Code = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    요청시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    만료시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    응답시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_운영체제업무인계", x => x.인계StableId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "운영체제업무인계_Outbox",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    멱등Key = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    인계StableId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    이벤트Type = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    처리상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    처리시도수 = table.Column<int>(type: "int", nullable: false),
                    다음처리시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_운영체제업무인계_Outbox", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_운영체제업무인계_도착운영체제Id_상태Code_요청시각Utc",
                table: "운영체제업무인계",
                columns: new[] { "도착운영체제Id", "상태Code", "요청시각Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_운영체제업무인계_출발업무StableId_출발업무Revision",
                table: "운영체제업무인계",
                columns: new[] { "출발업무StableId", "출발업무Revision" });

            migrationBuilder.CreateIndex(
                name: "IX_운영체제업무인계_출발운영체제Id_생성멱등Key",
                table: "운영체제업무인계",
                columns: new[] { "출발운영체제Id", "생성멱등Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_운영체제업무인계_Outbox_멱등Key",
                table: "운영체제업무인계_Outbox",
                column: "멱등Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_운영체제업무인계_Outbox_인계StableId_CreatedAt",
                table: "운영체제업무인계_Outbox",
                columns: new[] { "인계StableId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_운영체제업무인계_Outbox_처리상태Code_다음처리시각Utc",
                table: "운영체제업무인계_Outbox",
                columns: new[] { "처리상태Code", "다음처리시각Utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "운영체제업무인계");

            migrationBuilder.DropTable(
                name: "운영체제업무인계_Outbox");
        }
    }
}
