using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

public sealed partial class AddAutomaticStatementReview : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<DateTime>(name: "automatic_statement_reviewed_at_utc", table: "기사운송대금지급요청", type: "datetime(6)", nullable: true);
        m.AddColumn<string>(name: "automatic_statement_review_code", table: "기사운송대금지급요청", type: "varchar(80)", maxLength: 80, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }
    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("자동 명세 후보의 검토 근거를 삭제하지 않습니다. 별도 보존·복구 검토가 필요합니다.");
}
