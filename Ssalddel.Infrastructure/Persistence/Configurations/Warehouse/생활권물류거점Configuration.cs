using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.창고;

namespace 살뜰.Infrastructure.Persistence.Configurations.Warehouse;

public sealed class 생활권물류거점Configuration : IEntityTypeConfiguration<생활권물류거점>
{
    public void Configure(EntityTypeBuilder<생활권물류거점> builder)
    {
        builder.HasIndex(x => x.StableId).IsUnique();
        builder.HasIndex(x => x.신청가원장Id).IsUnique();
        builder.HasIndex(x => new { x.생활권Key, x.상태Code });
        builder.Property(x => x.Revision).IsConcurrencyToken();
    }
}

public sealed class 생활권물류거점용량예약Configuration : IEntityTypeConfiguration<생활권물류거점용량예약>
{
    public void Configure(EntityTypeBuilder<생활권물류거점용량예약> builder)
    {
        builder.HasIndex(x => new { x.거점Id, x.멱등성Key }).IsUnique();
        builder.HasIndex(x => new { x.거점Id, x.상태Code });
    }
}

public sealed class 생활권물류거점보상기록Configuration : IEntityTypeConfiguration<생활권물류거점보상기록>
{
    public void Configure(EntityTypeBuilder<생활권물류거점보상기록> builder)
    {
        builder.HasIndex(x => new { x.거점Id, x.멱등성Key }).IsUnique();
        builder.HasIndex(x => x.업무StableId);
    }
}
