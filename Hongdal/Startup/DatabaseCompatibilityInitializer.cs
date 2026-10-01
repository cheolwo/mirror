using System.Data.Common;
using Hongdal.Infrastructure.Persistence.AgriculturalFisheries;
using Hongdal.Infrastructure.Persistence.TraditionalMarkets;
using Hongdal.Services.Development;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;
using 홍달.Services.Documents;
using 홍달.Services.ViewSettings;

namespace Hongdal.Startup;

internal static class DatabaseCompatibilityInitializer
{
    internal static async Task InitializeAsync(
        HongdalContext db,
        IServiceProvider services,
        IWebHostEnvironment environment,
        ILogger logger,
        bool failOnError)
    {
        var traditionalMarketDb = services.GetRequiredService<TraditionalMarketDbContext>();
        var agriculturalFisheriesDb = services.GetRequiredService<AgriculturalFisheriesDbContext>();
        var migrationDelays = new[]
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20),
            TimeSpan.FromSeconds(30)
        };

        for (var attempt = 0; attempt <= migrationDelays.Length; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                await traditionalMarketDb.Database.MigrateAsync();
                await agriculturalFisheriesDb.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < migrationDelays.Length)
            {
                var delay = migrationDelays[attempt];
                logger.LogWarning(ex, "MySQL migration failed on attempt {Attempt}. Retrying in {Delay}.", attempt + 1, delay);
                await Task.Delay(delay);
            }
            catch (Exception ex)
            {
                if (failOnError)
                {
                    throw new InvalidOperationException(
                        $"MySQL migration failed after {attempt + 1} attempts.",
                        ex);
                }

                logger.LogWarning(ex, "MySQL migration failed after {Attempt} attempts. Application will continue without applying migrations at startup.", attempt + 1);
                return;
            }
        }

        await EnsureIdentityCompatibilityAsync(db, logger);
        await EnsureVehicleRateCompatibilityAsync(db, logger);
        await EnsureHrRoleAssignmentCompatibilityAsync(db, logger);
        await EnsureHrEmploymentContractCompatibilityAsync(db, logger);
        await EnsurePlatformProfitReturnCompatibilityAsync(db, logger);

        try
        {
            await IdentityDataSeeder.SeedAsync(services);
            var viewVisibilityService = services.GetRequiredService<IView가시성Service>();
            await viewVisibilityService.SeedPoliciesAsync();
            var documentService = services.GetRequiredService<I문서관리Service>();
            await documentService.SeedDefaultsAsync();
            if (environment.IsDevelopment())
            {
                await HongdalV1DevelopmentDataSeeder.SeedAsync(services, logger);
                await CommunityLedgerDevelopmentDataSeeder.SeedAsync(services, logger);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Initial data seeding failed after database migration.");
        }
    }

    private static async Task EnsureIdentityCompatibilityAsync(HongdalContext db, ILogger logger)
    {
        var connection = db.Database.GetDbConnection();

        try
        {
            await db.Database.OpenConnectionAsync();

            if (!await ColumnExistsAsync(connection, "AspNetUsers", "BusinessRegistrationNumber"))
            {
                await using var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE `AspNetUsers` ADD COLUMN `BusinessRegistrationNumber` varchar(256) NULL;";
                await alterCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Added missing column AspNetUsers.BusinessRegistrationNumber.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Identity schema compatibility check failed.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string tableName, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
  AND COLUMN_NAME = @columnName;";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var columnParam = command.CreateParameter();
        columnParam.ParameterName = "@columnName";
        columnParam.Value = columnName;
        command.Parameters.Add(columnParam);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task EnsureVehicleRateCompatibilityAsync(HongdalContext db, ILogger logger)
    {
        var connection = db.Database.GetDbConnection();

        try
        {
            await db.Database.OpenConnectionAsync();

            if (!await TableExistsAsync(connection, "차량단가"))
            {
                await using var createCommand = connection.CreateCommand();
                createCommand.CommandText = @"
CREATE TABLE `차량단가` (
    `차량종류` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    PRIMARY KEY (`차량종류`)
) CHARACTER SET=utf8mb4;";
                await createCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table 차량단가.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Vehicle rate schema compatibility check failed.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureHrRoleAssignmentCompatibilityAsync(HongdalContext db, ILogger logger)
    {
        var connection = db.Database.GetDbConnection();

        try
        {
            await db.Database.OpenConnectionAsync();

            if (!await TableExistsAsync(connection, "hr_role_assignments"))
            {
                await using var createCommand = connection.CreateCommand();
                createCommand.CommandText = @"
CREATE TABLE `hr_role_assignments` (
    `id` char(36) COLLATE ascii_general_ci NOT NULL,
    `user_id` varchar(450) NOT NULL,
    `scope_type` varchar(100) NOT NULL,
    `scope_id` varchar(200) NOT NULL,
    `participant_category` varchar(100) NOT NULL,
    `role_code` varchar(100) NOT NULL,
    `role_name` varchar(200) NOT NULL,
    `is_active` tinyint(1) NOT NULL,
    `assigned_at_utc` datetime(6) NOT NULL,
    `assigned_by_user_id` varchar(450) NOT NULL,
    `work_schedule_enabled` tinyint(1) NOT NULL,
    `time_zone_id` varchar(100) NOT NULL,
    `allowed_days_of_week` varchar(100) NOT NULL,
    `work_start_local_time` varchar(16) NULL,
    `work_end_local_time` varchar(16) NULL,
    `worksite_ip_restriction_enabled` tinyint(1) NOT NULL,
    `allowed_worksite_ip_ranges` varchar(2000) NOT NULL,
    `created_at` datetime(6) NOT NULL,
    `updated_at` datetime(6) NOT NULL,
    PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;";
                await createCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table hr_role_assignments.");
            }

            if (!await IndexExistsAsync(connection, "hr_role_assignments", "IX_hr_role_assignments_user_scope_role_active"))
            {
                await using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = @"
CREATE INDEX `IX_hr_role_assignments_user_scope_role_active`
ON `hr_role_assignments` (`user_id`, `scope_type`, `role_code`, `is_active`);";
                await indexCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing index IX_hr_role_assignments_user_scope_role_active.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HR role assignment schema compatibility check failed.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureHrEmploymentContractCompatibilityAsync(HongdalContext db, ILogger logger)
    {
        var connection = db.Database.GetDbConnection();

        try
        {
            await db.Database.OpenConnectionAsync();

            if (!await TableExistsAsync(connection, "hr_employment_contracts"))
            {
                await using var createContractsCommand = connection.CreateCommand();
                createContractsCommand.CommandText = @"
CREATE TABLE `hr_employment_contracts` (
    `id` char(36) COLLATE ascii_general_ci NOT NULL,
    `worker_user_id` varchar(450) NOT NULL,
    `worker_name` varchar(200) NOT NULL,
    `employer_scope_type` varchar(100) NOT NULL,
    `employer_scope_id` varchar(200) NOT NULL,
    `employer_name` varchar(200) NOT NULL,
    `contract_type` varchar(100) NOT NULL,
    `contract_status` varchar(100) NOT NULL,
    `contract_start_date` date NOT NULL,
    `contract_end_date` date NULL,
    `work_description` varchar(1000) NOT NULL,
    `wage_type` varchar(100) NOT NULL,
    `wage_amount` decimal(18,2) NOT NULL,
    `minimum_wage_amount` decimal(18,2) NULL,
    `minimum_wage_check_passed` tinyint(1) NOT NULL,
    `minimum_wage_check_message` varchar(1000) NOT NULL,
    `payment_cycle` varchar(100) NOT NULL,
    `payment_day_of_month` int NOT NULL,
    `payment_method` varchar(100) NOT NULL,
    `bank_name` varchar(100) NOT NULL,
    `account_number` varchar(200) NOT NULL,
    `account_holder_name` varchar(100) NOT NULL,
    `signed_at_utc` datetime(6) NULL,
    `signed_by_user_id` varchar(450) NOT NULL,
    `memo` varchar(2000) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;";
                await createContractsCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table hr_employment_contracts.");
            }

            if (!await TableExistsAsync(connection, "hr_payroll_schedules"))
            {
                await using var createSchedulesCommand = connection.CreateCommand();
                createSchedulesCommand.CommandText = @"
CREATE TABLE `hr_payroll_schedules` (
    `id` char(36) COLLATE ascii_general_ci NOT NULL,
    `contract_id` char(36) COLLATE ascii_general_ci NOT NULL,
    `worker_user_id` varchar(450) NOT NULL,
    `employer_scope_type` varchar(100) NOT NULL,
    `employer_scope_id` varchar(200) NOT NULL,
    `work_period_start_date` date NOT NULL,
    `work_period_end_date` date NOT NULL,
    `scheduled_payment_date` date NOT NULL,
    `planned_amount` decimal(18,2) NOT NULL,
    `currency_code` varchar(10) NOT NULL,
    `payment_method` varchar(100) NOT NULL,
    `status` varchar(100) NOT NULL,
    `memo` varchar(1000) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `FK_hr_payroll_schedules_hr_employment_contracts_contract_id`
        FOREIGN KEY (`contract_id`) REFERENCES `hr_employment_contracts` (`id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;";
                await createSchedulesCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table hr_payroll_schedules.");
            }

            if (!await IndexExistsAsync(connection, "hr_employment_contracts", "IX_hr_employment_contracts_worker_scope_status"))
            {
                await using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = @"
CREATE INDEX `IX_hr_employment_contracts_worker_scope_status`
ON `hr_employment_contracts` (`worker_user_id`, `employer_scope_type`, `contract_status`);";
                await indexCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing index IX_hr_employment_contracts_worker_scope_status.");
            }

            if (!await IndexExistsAsync(connection, "hr_payroll_schedules", "IX_hr_payroll_schedules_worker_payment_status"))
            {
                await using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = @"
CREATE INDEX `IX_hr_payroll_schedules_worker_payment_status`
ON `hr_payroll_schedules` (`worker_user_id`, `scheduled_payment_date`, `status`);";
                await indexCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing index IX_hr_payroll_schedules_worker_payment_status.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HR employment contract schema compatibility check failed.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsurePlatformProfitReturnCompatibilityAsync(HongdalContext db, ILogger logger)
    {
        var connection = db.Database.GetDbConnection();

        try
        {
            await db.Database.OpenConnectionAsync();

            if (!await TableExistsAsync(connection, "platform_revenue_entries"))
            {
                await using var createCommand = connection.CreateCommand();
                createCommand.CommandText = @"
CREATE TABLE `platform_revenue_entries` (
    `id` char(36) COLLATE ascii_general_ci NOT NULL,
    `revenue_source` varchar(100) NOT NULL,
    `source_reference_type` varchar(100) NOT NULL,
    `source_reference_id` varchar(200) NOT NULL,
    `payer_user_id` varchar(450) NOT NULL,
    `related_participant_user_id` varchar(450) NOT NULL,
    `gross_amount` decimal(18,2) NOT NULL,
    `platform_revenue_amount` decimal(18,2) NOT NULL,
    `currency_code` varchar(10) NOT NULL,
    `occurred_at_utc` datetime(6) NOT NULL,
    `memo` varchar(1000) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;";
                await createCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table platform_revenue_entries.");
            }

            if (!await TableExistsAsync(connection, "platform_profit_return_policies"))
            {
                await using var createCommand = connection.CreateCommand();
                createCommand.CommandText = @"
CREATE TABLE `platform_profit_return_policies` (
    `id` char(36) COLLATE ascii_general_ci NOT NULL,
    `policy_name` varchar(200) NOT NULL,
    `target_participant_category` varchar(100) NOT NULL,
    `return_rate_percent` decimal(9,4) NOT NULL,
    `company_reserve_amount` decimal(18,2) NOT NULL,
    `minimum_profit_threshold` decimal(18,2) NOT NULL,
    `effective_start_date` date NOT NULL,
    `effective_end_date` date NULL,
    `is_active` tinyint(1) NOT NULL,
    `memo` varchar(1000) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    PRIMARY KEY (`id`)
) CHARACTER SET=utf8mb4;";
                await createCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table platform_profit_return_policies.");
            }

            if (!await TableExistsAsync(connection, "platform_profit_return_schedules"))
            {
                await using var createCommand = connection.CreateCommand();
                createCommand.CommandText = @"
CREATE TABLE `platform_profit_return_schedules` (
    `id` char(36) COLLATE ascii_general_ci NOT NULL,
    `policy_id` char(36) COLLATE ascii_general_ci NOT NULL,
    `participant_user_id` varchar(450) NOT NULL,
    `participant_name` varchar(200) NOT NULL,
    `participant_category` varchar(100) NOT NULL,
    `period_start_date` date NOT NULL,
    `period_end_date` date NOT NULL,
    `scheduled_payment_date` date NOT NULL,
    `total_platform_revenue_amount` decimal(18,2) NOT NULL,
    `operating_cost_amount` decimal(18,2) NOT NULL,
    `estimated_profit_amount` decimal(18,2) NOT NULL,
    `return_pool_amount` decimal(18,2) NOT NULL,
    `participant_weight` decimal(18,4) NOT NULL,
    `planned_return_amount` decimal(18,2) NOT NULL,
    `status` varchar(100) NOT NULL,
    `memo` varchar(1000) NOT NULL,
    `created_at_utc` datetime(6) NOT NULL,
    `updated_at_utc` datetime(6) NOT NULL,
    PRIMARY KEY (`id`),
    CONSTRAINT `FK_platform_profit_return_schedules_policies_policy_id`
        FOREIGN KEY (`policy_id`) REFERENCES `platform_profit_return_policies` (`id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;";
                await createCommand.ExecuteNonQueryAsync();
                logger.LogWarning("Created missing table platform_profit_return_schedules.");
            }

            if (!await IndexExistsAsync(connection, "platform_revenue_entries", "IX_platform_revenue_entries_source_occurred"))
            {
                await using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = @"
CREATE INDEX `IX_platform_revenue_entries_source_occurred`
ON `platform_revenue_entries` (`revenue_source`, `occurred_at_utc`);";
                await indexCommand.ExecuteNonQueryAsync();
            }

            if (!await IndexExistsAsync(connection, "platform_profit_return_schedules", "IX_platform_profit_return_schedules_participant_payment_status"))
            {
                await using var indexCommand = connection.CreateCommand();
                indexCommand.CommandText = @"
CREATE INDEX `IX_platform_profit_return_schedules_participant_payment_status`
ON `platform_profit_return_schedules` (`participant_user_id`, `scheduled_payment_date`, `status`);";
                await indexCommand.ExecuteNonQueryAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Platform profit return schema compatibility check failed.");
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName;";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }

    private static async Task<bool> IndexExistsAsync(DbConnection connection, string tableName, string indexName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = @tableName
  AND INDEX_NAME = @indexName;";

        var tableParam = command.CreateParameter();
        tableParam.ParameterName = "@tableName";
        tableParam.Value = tableName;
        command.Parameters.Add(tableParam);

        var indexParam = command.CreateParameter();
        indexParam.ParameterName = "@indexName";
        indexParam.Value = indexName;
        command.Parameters.Add(indexParam);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result) > 0;
    }
}
