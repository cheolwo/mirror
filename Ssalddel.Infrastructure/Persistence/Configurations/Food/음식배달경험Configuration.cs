using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Infrastructure.Persistence.Configurations.Food;

public sealed class 음식배달경험공유설정Configuration : IEntityTypeConfiguration<음식배달경험공유설정>
{
    public void Configure(EntityTypeBuilder<음식배달경험공유설정> builder)
        => builder.Property(x => x.Revision).IsConcurrencyToken();
}
public sealed class 음식배달전달지역SnapshotConfiguration : IEntityTypeConfiguration<음식배달전달지역Snapshot>
{
    public void Configure(EntityTypeBuilder<음식배달전달지역Snapshot> builder) { }
}
public sealed class 음식배달경험완료OutboxConfiguration : IEntityTypeConfiguration<음식배달경험완료Outbox>
{
    public void Configure(EntityTypeBuilder<음식배달경험완료Outbox> builder)
    {
        builder.HasIndex(x => new { x.기사Id, x.주문번호, x.완료종류Code, x.실행모드Code }).IsUnique();
        builder.HasIndex(x => new { x.ProjectedAtUtc, x.CreatedAtUtc });
    }
}
public sealed class 음식배달경험FactConfiguration : IEntityTypeConfiguration<음식배달경험Fact>
{
    public void Configure(EntityTypeBuilder<음식배달경험Fact> builder)
    {
        builder.HasIndex(x => new { x.완료OutboxId, x.장소종류Code }).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.완료종류Code, x.장소종류Code, x.장소Id, x.실행모드Code });
    }
}
