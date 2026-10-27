using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.기사;

namespace 살뜰.Infrastructure.Persistence.Configurations.Driver;

public sealed class 운송자격Configuration : IEntityTypeConfiguration<운송자격Record>
{
    public void Configure(EntityTypeBuilder<운송자격Record> b)
    {
        b.ToTable("driver_transport_qualification");
        b.HasKey(x => x.DriverId);
        b.Property(x => x.DriverId).HasMaxLength(191);
        b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.Status).HasMaxLength(32);
        b.Property(x => x.PolicyVersion).HasMaxLength(80);
        b.Property(x => x.VehicleKind).HasMaxLength(32);
        b.Property(x => x.VehicleProfileValue).HasMaxLength(150);
        b.Property(x => x.PrivateDetailsJson).HasColumnType("longtext");
        b.Property(x => x.HistoryJson).HasColumnType("longtext");
        b.Property(x => x.ReviewerId).HasMaxLength(191);
        b.Property(x => x.SubmittedAtUtc).HasPrecision(6);
        b.Property(x => x.ReviewedAtUtc).HasPrecision(6);
        b.Property(x => x.RecheckAtUtc).HasPrecision(6);
    }
}
