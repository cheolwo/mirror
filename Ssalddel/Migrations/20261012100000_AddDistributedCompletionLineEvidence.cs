using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261012100000_AddDistributedCompletionLineEvidence")]
public sealed class AddDistributedCompletionLineEvidence : Migration
{
    protected override void Up(MigrationBuilder m)
        => m.AddColumn<string>(name: "CompletionLinesJson", table: "distributed_receipts", type: "longtext", nullable: true);

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("행별 완료 근거는 기존 업무 영수증과 함께 보존해야 합니다.");
}
