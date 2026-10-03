using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.음식;

namespace 살뜰.Infrastructure.Persistence.Configurations.Food;

public sealed class 음식주문기사정산Configuration : IEntityTypeConfiguration<음식주문기사정산>
{
    public void Configure(EntityTypeBuilder<음식주문기사정산> builder)
    {
        builder.ToTable("음식주문기사정산");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.정산StableId).HasMaxLength(100);
        builder.Property(x => x.주문번호).HasMaxLength(100);
        builder.Property(x => x.음식점명).HasMaxLength(200);
        builder.Property(x => x.배달시도StableId).HasMaxLength(220);
        builder.Property(x => x.기사Id).HasMaxLength(450);
        builder.Property(x => x.세전대금).HasPrecision(18, 2);
        builder.Property(x => x.공제액).HasPrecision(18, 2);
        builder.Property(x => x.수령액).HasPrecision(18, 2);
        builder.Property(x => x.요금정책판본).HasMaxLength(180);
        builder.Property(x => x.요금계산근거Json).HasColumnType("longtext");
        builder.Property(x => x.공제근거참조).HasMaxLength(500);
        builder.Property(x => x.공제근거범위Code).HasMaxLength(40);
        builder.Property(x => x.정산상태Code).HasMaxLength(40);
        builder.Property(x => x.지급상태Code).HasMaxLength(40);
        builder.Property(x => x.실행모드Code).HasMaxLength(30);
        builder.Property(x => x.보류사유).HasMaxLength(500);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => x.정산StableId).IsUnique();
        builder.HasIndex(x => x.주문번호).IsUnique();
        builder.HasIndex(x => x.배달시도Id).IsUnique();
        builder.HasIndex(x => new { x.기사Id, x.전달완료시각Utc });
        builder.HasOne<음식주문>().WithMany().HasForeignKey(x => x.음식주문Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.배달시도).WithMany().HasForeignKey(x => x.배달시도Id).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<살뜰.도메인.운송.운송원장>().WithMany().HasForeignKey(x => x.운송Id).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class 음식주문기사지급검증Configuration : IEntityTypeConfiguration<음식주문기사지급검증>
{
    public void Configure(EntityTypeBuilder<음식주문기사지급검증> builder)
    {
        builder.ToTable("음식주문기사지급검증");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.지급StableId).HasMaxLength(100);
        builder.Property(x => x.멱등키).HasMaxLength(128);
        builder.Property(x => x.확인세전대금).HasPrecision(18, 2);
        builder.Property(x => x.확인공제액).HasPrecision(18, 2);
        builder.Property(x => x.모의수령액).HasPrecision(18, 2);
        builder.Property(x => x.공제근거참조).HasMaxLength(500);
        builder.Property(x => x.결과Code).HasMaxLength(20);
        builder.Property(x => x.검증관리자Id).HasMaxLength(450);
        builder.HasIndex(x => x.지급StableId).IsUnique();
        builder.HasIndex(x => x.멱등키).IsUnique();
        builder.HasOne(x => x.정산).WithMany(x => x.지급검증목록).HasForeignKey(x => x.정산Id).OnDelete(DeleteBehavior.Restrict);
    }
}
