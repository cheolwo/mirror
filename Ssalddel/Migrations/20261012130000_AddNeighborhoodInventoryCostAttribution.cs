using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261012130000_AddNeighborhoodInventoryCostAttribution")]
public sealed class AddNeighborhoodInventoryCostAttribution : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(
            name: "NeighborhoodInventoryCostAttributions",
            columns: table => new
            {
                AttributionId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                SourceEntryId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                SourceAgreementId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                OrderId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                AgreementId = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false),
                CalculationDigest = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                DetailsJson = table.Column<string>(type: "longtext", nullable: false),
                RecordedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_NeighborhoodInventoryCostAttributions", x => x.AttributionId));
        m.CreateIndex(name: "IX_neighborhood_inventory_cost_source_order",
            table: "NeighborhoodInventoryCostAttributions", columns: ["SourceEntryId", "OrderId"], unique: true);
        m.CreateIndex(name: "IX_neighborhood_inventory_cost_source_agreement",
            table: "NeighborhoodInventoryCostAttributions", column: "SourceAgreementId");
    }

    protected override void Down(MigrationBuilder m)
        => throw new NotSupportedException("주문별 원가 귀속 근거는 별도 보존·대사 절차 없이 삭제할 수 없습니다.");
}
