using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddAbnormalTransportIncidentScopeAndDecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "보류범위Code",
                table: "비정상운송사건",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "보험검토상태Code",
                table: "비정상운송사건",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "업무통제상태Code",
                table: "비정상운송사건",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "영향수량",
                table: "비정상운송사건",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "전체수량",
                table: "비정상운송사건",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "정상확인수량",
                table: "비정상운송사건",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "최근검토사유",
                table: "비정상운송사건",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "최근검토시각Utc",
                table: "비정상운송사건",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "최근검토요청Id",
                table: "비정상운송사건",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "최근검토자UserId",
                table: "비정상운송사건",
                type: "varchar(191)",
                maxLength: 191,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "현장진행불가",
                table: "비정상운송사건",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "보류범위Code", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "보험검토상태Code", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "업무통제상태Code", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "영향수량", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "전체수량", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "정상확인수량", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "최근검토사유", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "최근검토시각Utc", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "최근검토요청Id", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "최근검토자UserId", table: "비정상운송사건");
            migrationBuilder.DropColumn(name: "현장진행불가", table: "비정상운송사건");
        }
    }
}
