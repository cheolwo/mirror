using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.정산;

namespace 살뜰.Infrastructure.Persistence.Configurations.Settlement;

public sealed class 재무사건Configuration : IEntityTypeConfiguration<재무사건>
{
    public void Configure(EntityTypeBuilder<재무사건> builder)
    {
        builder.Property(x => x.금액).HasPrecision(18, 2);
        builder.HasIndex(x => x.StableId).IsUnique();
        builder.HasIndex(x => new
        {
            x.원본Event유형,
            x.원본StableId,
            x.원본Revision,
            x.재무의미Code
        }).IsUnique();
        builder.HasIndex(x => new { x.통화Code, x.업무발생일시Utc });
    }
}

public sealed class 관리계정전기Configuration : IEntityTypeConfiguration<관리계정전기>
{
    public void Configure(EntityTypeBuilder<관리계정전기> builder)
    {
        builder.Property(x => x.금액).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.재무사건Id, x.LineNumber }).IsUnique();
        builder.HasIndex(x => new { x.관리계정StableId, x.통화Code });
        builder.HasOne(x => x.재무사건)
            .WithMany(x => x.관리계정전기목록)
            .HasForeignKey(x => x.재무사건Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class 재무사건증빙Configuration : IEntityTypeConfiguration<재무사건증빙>
{
    public void Configure(EntityTypeBuilder<재무사건증빙> builder)
    {
        builder.HasIndex(x => new { x.재무사건Id, x.증빙유형Code, x.원본참조 }).IsUnique();
        builder.HasOne(x => x.재무사건)
            .WithMany(x => x.증빙목록)
            .HasForeignKey(x => x.재무사건Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class 재무대사예외Configuration : IEntityTypeConfiguration<재무대사예외>
{
    public void Configure(EntityTypeBuilder<재무대사예외> builder)
    {
        builder.HasIndex(x => x.StableId).IsUnique();
        builder.HasIndex(x => new { x.상태Code, x.감지일시Utc });
    }
}
