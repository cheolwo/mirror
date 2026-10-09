using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

/// <summary>기존 정액 설정/동의는 null 정책으로 보존하며 최초 확인된 정책 전체만 추가 저장합니다.</summary>
[DbContext(typeof(SsalddelContext))]
[Migration("20261007180000_AddDeliveryCostSharingPolicies")]
public sealed class AddDeliveryCostSharingPolicies : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("CostSharingPolicyJson", "restaurant_fulfillment_supports", type: "longtext", nullable: true);
        m.AddColumn<string>("FrozenCostSharingPolicyJson", "fulfillment_routing_consents", type: "longtext", nullable: true);
    }

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("배송비 정책과 주문 동의의 의미가 유실될 수 있어 자동 되돌림을 지원하지 않습니다. 별도 검토한 데이터 보존 migration이 필요합니다.");
}
