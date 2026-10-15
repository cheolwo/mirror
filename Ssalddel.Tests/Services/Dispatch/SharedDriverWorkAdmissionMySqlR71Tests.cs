using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Ssalddel.Services.Dispatch.Participation;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.기사;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Services.Dispatch;

public sealed class SharedDriverWorkAdmissionMySqlR71Tests
{
    [SharedDriverMySqlR71Fact]
    [Trait("Category", "LocalMySqlIntegration")]
    public async Task 프로세스잠금없이두연결의SQL행잠금으로마지막한자리수락을직렬화한다()
    {
        var input = Environment.GetEnvironmentVariable(SharedDriverMySqlR71FactAttribute.Variable)!;
        var adminSettings = new MySqlConnectionStringBuilder(input) { Database = "", Pooling = false };
        Assert.True(adminSettings.Server is "127.0.0.1" or "localhost" or "::1" or "[::1]",
            "이 시험은 로컬 MySQL 연결만 허용합니다.");
        var databaseName = "mirror_r71_" + Guid.NewGuid().ToString("N");
        RequireIsolatedName(databaseName);
        await using var admin = new MySqlConnection(adminSettings.ConnectionString);
        await admin.OpenAsync();
        var created = false;
        try
        {
            await using (var create = admin.CreateCommand())
            {
                create.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4";
                await create.ExecuteNonQueryAsync(); created = true;
            }
            var isolated = new MySqlConnectionStringBuilder(adminSettings.ConnectionString)
            { Database = databaseName, AllowUserVariables = true, UseAffectedRows = false, Pooling = false };
            DbContextOptions<SsalddelContext> DbOptions() => new DbContextOptionsBuilder<SsalddelContext>()
                .UseMySql(isolated.ConnectionString, new MySqlServerVersion(new Version(8, 4, 0))).Options;
            SsalddelContext OpenDb() => new(DbOptions(), new Encryption());
            await using (var schema = OpenDb())
            {
                var model = schema.GetService<IDesignTimeModel>().Model;
                Type[] entities = [typeof(DriverWorkParticipationRecord), typeof(운송원장), typeof(화주운송의뢰),
                    typeof(분산배송WaveRecord), typeof(분산배송주문Record), typeof(분산재고예약Record)];
                var tableNames = entities.Select(x => model.FindEntityType(x)!.GetTableName()!).ToHashSet(StringComparer.Ordinal);
                Assert.Equal(6, tableNames.Count);
                var tables = schema.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel())
                    .OfType<CreateTableOperation>().Where(x => tableNames.Contains(x.Name)).ToArray();
                Assert.Equal(6, tables.Length);
                // 전체 운영 스키마/공유 DB를 만들지 않습니다. 수락 관문이 읽는 여섯 테이블의 독립 fixture입니다.
                foreach (var table in tables) table.ForeignKeys.Clear();
                var commands = schema.GetService<IMigrationsSqlGenerator>().Generate(tables, model);
                foreach (var command in commands) await schema.Database.ExecuteSqlRawAsync(command.CommandText);
                schema.Add(new DriverWorkParticipationRecord { UserId = "driver", Revision = 1, FoodDelivery = true,
                    VehicleKind = "Motorcycle", MaxConcurrentJobs = 1, ReceiveAdditionalOffers = true, UpdatedAtUtc = DateTime.UtcNow });
                schema.운송원장.AddRange(Candidate("a"), Candidate("b"));
                await schema.SaveChangesAsync();
            }

            var bothStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var arrived = 0; var acquired = 0;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            async Task<bool> Accept(string id)
            {
                await using var db = OpenDb();
                var service = new SharedDriverWorkAdmissionService(db, Options.Create(new SharedDriverWorkOptions { Enabled = true }));
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, timeout.Token);
                if (Interlocked.Increment(ref arrived) == 2) bothStarted.TrySetResult();
                await bothStarted.Task.WaitAsync(timeout.Token);
                // EnterAsync는 의도적으로 호출하지 않습니다. 별도 연결의 SQL 잠금이 검사/확정을 직렬화해야 합니다.
                await service.LockAsync("driver", timeout.Token);
                var lockPosition = Interlocked.Increment(ref acquired);
                var result = await service.CheckAsync("driver", [id], timeout.Token);
                if (result.IsFailed)
                {
                    Assert.Equal("DriverWork.ConcurrentLimitExceeded", result.Errors[0].Metadata["ErrorCode"]);
                    await transaction.RollbackAsync(timeout.Token); return false;
                }
                Assert.Equal(1, lockPosition);
                await Task.Delay(200, timeout.Token);
                Assert.Equal(1, Volatile.Read(ref acquired));
                var queue = await db.운송원장.SingleAsync(x => x.의뢰Id == id, timeout.Token);
                queue.확정기사Id = "driver"; queue.기사_운송자 = "driver";
                queue.상태 = "확정"; queue.배차큐단계 = 상태값.배차큐단계.확정;
                await db.SaveChangesAsync(timeout.Token); await transaction.CommitAsync(timeout.Token); return true;
            }
            var results = await Task.WhenAll(Task.Run(() => Accept("a")), Task.Run(() => Accept("b")));
            Assert.Single(results.Where(x => x)); Assert.Equal(2, acquired);
            await using var final = OpenDb();
            Assert.Equal(1, await final.운송원장.CountAsync(x => x.확정기사Id == "driver"));
            var profile = await final.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleAsync();
            Assert.Equal(1, profile.Revision);
            Assert.Equal(2, profile.WorkLockRevision); // 첫 성공의 Lock+Check만 확정되고 다른 연결의 실패 잠금은 rollback됩니다.
        }
        finally
        {
            if (created)
            {
                RequireIsolatedName(databaseName);
                await using var drop = admin.CreateCommand();
                drop.CommandText = $"DROP DATABASE `{databaseName}`";
                await drop.ExecuteNonQueryAsync();
            }
        }
    }

    private static void RequireIsolatedName(string value)
    {
        if (!Regex.IsMatch(value, @"\Amirror_r71_[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Only the newly generated isolated r71 database may be created or removed.");
    }
    private static 운송원장 Candidate(string id) => new()
    {
        의뢰Id = id, 운송번호 = id, 배차업무유형 = 상태값.배차업무유형.음식배달,
        원본의뢰유형 = "FoodOrder", 상태 = "배차대기", 배차큐단계 = 상태값.배차큐단계.배차추천
    };
    private sealed class Encryption : IPersonalDataEncryptionService { public string? Protect(string? v) => v; public string? Unprotect(string? v) => v; }
}

public sealed class SharedDriverMySqlR71FactAttribute : FactAttribute
{
    public const string Variable = "R71_MYSQL_CONNECTION";
    public SharedDriverMySqlR71FactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = "격리된 로컬 MySQL 행 잠금 검증 연결이 지정되지 않았습니다.";
    }
}
