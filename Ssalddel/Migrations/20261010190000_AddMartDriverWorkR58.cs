using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using 살뜰.Data;

#nullable disable
namespace Ssalddel.Migrations;

[DbContext(typeof(SsalddelContext)), Migration("20261010190000_AddMartDriverWorkR58")]
public sealed class AddMartDriverWorkR58 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("마트배송시도", columns: table => new
        {
            시도StableId = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            배차의뢰Id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            주문참조번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            기사Id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            추천라운드 = table.Column<int>(type: "int", nullable: false),
            픽업전표Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true).Annotation("MySql:CharSet", "utf8mb4"),
            픽업전표Revision = table.Column<long>(type: "bigint", nullable: true),
            수락시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            픽업시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            전달시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_마트배송시도", x => x.시도StableId))
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_마트배송시도_배차의뢰Id", "마트배송시도", "배차의뢰Id", unique: true);
        migrationBuilder.CreateIndex("IX_마트배송시도_기사Id_상태Code_UpdatedAtUtc", "마트배송시도", new[] { "기사Id", "상태Code", "UpdatedAtUtc" });
        migrationBuilder.CreateTable("마트포장전표", columns: table => new
        {
            주문참조번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            전표Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            Revision = table.Column<long>(type: "bigint", nullable: false),
            보호코드Json = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            QrDigest = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            활성여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
            발급시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            픽업시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            확인실패횟수 = table.Column<int>(type: "int", nullable: false),
            번호차단만료시각Utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
        }, constraints: table => table.PrimaryKey("PK_마트포장전표", x => x.주문참조번호))
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_마트포장전표_전표Id", "마트포장전표", "전표Id", unique: true);
        migrationBuilder.CreateTable("마트기사요청영수증", columns: table => new
        {
            작업자UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            클라이언트요청Id = table.Column<Guid>(type: "char(36)", nullable: false),
            작업Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            배차의뢰Id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            주문참조번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            보호입력Digest = table.Column<string>(type: "longtext", nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            성공여부 = table.Column<bool>(type: "tinyint(1)", nullable: false),
            결과Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            결과StatusCode = table.Column<int>(type: "int", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_마트기사요청영수증", x => new { x.작업자UserId, x.클라이언트요청Id }))
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_마트기사요청영수증_CreatedAtUtc", "마트기사요청영수증", "CreatedAtUtc");
        migrationBuilder.CreateTable("마트기사후속Outbox", columns: table => new
        {
            멱등키 = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            배차의뢰Id = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            주문참조번호 = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            기사Id = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            시도StableId = table.Column<string>(type: "varchar(220)", maxLength: 220, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            업무Revision = table.Column<long>(type: "bigint", nullable: false),
            종류Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            상태Code = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
            시도횟수 = table.Column<int>(type: "int", nullable: false),
            ClaimRevision = table.Column<long>(type: "bigint", nullable: false),
            LeaseOwner = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true).Annotation("MySql:CharSet", "utf8mb4"),
            LeaseUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            NextAttemptAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
            마지막실패Code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true).Annotation("MySql:CharSet", "utf8mb4"),
            CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
            UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_마트기사후속Outbox", x => x.멱등키))
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.CreateIndex("IX_마트기사후속Outbox_상태Code_NextAttemptAtUtc_CreatedAtUtc", "마트기사후속Outbox", new[] { "상태Code", "NextAttemptAtUtc", "CreatedAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("마트배송시도");
        migrationBuilder.DropTable("마트포장전표");
        migrationBuilder.DropTable("마트기사요청영수증");
        migrationBuilder.DropTable("마트기사후속Outbox");
    }
}
