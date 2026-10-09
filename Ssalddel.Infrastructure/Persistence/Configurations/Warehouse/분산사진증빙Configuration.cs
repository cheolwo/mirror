using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.창고;

namespace 살뜰.Infrastructure.Persistence.Configurations.Warehouse;

public static class 분산사진증빙Model
{
    public static readonly string[] Tables = ["distributed_photo_evidence", "distributed_photo_holds"];
    public static void Configure(ModelBuilder m)
    {
        Photo(m.Entity<분산사진증빙Record>());
        Hold(m.Entity<분산사진보존정지Record>());
    }
    public static void Photo(EntityTypeBuilder<분산사진증빙Record> p)
    {
        p.ToTable(Tables[0]); p.HasKey(x => x.EvidenceId);
        p.Property(x => x.EvidenceId).HasMaxLength(64);
        p.Property(x => x.ActorUserId).HasMaxLength(191);
        p.Property(x => x.Kind).HasMaxLength(20); p.Property(x => x.AggregateId).HasMaxLength(64);
        p.Property(x => x.BindingDigest).HasMaxLength(64); p.Property(x => x.InputSha256).HasMaxLength(64);
        p.Property(x => x.StoredSha256).HasMaxLength(64); p.Property(x => x.ContentType).HasMaxLength(30);
        p.Property(x => x.StatusCode).HasMaxLength(30); p.Property(x => x.PrivateDetailsJson).HasColumnType("longtext");
        p.Property(x => x.DeletionLimitationCode).HasMaxLength(100); p.Property(x => x.Revision).IsConcurrencyToken();
        p.HasIndex(x => new { x.ActorUserId, x.UploadRequestId }).IsUnique();
        p.HasIndex(x => new { x.ActorUserId, x.CompletionRequestId });
        p.HasIndex(x => new { x.StatusCode, x.CreatedAtUtc }); p.HasIndex(x => x.DeleteAfterUtc);
    }
    public static void Hold(EntityTypeBuilder<분산사진보존정지Record> h)
    {
        h.ToTable(Tables[1]); h.HasKey(x => x.CaseId); h.Property(x => x.CaseId).HasMaxLength(100);
        h.Property(x => x.SourceKind).HasMaxLength(40); h.Property(x => x.SourceId).HasMaxLength(64);
        h.Property(x => x.LegalBasisJson).HasColumnType("longtext"); h.Property(x => x.Revision).IsConcurrencyToken();
        h.HasIndex(x => new { x.SourceKind, x.SourceId, x.Active });
    }
}

public sealed class 분산사진증빙Configuration : IEntityTypeConfiguration<분산사진증빙Record>
{
    public void Configure(EntityTypeBuilder<분산사진증빙Record> builder) => 분산사진증빙Model.Photo(builder);
}
public sealed class 분산사진보존정지Configuration : IEntityTypeConfiguration<분산사진보존정지Record>
{
    public void Configure(EntityTypeBuilder<분산사진보존정지Record> builder) => 분산사진증빙Model.Hold(builder);
}
