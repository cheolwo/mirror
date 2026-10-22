using System.Data.Common;
using FluentResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Shipper.Payment;
using Ssalddel.Contracts.Shipper.Payment;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Options;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Shipper.Payment;

public sealed class CargoFakePaymentExecutionStrategyR20Tests
{
    // 실제 관계형 transaction을 사용하되 외부 PG·Mongo·배차·발송 서비스는 호출하지 않습니다.
    private static readonly ServiceProvider OrdinaryProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);
    private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, StrategyFactory>()
        .BuildServiceProvider(validateScopes: true);
    private const string RequestId = "fake-payment-r20-request";
    private const string OwnerId = "fake-payment-r20-owner";
    private const int Amount = 10000;

    [Fact]
    public async Task 재시도전략에서도결제와대기Outbox를한트랜잭션으로저장한다()
    {
        await using var f = await Fixture.Create();

        var result = await f.Approve();

        await f.AssertCommitted(result);
        Assert.False(result.Value.이미완료됨);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 결제저장이나Outbox저장직후실패는롤백후같은요청을중복없이완료한다(bool afterOutbox)
    {
        var failure = new SaveFailure(afterOutbox);
        await using var f = await Fixture.Create(failure);

        var result = await f.Approve();

        await f.AssertCommitted(result);
        Assert.Equal(1, failure.Failures);
        Assert.False(result.Value.이미완료됨);
        Assert.Single(f.Db.ChangeTracker.Entries<결제승인완료Outbox>());
    }

    [Fact]
    public async Task 커밋응답유실은같은결제와Outbox로수렴하고재확인도새결제를만들지않는다()
    {
        var failure = new CommitReplyFailure();
        await using var f = await Fixture.Create(failure);

        var result = await f.Approve();

        await f.AssertCommitted(result);
        Assert.True(result.Value.이미완료됨);
        Assert.Equal(1, failure.Failures);
        var replay = await f.Approve();
        await f.AssertCommitted(replay);
        Assert.Equal(result.Value.결제Id, replay.Value.결제Id);
        Assert.Equal(result.Value.PaymentKey, replay.Value.PaymentKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 롤백이나커밋응답유실후호출자의추가행을버리거나다시삽입하지않는다(bool lostCommitReply)
    {
        IInterceptor failure = lostCommitReply ? new CommitReplyFailure() : new SaveFailure(true);
        await using var f = await Fixture.Create(failure);
        var callerRow = new 운임구성 { 의뢰Id = "caller-added-r20", 최종운임 = 7000m };
        f.Db.운임구성.Add(callerRow);

        await f.AssertCommitted(await f.Approve());

        Assert.True(callerRow.Id > 0);
        Assert.Equal(EntityState.Unchanged, f.Db.Entry(callerRow).State);
        Assert.False(f.Db.Entry(callerRow).Property(x => x.Id).IsTemporary);
        await f.Db.SaveChangesAsync();
        await using var canonical = Db(f.Connection);
        var row = Assert.Single(await canonical.운임구성.ToArrayAsync());
        Assert.Equal(callerRow.Id, row.Id);
        Assert.Equal(7000m, row.최종운임);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("owner")]
    [InlineData("option")]
    [InlineData("operational")]
    public async Task 재시도환경에서도서버운임소유권과초기모의선결제옵션을우회하지않는다(string scenario)
    {
        await using var f = await Fixture.Create();
        var handler = f.Handler(enabled: scenario != "option",
            user: scenario == "owner" ? "another-owner" : OwnerId,
            mode: scenario == "operational" ? SsalddelExecutionMode.Operational : SsalddelExecutionMode.Simulation);

        var result = await handler.Handle(Command(scenario == "amount" ? Amount + 1 : Amount), default);

        Assert.True(result.IsFailed);
        await f.AssertUnpaid();
    }

    [Fact]
    public async Task 외부트랜잭션이있으면중첩커밋없이참여하고호출자롤백시결제와Outbox가남지않는다()
    {
        await using var f = await Fixture.Create();
        await f.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await f.Db.Database.BeginTransactionAsync();

            var result = await f.Approve();

            Assert.True(result.IsSuccess);
            Assert.Same(transaction, f.Db.Database.CurrentTransaction);
            Assert.Single(await f.Db.결제.AsNoTracking().ToArrayAsync());
            Assert.Single(await f.Db.결제승인완료Outbox.AsNoTracking().ToArrayAsync());
            await transaction.RollbackAsync();
        });
        await f.AssertUnpaid();
    }

    private static 페이크결제승인Command Command(int amount = Amount)
        => new(RequestId, amount, "카드", null, "fake-payment-r20-stable-key");

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = connection;
        public SsalddelContext Db { get; } = db;

        public 페이크결제승인CommandHandler Handler(bool enabled = true, string user = OwnerId,
            SsalddelExecutionMode mode = SsalddelExecutionMode.Simulation)
            => new(Db, new Actor(user), new Host(), new Execution(mode),
                Options.Create(new TransportPrepaymentOptions { Enabled = enabled }));

        public Task<Result<페이크결제승인응답>> Approve() => Handler().Handle(Command(), default);

        public static async Task<Fixture> Create(IInterceptor? failure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using (var seed = CargoFakePaymentExecutionStrategyR20Tests.Db(connection))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.화주운송의뢰.Add(new 화주운송의뢰
                {
                    의뢰Id = RequestId, 화주Id = OwnerId, 주문자UserId = OwnerId,
                    화물종류 = "합성 상자", 차량종류 = "1톤 카고", 화물수량 = 1, 화물중량Kg = 5,
                    화물길이Mm = 500, 화물폭Mm = 400, 화물높이Mm = 300,
                    상태 = "생성됨", 배차상태 = "미시작", 결제상태 = "결제대기", 정산상태 = "결제대기",
                    정산시점 = "선결제", 최종운임 = Amount, 결제예정금액 = Amount,
                    픽업_도로명주소 = "합성 상차지", 픽업_연락처_전화번호 = "010-0000-0001",
                    픽업_위도 = 37.5m, 픽업_경도 = 127m, 하차_도로명주소 = "합성 하차지",
                    하차_위도 = 37.6m, 하차_경도 = 127.1m,
                    픽업_시간창_시작일시 = new(2026, 10, 22, 1, 0, 0, DateTimeKind.Utc),
                    픽업_시간창_종료일시 = new(2026, 10, 22, 2, 0, 0, DateTimeKind.Utc)
                });
                await seed.SaveChangesAsync();
            }
            return new(connection, CargoFakePaymentExecutionStrategyR20Tests.Db(connection, retry: true, failure));
        }

        public async Task AssertCommitted(Result<페이크결제승인응답> result)
        {
            Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(x => x.Message)));
            Assert.Null(Db.Database.CurrentTransaction);
            await using var canonical = CargoFakePaymentExecutionStrategyR20Tests.Db(Connection);
            var source = await canonical.화주운송의뢰.AsNoTracking().SingleAsync();
            var payment = Assert.Single(await canonical.결제.AsNoTracking().ToArrayAsync());
            var outbox = Assert.Single(await canonical.결제승인완료Outbox.AsNoTracking().ToArrayAsync());
            Assert.Equal("결제완료", source.결제상태);
            Assert.Equal("미시작", source.배차상태);
            Assert.Equal(Amount, payment.결제금액);
            Assert.Equal(result.Value.결제Id, payment.결제Id);
            Assert.Equal("FakePG", payment.PG사);
            Assert.NotNull(payment.준비근거Json);
            Assert.Equal(payment.Id, outbox.결제레코드Id);
            Assert.Equal(payment.결제Id, outbox.결제Id);
            Assert.Equal("Pending", outbox.처리상태);
            Assert.Empty(await canonical.운송원장.AsNoTracking().ToArrayAsync());
        }

        public async Task AssertUnpaid()
        {
            Assert.Null(Db.Database.CurrentTransaction);
            await using var canonical = CargoFakePaymentExecutionStrategyR20Tests.Db(Connection);
            Assert.Equal("결제대기", (await canonical.화주운송의뢰.AsNoTracking().SingleAsync()).결제상태);
            Assert.Empty(await canonical.결제.AsNoTracking().ToArrayAsync());
            Assert.Empty(await canonical.결제승인완료Outbox.AsNoTracking().ToArrayAsync());
            Assert.Empty(await canonical.운송원장.AsNoTracking().ToArrayAsync());
        }

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private static SsalddelContext Db(SqliteConnection connection, bool retry = false, IInterceptor? failure = null)
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(connection)
            .UseInternalServiceProvider(retry ? RetryingProvider : OrdinaryProvider);
        if (failure is not null) options.AddInterceptors(failure);
        return new(options.Options, new Encryption());
    }

    private sealed class TransientFailure : Exception { }
    private sealed class StrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    { public IExecutionStrategy Create() => new Strategy(dependencies); }
    private sealed class Strategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    { protected override bool ShouldRetryOn(Exception exception) => exception is TransientFailure; }

    private sealed class SaveFailure(bool afterOutbox) : SaveChangesInterceptor
    {
        public int Failures { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken ct = default)
        {
            if (Failures == 0 && (!afterOutbox || data.Context!.ChangeTracker.Entries<결제승인완료Outbox>().Any()))
            { Failures++; throw new TransientFailure(); }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommitReplyFailure : DbTransactionInterceptor
    {
        public int Failures { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData data, CancellationToken ct = default)
        {
            if (Failures == 0) { Failures++; throw new TransientFailure(); }
            return Task.CompletedTask;
        }
    }

    private sealed record Actor(string? UserId) : ICurrentUserAccessor { public string? Role => "화주"; }
    private sealed record Execution(SsalddelExecutionMode Mode) : ISsalddelExecutionModePolicy
    {
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => Mode == SsalddelExecutionMode.Operational;
    }
    private sealed class Host : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Ssalddel.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
