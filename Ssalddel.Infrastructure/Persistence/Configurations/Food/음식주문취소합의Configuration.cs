using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Infrastructure.Persistence.Configurations.Food;

public sealed class 음식주문취소합의Configuration : IEntityTypeConfiguration<음식주문취소합의>
{
    public void Configure(EntityTypeBuilder<음식주문취소합의> builder)
    {
        builder.ToTable("음식주문취소합의");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.합의StableId).HasMaxLength(64);
        builder.Property(x => x.주문번호).HasMaxLength(100);
        builder.Property(x => x.주문상태).HasMaxLength(50);
        builder.Property(x => x.주문자부담액).HasPrecision(18, 2);
        builder.Property(x => x.음식점부담액).HasPrecision(18, 2);
        builder.Property(x => x.비용근거참조).HasMaxLength(200);
        builder.Property(x => x.영수증Json).HasColumnType("longtext");
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => x.합의StableId).IsUnique();
        builder.HasIndex(x => new { x.음식주문Id, x.CreatedAtUtc });
        builder.HasOne<음식주문>().WithMany().HasForeignKey(x => x.음식주문Id).OnDelete(DeleteBehavior.Restrict);
    }
}
