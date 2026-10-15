using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261012143000_AddDistributedInboundCollaboration")]
public sealed class AddDistributedInboundCollaboration : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name: "AcceptancePolicyJson", table: "distributed_sites", type: "longtext", nullable: true);
        m.AddColumn<long>(name: "AcceptancePolicyRevision", table: "distributed_sites", type: "bigint", nullable: false, defaultValue: 0L);
        m.AddColumn<string>(name: "InboundCollaborationJson", table: "distributed_lots", type: "longtext", nullable: true);
        m.AddColumn<string>(name: "PendingTermsJson", table: "distributed_storage_agreements", type: "longtext", nullable: true);
        m.AddColumn<int>(name: "PendingTermsVersion", table: "distributed_storage_agreements", type: "int", nullable: true);
        m.AddColumn<bool>(name: "PendingSpaceOwnerConsented", table: "distributed_storage_agreements", type: "tinyint(1)", nullable: false, defaultValue: false);
        m.AddColumn<bool>(name: "PendingGoodsOwnerConsented", table: "distributed_storage_agreements", type: "tinyint(1)", nullable: false, defaultValue: false);
    }

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("입고 협의·동의 판본은 보존·대사 절차 없이 삭제할 수 없습니다.");
}
