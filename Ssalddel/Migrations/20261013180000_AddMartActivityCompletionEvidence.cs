using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261013180000_AddMartActivityCompletionEvidence")]
public sealed class AddMartActivityCompletionEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<string>(name: "completion_evidence_json", table: "피킹포장작업", type: "longtext", nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "completion_evidence_json", table: "피킹포장작업");
}
