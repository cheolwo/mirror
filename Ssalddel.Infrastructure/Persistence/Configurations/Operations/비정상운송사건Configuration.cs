using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.운송;

namespace 살뜰.Infrastructure.Persistence.Configurations.Operations;

public sealed class 비정상운송사건Configuration : IEntityTypeConfiguration<비정상운송사건>
{
    public void Configure(EntityTypeBuilder<비정상운송사건> builder)
    {
        builder.ToTable("비정상운송사건");
        builder.HasKey(x => x.사건StableId);
        builder.Property(x => x.사건StableId).HasMaxLength(128);
        builder.Property(x => x.운송의뢰Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.사건유형Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.원본예외Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.단계Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.상태Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.현재담당Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.보류전정산상태Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.업무통제상태Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.보류범위Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.보험검토상태Code).HasMaxLength(40).IsRequired();
        builder.Property(x => x.해결결과Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.최근검토자UserId).HasMaxLength(191).IsRequired();
        builder.Property(x => x.최근검토사유).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => new { x.운송의뢰Id, x.상태Code, x.UpdatedAt })
            .HasDatabaseName("IX_비정상운송사건_의뢰_상태_갱신");
        builder.HasIndex(x => new { x.운송Id, x.상태Code })
            .HasDatabaseName("IX_비정상운송사건_운송_상태");
    }
}
