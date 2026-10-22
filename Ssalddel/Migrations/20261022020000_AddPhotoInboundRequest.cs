using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261022020000_AddPhotoInboundRequest")]
public sealed partial class AddPhotoInboundRequest : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "가입고사진신청_id", table: "입고요청", type: "char(36)", nullable: true);

        migrationBuilder.CreateTable(
            name: "가입고사진신청",
            columns: table => new
            {
                id = table.Column<Guid>(type: "char(36)", nullable: false),
                작성자_user_id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                이름 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                revision = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                사진_container = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                사진_object_name = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                사진_sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                최초_요청_해시 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                content_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                bytes = table.Column<long>(type: "bigint", nullable: false),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_가입고사진신청", x => x.id))
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_가입고사진신청_작성자_user_id_created_at_utc", table: "가입고사진신청",
            columns: new[] { "작성자_user_id", "created_at_utc" });
        migrationBuilder.CreateIndex(
            name: "IX_입고요청_가입고사진신청_id", table: "입고요청", column: "가입고사진신청_id", unique: true);
        migrationBuilder.AddForeignKey(
            name: "FK_입고요청_가입고사진신청_가입고사진신청_id",
            table: "입고요청", column: "가입고사진신청_id",
            principalTable: "가입고사진신청", principalColumn: "id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_입고요청_가입고사진신청_가입고사진신청_id", table: "입고요청");
        migrationBuilder.DropIndex(name: "IX_입고요청_가입고사진신청_id", table: "입고요청");
        migrationBuilder.DropColumn(name: "가입고사진신청_id", table: "입고요청");
        migrationBuilder.DropTable(name: "가입고사진신청");
    }
}
