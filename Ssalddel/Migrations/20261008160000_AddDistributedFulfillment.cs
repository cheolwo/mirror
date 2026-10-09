using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261008160000_AddDistributedFulfillment")]
public sealed class AddDistributedFulfillment : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        {
            var t = new CreateTableOperation { Name = "distributed_sites" };
            t.Columns.Add(new AddColumnOperation { Name = "SiteId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "WarehouseId", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "PlaceId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(120)", IsNullable = false, MaxLength = 120 });
            t.Columns.Add(new AddColumnOperation { Name = "PublicCandidateRef", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(200)", IsNullable = false, MaxLength = 200 });
            t.Columns.Add(new AddColumnOperation { Name = "PublicLabel", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(120)", IsNullable = false, MaxLength = 120 });
            t.Columns.Add(new AddColumnOperation { Name = "OwnerUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "ManagerUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "PrivateDetailsJson", Table = t.Name, ClrType = typeof(string), ColumnType = "longtext", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "SpaceConfirmed", Table = t.Name, ClrType = typeof(bool), ColumnType = "tinyint(1)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "PickupPointConfirmed", Table = t.Name, ClrType = typeof(bool), ColumnType = "tinyint(1)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "OwnerConsented", Table = t.Name, ClrType = typeof(bool), ColumnType = "tinyint(1)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "ManagerConsented", Table = t.Name, ClrType = typeof(bool), ColumnType = "tinyint(1)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "ConsentEvidenceJson", Table = t.Name, ClrType = typeof(string), ColumnType = "longtext", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "StatusCode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(40)", IsNullable = false, MaxLength = 40 });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_sites", Table = t.Name, Columns = ["SiteId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_locations" };
            t.Columns.Add(new AddColumnOperation { Name = "LocationId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "SiteId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "Barcode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(100)", IsNullable = false, MaxLength = 100 });
            t.Columns.Add(new AddColumnOperation { Name = "Label", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(100)", IsNullable = false, MaxLength = 100 });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_locations", Table = t.Name, Columns = ["LocationId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_inbound_plans" };
            t.Columns.Add(new AddColumnOperation { Name = "PlanId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "SourceReference", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(120)", IsNullable = false, MaxLength = 120 });
            t.Columns.Add(new AddColumnOperation { Name = "CreatorUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "StatusCode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(40)", IsNullable = false, MaxLength = 40 });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_inbound_plans", Table = t.Name, Columns = ["PlanId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_lots" };
            t.Columns.Add(new AddColumnOperation { Name = "LotId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "PlanId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "LineKey", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "SiteId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "LocationId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "SourceInboundId", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "TargetInboundId", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "InboundItemId", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "OwnerUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "SellerUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "Sku", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(100)", IsNullable = false, MaxLength = 100 });
            t.Columns.Add(new AddColumnOperation { Name = "LotCode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(100)", IsNullable = false, MaxLength = 100 });
            t.Columns.Add(new AddColumnOperation { Name = "ExpectedQuantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "ReceivedQuantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_lots", Table = t.Name, Columns = ["LotId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_waves" };
            t.Columns.Add(new AddColumnOperation { Name = "WaveId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "RoundId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "CreatorUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "Label", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(120)", IsNullable = false, MaxLength = 120 });
            t.Columns.Add(new AddColumnOperation { Name = "StatusCode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(40)", IsNullable = false, MaxLength = 40 });
            t.Columns.Add(new AddColumnOperation { Name = "DriverId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = true, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "CutoffUtc", Table = t.Name, ClrType = typeof(DateTime), ColumnType = "datetime(6)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "PickupStartUtc", Table = t.Name, ClrType = typeof(DateTime), ColumnType = "datetime(6)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "PickupEndUtc", Table = t.Name, ClrType = typeof(DateTime), ColumnType = "datetime(6)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "DeliveryStartUtc", Table = t.Name, ClrType = typeof(DateTime), ColumnType = "datetime(6)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "DeliveryEndUtc", Table = t.Name, ClrType = typeof(DateTime), ColumnType = "datetime(6)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "PickupServiceMinutes", Table = t.Name, ClrType = typeof(decimal), ColumnType = "decimal(18,3)", IsNullable = false, Precision = 18, Scale = 3 });
            t.Columns.Add(new AddColumnOperation { Name = "DeliveryServiceMinutes", Table = t.Name, ClrType = typeof(decimal), ColumnType = "decimal(18,3)", IsNullable = false, Precision = 18, Scale = 3 });
            t.Columns.Add(new AddColumnOperation { Name = "RouteJson", Table = t.Name, ClrType = typeof(string), ColumnType = "longtext", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "RouteEvidenceJson", Table = t.Name, ClrType = typeof(string), ColumnType = "longtext", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "TotalElapsedMinutes", Table = t.Name, ClrType = typeof(decimal), ColumnType = "decimal(18,3)", IsNullable = true, Precision = 18, Scale = 3 });
            t.Columns.Add(new AddColumnOperation { Name = "TotalDistanceKm", Table = t.Name, ClrType = typeof(decimal), ColumnType = "decimal(18,3)", IsNullable = true, Precision = 18, Scale = 3 });
            t.Columns.Add(new AddColumnOperation { Name = "OfferedDriverFareKrw", Table = t.Name, ClrType = typeof(decimal), ColumnType = "decimal(18,2)", IsNullable = true, Precision = 18, Scale = 2 });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_waves", Table = t.Name, Columns = ["WaveId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_orders" };
            t.Columns.Add(new AddColumnOperation { Name = "CargoEvidenceDigest", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "OrderId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "WaveId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "OrderReference", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(100)", IsNullable = false, MaxLength = 100 });
            t.Columns.Add(new AddColumnOperation { Name = "CargoRequestId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "SellerUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "CustomerUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "StatusCode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(40)", IsNullable = false, MaxLength = 40 });
            t.Columns.Add(new AddColumnOperation { Name = "CustomerAcknowledged", Table = t.Name, ClrType = typeof(bool), ColumnType = "tinyint(1)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_orders", Table = t.Name, Columns = ["OrderId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_reservations" };
            t.Columns.Add(new AddColumnOperation { Name = "ReservationId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "WaveId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "OrderId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "LotId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "OutboundPlanId", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "Quantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "PickedQuantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "DeliveredQuantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "ReleasedQuantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "ReturnedQuantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_reservations", Table = t.Name, Columns = ["ReservationId"] };
            m.Operations.Add(t);
        }
        {
            var t = new CreateTableOperation { Name = "distributed_receipts" };
            t.Columns.Add(new AddColumnOperation { Name = "ActorUserId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(191)", IsNullable = false, MaxLength = 191 });
            t.Columns.Add(new AddColumnOperation { Name = "RequestId", Table = t.Name, ClrType = typeof(Guid), ColumnType = "char(36)", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "InputDigest", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "Kind", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(40)", IsNullable = false, MaxLength = 40 });
            t.Columns.Add(new AddColumnOperation { Name = "AggregateId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = false, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "ReservationId", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(64)", IsNullable = true, MaxLength = 64 });
            t.Columns.Add(new AddColumnOperation { Name = "Quantity", Table = t.Name, ClrType = typeof(int), ColumnType = "int", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "Revision", Table = t.Name, ClrType = typeof(long), ColumnType = "bigint", IsNullable = false });
            t.Columns.Add(new AddColumnOperation { Name = "StatusCode", Table = t.Name, ClrType = typeof(string), ColumnType = "varchar(40)", IsNullable = false, MaxLength = 40 });
            t.Columns.Add(new AddColumnOperation { Name = "RecordedAtUtc", Table = t.Name, ClrType = typeof(DateTime), ColumnType = "datetime(6)", IsNullable = false });
            t.PrimaryKey = new AddPrimaryKeyOperation { Name = "PK_distributed_receipts", Table = t.Name, Columns = ["ActorUserId", "RequestId"] };
            m.Operations.Add(t);
        }
        m.CreateIndex("IX_distributed_sites_WarehouseId", "distributed_sites", ["WarehouseId"], unique: true);
        m.CreateIndex("IX_distributed_locations_Barcode", "distributed_locations", ["Barcode"], unique: true);
        m.CreateIndex("IX_distributed_locations_SiteId", "distributed_locations", ["SiteId"], unique: false);
        m.CreateIndex("IX_distributed_lots_PlanId_LineKey", "distributed_lots", ["PlanId", "LineKey"], unique: true);
        m.CreateIndex("IX_distributed_lots_InboundItemId", "distributed_lots", ["InboundItemId"], unique: true);
        m.CreateIndex("IX_distributed_lots_OwnerUserId_Sku", "distributed_lots", ["OwnerUserId", "Sku"], unique: false);
        m.CreateIndex("IX_distributed_lots_LocationId", "distributed_lots", ["LocationId"], unique: false);
        m.CreateIndex("IX_distributed_waves_RoundId", "distributed_waves", ["RoundId"], unique: true);
        m.CreateIndex("IX_distributed_orders_CargoRequestId", "distributed_orders", ["CargoRequestId"], unique: true);
        m.CreateIndex("IX_distributed_orders_WaveId", "distributed_orders", ["WaveId"], unique: false);
        m.CreateIndex("IX_distributed_reservations_OutboundPlanId", "distributed_reservations", ["OutboundPlanId"], unique: true);
        m.CreateIndex("IX_distributed_reservations_OrderId_LotId", "distributed_reservations", ["OrderId", "LotId"], unique: true);
        m.CreateIndex("IX_distributed_reservations_LotId", "distributed_reservations", ["LotId"], unique: false);
        ((CreateTableOperation)m.Operations.Single(x => x is CreateTableOperation t && t.Name == "distributed_locations")).ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_distributed_locations_distributed_sites_SiteId", Table = "distributed_locations", Columns = ["SiteId"], PrincipalTable = "distributed_sites", PrincipalColumns = ["SiteId"], OnDelete = ReferentialAction.Restrict });
        ((CreateTableOperation)m.Operations.Single(x => x is CreateTableOperation t && t.Name == "distributed_lots")).ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_distributed_lots_distributed_inbound_plans_PlanId", Table = "distributed_lots", Columns = ["PlanId"], PrincipalTable = "distributed_inbound_plans", PrincipalColumns = ["PlanId"], OnDelete = ReferentialAction.Restrict });
        ((CreateTableOperation)m.Operations.Single(x => x is CreateTableOperation t && t.Name == "distributed_lots")).ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_distributed_lots_distributed_locations_LocationId", Table = "distributed_lots", Columns = ["LocationId"], PrincipalTable = "distributed_locations", PrincipalColumns = ["LocationId"], OnDelete = ReferentialAction.Restrict });
        ((CreateTableOperation)m.Operations.Single(x => x is CreateTableOperation t && t.Name == "distributed_orders")).ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_distributed_orders_distributed_waves_WaveId", Table = "distributed_orders", Columns = ["WaveId"], PrincipalTable = "distributed_waves", PrincipalColumns = ["WaveId"], OnDelete = ReferentialAction.Restrict });
        ((CreateTableOperation)m.Operations.Single(x => x is CreateTableOperation t && t.Name == "distributed_reservations")).ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_distributed_reservations_distributed_orders_OrderId", Table = "distributed_reservations", Columns = ["OrderId"], PrincipalTable = "distributed_orders", PrincipalColumns = ["OrderId"], OnDelete = ReferentialAction.Restrict });
        ((CreateTableOperation)m.Operations.Single(x => x is CreateTableOperation t && t.Name == "distributed_reservations")).ForeignKeys.Add(new AddForeignKeyOperation { Name = "FK_distributed_reservations_distributed_lots_LotId", Table = "distributed_reservations", Columns = ["LotId"], PrincipalTable = "distributed_lots", PrincipalColumns = ["LotId"], OnDelete = ReferentialAction.Restrict });
    }
    protected override void Down(MigrationBuilder m) => throw new NotSupportedException("실물 보관·인수 영수증이 있는 분산 물류 원장은 자동 제거하지 않습니다.");
}
