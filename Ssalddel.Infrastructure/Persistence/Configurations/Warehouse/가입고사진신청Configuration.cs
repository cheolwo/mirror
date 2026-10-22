using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.창고;

namespace 살뜰.Infrastructure.Persistence.Configurations.Warehouse;

public sealed class 가입고사진신청Configuration : IEntityTypeConfiguration<가입고사진신청>
{
    public void Configure(EntityTypeBuilder<가입고사진신청> builder)
    {
        // 클라이언트의 최초 요청 번호를 저장한다. DB나 EF가 새 번호를 만들지 않는다.
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Revision).IsConcurrencyToken().HasDefaultValue(1L);
        builder.HasIndex(x => new { x.작성자UserId, x.CreatedAtUtc });
        builder.HasOne(x => x.입고요청)
            .WithOne()
            .HasForeignKey<입고요청>(x => x.가입고사진신청Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
