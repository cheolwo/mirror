using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Infrastructure.Persistence.Configurations.Food;

public sealed class 음식주문Configuration : IEntityTypeConfiguration<음식주문>
{
    public void Configure(EntityTypeBuilder<음식주문> builder)
    {
        builder.ToTable("음식주문");

        builder.HasIndex(x => x.주문번호).IsUnique();
        builder.HasIndex(x => x.결제승인Id).IsUnique();
        builder.HasIndex(x => new { x.주문자UserId, x.클라이언트요청Id }).IsUnique();
        builder.HasIndex(x => new { x.주문자UserId, x.상태, x.CreatedAt });
        builder.HasIndex(x => new { x.음식점Id, x.상태, x.CreatedAt });
        builder.HasIndex(x => x.배차대기Id);
        builder.HasIndex(x => x.커뮤니티원장Id);

        builder.HasMany(x => x.상품목록)
            .WithOne(x => x.음식주문)
            .HasForeignKey(x => x.음식주문Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.상태이력)
            .WithOne(x => x.음식주문)
            .HasForeignKey(x => x.음식주문Id)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
