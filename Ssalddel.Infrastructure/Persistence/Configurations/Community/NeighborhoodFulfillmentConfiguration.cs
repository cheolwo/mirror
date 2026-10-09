using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.Community;

namespace Ssalddel.Infrastructure.Persistence.Configurations.Community;

public sealed class NeighborhoodFulfillmentQuoteConfiguration : IEntityTypeConfiguration<NeighborhoodFulfillmentQuoteRecord>
{
    public void Configure(EntityTypeBuilder<NeighborhoodFulfillmentQuoteRecord> b)
    {
        b.ToTable("neighborhood_fulfillment_quotes"); b.HasKey(x => x.QuoteId);
        b.Property(x => x.SourceKind).HasMaxLength(40).HasDefaultValue("NeighborhoodDelivery");
        b.Property(x => x.Leg).HasMaxLength(100);
        b.Property(x => x.QuoteId).HasMaxLength(64); b.Property(x => x.ActorId).HasMaxLength(191);
        b.Property(x => x.InputDigest).HasMaxLength(64); b.Property(x => x.RequestId).HasMaxLength(191);
        b.Property(x => x.ProviderId).HasMaxLength(60); b.Property(x => x.CostKrw).HasPrecision(18, 2);
        b.Property(x => x.WeightKg).HasPrecision(18, 3); b.Property(x => x.DistanceKm).HasPrecision(18, 3);
        b.HasIndex(x => new { x.ActorId, x.ExpiresAtUtc });
    }
}
public sealed class NeighborhoodFulfillmentConfiguration : IEntityTypeConfiguration<NeighborhoodFulfillmentRecord>
{
    public void Configure(EntityTypeBuilder<NeighborhoodFulfillmentRecord> b)
    {
        b.ToTable("neighborhood_fulfillment"); b.HasKey(x => new { x.SourceKind, x.RequestId });
        b.Property(x => x.SourceKind).HasMaxLength(40).HasDefaultValue("NeighborhoodDelivery");
        b.Property(x => x.StateCode).HasMaxLength(40); b.Property(x => x.ReviewCode).HasMaxLength(80);
        b.Property(x => x.SelectionReason).HasMaxLength(500);
        b.Property(x => x.RequestId).HasMaxLength(191); b.Property(x => x.ActorId).HasMaxLength(191);
        b.Property(x => x.AttemptId).HasMaxLength(64); b.Property(x => x.Revision).IsConcurrencyToken();
    }
}
public sealed class NeighborhoodFulfillmentAttemptConfiguration : IEntityTypeConfiguration<NeighborhoodFulfillmentAttemptRecord>
{
    public void Configure(EntityTypeBuilder<NeighborhoodFulfillmentAttemptRecord> b)
    {
        b.ToTable("neighborhood_fulfillment_attempts"); b.HasKey(x => x.AttemptId);
        b.Property(x => x.SourceKind).HasMaxLength(40).HasDefaultValue("NeighborhoodDelivery");
        b.Property(x => x.Leg).HasMaxLength(100); b.Property(x => x.ConfirmedIncurredCostKrw).HasPrecision(18, 2);
        b.Property(x => x.ResultOperationId).HasMaxLength(64);
        b.Property(x => x.AttemptId).HasMaxLength(64); b.Property(x => x.RequestId).HasMaxLength(191);
        b.Property(x => x.SelectionDigest).HasMaxLength(64); b.Property(x => x.QuoteId).HasMaxLength(64);
        b.Property(x => x.ProviderId).HasMaxLength(60); b.Property(x => x.StateCode).HasMaxLength(40);
        b.Property(x => x.ExternalRequestId).HasMaxLength(191); b.Property(x => x.CostKrw).HasPrecision(18, 2);
        b.Property(x => x.HistoryJson).HasColumnType("longtext"); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => new { x.SourceKind, x.RequestId, x.ClientRequestId }).IsUnique().HasDatabaseName("IX_fulfillment_attempt_source_client");
        b.HasIndex(x => new { x.SourceKind, x.RequestId, x.ConsentRevision, x.ProviderId }).IsUnique().HasDatabaseName("IX_fulfillment_attempt_source_consent_provider");
    }
}

