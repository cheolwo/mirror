using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261019100000_운송자격검토준비")]
public sealed partial class 운송자격검토준비 : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>(name: "현재실차참조", table: "용달기사", type: "varchar(1000)", maxLength: 1000, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        m.AddColumn<string>(name: "현재실차참조", table: "배달기사", type: "varchar(1000)", maxLength: 1000, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        m.CreateTable(name: "driver_transport_qualification", columns: t => new
        {
            DriverId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            Revision = t.Column<int>(type: "int", nullable: false),
            Status = t.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            PolicyVersion = t.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            VehicleKind = t.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            VehicleProfileValue = t.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            PrivateDetailsJson = t.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            HistoryJson = t.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            SubmittedAtUtc = t.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
            ReviewedAtUtc = t.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
            RecheckAtUtc = t.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
            ReviewerId = t.Column<string>(type: "varchar(191)", maxLength: 191, nullable: true).Annotation("MySql:CharSet", "utf8mb4")
        }, constraints: t => t.PrimaryKey("PK_driver_transport_qualification", x => x.DriverId))
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("자격 검토 근거·정지 이력을 임의로 삭제하지 않습니다. 별도의 보존·복구 검토가 필요합니다.");
}
