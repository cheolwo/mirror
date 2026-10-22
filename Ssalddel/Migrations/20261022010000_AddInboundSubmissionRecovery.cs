using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261022010000_AddInboundSubmissionRecovery")]
public sealed partial class AddInboundSubmissionRecovery : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "일반입고_클라이언트_요청_id",
            table: "입고요청",
            type: "char(36)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "일반입고_최초_요청_해시",
            table: "입고요청",
            type: "varchar(64)",
            maxLength: 64,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_입고요청_주문자_user_id_일반입고_클라이언트_요청_id",
            table: "입고요청",
            columns: new[] { "주문자_user_id", "일반입고_클라이언트_요청_id" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_입고요청_주문자_user_id_일반입고_클라이언트_요청_id",
            table: "입고요청");
        migrationBuilder.DropColumn(name: "일반입고_클라이언트_요청_id", table: "입고요청");
        migrationBuilder.DropColumn(name: "일반입고_최초_요청_해시", table: "입고요청");
    }
}
