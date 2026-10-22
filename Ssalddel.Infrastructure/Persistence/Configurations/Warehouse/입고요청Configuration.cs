using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.창고;

namespace 살뜰.Infrastructure.Persistence.Configurations.Warehouse;

public sealed class 입고요청Configuration : IEntityTypeConfiguration<입고요청>
{
    public void Configure(EntityTypeBuilder<입고요청> builder)
    {
        builder.Property(x => x.입고흐름유형)
            .HasDefaultValue("ContractBased");

        builder.Property(x => x.입고생성경로)
            .HasDefaultValue("계약 DB 기반 등록");

        builder.Property(x => x.계약선행여부)
            .HasDefaultValue(true);

        builder.Property(x => x.자동생성여부)
            .HasDefaultValue(false);

        builder.Property(x => x.커뮤니티원장Id)
            .HasColumnName("community_ledger_id")
            .HasMaxLength(120);

        builder.Property(x => x.커뮤니티원장템플릿Key)
            .HasColumnName("community_ledger_template_key")
            .HasMaxLength(120);

        builder.Property(x => x.커뮤니티원장상태)
            .HasColumnName("community_ledger_state")
            .HasMaxLength(80);

        builder.Property(x => x.커뮤니티원장동기화시각Utc)
            .HasColumnName("community_ledger_synced_at_utc");

        builder.HasIndex(x => x.커뮤니티원장Id);
        builder.HasIndex(x => new { x.창고Id, x.주문자UserId, x.상태 });
        builder.HasIndex(x => new { x.주문Id, x.주문자UserId });
        builder.HasIndex(x => x.출고예정Id);
        builder.HasIndex(x => new { x.입고흐름유형, x.자동생성여부 });
        builder.HasIndex(x => new { x.주문자UserId, x.현장입고클라이언트요청Id })
            .IsUnique();
        builder.HasIndex(x => new { x.주문자UserId, x.일반입고클라이언트요청Id })
            .IsUnique();
        builder.HasIndex(x => new { x.창고Id, x.예정SKU, x.상태 });
    }
}
