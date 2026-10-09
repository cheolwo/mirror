using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

/// <summary>r24의 기존 테이블·실행 ID를 보존하고 원천 종류와 자동 수행 근거만 확장합니다.</summary>
[DbContext(typeof(SsalddelContext))]
[Migration("20261007160000_AddAutomaticFulfillmentRouting")]
public sealed class AddAutomaticFulfillmentRouting : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        foreach (var table in new[] { "neighborhood_fulfillment_quotes", "neighborhood_fulfillment", "neighborhood_fulfillment_attempts" })
            m.AddColumn<string>("SourceKind", table, "varchar(40)", maxLength: 40, nullable: false, defaultValue: "NeighborhoodDelivery");
        // 기존 생활 기록은 음식 주문과 구분한다. RequestId/AttemptId/QuoteId는 변경하지 않는다.
        foreach (var table in new[] { "neighborhood_fulfillment_quotes", "neighborhood_fulfillment_attempts" })
        {
            m.AddColumn<long>("ConsentRevision", table, "bigint", nullable: true);
            m.AddColumn<long>("SourceRevision", table, "bigint", nullable: false, defaultValue: 0L);
            m.AddColumn<string>("Leg", table, "varchar(100)", maxLength: 100, nullable: false, defaultValue: "initial");
        }
        m.AlterColumn<decimal>("WeightKg", "neighborhood_fulfillment_quotes", "decimal(18,3)", precision: 18, scale: 3,
            nullable: true, oldClrType: typeof(decimal), oldType: "decimal(18,3)", oldPrecision: 18, oldScale: 3);
        m.AddColumn<bool>("IsAutomatic", "neighborhood_fulfillment", "tinyint(1)", nullable: false, defaultValue: false);
        m.AddColumn<string>("StateCode", "neighborhood_fulfillment", "varchar(40)", maxLength: 40, nullable: false, defaultValue: "AwaitingRouting");
        m.AddColumn<string>("ReviewCode", "neighborhood_fulfillment", "varchar(80)", maxLength: 80, nullable: true);
        m.AddColumn<string>("SelectionReason", "neighborhood_fulfillment", "varchar(500)", maxLength: 500, nullable: false, defaultValue: "");
        m.AddColumn<decimal>("ConfirmedIncurredCostKrw", "neighborhood_fulfillment_attempts", "decimal(18,2)", precision: 18, scale: 2, nullable: true);
        m.AddColumn<string>("ResultOperationId", "neighborhood_fulfillment_attempts", "varchar(64)", maxLength: 64, nullable: true);
        m.DropPrimaryKey("PK_neighborhood_fulfillment", "neighborhood_fulfillment");
        m.AddPrimaryKey("PK_neighborhood_fulfillment", "neighborhood_fulfillment", new[] { "SourceKind", "RequestId" });
        m.DropIndex("IX_neighborhood_fulfillment_attempts_RequestId_ClientRequestId", "neighborhood_fulfillment_attempts");
        m.CreateIndex("IX_fulfillment_attempt_source_client", "neighborhood_fulfillment_attempts",
            new[] { "SourceKind", "RequestId", "ClientRequestId" }, unique: true);
        m.CreateIndex("IX_fulfillment_attempt_source_consent_provider", "neighborhood_fulfillment_attempts",
            new[] { "SourceKind", "RequestId", "ConsentRevision", "ProviderId" }, unique: true);
        m.AddColumn<bool>("자동처리처수행", "shipper_requests", "tinyint(1)", nullable: false, defaultValue: false);
        m.AddColumn<bool>("simulation_fulfillment_routing", "음식주문", "tinyint(1)", nullable: false, defaultValue: false);

        m.CreateTable("fulfillment_routing_consents", c => new
        {
            SourceKind = c.Column<string>("varchar(40)", maxLength: 40, nullable: false),
            RequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            OwnerId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            ClientRequestId = c.Column<Guid>("char(36)", nullable: false),
            InputDigest = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            Revision = c.Column<long>("bigint", nullable: false),
            MaxUserChargeKrw = c.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false),
            DesiredCompletionUtc = c.Column<DateTime>("datetime(6)", nullable: true),
            AllowedProvidersJson = c.Column<string>("varchar(1000)", maxLength: 1000, nullable: false),
            InformationFieldsJson = c.Column<string>("varchar(1000)", maxLength: 1000, nullable: false),
            NoticeVersion = c.Column<string>("varchar(100)", maxLength: 100, nullable: false),
            ReceiptsJson = c.Column<string>("longtext", nullable: false),
            SharingAgreed = c.Column<bool>("tinyint(1)", nullable: false),
            AutomaticSubmissionAgreed = c.Column<bool>("tinyint(1)", nullable: false),
            DirectPaymentAgreed = c.Column<bool>("tinyint(1)", nullable: false),
            Revoked = c.Column<bool>("tinyint(1)", nullable: false),
            FrozenRestaurantSupportKrw = c.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false),
            RestaurantSupportRevision = c.Column<long>("bigint", nullable: true),
            UpdatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_fulfillment_routing_consents", x => new { x.SourceKind, x.RequestId }));
        m.CreateTable("restaurant_fulfillment_supports", c => new
        {
            RestaurantId = c.Column<long>("bigint", nullable: false),
            Revision = c.Column<long>("bigint", nullable: false),
            FixedContributionKrw = c.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false),
            Enabled = c.Column<bool>("tinyint(1)", nullable: false),
            ClientRequestId = c.Column<Guid>("char(36)", nullable: false),
            ActorId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            UpdatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_restaurant_fulfillment_supports", x => x.RestaurantId));
        m.CreateTable("fulfillment_routing_outbox", c => new
        {
            Id = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            SourceKind = c.Column<string>("varchar(40)", maxLength: 40, nullable: false),
            RequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            OperationId = c.Column<Guid>("char(36)", nullable: false),
            StateCode = c.Column<string>("varchar(40)", maxLength: 40, nullable: false),
            ClaimRevision = c.Column<int>("int", nullable: false),
            LastErrorCode = c.Column<string>("varchar(80)", maxLength: 80, nullable: true),
            UpdatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_fulfillment_routing_outbox", x => x.Id));
        m.CreateIndex("IX_fulfillment_routing_outbox_SourceKind_RequestId_OperationId", "fulfillment_routing_outbox", new[] { "SourceKind", "RequestId", "OperationId" }, unique: true);
        m.CreateIndex("IX_fulfillment_routing_outbox_StateCode_UpdatedAtUtc", "fulfillment_routing_outbox", new[] { "StateCode", "UpdatedAtUtc" });
        m.CreateTable("fulfillment_mock_jobs", c => new
        {
            AttemptId = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            SourceKind = c.Column<string>("varchar(40)", maxLength: 40, nullable: false),
            RequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            ProviderId = c.Column<string>("varchar(60)", maxLength: 60, nullable: false),
            ExternalRequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            StateCode = c.Column<string>("varchar(40)", maxLength: 40, nullable: false),
            Leg = c.Column<string>("varchar(100)", maxLength: 100, nullable: false),
            Revision = c.Column<long>("bigint", nullable: false),
            AssignmentRevision = c.Column<long>("bigint", nullable: false),
            ExternalDriverReference = c.Column<string>("varchar(100)", maxLength: 100, nullable: true),
            LastClientRequestId = c.Column<Guid>("char(36)", nullable: true),
            LastRequestDigest = c.Column<string>("varchar(64)", maxLength: 64, nullable: true),
            ConfirmedIncurredCostKrw = c.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: true),
            CreatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false),
            UpdatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_fulfillment_mock_jobs", x => x.AttemptId));
    }

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("r25의 음식 원천·동의·수행 기록은 r24 구조로 손실 없이 자동 축소할 수 없습니다. 데이터 검토와 보존 계획을 포함한 별도 downgrade migration이 필요합니다.");
}
