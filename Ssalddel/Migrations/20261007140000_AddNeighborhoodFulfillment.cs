using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261007140000_AddNeighborhoodFulfillment")]
public sealed class AddNeighborhoodFulfillment : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<bool>("neighborhood_simulation_fulfillment", "shipper_requests", "tinyint(1)", nullable: false, defaultValue: false);
        m.AddColumn<DateTime>("neighborhood_simulation_deadline_utc", "shipper_requests", "datetime(6)", nullable: true);
        m.CreateTable("neighborhood_fulfillment_quotes", c => new
        {
            QuoteId = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            ActorId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            InputDigest = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            RequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: true),
            ProviderId = c.Column<string>("varchar(60)", maxLength: 60, nullable: false),
            CostKrw = c.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false),
            WeightKg = c.Column<decimal>("decimal(18,3)", precision: 18, scale: 3, nullable: false),
            DistanceKm = c.Column<decimal>("decimal(18,3)", precision: 18, scale: 3, nullable: false),
            DesiredCompletionUtc = c.Column<DateTime>("datetime(6)", nullable: true),
            EstimatedCompletionUtc = c.Column<DateTime>("datetime(6)", nullable: false),
            ExpiresAtUtc = c.Column<DateTime>("datetime(6)", nullable: false),
            IsSimulation = c.Column<bool>("tinyint(1)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_neighborhood_fulfillment_quotes", x => x.QuoteId));
        m.CreateIndex("IX_neighborhood_fulfillment_quotes_ActorId_ExpiresAtUtc", "neighborhood_fulfillment_quotes", new[] { "ActorId", "ExpiresAtUtc" });
        m.CreateTable("neighborhood_fulfillment", c => new
        {
            RequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            ActorId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            AttemptId = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            Revision = c.Column<long>("bigint", nullable: false),
            UpdatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_neighborhood_fulfillment", x => x.RequestId));
        m.CreateTable("neighborhood_fulfillment_attempts", c => new
        {
            AttemptId = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            RequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: false),
            ClientRequestId = c.Column<Guid>("char(36)", nullable: false),
            SelectionDigest = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            QuoteId = c.Column<string>("varchar(64)", maxLength: 64, nullable: false),
            ProviderId = c.Column<string>("varchar(60)", maxLength: 60, nullable: false),
            StateCode = c.Column<string>("varchar(40)", maxLength: 40, nullable: false),
            ExternalRequestId = c.Column<string>("varchar(191)", maxLength: 191, nullable: true),
            CostKrw = c.Column<decimal>("decimal(18,2)", precision: 18, scale: 2, nullable: false),
            IsSimulation = c.Column<bool>("tinyint(1)", nullable: false),
            SharingAgreed = c.Column<bool>("tinyint(1)", nullable: false),
            CreatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false),
            UpdatedAtUtc = c.Column<DateTime>("datetime(6)", nullable: false),
            DesiredCompletionUtc = c.Column<DateTime>("datetime(6)", nullable: true),
            EstimatedCompletionUtc = c.Column<DateTime>("datetime(6)", nullable: false),
            Revision = c.Column<long>("bigint", nullable: false),
            CancelClientRequestId = c.Column<Guid>("char(36)", nullable: true),
            HistoryJson = c.Column<string>("longtext", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_neighborhood_fulfillment_attempts", x => x.AttemptId));
        m.CreateIndex("IX_neighborhood_fulfillment_attempts_RequestId_ClientRequestId", "neighborhood_fulfillment_attempts", new[] { "RequestId", "ClientRequestId" }, unique: true);
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("neighborhood_fulfillment_attempts"); m.DropTable("neighborhood_fulfillment"); m.DropTable("neighborhood_fulfillment_quotes");
        m.DropColumn("neighborhood_simulation_fulfillment", "shipper_requests");
        m.DropColumn("neighborhood_simulation_deadline_utc", "shipper_requests");
    }
}
