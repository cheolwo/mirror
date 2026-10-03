using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations;

public partial class 음식배달제안계산근거 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "기사픽업지급액", table: "음식운영정책",
            type: "decimal(18,2)", nullable: true);
        migrationBuilder.AddColumn<string>(name: "driver_offer_calculation_json", table: "운송실행투영",
            type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4");

        // 기존 시작 시 호환 검사에서 이미 생성했을 수 있는 한시 수요 할증 열을 migration 이력과 결속한다.
        // 존재하는 열/값은 변경하지 않으며 Down에서도 이 기존 기능의 열을 삭제하지 않는다.
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증_client_request_id", "varchar(36) NOT NULL DEFAULT ''");
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증_revision", "bigint NOT NULL DEFAULT 0");
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증범위_code", "varchar(80) NOT NULL DEFAULT 'AllFoodDelivery'");
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증사유_code", "varchar(80) NOT NULL DEFAULT ''");
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증시작일시_utc", "datetime(6) NULL");
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증액", "decimal(18,2) NOT NULL DEFAULT 0.00");
        AddCompatibilityColumn(migrationBuilder, "음식운영정책", "기사한시수요할증종료일시_utc", "datetime(6) NULL");
        AddCompatibilityColumn(migrationBuilder, "운송실행투영", "driver_temporary_demand_surcharge", "decimal(18,2) NULL");
        AddCompatibilityColumn(migrationBuilder, "운송실행투영", "driver_temporary_demand_surcharge_applied", "tinyint(1) NOT NULL DEFAULT 0");
    }

    private static void AddCompatibilityColumn(MigrationBuilder migrationBuilder, string table, string column, string definition)
    {
        // 고정 schema 식별자만 호출한다. 정보 조회는 현재 연결의 DATABASE()로 한정한다.
        var alter = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}".Replace("'", "''");
        migrationBuilder.Sql($"""
            SET @food_offer_schema_sql = IF(
                EXISTS(SELECT 1 FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'),
                'SELECT 1', '{alter}');
            PREPARE food_offer_schema_statement FROM @food_offer_schema_sql;
            EXECUTE food_offer_schema_statement;
            DEALLOCATE PREPARE food_offer_schema_statement;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "기사픽업지급액", table: "음식운영정책");
        migrationBuilder.DropColumn(name: "driver_offer_calculation_json", table: "운송실행투영");
    }
}
