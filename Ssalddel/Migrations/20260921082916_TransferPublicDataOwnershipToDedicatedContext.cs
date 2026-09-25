using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ssalddel.Migrations
{
    /// <summary>
    /// 공공데이터 테이블의 모델 소유권을 중앙 Context에서 전용 Context로 이전합니다.
    /// 실제 테이블과 데이터는 변경하지 않고 모델 스냅샷의 소유권만 정리합니다.
    /// </summary>
    public partial class TransferPublicDataOwnershipToDedicatedContext : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 과거 중앙 Context migration이 만든 공공데이터 스키마를 전용 Context가
            // 다시 만들지 않도록 이력만 인계합니다. 스키마가 완전하지 않으면 아무것도
            // 기록하지 않아 전용 migration이 결손을 명시적으로 드러내게 합니다.
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory_PublicDataIngestion` (
                    `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
                    `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
                    CONSTRAINT `PK___EFMigrationsHistory_PublicDataIngestion` PRIMARY KEY (`MigrationId`)
                ) CHARACTER SET=utf8mb4;

                INSERT IGNORE INTO `__EFMigrationsHistory_PublicDataIngestion` (`MigrationId`, `ProductVersion`)
                SELECT migration_id, '9.0.0'
                FROM (
                    SELECT '20260808131958_AddExternalPublicDataIngestionFoundation' AS migration_id
                    UNION ALL SELECT '20260808134522_AddExternalDataTemporalPrecision'
                    UNION ALL SELECT '20260808134705_SeedWorldBankCountryRegionMappings'
                    UNION ALL SELECT '20260812234011_AddAdministrativeBuildingClassificationLedger'
                    UNION ALL SELECT '20260812235607_AddBuildingMassingAndVisualComposition'
                    UNION ALL SELECT '20260813001414_AddPublicLicensedBusinessBuildingLedger'
                ) AS inherited_migrations
                WHERE (
                    SELECT COUNT(*)
                    FROM information_schema.tables
                    WHERE table_schema = DATABASE()
                      AND table_name IN (
                          'public_data_ingestion_runs',
                          'public_data_raw_snapshots',
                          'public_data_normalized_records',
                          'public_data_region_mappings',
                          'public_building_category_catalog',
                          'public_building_register_titles',
                          'public_building_region_assignments',
                          'public_building_category_assignments',
                          'public_administrative_building_category_aggregates',
                          'public_building_massing_profiles',
                          'public_building_visual_composition_plans',
                          'public_licensed_business_records',
                          'public_business_building_assignments',
                          'public_building_business_aggregates'
                      )
                ) = 14
                  AND EXISTS (
                      SELECT 1
                      FROM information_schema.columns
                      WHERE table_schema = DATABASE()
                        AND table_name = 'public_data_normalized_records'
                        AND column_name = 'TemporalPrecisionCode'
                  )
                  AND (
                      SELECT COUNT(*)
                      FROM `public_data_region_mappings`
                      WHERE `Id` IN (-1001, -1002, -1003)
                  ) = 3;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 소유권 경계 변경은 데이터 정의 변경이 아니므로 역방향 작업도 없습니다.
        }
    }
}
