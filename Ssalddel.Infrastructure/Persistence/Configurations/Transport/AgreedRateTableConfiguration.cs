using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.Pricing;

namespace Ssalddel.Infrastructure.Persistence.Configurations.Transport;

public sealed class AgreedRateTableConfiguration : IEntityTypeConfiguration<AgreedRateTableRecord>
{
    public void Configure(EntityTypeBuilder<AgreedRateTableRecord> b)
    {
        b.ToTable("agreed_rate_tables"); b.HasKey(x => x.AgreementId);
        b.Property(x => x.AgreementId).HasMaxLength(64);
        b.Property(x => x.BusinessKind).HasMaxLength(40); b.Property(x => x.VehicleCode).HasMaxLength(80);
        b.Property(x => x.ProposerKind).HasMaxLength(30); b.Property(x => x.ProposerId).HasMaxLength(191);
        b.Property(x => x.CounterpartyKind).HasMaxLength(30); b.Property(x => x.CounterpartyId).HasMaxLength(191);
        b.Property(x => x.TermsJson).HasColumnType("longtext"); b.Property(x => x.TermsDigest).HasMaxLength(64);
        b.Property(x => x.HistoryJson).HasColumnType("longtext"); b.Property(x => x.StatusCode).HasMaxLength(30);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.EffectiveFromUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.Property(x => x.EffectiveUntilUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.Property(x => x.AcceptedAtUtc).HasConversion(x => x, x => x.HasValue ? DateTime.SpecifyKind(x.Value, DateTimeKind.Utc) : (DateTime?)null);
        b.HasIndex(x => new { x.ProposerKind, x.ProposerId });
        b.HasIndex(x => new { x.CounterpartyKind, x.CounterpartyId });
    }
}
public sealed class AgreedRateTableCommandReceiptConfiguration : IEntityTypeConfiguration<AgreedRateTableCommandReceipt>
{
    public void Configure(EntityTypeBuilder<AgreedRateTableCommandReceipt> b)
    {
        b.ToTable("agreed_rate_table_receipts"); b.HasKey(x => new { x.PartyKind, x.PartyId, x.RequestId });
        b.Property(x => x.PartyKind).HasMaxLength(30); b.Property(x => x.PartyId).HasMaxLength(191);
        b.Property(x => x.InputDigest).HasMaxLength(64); b.Property(x => x.ResultJson).HasColumnType("longtext");
        b.Property(x => x.RecordedAtUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
    }
}
public sealed class AgreedRateTableSelectionConfiguration : IEntityTypeConfiguration<AgreedRateTableSelectionRecord>
{
    public void Configure(EntityTypeBuilder<AgreedRateTableSelectionRecord> b)
    {
        b.ToTable("agreed_rate_table_selections"); b.HasKey(x => new { x.BusinessKind, x.WorkId });
        b.Property(x => x.BusinessKind).HasMaxLength(40); b.Property(x => x.WorkId).HasMaxLength(191);
        b.Property(x => x.AgreementId).HasMaxLength(64); b.Property(x => x.SnapshotJson).HasColumnType("longtext");
        b.Property(x => x.SelectingPartyKind).HasMaxLength(30); b.Property(x => x.SelectingPartyId).HasMaxLength(191);
        b.Property(x => x.AgreedAmountKrw).HasPrecision(18, 2); b.Property(x => x.DistanceKm).HasPrecision(18, 3);
        b.Property(x => x.DistanceBasisCode).HasMaxLength(80); b.Property(x => x.SynchronizationStatusCode).HasMaxLength(40);
        b.Property(x => x.SelectedAtUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
    }
}
