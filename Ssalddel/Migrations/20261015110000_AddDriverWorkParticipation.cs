using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261015110000_AddDriverWorkParticipation")]
public sealed class AddDriverWorkParticipation : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "driver_work_participation", columns: t => new
        {
            UserId = t.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
            Revision = t.Column<long>(type: "bigint", nullable: false),
            WorkLockRevision = t.Column<long>(type: "bigint", nullable: false),
            FoodDelivery = t.Column<bool>(type: "tinyint(1)", nullable: false),
            QuickDelivery = t.Column<bool>(type: "tinyint(1)", nullable: false),
            CargoTransport = t.Column<bool>(type: "tinyint(1)", nullable: false),
            DirectPicking = t.Column<bool>(type: "tinyint(1)", nullable: false),
            VehicleKind = t.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
            MaxConcurrentJobs = t.Column<int>(type: "int", nullable: false),
            CapacityKg = t.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
            CapacityLitres = t.Column<decimal>(type: "decimal(12,3)", precision: 12, scale: 3, nullable: true),
            FoodCargoSeparationConfirmed = t.Column<bool>(type: "tinyint(1)", nullable: false),
            ReceiveAdditionalOffers = t.Column<bool>(type: "tinyint(1)", nullable: false),
            UpdatedAtUtc = t.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
        }, constraints: t => t.PrimaryKey("PK_driver_work_participation", x => x.UserId));
    }

    protected override void Down(MigrationBuilder m) => m.DropTable("driver_work_participation");
}
