using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.Notifications;

namespace Ssalddel.Infrastructure.Persistence.Configurations.Notifications;

public sealed class OperationsAlertConfiguration : IEntityTypeConfiguration<OperationsAlert>
{
    public void Configure(EntityTypeBuilder<OperationsAlert> b)
    {
        b.ToTable("operations_alerts"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.SourceKind).HasMaxLength(32); b.Property(x => x.SourceId).HasMaxLength(128);
        b.Property(x => x.ProblemCode).HasMaxLength(64); b.Property(x => x.Severity).HasMaxLength(16);
        b.Property(x => x.Status).HasMaxLength(16); b.Property(x => x.AcknowledgedBy).HasMaxLength(450);
        b.Property(x => x.Revision).IsConcurrencyToken(); b.HasIndex(x => new { x.Status, x.CreatedAtUtc, x.Id });
        b.HasIndex(x => new { x.SourceKind, x.SourceId });
    }
}
public sealed class OperationsAlertOutboxConfiguration : IEntityTypeConfiguration<OperationsAlertOutbox>
{
    public void Configure(EntityTypeBuilder<OperationsAlertOutbox> b)
    {
        b.ToTable("operations_alert_outbox"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.AlertId).HasMaxLength(64); b.Property(x => x.Kind).HasMaxLength(16);
        b.Property(x => x.Channel).HasMaxLength(16); b.Property(x => x.Status).HasMaxLength(24);
        b.Property(x => x.RecipientUserId).HasMaxLength(450); b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.Property(x => x.Revision).IsConcurrencyToken(); b.HasIndex(x => new { x.Status, x.NextAttemptUtc, x.LeaseUntilUtc });
    }
}
public sealed class OperationsAlertCheckpointConfiguration : IEntityTypeConfiguration<OperationsAlertCheckpoint>
{
    public void Configure(EntityTypeBuilder<OperationsAlertCheckpoint> b)
    {
        b.ToTable("operations_alert_checkpoints"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.CursorId).HasMaxLength(128); b.Property(x => x.Revision).IsConcurrencyToken();
    }
}
public sealed class OperationsAlertReceiptConfiguration : IEntityTypeConfiguration<OperationsAlertReceipt>
{
    public void Configure(EntityTypeBuilder<OperationsAlertReceipt> b)
    {
        b.ToTable("operations_alert_receipts"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.AlertId).HasMaxLength(64); b.Property(x => x.ActorUserId).HasMaxLength(450);
        b.HasIndex(x => new { x.ActorUserId, x.RequestId }).IsUnique();
    }
}
public sealed class OperationsBudgetUsageConfiguration : IEntityTypeConfiguration<OperationsBudgetUsage>
{
    public void Configure(EntityTypeBuilder<OperationsBudgetUsage> b)
    {
        b.ToTable("operations_budget_usage"); b.HasKey(x => x.Id); b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Category).HasMaxLength(32); b.HasIndex(x => new { x.DayUtc, x.Category });
    }
}
