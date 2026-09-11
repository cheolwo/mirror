using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.운영;

namespace 살뜰.Infrastructure.Persistence.Configurations.Operations;

public sealed class 운영체제업무인계Configuration : IEntityTypeConfiguration<운영체제업무인계>
{
    public void Configure(EntityTypeBuilder<운영체제업무인계> builder)
    {
        builder.ToTable("운영체제업무인계");
        builder.HasKey(x => x.인계StableId);
        builder.Property(x => x.인계StableId).HasMaxLength(128);
        builder.Property(x => x.생성멱등Key).HasMaxLength(128).IsRequired();
        builder.Property(x => x.출발운영체제Id).HasMaxLength(96).IsRequired();
        builder.Property(x => x.도착운영체제Id).HasMaxLength(96).IsRequired();
        builder.Property(x => x.현재책임운영체제Id).HasMaxLength(96).IsRequired();
        builder.Property(x => x.출발업무유형Code).HasMaxLength(96).IsRequired();
        builder.Property(x => x.출발업무StableId).HasMaxLength(191).IsRequired();
        builder.Property(x => x.도착업무StableId).HasMaxLength(191).IsRequired();
        builder.Property(x => x.인계계약Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.인계계약Revision).HasMaxLength(64).IsRequired();
        builder.Property(x => x.최소상태사본Json).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.공개범위Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.상태Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.마지막응답요청Id).HasMaxLength(64).IsRequired();
        builder.Property(x => x.마지막응답결정Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.응답사유Code).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => new { x.출발운영체제Id, x.생성멱등Key }).IsUnique();
        builder.HasIndex(x => new { x.도착운영체제Id, x.상태Code, x.요청시각Utc });
        builder.HasIndex(x => new { x.출발업무StableId, x.출발업무Revision });
    }
}

public sealed class 운영체제업무인계OutboxConfiguration : IEntityTypeConfiguration<운영체제업무인계Outbox>
{
    public void Configure(EntityTypeBuilder<운영체제업무인계Outbox> builder)
    {
        builder.ToTable("운영체제업무인계_Outbox");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.멱등Key).HasMaxLength(191).IsRequired();
        builder.Property(x => x.인계StableId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.이벤트Type).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.처리상태Code).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => x.멱등Key).IsUnique();
        builder.HasIndex(x => new { x.처리상태Code, x.다음처리시각Utc });
        builder.HasIndex(x => new { x.인계StableId, x.CreatedAt });
    }
}
