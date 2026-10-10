using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable
namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext)), Migration("20261009160000_AddDriverWorkStartReliabilityR50")]
public sealed class AddDriverWorkStartReliabilityR50 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("work_start_client_request_id", "driver_shifts", type: "char(36)", nullable: true);
        migrationBuilder.AddColumn<string>("work_start_request_key", "driver_shifts", type: "varchar(64)", maxLength: 64, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<string>("work_start_input_fingerprint", "driver_shifts", type: "varchar(64)", maxLength: 64, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<string>("active_driver_id", "driver_shifts", type: "varchar(450)", maxLength: 450, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<DateTime>("stopped_at_utc", "driver_shifts", type: "datetime(6)", nullable: true);
        migrationBuilder.AddColumn<string>("start_return_preference", "driver_shifts", type: "varchar(32)", maxLength: 32, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_driver_shifts_WorkStartRequestKey", "driver_shifts", "work_start_request_key", unique: true);
        migrationBuilder.CreateIndex("IX_driver_shifts_ActiveDriverId", "driver_shifts", "active_driver_id", unique: true);
        migrationBuilder.CreateTable("driver_work_start_followups", columns: table => new
        {
            ShiftId = table.Column<long>(type: "bigint", nullable: false),
            DriverId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            Status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            AttemptCount = table.Column<int>(type: "int", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            LastFailureCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true).Annotation("MySql:CharSet", "utf8mb4")
        }, constraints: table =>
        {
            table.PrimaryKey("PK_driver_work_start_followups", x => x.ShiftId);
            table.ForeignKey("FK_driver_work_start_followups_driver_shifts_ShiftId", x => x.ShiftId,
                "driver_shifts", "id", onDelete: ReferentialAction.Cascade);
        }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_driver_work_start_followups_Status_NextAttemptAtUtc",
            "driver_work_start_followups", new[] { "Status", "NextAttemptAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("driver_work_start_followups");
        migrationBuilder.DropIndex("IX_driver_shifts_WorkStartRequestKey", "driver_shifts");
        migrationBuilder.DropIndex("IX_driver_shifts_ActiveDriverId", "driver_shifts");
        foreach (var column in new[] { "work_start_client_request_id", "work_start_request_key", "work_start_input_fingerprint",
            "active_driver_id", "stopped_at_utc", "start_return_preference" })
            migrationBuilder.DropColumn(column, "driver_shifts");
    }
}
