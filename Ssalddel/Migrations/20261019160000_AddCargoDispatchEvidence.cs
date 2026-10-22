using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable

namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext))]
[Migration("20261019160000_AddCargoDispatchEvidence")]
public sealed partial class AddCargoDispatchEvidence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "실차적재제원Json", table: "용달기사", type: "longtext", nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<long>(name: "실차적재판본", table: "용달기사", type: "bigint", nullable: true);
        migrationBuilder.AddColumn<string>(name: "recommendation_decision_evidence_json", table: "운송실행투영", type: "longtext", nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "실차적재제원Json", table: "용달기사");
        migrationBuilder.DropColumn(name: "실차적재판본", table: "용달기사");
        migrationBuilder.DropColumn(name: "recommendation_decision_evidence_json", table: "운송실행투영");
    }
}
