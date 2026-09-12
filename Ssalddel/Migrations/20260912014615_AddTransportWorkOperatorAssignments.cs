using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportWorkOperatorAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "운송업무담당자배정",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    운송의뢰Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    배정세트Revision = table.Column<long>(type: "bigint", nullable: false),
                    클라이언트요청Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    화주Id = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    담당자UserId = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    담당유형Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    주담당Slot = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    권한CodesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    지정자UserId = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    지정시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_운송업무담당자배정", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_운송업무담당자배정_담당자_의뢰_판본",
                table: "운송업무담당자배정",
                columns: new[] { "담당자UserId", "운송의뢰Id", "배정세트Revision" });

            migrationBuilder.CreateIndex(
                name: "UX_운송업무담당자배정_의뢰_요청_담당자",
                table: "운송업무담당자배정",
                columns: new[] { "운송의뢰Id", "클라이언트요청Id", "담당자UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_운송업무담당자배정_의뢰_판본_담당자",
                table: "운송업무담당자배정",
                columns: new[] { "운송의뢰Id", "배정세트Revision", "담당자UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_운송업무담당자배정_의뢰_판본_주담당",
                table: "운송업무담당자배정",
                columns: new[] { "운송의뢰Id", "배정세트Revision", "주담당Slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "운송업무담당자배정");
        }
    }
}
