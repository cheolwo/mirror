using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.운송;

namespace 살뜰.Infrastructure.Persistence.Configurations.Operations;

public sealed class 운송업무담당자배정Configuration : IEntityTypeConfiguration<운송업무담당자배정>
{
    public void Configure(EntityTypeBuilder<운송업무담당자배정> builder)
    {
        builder.ToTable("운송업무담당자배정");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.운송의뢰Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.화주Id).HasMaxLength(191).IsRequired();
        builder.Property(x => x.담당자UserId).HasMaxLength(191).IsRequired();
        builder.Property(x => x.담당유형Code).HasMaxLength(32).IsRequired();
        builder.Property(x => x.주담당Slot).HasMaxLength(16);
        builder.Property(x => x.권한CodesJson).HasColumnType("longtext").IsRequired();
        builder.Property(x => x.지정자UserId).HasMaxLength(191).IsRequired();

        builder.HasIndex(x => new { x.운송의뢰Id, x.배정세트Revision, x.담당자UserId })
            .HasDatabaseName("UX_운송업무담당자배정_의뢰_판본_담당자")
            .IsUnique();
        builder.HasIndex(x => new { x.운송의뢰Id, x.배정세트Revision, x.주담당Slot })
            .HasDatabaseName("UX_운송업무담당자배정_의뢰_판본_주담당")
            .IsUnique();
        builder.HasIndex(x => new { x.운송의뢰Id, x.클라이언트요청Id, x.담당자UserId })
            .HasDatabaseName("UX_운송업무담당자배정_의뢰_요청_담당자")
            .IsUnique();
        builder.HasIndex(x => new { x.담당자UserId, x.운송의뢰Id, x.배정세트Revision })
            .HasDatabaseName("IX_운송업무담당자배정_담당자_의뢰_판본");
    }
}
