using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ssalddel.Domain.HumanResources;

namespace 살뜰.Infrastructure.Persistence.Configurations.HumanResources;

public sealed class 근무가능요청Configuration : IEntityTypeConfiguration<근무가능요청Record>
{
    public void Configure(EntityTypeBuilder<근무가능요청Record> b)
    {
        b.HasIndex(x => new { x.ManagerUserId, x.ClientRequestId }).IsUnique();
        b.HasIndex(x => x.ActiveKey).IsUnique();
        b.HasIndex(x => new { x.WarehouseId, x.CreatedAtUtc });
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.ProposedHourlyAmount).HasPrecision(18, 2);
    }
}

public sealed class 근무가능대상Configuration : IEntityTypeConfiguration<근무가능대상Record>
{
    public void Configure(EntityTypeBuilder<근무가능대상Record> b)
    {
        b.HasIndex(x => new { x.RequestId, x.UserId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Status });
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<근무가능요청Record>().WithMany().HasForeignKey(x => x.RequestId)
            .HasConstraintName("FK_hr_work_recipients_request").OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class 근무연락동의Configuration : IEntityTypeConfiguration<근무연락동의Record>
{
    public void Configure(EntityTypeBuilder<근무연락동의Record> b)
        => b.Property(x => x.Revision).IsConcurrencyToken();
}
