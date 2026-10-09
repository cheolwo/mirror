using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

/// <summary>당사자 합의와 멱등 요청·업무 선택 증거만 추가합니다. 기존 요금과 지급 상태는 갱신하지 않습니다.</summary>
[DbContext(typeof(SsalddelContext))]
[Migration("20261008120000_AddAgreedRateTables")]
public sealed class AddAgreedRateTables : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("agreed_rate_tables", columns: t => new
        {
            AgreementId = t.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
            BusinessKind = t.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
            VehicleCode = t.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
            ProposerKind = t.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            ProposerId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false),
            CounterpartyKind = t.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            CounterpartyId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false),
            TermsJson = t.Column<string>(type: "longtext", nullable: false),
            TermsDigest = t.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
            StatusCode = t.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            EffectiveFromUtc = t.Column<DateTime>(type: "datetime(6)", nullable: false),
            EffectiveUntilUtc = t.Column<DateTime>(type: "datetime(6)", nullable: false),
            AcceptedAtUtc = t.Column<DateTime>(type: "datetime(6)", nullable: true),
            Revision = t.Column<long>(type: "bigint", nullable: false),
            HistoryJson = t.Column<string>(type: "longtext", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_agreed_rate_tables", x => x.AgreementId));
        m.CreateIndex("IX_agreed_rate_tables_ProposerKind_ProposerId", "agreed_rate_tables", new[] { "ProposerKind", "ProposerId" });
        m.CreateIndex("IX_agreed_rate_tables_CounterpartyKind_CounterpartyId", "agreed_rate_tables", new[] { "CounterpartyKind", "CounterpartyId" });
        m.CreateTable("agreed_rate_table_receipts", columns: t => new
        {
            PartyKind = t.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            PartyId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false),
            RequestId = t.Column<Guid>(type: "char(36)", nullable: false),
            InputDigest = t.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
            ResultJson = t.Column<string>(type: "longtext", nullable: false),
            RecordedAtUtc = t.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_agreed_rate_table_receipts", x => new { x.PartyKind, x.PartyId, x.RequestId }));
        m.CreateTable("agreed_rate_table_selections", columns: t => new
        {
            SelectionRequestId = t.Column<Guid>(type: "char(36)", nullable: false),
            BusinessKind = t.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
            WorkId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false),
            AgreementId = t.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
            AgreementRevision = t.Column<long>(type: "bigint", nullable: false),
            SnapshotJson = t.Column<string>(type: "longtext", nullable: false),
            SelectingPartyKind = t.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
            SelectingPartyId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false),
            SelectedAtUtc = t.Column<DateTime>(type: "datetime(6)", nullable: false),
            AgreedAmountKrw = t.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
            DistanceKm = t.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
            DistanceBasisCode = t.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
            SynchronizationStatusCode = t.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
        }, constraints: t => t.PrimaryKey("PK_agreed_rate_table_selections", x => new { x.BusinessKind, x.WorkId }));
        m.AddColumn<string>("agreed_rate_table_snapshot_json", "운임구성", type: "longtext", nullable: true);
    }

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("합의와 수행 판본 근거를 삭제할 수 없어 자동 되돌림은 지원하지 않습니다. 데이터 보존 검토가 필요합니다.");
}
