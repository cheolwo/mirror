using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
using 살뜰.도메인.정산;

namespace 살뜰.Infrastructure.Persistence.Configurations.Settlement;

public static class NeighborhoodSettlementModel
{
    public static readonly string[] Tables = ["neighborhood_settlement_agreements", "neighborhood_settlement_versions", "neighborhood_settlement_entries", "neighborhood_settlement_payments", "neighborhood_settlement_receipts"];
    public static void Configure(ModelBuilder m)
    {
        Agreement(m.Entity<NeighborhoodSettlementAgreementRecord>());
        Version(m.Entity<NeighborhoodSettlementAgreementVersionRecord>());
        Entry(m.Entity<NeighborhoodSettlementEntryRecord>());
        Payment(m.Entity<NeighborhoodSettlementPaymentRecord>());
        Receipt(m.Entity<NeighborhoodSettlementCommandReceiptRecord>());
    }
    public static void Agreement(EntityTypeBuilder<NeighborhoodSettlementAgreementRecord> b)
    {
        b.ToTable(Tables[0]); b.HasKey(x => x.AgreementId); b.Property(x => x.AgreementId).HasMaxLength(64);
        b.Property(x => x.SourceKind).HasMaxLength(30); b.Property(x => x.SourceId).HasMaxLength(120);
        b.Property(x => x.CreatorUserId).HasMaxLength(191); b.Property(x => x.ActivityKind).HasMaxLength(30); b.Property(x => x.StatusCode).HasMaxLength(30);
        b.Property(x => x.TermsJson).HasColumnType("longtext"); b.Property(x => x.SourceSnapshotJson).HasColumnType("longtext");
        b.Property(x => x.ConsentsJson).HasColumnType("longtext"); b.Property(x => x.HistoryJson).HasColumnType("longtext");
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => new { x.SourceKind, x.SourceId }).IsUnique().HasDatabaseName("IX_neighborhood_agreement_source");
    }
    public static void Version(EntityTypeBuilder<NeighborhoodSettlementAgreementVersionRecord> b)
    {
        b.ToTable(Tables[1]); b.HasKey(x => new { x.AgreementId, x.TermsVersion }); b.Property(x => x.AgreementId).HasMaxLength(64);
        b.Property(x => x.TermsJson).HasColumnType("longtext"); b.Property(x => x.SourceSnapshotJson).HasColumnType("longtext"); b.Property(x => x.ConsentsJson).HasColumnType("longtext");
        b.Property(x => x.TermsJson).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.Property(x => x.SourceSnapshotJson).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.HasOne<NeighborhoodSettlementAgreementRecord>().WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_neighborhood_version_agreement");
    }
    public static void Entry(EntityTypeBuilder<NeighborhoodSettlementEntryRecord> b)
    {
        b.ToTable(Tables[2]); b.HasKey(x => x.EntryId); b.Property(x => x.EntryId).HasMaxLength(64); b.Property(x => x.AgreementId).HasMaxLength(64);
        b.Property(x => x.LineKey).HasMaxLength(64); b.Property(x => x.Kind).HasMaxLength(30); b.Property(x => x.OriginKind).HasMaxLength(40);
        b.Property(x => x.OriginId).HasMaxLength(120); b.Property(x => x.AccrualKey).HasMaxLength(64); b.Property(x => x.CanonicalDeliveryOrigin).HasMaxLength(191);
        b.Property(x => x.OriginalEntryId).HasMaxLength(64); b.Property(x => x.DetailsJson).HasColumnType("longtext"); b.Property(x => x.StatusCode).HasMaxLength(30); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.AccrualKey).IsUnique().HasDatabaseName("IX_neighborhood_entry_accrual");
        b.HasIndex(x => x.CanonicalDeliveryOrigin).IsUnique().HasDatabaseName("IX_neighborhood_entry_delivery_origin");
        b.HasIndex(x => new { x.AgreementId, x.TermsVersion }).HasDatabaseName("IX_neighborhood_entry_version");
        b.HasIndex(x => x.OriginalEntryId).HasDatabaseName("IX_neighborhood_entry_original");
        b.HasOne<NeighborhoodSettlementAgreementVersionRecord>().WithMany().HasForeignKey(x => new { x.AgreementId, x.TermsVersion }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_neighborhood_entry_version");
        b.HasOne<NeighborhoodSettlementEntryRecord>().WithMany().HasForeignKey(x => x.OriginalEntryId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_neighborhood_entry_original");
    }
    public static void Payment(EntityTypeBuilder<NeighborhoodSettlementPaymentRecord> b)
    {
        b.ToTable(Tables[3]); b.HasKey(x => x.PaymentId); b.Property(x => x.PaymentId).HasMaxLength(64); b.Property(x => x.AgreementId).HasMaxLength(64);
        b.Property(x => x.DetailsJson).HasColumnType("longtext"); b.Property(x => x.StatusCode).HasMaxLength(30); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.AgreementId).HasDatabaseName("IX_neighborhood_payment_agreement");
        b.HasOne<NeighborhoodSettlementAgreementRecord>().WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_neighborhood_payment_agreement");
    }
    public static void Receipt(EntityTypeBuilder<NeighborhoodSettlementCommandReceiptRecord> b)
    {
        b.ToTable(Tables[4]); b.HasKey(x => new { x.ActorUserId, x.RequestId }); b.Property(x => x.ActorUserId).HasMaxLength(191);
        b.Property(x => x.AgreementId).HasMaxLength(64); b.Property(x => x.InputDigest).HasMaxLength(64); b.Property(x => x.Kind).HasMaxLength(40); b.Property(x => x.ResponseJson).HasColumnType("longtext");
        b.HasIndex(x => x.AgreementId).HasDatabaseName("IX_neighborhood_receipt_agreement");
        b.HasOne<NeighborhoodSettlementAgreementRecord>().WithMany().HasForeignKey(x => x.AgreementId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_neighborhood_receipt_agreement");
    }
}

public sealed class NeighborhoodSettlementAgreementConfiguration : IEntityTypeConfiguration<NeighborhoodSettlementAgreementRecord>
{ public void Configure(EntityTypeBuilder<NeighborhoodSettlementAgreementRecord> b) => NeighborhoodSettlementModel.Agreement(b); }
public sealed class NeighborhoodSettlementVersionConfiguration : IEntityTypeConfiguration<NeighborhoodSettlementAgreementVersionRecord>
{ public void Configure(EntityTypeBuilder<NeighborhoodSettlementAgreementVersionRecord> b) => NeighborhoodSettlementModel.Version(b); }
public sealed class NeighborhoodSettlementEntryConfiguration : IEntityTypeConfiguration<NeighborhoodSettlementEntryRecord>
{ public void Configure(EntityTypeBuilder<NeighborhoodSettlementEntryRecord> b) => NeighborhoodSettlementModel.Entry(b); }
public sealed class NeighborhoodSettlementPaymentConfiguration : IEntityTypeConfiguration<NeighborhoodSettlementPaymentRecord>
{ public void Configure(EntityTypeBuilder<NeighborhoodSettlementPaymentRecord> b) => NeighborhoodSettlementModel.Payment(b); }
public sealed class NeighborhoodSettlementReceiptConfiguration : IEntityTypeConfiguration<NeighborhoodSettlementCommandReceiptRecord>
{ public void Configure(EntityTypeBuilder<NeighborhoodSettlementCommandReceiptRecord> b) => NeighborhoodSettlementModel.Receipt(b); }