public sealed class AutoFulfillmentConsentConfiguration : IEntityTypeConfiguration<AutoFulfillmentConsentRecord>
{
    public void Configure(EntityTypeBuilder<AutoFulfillmentConsentRecord> b)
    {
        b.ToTable("fulfillment_routing_consents"); b.HasKey(x => new { x.SourceKind, x.RequestId });
        b.Property(x => x.SourceKind).HasMaxLength(40); b.Property(x => x.RequestId).HasMaxLength(191);
        b.Property(x => x.OwnerId).HasMaxLength(191); b.Property(x => x.InputDigest).HasMaxLength(64);
        b.Property(x => x.NoticeVersion).HasMaxLength(100); b.Property(x => x.AllowedProvidersJson).HasMaxLength(1000);
        b.Property(x => x.InformationFieldsJson).HasMaxLength(1000); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.ReceiptsJson).HasColumnType("longtext");
        b.Property(x => x.FrozenCostSharingPolicyJson).HasColumnType("longtext");
        b.Property(x => x.MaxUserChargeKrw).HasPrecision(18, 2); b.Property(x => x.FrozenRestaurantSupportKrw).HasPrecision(18, 2);
    }
}
public sealed class RestaurantFulfillmentSupportConfiguration : IEntityTypeConfiguration<RestaurantFulfillmentSupportRecord>
{
    public void Configure(EntityTypeBuilder<RestaurantFulfillmentSupportRecord> b)
    {
        b.ToTable("restaurant_fulfillment_supports"); b.HasKey(x => x.RestaurantId);
        b.Property(x => x.RestaurantId).ValueGeneratedNever();
        b.Property(x => x.ActorId).HasMaxLength(191); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.FixedContributionKrw).HasPrecision(18, 2);
        b.Property(x => x.CostSharingPolicyJson).HasColumnType("longtext");
    }
}
public sealed class AutoFulfillmentOutboxConfiguration : IEntityTypeConfiguration<AutoFulfillmentOutboxRecord>
{
    public void Configure(EntityTypeBuilder<AutoFulfillmentOutboxRecord> b)
    {
        b.ToTable("fulfillment_routing_outbox"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64); b.Property(x => x.SourceKind).HasMaxLength(40);
        b.Property(x => x.RequestId).HasMaxLength(191); b.Property(x => x.StateCode).HasMaxLength(40);
        b.Property(x => x.LastErrorCode).HasMaxLength(80); b.Property(x => x.ClaimRevision).IsConcurrencyToken();
        b.HasIndex(x => new { x.SourceKind, x.RequestId, x.OperationId }).IsUnique(); b.HasIndex(x => new { x.StateCode, x.UpdatedAtUtc });
    }
}
public sealed class FulfillmentMockJobConfiguration : IEntityTypeConfiguration<FulfillmentMockJobRecord>
{
    public void Configure(EntityTypeBuilder<FulfillmentMockJobRecord> b)
    {
        b.ToTable("fulfillment_mock_jobs"); b.HasKey(x => x.AttemptId);
        b.Property(x => x.AttemptId).HasMaxLength(64); b.Property(x => x.SourceKind).HasMaxLength(40);
        b.Property(x => x.RequestId).HasMaxLength(191); b.Property(x => x.ProviderId).HasMaxLength(60);
        b.Property(x => x.ExternalRequestId).HasMaxLength(191); b.Property(x => x.StateCode).HasMaxLength(40);
        b.Property(x => x.Leg).HasMaxLength(100); b.Property(x => x.ExternalDriverReference).HasMaxLength(100);
        b.Property(x => x.LastRequestDigest).HasMaxLength(64); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.ConfirmedIncurredCostKrw).HasPrecision(18, 2);
    }
}
