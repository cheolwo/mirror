using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Infrastructure.Persistence.Configurations.Food;

public sealed class 음식배달완료WorldSnapshotConfiguration
    : IEntityTypeConfiguration<음식배달완료WorldSnapshot>
{
    public void Configure(EntityTypeBuilder<음식배달완료WorldSnapshot> builder)
    {
        builder.HasIndex(x => x.원천OutboxId).IsUnique();
        builder.HasIndex(x => x.SnapshotStableId).IsUnique();
        builder.HasIndex(x => new { x.AreaStableId, x.ExpiresAtUtc, x.PublishedAtUtc });
    }
}
