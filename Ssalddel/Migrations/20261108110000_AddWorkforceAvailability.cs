using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;
namespace Ssalddel.Migrations;
[DbContext(typeof(SsalddelContext))]
[Migration("20261108110000_AddWorkforceAvailability")]
public sealed partial class AddWorkforceAvailability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "hr_work_contact_preferences", columns: table => new
        {
                UserId = table.Column<string>(type: "varchar(450)", nullable: false, maxLength: 450),
                AllowInApp = table.Column<bool>(type: "tinyint(1)", nullable: false),
                AllowKakao = table.Column<bool>(type: "tinyint(1)", nullable: false),
                Revision = table.Column<Guid>(type: "char(36)", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table =>
        {
                table.PrimaryKey("PK_hr_work_contact_preferences", x => x.UserId);
        }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateTable(name: "hr_work_availability_requests", columns: table => new
        {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                ManagerUserId = table.Column<string>(type: "varchar(450)", nullable: false, maxLength: 450),
                ClientRequestId = table.Column<Guid>(type: "char(36)", nullable: false),
                PayloadHash = table.Column<string>(type: "varchar(64)", nullable: false, maxLength: 64),
                ActiveKey = table.Column<string>(type: "varchar(64)", nullable: true, maxLength: 64),
                WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                WarehouseName = table.Column<string>(type: "varchar(200)", nullable: false, maxLength: 200),
                Stage = table.Column<string>(type: "varchar(32)", nullable: false, maxLength: 32),
                GroupKey = table.Column<string>(type: "varchar(200)", nullable: false, maxLength: 200),
                GroupLabel = table.Column<string>(type: "varchar(300)", nullable: false, maxLength: 300),
                GroupPage = table.Column<int>(type: "int", nullable: false),
                RemainingQuantity = table.Column<long>(type: "bigint", nullable: false),
                NeededWorkers = table.Column<int>(type: "int", nullable: false),
                ConfirmedWorkers = table.Column<int>(type: "int", nullable: false),
                WorkStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                WorkEndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ReplyByUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                SourceObservedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PeriodStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                PeriodEndUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ProposedHourlyAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                Channel = table.Column<string>(type: "varchar(32)", nullable: false, maxLength: 32),
                Revision = table.Column<Guid>(type: "char(36)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table =>
        {
                table.PrimaryKey("PK_hr_work_availability_requests", x => x.Id);
        }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_hr_work_availability_requests_ManagerUserId_ClientRequestId", "hr_work_availability_requests", new[] { "ManagerUserId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_hr_work_availability_requests_ActiveKey", "hr_work_availability_requests", new[] { "ActiveKey" }, unique: true);
        migrationBuilder.CreateIndex("IX_hr_work_availability_requests_WarehouseId_CreatedAtUtc", "hr_work_availability_requests", new[] { "WarehouseId", "CreatedAtUtc" });
        migrationBuilder.CreateTable(name: "hr_work_availability_recipients", columns: table => new
        {
                Id = table.Column<Guid>(type: "char(36)", nullable: false),
                RequestId = table.Column<Guid>(type: "char(36)", nullable: false),
                UserId = table.Column<string>(type: "varchar(450)", nullable: false, maxLength: 450),
                DisplayName = table.Column<string>(type: "varchar(200)", nullable: false, maxLength: 200),
                Status = table.Column<string>(type: "varchar(32)", nullable: false, maxLength: 32),
                DeliveryStatus = table.Column<string>(type: "varchar(32)", nullable: false, maxLength: 32),
                RespondedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                Revision = table.Column<Guid>(type: "char(36)", nullable: false)
        }, constraints: table =>
        {
                table.PrimaryKey("PK_hr_work_availability_recipients", x => x.Id);
                table.ForeignKey("FK_hr_work_recipients_request", x => x.RequestId, "hr_work_availability_requests", "Id", onDelete: ReferentialAction.Restrict);
        }).Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_hr_work_availability_recipients_RequestId_UserId", "hr_work_availability_recipients", new[] { "RequestId", "UserId" }, unique: true);
        migrationBuilder.CreateIndex("IX_hr_work_availability_recipients_UserId_Status", "hr_work_availability_recipients", new[] { "UserId", "Status" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("hr_work_availability_recipients");
        migrationBuilder.DropTable("hr_work_availability_requests");
        migrationBuilder.DropTable("hr_work_contact_preferences");
    }
}
