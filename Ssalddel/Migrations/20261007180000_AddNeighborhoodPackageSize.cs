using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

/// <summary>기존 생활 의뢰를 null로 보존하며 선택한 포장 크기만 별도로 저장합니다.</summary>
[DbContext(typeof(SsalddelContext))]
[Migration("20261007180000_AddNeighborhoodPackageSize")]
public sealed class AddNeighborhoodPackageSize : Migration
{
    protected override void Up(MigrationBuilder m)
        => m.AddColumn<string>("neighborhood_package_size_code", "shipper_requests", type: "varchar(16)", maxLength: 16, nullable: true);

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("의뢰 입력 근거를 잃지 않도록 별도 검토한 데이터 보존 migration이 필요합니다.");
}
