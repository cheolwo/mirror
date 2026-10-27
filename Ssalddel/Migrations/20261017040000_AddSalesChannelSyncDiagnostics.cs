using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

public sealed partial class AddSalesChannelSyncDiagnostics : Migration
{
    protected override void Up(MigrationBuilder m)
        => m.AddColumn<string>(name: "동기화진단_json", table: "판매채널계정", type: "longtext", nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("판매채널 보류 관측을 보존해야 합니다. 별도 검토 없는 진단 기록 삭제는 지원하지 않습니다.");
}
