using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.기사;

namespace 살뜰.Infrastructure.Persistence.Configurations.Driver;

public sealed class DriverWorkParticipationConfiguration : IEntityTypeConfiguration<DriverWorkParticipationRecord>
{
    public void Configure(EntityTypeBuilder<DriverWorkParticipationRecord> b)
    {
        b.ToTable("driver_work_participation");
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.VehicleKind).HasMaxLength(32).IsRequired();
        b.Property(x => x.CapacityKg).HasPrecision(12, 3);
        b.Property(x => x.CapacityLitres).HasPrecision(12, 3);
        b.Property(x => x.UpdatedAtUtc).HasPrecision(6);
    }
}
