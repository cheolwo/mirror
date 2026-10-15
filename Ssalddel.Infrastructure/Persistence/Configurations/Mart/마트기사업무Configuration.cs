using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.마트;

namespace 살뜰.Infrastructure.Persistence.Configurations.Mart;

public sealed class 마트배송시도Configuration : IEntityTypeConfiguration<마트배송시도>
{
    public void Configure(EntityTypeBuilder<마트배송시도> b)
    {
        b.ToTable("마트배송시도"); b.HasKey(x => x.시도StableId);
        b.Property(x => x.시도StableId).HasMaxLength(220);
        b.Property(x => x.배차의뢰Id).HasMaxLength(120).IsRequired();
        b.Property(x => x.주문참조번호).HasMaxLength(100).IsRequired();
        b.Property(x => x.기사Id).HasMaxLength(450).IsRequired();
        b.Property(x => x.상태Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.픽업전표Id).HasMaxLength(64);
        b.Property(x => x.고객수령근거Json).HasColumnName("customer_receipt_evidence_json").HasColumnType("longtext");
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.배차의뢰Id).IsUnique();
        b.HasIndex(x => new { x.기사Id, x.상태Code, x.UpdatedAtUtc });
    }
}

public sealed class 마트포장전표Configuration : IEntityTypeConfiguration<마트포장전표>
{
    public void Configure(EntityTypeBuilder<마트포장전표> b)
    {
        b.ToTable("마트포장전표"); b.HasKey(x => x.주문참조번호);
        b.Property(x => x.주문참조번호).HasMaxLength(100);
        b.Property(x => x.전표Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.보호코드Json).HasColumnType("longtext").IsRequired();
        b.Property(x => x.QrDigest).HasMaxLength(64).IsRequired();
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.전표Id).IsUnique();
    }
}

public sealed class 마트기사요청영수증Configuration : IEntityTypeConfiguration<마트기사요청영수증>
{
    public void Configure(EntityTypeBuilder<마트기사요청영수증> b)
    {
        b.ToTable("마트기사요청영수증"); b.HasKey(x => new { x.작업자UserId, x.클라이언트요청Id });
        b.Property(x => x.작업자UserId).HasMaxLength(450);
        b.Property(x => x.작업Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.배차의뢰Id).HasMaxLength(120).IsRequired();
        b.Property(x => x.주문참조번호).HasMaxLength(100).IsRequired();
        b.Property(x => x.보호입력Digest).HasColumnType("longtext").IsRequired();
        b.Property(x => x.결과Code).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.CreatedAtUtc);
    }
}

public sealed class 마트기사후속OutboxConfiguration : IEntityTypeConfiguration<마트기사후속Outbox>
{
    public void Configure(EntityTypeBuilder<마트기사후속Outbox> b)
    {
        b.ToTable("마트기사후속Outbox"); b.HasKey(x => x.멱등키);
        b.Property(x => x.멱등키).HasMaxLength(200);
        b.Property(x => x.배차의뢰Id).HasMaxLength(120).IsRequired();
        b.Property(x => x.주문참조번호).HasMaxLength(100).IsRequired();
        b.Property(x => x.기사Id).HasMaxLength(450).IsRequired();
        b.Property(x => x.시도StableId).HasMaxLength(220).IsRequired();
        b.Property(x => x.종류Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.상태Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.LeaseOwner).HasMaxLength(64);
        b.Property(x => x.마지막실패Code).HasMaxLength(80);
        b.Property(x => x.ClaimRevision).IsConcurrencyToken();
        b.HasIndex(x => new { x.상태Code, x.NextAttemptAtUtc, x.CreatedAtUtc });
    }
}
