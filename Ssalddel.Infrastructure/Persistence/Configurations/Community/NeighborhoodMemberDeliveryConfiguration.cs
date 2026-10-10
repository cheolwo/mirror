using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.Community;

namespace Ssalddel.Infrastructure.Persistence.Configurations.Community;

public sealed class NeighborhoodMemberParticipationConfiguration : IEntityTypeConfiguration<NeighborhoodMemberParticipation>
{
    public void Configure(EntityTypeBuilder<NeighborhoodMemberParticipation> b)
    {
        b.ToTable("neighborhood_member_participation"); b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).HasMaxLength(191); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.PrivatePayload).HasColumnType("longtext"); b.Property(x => x.ActiveRequestId).HasMaxLength(120);
        b.HasIndex(x => x.ReceiveOffers);
    }
}
public sealed class NeighborhoodMemberDeliveryConfiguration : IEntityTypeConfiguration<NeighborhoodMemberDelivery>
{
    public void Configure(EntityTypeBuilder<NeighborhoodMemberDelivery> b)
    {
        b.ToTable("neighborhood_member_delivery"); b.HasKey(x => x.RequestId);
        b.Property(x => x.RequestId).HasMaxLength(120); b.Property(x => x.OwnerUserId).HasMaxLength(191);
        b.Property(x => x.AssignedMemberUserId).HasMaxLength(191); b.Property(x => x.StateCode).HasMaxLength(30);
        b.Property(x => x.NoticeVersion).HasMaxLength(80); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.FareKrw).HasPrecision(18,2); b.Property(x => x.DistanceKm).HasPrecision(18,3);
        b.Property(x => x.PickupEvidenceId).HasMaxLength(64); b.Property(x => x.DeliveryEvidenceId).HasMaxLength(64);
        b.Property(x => x.IssueCode).HasMaxLength(40); b.Property(x => x.PrivateIssuePayload).HasColumnType("longtext");
        b.Property(x => x.SupportCaseId).HasMaxLength(120);
        b.HasIndex(x => new { x.AssignedMemberUserId, x.StateCode });
        b.HasIndex(x => new { x.UpdatedAtUtc, x.RequestId });
    }
}
public sealed class NeighborhoodMemberOfferConfiguration : IEntityTypeConfiguration<NeighborhoodMemberOffer>
{
    public void Configure(EntityTypeBuilder<NeighborhoodMemberOffer> b)
    {
        b.ToTable("neighborhood_member_offer"); b.HasKey(x => x.OfferId);
        b.Property(x => x.OfferId).HasMaxLength(64); b.Property(x => x.RequestId).HasMaxLength(120);
        b.Property(x => x.MemberUserId).HasMaxLength(191); b.Property(x => x.StateCode).HasMaxLength(30);
        b.Property(x => x.PushStateCode).HasMaxLength(30); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.PushClaimRevision).IsConcurrencyToken();
        b.HasIndex(x => new { x.RequestId, x.MemberUserId }).IsUnique(); b.HasIndex(x => new { x.MemberUserId, x.CreatedAtUtc });
        b.HasIndex(x => new { x.PushStateCode, x.ExpiresAtUtc });
    }
}
public sealed class NeighborhoodMemberReceiptConfiguration : IEntityTypeConfiguration<NeighborhoodMemberReceipt>
{
    public void Configure(EntityTypeBuilder<NeighborhoodMemberReceipt> b)
    {
        b.ToTable("neighborhood_member_receipt"); b.HasKey(x => new { x.ActorUserId, x.ClientRequestId });
        b.Property(x => x.ActorUserId).HasMaxLength(191); b.Property(x => x.Kind).HasMaxLength(40);
        b.Property(x => x.AggregateId).HasMaxLength(120); b.Property(x => x.InputDigest).HasMaxLength(64);
    }
}
public sealed class NeighborhoodMemberPhotoConfiguration : IEntityTypeConfiguration<NeighborhoodMemberPhoto>
{
    public void Configure(EntityTypeBuilder<NeighborhoodMemberPhoto> b)
    {
        b.ToTable("neighborhood_member_photo"); b.HasKey(x => x.EvidenceId);
        b.Property(x => x.EvidenceId).HasMaxLength(64); b.Property(x => x.RequestId).HasMaxLength(120);
        b.Property(x => x.ActorUserId).HasMaxLength(191); b.Property(x => x.StageCode).HasMaxLength(30);
        b.Property(x => x.StateCode).HasMaxLength(30); b.Property(x => x.InputDigest).HasMaxLength(64);
        b.Property(x => x.StoredSha256).HasMaxLength(64); b.Property(x => x.PrivatePayload).HasColumnType("longtext");
        b.HasIndex(x => new { x.ActorUserId, x.UploadRequestId }).IsUnique();
    }
}
