using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Simulation.Persistence.Migrations.SimulationSession
{
    /// <inheritdoc />
    public partial class AddFranchiseSupplyChainProjectionFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈공급망Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    Scenario고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    자료종류코드 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    실행모드코드 = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태권위코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    합성자료여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    운영상태여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    읽기전용Projection여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    자료개정번호 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    규칙개정번호 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    출처고유식별자목록JSON = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태PayloadSHA256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본세션개정번호 = table.Column<long>(type: "bigint", nullable: false),
                    Projection개정번호 = table.Column<long>(type: "bigint", nullable: false),
                    기준WorldTick = table.Column<int>(type: "int", nullable: false),
                    생성시각UTC = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈공급망Projection", x => new { x.세션고유식별자, x.공급망고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈공급망Projection_읽기전용Simulation", "`실행모드코드` = 'Simulation' AND `상태권위코드` = 'SessionAggregate' AND `합성자료여부` = 1 AND `운영상태여부` = 0 AND `읽기전용Projection여부` = 1");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈공급자Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급자고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급자키 = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    표시명 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    공급자종류코드 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈공급자Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.공급자고유식별자 });
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈공급자Projection_시뮬레이션_프랜차이즈공급망Projection_세션고유식별자_공급~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈공급망Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈매장Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장코드 = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    표시명 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    의미장소고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈매장Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.매장고유식별자 });
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장Projection_시뮬레이션_프랜차이즈공급망Projection_세션고유식별자_공급망~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈공급망Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈본부Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부코드 = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    표시명 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈본부Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자 });
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈본부Projection_시뮬레이션_프랜차이즈공급망Projection_세션고유식별자_공급망~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈공급망Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈매장공급안Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급안고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급안번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    공급안문서판본 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상업흐름모형코드 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    이행모형코드 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    시작WorldTick = table.Column<int>(type: "int", nullable: false),
                    종료WorldTick = table.Column<int>(type: "int", nullable: true),
                    통화코드 = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈매장공급안Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.매장공급안고유식별자 });
                    table.UniqueConstraint("AK_시뮬레이션_프랜차이즈매장공급안Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장공급안고유식~", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈매장공급안Projection_유효기간", "`종료WorldTick` IS NULL OR `종료WorldTick` >= `시작WorldTick`");
                    table.CheckConstraint("CK_프랜차이즈매장공급안Projection_첫Slice", "`상업흐름모형코드` = 'HeadquartersResale' AND `이행모형코드` = 'HeadquartersFleet'");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장공급안Projection_시뮬레이션_프랜차이즈본부Projection_세션고유식별자_공~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈본부Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈매장소속Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장소속고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    시작WorldTick = table.Column<int>(type: "int", nullable: false),
                    종료WorldTick = table.Column<int>(type: "int", nullable: true),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈매장소속Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.매장소속고유식별자 });
                    table.UniqueConstraint("AK_시뮬레이션_프랜차이즈매장소속Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장소속고유식별자~", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장소속고유식별자, x.매장고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈매장소속Projection_유효기간", "`종료WorldTick` IS NULL OR `종료WorldTick` >= `시작WorldTick`");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장소속Projection_시뮬레이션_프랜차이즈매장Projection_세션고유식별자_공급~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.매장고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈매장Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "매장고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장소속Projection_시뮬레이션_프랜차이즈본부Projection_세션고유식별자_공급~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈본부Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈조달계약Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    조달계약고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급자고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    계약번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    계약문서판본 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    시작WorldTick = table.Column<int>(type: "int", nullable: false),
                    종료WorldTick = table.Column<int>(type: "int", nullable: true),
                    통화코드 = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈조달계약Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.조달계약고유식별자 });
                    table.UniqueConstraint("AK_시뮬레이션_프랜차이즈조달계약Projection_세션고유식별자_공급망고유식별자_본부고유식별자_조달계약고유식별자", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.조달계약고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈조달계약Projection_유효기간", "`종료WorldTick` IS NULL OR `종료WorldTick` >= `시작WorldTick`");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈조달계약Projection_시뮬레이션_프랜차이즈공급자Projection_세션고유식별자_공~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.공급자고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈공급자Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "공급자고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈조달계약Projection_시뮬레이션_프랜차이즈본부Projection_세션고유식별자_공급~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈본부Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈매장발주Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장발주고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장소속고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급안고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    요청고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    요청PayloadSHA256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    발주번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    공급안문서판본사본 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    판매본부고유식별자사본 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    상업흐름모형코드사본 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    이행모형코드사본 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    납품지고유식별자사본 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    통화코드사본 = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    요청납품WorldTick = table.Column<int>(type: "int", nullable: false),
                    생성WorldTick = table.Column<int>(type: "int", nullable: false),
                    제출WorldTick = table.Column<int>(type: "int", nullable: true),
                    발주합계금액사본 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈매장발주Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.매장발주고유식별자 });
                    table.UniqueConstraint("AK_시뮬레이션_프랜차이즈매장발주Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장공급안고유식별~", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자, x.매장발주고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈매장발주Projection_합계", "`발주합계금액사본` >= 0");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장발주Projection_시뮬레이션_프랜차이즈매장공급안Projection_세션고유식별자~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈매장공급안Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장공급안고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장발주Projection_시뮬레이션_프랜차이즈매장소속Projection_세션고유식별자_~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장소속고유식별자, x.매장고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈매장소속Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장소속고유식별자", "매장고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈조달계약품목Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    조달계약품목고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    조달계약고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    상품고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급자SKU = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    품목명 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    발주단위코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    포장내용수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    포장내용단위코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단위변환규칙개정번호 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단가 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    최소발주수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    최대발주수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    보관조건코드 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false),
                    출처고유식별자목록JSON = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈조달계약품목Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.조달계약품목고유식별자 });
                    table.UniqueConstraint("AK_시뮬레이션_프랜차이즈조달계약품목Projection_세션고유식별자_공급망고유식별자_본부고유식별자_조달계약고유식~", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.조달계약고유식별자, x.조달계약품목고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈조달계약품목Projection_수량가격", "`포장내용수량` > 0 AND `단가` >= 0 AND `최소발주수량` > 0 AND (`최대발주수량` IS NULL OR `최대발주수량` >= `최소발주수량`)");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈조달계약품목Projection_시뮬레이션_프랜차이즈조달계약Projection_세션고유식별~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.조달계약고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈조달계약Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "조달계약고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈매장공급안품목Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급안품목고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급안고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    원본조달계약고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    원본조달계약품목고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    상품고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급SKU = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    품목명 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    발주단위코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    포장내용수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    포장내용단위코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단위변환규칙개정번호 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단가 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    최소발주수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    최대발주수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    보관조건코드 = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    상태코드 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false),
                    출처고유식별자목록JSON = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈매장공급안품목Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.매장공급안품목고유식별자 });
                    table.UniqueConstraint("AK_시뮬레이션_프랜차이즈매장공급안품목Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장공급안고~", x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자, x.매장공급안품목고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈매장공급안품목Projection_수량가격", "`포장내용수량` > 0 AND `단가` >= 0 AND `최소발주수량` > 0 AND (`최대발주수량` IS NULL OR `최대발주수량` >= `최소발주수량`)");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장공급안품목Projection_시뮬레이션_프랜차이즈매장공급안Projection_세션고유~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈매장공급안Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장공급안고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장공급안품목Projection_시뮬레이션_프랜차이즈조달계약품목Projection_세션고~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.원본조달계약고유식별자, x.원본조달계약품목고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈조달계약품목Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "조달계약고유식별자", "조달계약품목고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "시뮬레이션_프랜차이즈매장발주품목Projection",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    공급망고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장발주품목고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    본부고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급안고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장발주고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급안품목고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    상품고유식별자사본 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "ascii_bin")
                        .Annotation("MySql:CharSet", "ascii"),
                    매장공급SKU사본 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    품목명사본 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    발주단위코드사본 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    포장내용수량사본 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    포장내용단위코드사본 = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단위변환규칙개정번호사본 = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    단가사본 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    통화코드사본 = table.Column<string>(type: "varchar(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    요청발주단위수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    요청내용수량사본 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    수락발주단위수량 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    품목금액사본 = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    원본개정번호 = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션_프랜차이즈매장발주품목Projection", x => new { x.세션고유식별자, x.공급망고유식별자, x.매장발주품목고유식별자 });
                    table.CheckConstraint("CK_프랜차이즈매장발주품목Projection_수량가격", "`요청발주단위수량` > 0 AND `요청내용수량사본` > 0 AND (`수락발주단위수량` IS NULL OR (`수락발주단위수량` >= 0 AND `수락발주단위수량` <= `요청발주단위수량`)) AND `품목금액사본` >= 0");
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장발주품목Projection_시뮬레이션_프랜차이즈매장공급안품목Projection_세션고~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자, x.매장공급안품목고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈매장공급안품목Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장공급안고유식별자", "매장공급안품목고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_시뮬레이션_프랜차이즈매장발주품목Projection_시뮬레이션_프랜차이즈매장발주Projection_세션고유식별~",
                        columns: x => new { x.세션고유식별자, x.공급망고유식별자, x.본부고유식별자, x.매장공급안고유식별자, x.매장발주고유식별자 },
                        principalTable: "시뮬레이션_프랜차이즈매장발주Projection",
                        principalColumns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장공급안고유식별자", "매장발주고유식별자" },
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈공급망Projection_세션고유식별자_Scenario고유식별자",
                table: "시뮬레이션_프랜차이즈공급망Projection",
                columns: new[] { "세션고유식별자", "Scenario고유식별자" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈공급자Projection_세션고유식별자_공급망고유식별자_공급자키",
                table: "시뮬레이션_프랜차이즈공급자Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "공급자키" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장공급안품목Projection_세션고유식별자_공급망고유식별자_매장공급안고유식별자_매장공~",
                table: "시뮬레이션_프랜차이즈매장공급안품목Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장공급안고유식별자", "매장공급SKU" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장공급안품목Projection_세션고유식별자_공급망고유식별자_본부고유식별자_원본조달계약~",
                table: "시뮬레이션_프랜차이즈매장공급안품목Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "원본조달계약고유식별자", "원본조달계약품목고유식별자" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장공급안Projection_세션고유식별자_공급망고유식별자_본부고유식별자_공급안번호",
                table: "시뮬레이션_프랜차이즈매장공급안Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "공급안번호" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주품목Projection_세션고유식별자_공급망고유식별자_매장발주고유식별자_매장공급안~",
                table: "시뮬레이션_프랜차이즈매장발주품목Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장발주고유식별자", "매장공급안품목고유식별자" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주품목Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장공급안고~1",
                table: "시뮬레이션_프랜차이즈매장발주품목Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장공급안고유식별자", "매장발주고유식별자" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주품목Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장공급안고유~",
                table: "시뮬레이션_프랜차이즈매장발주품목Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장공급안고유식별자", "매장공급안품목고유식별자" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주Projection_세션고유식별자_공급망고유식별자_매장공급안고유식별자_상태코드_요~",
                table: "시뮬레이션_프랜차이즈매장발주Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장공급안고유식별자", "상태코드", "요청납품WorldTick" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주Projection_세션고유식별자_공급망고유식별자_매장소속고유식별자_발주번호",
                table: "시뮬레이션_프랜차이즈매장발주Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장소속고유식별자", "발주번호" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주Projection_세션고유식별자_공급망고유식별자_매장소속고유식별자_요청고유식별자",
                table: "시뮬레이션_프랜차이즈매장발주Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장소속고유식별자", "요청고유식별자" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장발주Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장소속고유식별자~",
                table: "시뮬레이션_프랜차이즈매장발주Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장소속고유식별자", "매장고유식별자" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장소속Projection_세션고유식별자_공급망고유식별자_매장고유식별자",
                table: "시뮬레이션_프랜차이즈매장소속Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장고유식별자" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장소속Projection_세션고유식별자_공급망고유식별자_본부고유식별자_매장고유식별자_시~",
                table: "시뮬레이션_프랜차이즈매장소속Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "매장고유식별자", "시작WorldTick" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈매장Projection_세션고유식별자_공급망고유식별자_매장코드",
                table: "시뮬레이션_프랜차이즈매장Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "매장코드" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈본부Projection_세션고유식별자_공급망고유식별자_본부코드",
                table: "시뮬레이션_프랜차이즈본부Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부코드" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈조달계약품목Projection_세션고유식별자_공급망고유식별자_조달계약고유식별자_공급자SKU",
                table: "시뮬레이션_프랜차이즈조달계약품목Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "조달계약고유식별자", "공급자SKU" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈조달계약Projection_세션고유식별자_공급망고유식별자_공급자고유식별자_상태코드",
                table: "시뮬레이션_프랜차이즈조달계약Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "공급자고유식별자", "상태코드" });

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션_프랜차이즈조달계약Projection_세션고유식별자_공급망고유식별자_본부고유식별자_계약번호",
                table: "시뮬레이션_프랜차이즈조달계약Projection",
                columns: new[] { "세션고유식별자", "공급망고유식별자", "본부고유식별자", "계약번호" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈매장발주품목Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈매장공급안품목Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈매장발주Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈조달계약품목Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈매장공급안Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈매장소속Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈조달계약Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈매장Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈공급자Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈본부Projection");

            migrationBuilder.DropTable(
                name: "시뮬레이션_프랜차이즈공급망Projection");
        }
    }
}
