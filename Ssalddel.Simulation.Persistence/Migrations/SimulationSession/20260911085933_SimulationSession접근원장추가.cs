using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Simulation.Persistence.Migrations.SimulationSession
{
    /// <inheritdoc />
    public partial class SimulationSession접근원장추가 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "시뮬레이션세션_접근원장",
                columns: table => new
                {
                    세션고유식별자 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    로그인주체식별자 = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    접근역할코드 = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    개정번호 = table.Column<long>(type: "bigint", nullable: false),
                    생성시각UTC = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    수정시각UTC = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_시뮬레이션세션_접근원장", x => x.세션고유식별자);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_시뮬레이션세션_접근원장_로그인주체식별자_세션고유식별자",
                table: "시뮬레이션세션_접근원장",
                columns: new[] { "로그인주체식별자", "세션고유식별자" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "시뮬레이션세션_접근원장");
        }
    }
}
