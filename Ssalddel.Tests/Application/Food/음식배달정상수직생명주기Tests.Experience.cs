using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.DeliveryExperience;
using Ssalddel.Services.Food;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.음식;

namespace Ssalddel.Tests.Application.Food;

public sealed partial class 음식배달정상수직생명주기Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 실제픽업전달완료는_같은Transaction에서_경험근거를저장하고_실패시함께되돌린다(bool failFirstPickupCommit)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var probe = new ExperienceCompletionCommitProbe { FailFirst = failFirstPickupCommit };
        await using var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection).AddInterceptors(probe).Options, new PassThroughEncryptionService());
        await db.Database.EnsureCreatedAsync();
        var store = new EfSsalddelFoodOrderStore(db);
        var order = store.AddOrder(CreateOrderRequest());
        store.음식점수락멱등(order.주문번호,
            new 음식점주문수락요청 { 클라이언트요청Id = Guid.NewGuid(), 음식점명 = "검증음식점", 조리예상분 = 10 }, "restaurant-41");
        const string driverId = "experience-driver";
        var offerId = "food-dispatch:" + order.주문번호;
        db.운송원장.Add(CreateRecommendedTransport(offerId, order.주문번호, driverId));
        db.Set<음식배달전달지역Snapshot>().Add(new()
        {
            주문번호 = order.주문번호, 도로Id = "official-road", 도로표시명 = "면목로", 확인상태Code = "Confirmed"
        });
        await db.SaveChangesAsync();
        store.배차대기반영(order.주문번호, (await db.운송원장.SingleAsync()).Id, DateTime.UtcNow);
        var execution = new SsalddelExecutionModePolicy(Options.Create(new SsalddelExecutionOptions { Mode = SsalddelExecutionMode.Simulation }));
        var experience = new 음식배달경험Service(db, Options.Create(new FoodDeliveryExperienceOptions { Enabled = true }),
            execution, TimeProvider.System, NullLogger<음식배달경험Service>.Instance);
        var driver = CreateDriverWork(db, store, new RecordingPublisher(), driverId,
            experience: experience, experienceExecution: execution);
        Assert.True((await driver.수락Async(driverId, offerId)).IsSuccess);
        Assert.Equal("Simulation", (await db.음식배달시도.SingleAsync()).경험실행모드Code);
        Assert.True((await driver.픽업완료Async(driverId, offerId)).IsFailed);
        Assert.Empty(await db.Set<음식배달경험완료Outbox>().ToListAsync());
        store.음식점진행변경(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 예상Revision = store.GetOrder(order.주문번호)!.Revision,
            작업 = 음식점주문진행작업코드.조리시작 }, "restaurant-41");
        store.음식점진행변경(order.주문번호, new() { 클라이언트요청Id = Guid.NewGuid(), 작업 = 음식점주문진행작업코드.픽업준비 }, "restaurant-41");
        probe.Armed = true;
        var pickup = await driver.픽업완료Async(driverId, offerId);
        Assert.Equal(1, probe.OutboxCountInsideTransaction);
        Assert.Equal(음식주문상태코드.픽업완료, probe.OrderStateInsideTransaction);
        if (failFirstPickupCommit)
        {
            Assert.True(pickup.IsFailed);
            // 저장 직후 commit을 실패시켰으므로 상태·시도·Outbox가 모두 롤백되어야 합니다.
            db.ChangeTracker.Clear();
            Assert.Equal(음식주문상태코드.픽업대기, (await db.음식주문.AsNoTracking().SingleAsync()).상태);
            Assert.Null((await db.음식배달시도.AsNoTracking().SingleAsync()).픽업완료시각Utc);
            Assert.Empty(await db.Set<음식배달경험완료Outbox>().AsNoTracking().ToListAsync());
            Assert.True((await driver.픽업완료Async(driverId, offerId)).IsSuccess);
        }
        else Assert.True(pickup.IsSuccess);
        Assert.True((await driver.픽업완료Async(driverId, offerId)).IsSuccess);
        Assert.True((await driver.전달완료Async(driverId, offerId)).IsSuccess);
        Assert.True((await driver.전달완료Async(driverId, offerId)).IsSuccess);
        var attempt = await db.음식배달시도.AsNoTracking().SingleAsync();
        var evidence = await db.Set<음식배달경험완료Outbox>().AsNoTracking().OrderBy(x => x.완료종류Code).ToListAsync();
        Assert.Equal(2, evidence.Count);
        Assert.All(evidence, x => Assert.Equal(attempt.시도StableId, x.원천시도Id));
        Assert.Equal(attempt.픽업완료시각Utc, evidence.Single(x => x.완료종류Code == "PickupCompleted").CompletedAtUtc);
        Assert.Equal(attempt.전달완료시각Utc, evidence.Single(x => x.완료종류Code == "DeliveryCompleted").CompletedAtUtc);
        Assert.Equal(2, await experience.투영Async(default));
        Assert.Equal(2, await db.Set<음식배달경험Fact>().CountAsync());
        Assert.Equal(0, await experience.투영Async(default));
    }

    private sealed class ExperienceCompletionCommitProbe : DbTransactionInterceptor
    {
        public bool Armed, FailFirst;
        public long OutboxCountInsideTransaction;
        public string? OrderStateInsideTransaction;
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!Armed) return result;
            Armed = false;
            await using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM \"음식배달경험완료Outbox\"";
            OutboxCountInsideTransaction = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            command.CommandText = "SELECT \"상태\" FROM \"음식주문\" LIMIT 1";
            OrderStateInsideTransaction = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
            if (FailFirst) throw new DbUpdateException("fixture: completion commit rejected");
            return result;
        }
    }
}
