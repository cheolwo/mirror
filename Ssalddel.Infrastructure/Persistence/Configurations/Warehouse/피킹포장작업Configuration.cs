using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.창고;

namespace 살뜰.Infrastructure.Persistence.Configurations.Warehouse;

public sealed class 피킹포장작업Configuration : IEntityTypeConfiguration<피킹포장작업>
{
    public void Configure(EntityTypeBuilder<피킹포장작업> builder)
    {
        builder.ToTable("피킹포장작업");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.작업Key).HasColumnName("작업_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.작업유형).HasColumnName("작업유형").HasMaxLength(30).IsRequired();
        builder.Property(x => x.처리방식).HasColumnName("처리방식").HasMaxLength(50).IsRequired();
        builder.Property(x => x.상태).HasColumnName("상태").HasMaxLength(30).IsRequired().IsConcurrencyToken();
        builder.Property(x => x.출고묶음Id).HasColumnName("출고묶음_id");
        builder.Property(x => x.출고예정Id).HasColumnName("출고예정_id");
        builder.Property(x => x.입고상품Id).HasColumnName("입고상품_id");
        builder.Property(x => x.창고Id).HasColumnName("창고_id");
        builder.Property(x => x.창고명).HasColumnName("창고명").HasMaxLength(200).IsRequired();
        builder.Property(x => x.작업자UserId).HasColumnName("작업자_user_id").HasMaxLength(450).IsRequired();
        builder.Property(x => x.작업자표시명).HasColumnName("작업자표시명").HasMaxLength(120).IsRequired();
        builder.Property(x => x.상대작업자UserId).HasColumnName("상대작업자_user_id").HasMaxLength(450);
        builder.Property(x => x.이전작업Key).HasColumnName("이전작업_key").HasMaxLength(120);
        builder.Property(x => x.다음작업Key).HasColumnName("다음작업_key").HasMaxLength(120);
        builder.Property(x => x.주문참조번호).HasColumnName("주문참조번호").HasMaxLength(100).IsRequired();
        builder.Property(x => x.라인Key).HasColumnName("라인_key").HasMaxLength(120).IsRequired();
        builder.Property(x => x.상품명).HasColumnName("상품명").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SKU).HasColumnName("sku").HasMaxLength(100).IsRequired();
        builder.Property(x => x.수량).HasColumnName("수량");
        builder.Property(x => x.적재대코드).HasColumnName("적재대코드").HasMaxLength(80);
        builder.Property(x => x.보관위치코드).HasColumnName("보관위치코드").HasMaxLength(120);
        builder.Property(x => x.묶음바코드).HasColumnName("묶음바코드").HasMaxLength(120);
        builder.Property(x => x.할당사유).HasColumnName("할당사유").HasMaxLength(500);
        builder.Property(x => x.커뮤니티원장Id).HasColumnName("community_ledger_id").HasMaxLength(120);
        builder.Property(x => x.커뮤니티원장블록Id).HasColumnName("community_ledger_block_id").HasMaxLength(120);
        builder.Property(x => x.시작일시Utc).HasColumnName("started_at_utc");
        builder.Property(x => x.완료일시Utc).HasColumnName("completed_at_utc");
        builder.Property(x => x.완료근거Json).HasColumnName("completion_evidence_json").HasColumnType("longtext");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(x => x.출고묶음)
            .WithMany()
            .HasForeignKey(x => x.출고묶음Id)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.작업Key).IsUnique();
        builder.HasIndex(x => new { x.창고Id, x.상태, x.작업유형, x.CreatedAt });
        builder.HasIndex(x => new { x.작업자UserId, x.상태, x.CreatedAt });
        builder.HasIndex(x => new { x.출고묶음Id, x.작업유형 });
        builder.HasIndex(x => x.출고예정Id);
        builder.HasIndex(x => x.입고상품Id);
        builder.HasIndex(x => x.커뮤니티원장Id);
    }
}
