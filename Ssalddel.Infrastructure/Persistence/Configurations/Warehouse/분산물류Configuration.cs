using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using 살뜰.도메인.창고;

namespace 살뜰.Infrastructure.Persistence.Configurations.Warehouse;

public static class 분산물류Model
{
    public static readonly string[] Tables = ["distributed_sites", "distributed_locations", "distributed_inbound_plans",
        "distributed_lots", "distributed_waves", "distributed_orders", "distributed_reservations", "distributed_receipts", "distributed_storage_agreements"];
    public static void Configure(ModelBuilder m)
    {
        Site(m.Entity<분산거점Record>()); Location(m.Entity<분산적재위치Record>());
        Inbound(m.Entity<분산입고계획Record>()); Lot(m.Entity<분산재고LotRecord>());
        Wave(m.Entity<분산배송WaveRecord>()); Order(m.Entity<분산배송주문Record>());
        Reservation(m.Entity<분산재고예약Record>()); Receipt(m.Entity<분산물류CommandReceipt>());
        Agreement(m.Entity<분산보관합의Record>());
        분산사진증빙Model.Configure(m);
    }
    public static void Site(EntityTypeBuilder<분산거점Record> b)
    {
        b.ToTable(Tables[0]); b.HasKey(x => x.SiteId); b.Property(x => x.SiteId).HasMaxLength(64);
        b.HasIndex(x => x.WarehouseId).IsUnique(); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.PlaceId).HasMaxLength(120); b.Property(x => x.PublicLabel).HasMaxLength(120);
        b.Property(x => x.PublicCandidateRef).HasMaxLength(200); b.Property(x => x.OwnerUserId).HasMaxLength(191);
        b.Property(x => x.ManagerUserId).HasMaxLength(191); b.Property(x => x.StatusCode).HasMaxLength(40);
        b.Property(x => x.PrivateDetailsJson).HasColumnType("longtext"); b.Property(x => x.ConsentEvidenceJson).HasColumnType("longtext");
        b.Property(x => x.AcceptancePolicyJson).HasColumnType("longtext");
    }
    public static void Location(EntityTypeBuilder<분산적재위치Record> b)
    {
        b.ToTable(Tables[1]); b.HasKey(x => x.LocationId); b.Property(x => x.LocationId).HasMaxLength(64);
        b.Property(x => x.SiteId).HasMaxLength(64); b.Property(x => x.Barcode).HasMaxLength(100);
        b.Property(x => x.Label).HasMaxLength(100); b.HasIndex(x => x.Barcode).IsUnique();
        b.HasOne<분산거점Record>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
    }
    public static void Inbound(EntityTypeBuilder<분산입고계획Record> b)
    {
        b.ToTable(Tables[2]); b.HasKey(x => x.PlanId); b.Property(x => x.PlanId).HasMaxLength(64);
        b.Property(x => x.CreatorUserId).HasMaxLength(191); b.Property(x => x.SourceReference).HasMaxLength(120);
        b.Property(x => x.StatusCode).HasMaxLength(40); b.Property(x => x.Revision).IsConcurrencyToken();
    }
    public static void Lot(EntityTypeBuilder<분산재고LotRecord> b)
    {
        b.ToTable(Tables[3]); b.HasKey(x => x.LotId); b.Property(x => x.LotId).HasMaxLength(64);
        b.Property(x => x.PlanId).HasMaxLength(64); b.Property(x => x.LineKey).HasMaxLength(64);
        b.Property(x => x.SiteId).HasMaxLength(64); b.Property(x => x.LocationId).HasMaxLength(64);
        b.Property(x => x.OwnerUserId).HasMaxLength(191); b.Property(x => x.SellerUserId).HasMaxLength(191); b.Property(x => x.Sku).HasMaxLength(100);
        b.Property(x => x.LotCode).HasMaxLength(100); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.StorageAgreementId).HasMaxLength(64); b.Property(x => x.StorageAgreementSnapshotJson).HasColumnType("longtext");
        b.Property(x => x.InboundCollaborationJson).HasColumnType("longtext");
        b.HasIndex(x => new { x.PlanId, x.LineKey }).IsUnique(); b.HasIndex(x => x.InboundItemId).IsUnique();
        b.HasIndex(x => new { x.OwnerUserId, x.Sku });
        b.HasOne<분산입고계획Record>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<분산적재위치Record>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
    }
    public static void Wave(EntityTypeBuilder<분산배송WaveRecord> b)
    {
        b.ToTable(Tables[4]); b.HasKey(x => x.WaveId); b.Property(x => x.WaveId).HasMaxLength(64);
        b.Property(x => x.RoundId).HasMaxLength(64); b.HasIndex(x => x.RoundId).IsUnique();
        b.Property(x => x.CreatorUserId).HasMaxLength(191); b.Property(x => x.DriverId).HasMaxLength(191);
        b.Property(x => x.Label).HasMaxLength(120); b.Property(x => x.StatusCode).HasMaxLength(40);
        b.Property(x => x.RouteJson).HasColumnType("longtext"); b.Property(x => x.RouteEvidenceJson).HasColumnType("longtext");
        b.Property(x => x.PickupServiceMinutes).HasPrecision(18, 3); b.Property(x => x.DeliveryServiceMinutes).HasPrecision(18, 3);
        b.Property(x => x.TotalElapsedMinutes).HasPrecision(18, 3); b.Property(x => x.TotalDistanceKm).HasPrecision(18, 3);
        b.Property(x => x.OfferedDriverFareKrw).HasPrecision(18, 2); b.Property(x => x.Revision).IsConcurrencyToken();
        foreach (var name in new[] { "CutoffUtc", "PickupStartUtc", "PickupEndUtc", "DeliveryStartUtc", "DeliveryEndUtc" })
            b.Property<DateTime>(name).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
    }
    public static void Order(EntityTypeBuilder<분산배송주문Record> b)
    {
        b.ToTable(Tables[5]); b.HasKey(x => x.OrderId); b.Property(x => x.OrderId).HasMaxLength(64);
        b.Property(x => x.WaveId).HasMaxLength(64); b.Property(x => x.OrderReference).HasMaxLength(100);
        b.Property(x => x.CargoRequestId).HasMaxLength(191); b.HasIndex(x => x.CargoRequestId).IsUnique();
        b.Property(x => x.CargoEvidenceDigest).HasMaxLength(64);
        b.Property(x => x.SellerUserId).HasMaxLength(191); b.Property(x => x.CustomerUserId).HasMaxLength(191);
        b.Property(x => x.StatusCode).HasMaxLength(40); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasOne<분산배송WaveRecord>().WithMany().HasForeignKey(x => x.WaveId).OnDelete(DeleteBehavior.Restrict);
    }
    public static void Reservation(EntityTypeBuilder<분산재고예약Record> b)
    {
        b.ToTable(Tables[6]); b.HasKey(x => x.ReservationId); b.Property(x => x.ReservationId).HasMaxLength(64);
        b.Property(x => x.WaveId).HasMaxLength(64); b.Property(x => x.OrderId).HasMaxLength(64);
        b.Property(x => x.LotId).HasMaxLength(64); b.Property(x => x.Revision).IsConcurrencyToken();
        b.HasIndex(x => x.OutboundPlanId).IsUnique(); b.HasIndex(x => new { x.OrderId, x.LotId }).IsUnique();
        b.HasOne<분산배송주문Record>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<분산재고LotRecord>().WithMany().HasForeignKey(x => x.LotId).OnDelete(DeleteBehavior.Restrict);
    }
    public static void Receipt(EntityTypeBuilder<분산물류CommandReceipt> b)
    {
        b.ToTable(Tables[7]); b.HasKey(x => new { x.ActorUserId, x.RequestId });
        b.Property(x => x.ActorUserId).HasMaxLength(191); b.Property(x => x.InputDigest).HasMaxLength(64);
        b.Property(x => x.Kind).HasMaxLength(40); b.Property(x => x.AggregateId).HasMaxLength(64);
        b.Property(x => x.ReservationId).HasMaxLength(64); b.Property(x => x.StatusCode).HasMaxLength(40);
        b.Property(x => x.CompletionLinesJson).HasColumnType("longtext").Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        b.Property(x => x.RecordedAtUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
    }
    public static void Agreement(EntityTypeBuilder<분산보관합의Record> b)
    {
        b.ToTable(Tables[8]); b.HasKey(x => x.AgreementId); b.Property(x => x.AgreementId).HasMaxLength(64);
        b.Property(x => x.SiteId).HasMaxLength(64); b.Property(x => x.LocationId).HasMaxLength(64);
        b.Property(x => x.SpaceOwnerUserId).HasMaxLength(191); b.Property(x => x.GoodsOwnerUserId).HasMaxLength(191);
        b.Property(x => x.CreatorUserId).HasMaxLength(191);
        b.Property(x => x.TermsJson).HasColumnType("longtext"); b.Property(x => x.HistoryJson).HasColumnType("longtext");
        b.Property(x => x.PendingTermsJson).HasColumnType("longtext");
        b.Property(x => x.StatusCode).HasMaxLength(40); b.Property(x => x.Revision).IsConcurrencyToken();
        b.Property(x => x.StartsAtUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.Property(x => x.EndsAtUtc).HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.HasIndex(x => new { x.LocationId, x.GoodsOwnerUserId }).IsUnique();
        b.HasOne<분산적재위치Record>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_distributed_storage_agreements_locations");
    }
}
public sealed class 분산보관합의Configuration : IEntityTypeConfiguration<분산보관합의Record>
{ public void Configure(EntityTypeBuilder<분산보관합의Record> b) => 분산물류Model.Agreement(b); }
public sealed class 분산거점Configuration : IEntityTypeConfiguration<분산거점Record>
{ public void Configure(EntityTypeBuilder<분산거점Record> b) => 분산물류Model.Site(b); }
public sealed class 분산적재위치Configuration : IEntityTypeConfiguration<분산적재위치Record>
{ public void Configure(EntityTypeBuilder<분산적재위치Record> b) => 분산물류Model.Location(b); }
public sealed class 분산입고계획Configuration : IEntityTypeConfiguration<분산입고계획Record>
{ public void Configure(EntityTypeBuilder<분산입고계획Record> b) => 분산물류Model.Inbound(b); }
public sealed class 분산재고LotConfiguration : IEntityTypeConfiguration<분산재고LotRecord>
{ public void Configure(EntityTypeBuilder<분산재고LotRecord> b) => 분산물류Model.Lot(b); }
public sealed class 분산배송WaveConfiguration : IEntityTypeConfiguration<분산배송WaveRecord>
{ public void Configure(EntityTypeBuilder<분산배송WaveRecord> b) => 분산물류Model.Wave(b); }
public sealed class 분산배송주문Configuration : IEntityTypeConfiguration<분산배송주문Record>
{ public void Configure(EntityTypeBuilder<분산배송주문Record> b) => 분산물류Model.Order(b); }
public sealed class 분산재고예약Configuration : IEntityTypeConfiguration<분산재고예약Record>
{ public void Configure(EntityTypeBuilder<분산재고예약Record> b) => 분산물류Model.Reservation(b); }
public sealed class 분산물류CommandReceiptConfiguration : IEntityTypeConfiguration<분산물류CommandReceipt>
{ public void Configure(EntityTypeBuilder<분산물류CommandReceipt> b) => 분산물류Model.Receipt(b); }
