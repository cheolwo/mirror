using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Ssalddel.Domain.Community;
using 살뜰.Data;

namespace Ssalddel.Tests.Services.Community;

/// <summary>실제 관계형 transaction과 재시도 전략의 호환·동일 요청 복구. 외부 발송/운영 증거가 아니다.</summary>
public sealed class NeighborhoodMemberDeliveryTransactionRetryTests
{
    private static readonly ServiceProvider RetryingProvider = new ServiceCollection()
        .AddEntityFrameworkSqlite().AddScoped<IExecutionStrategyFactory, RetryStrategyFactory>()
        .BuildServiceProvider(validateScopes: true);
    internal static SsalddelContext Open(NeighborhoodMemberDeliveryTests.Fixture f, params IInterceptor[] interceptors)
        => new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(f.Db.Database.GetConnectionString()!)
            .UseInternalServiceProvider(RetryingProvider).AddInterceptors(interceptors).Options,
            new NeighborhoodMemberDeliveryTests.Encryption());

    [Fact]
    public async Task 기본생성재조회와재시도모델은같은엄격한개인정보보호형식을사용한다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        await f.Seed();
        await using var ordinary = f.OpenDb(); await using var retrying = Open(f);
        Assert.Same(f.Db.Model, ordinary.Model);
        foreach (var db in new[] { f.Db, ordinary, retrying })
        {
            var property = db.Model.FindEntityType(typeof(살뜰.도메인.화주.화주운송의뢰))!
                .FindProperty(nameof(살뜰.도메인.화주.화주운송의뢰.픽업_상세주소))!;
            var converter = property.GetValueConverter()!;
            var protectedValue = Assert.IsType<string>(converter.ConvertToProvider("1층"));
            Assert.StartsWith("protected:", protectedValue);
            Assert.Equal("1층", converter.ConvertFromProvider(protectedValue));
        }
        Assert.Equal("1층", (await ordinary.화주운송의뢰.SingleAsync()).픽업_상세주소);
        Assert.Equal("1층", (await retrying.화주운송의뢰.SingleAsync()).픽업_상세주소);
    }

    [Fact]
    public async Task 참여저장중일시실패는같은요청과판본으로재실행하고증적을한번만남긴다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var fault = new FailAfterSave(typeof(NeighborhoodMemberParticipation));
        await using var db = Open(f, fault); var request = f.Setting();
        var first = await f.Service(db).SaveParticipationAsync(request);
        Assert.Equal(1, first.Revision); Assert.True(first.ReceiveOffers); Assert.Equal(2, fault.MatchedSaveAttempts);
        await using var reread = f.OpenDb();
        Assert.Equal(1, await reread.Set<NeighborhoodMemberParticipation>().CountAsync());
        Assert.Equal(1, await reread.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.ClientRequestId == request.ClientRequestId));
        Assert.Equal(1, (await f.Service(reread).SaveParticipationAsync(request)).Revision);
    }

    [Fact]
    public async Task 참여저장커밋응답유실은기존증적으로복구하고금액이나업무를추가하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        var fault = new FailAfterCommit();
        await using var db = Open(f, fault); var request = f.Setting();
        var result = await f.Service(db).SaveParticipationAsync(request);
        Assert.True(fault.Fired); Assert.Equal(1, result.Revision);
        await using var reread = f.OpenDb();
        Assert.Equal(1, await reread.Set<NeighborhoodMemberParticipation>().CountAsync());
        Assert.Equal(1, await reread.Set<NeighborhoodMemberReceipt>().CountAsync());
        Assert.Empty(await reread.Set<NeighborhoodMemberDelivery>().ToListAsync());
        Assert.Empty(await reread.기사운송대금지급요청.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 배송등록과추천은별도트랜잭션으로재시도하고정본이나제안을중복하지않는다(bool failFanout)
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        await f.Enroll("member-a"); await f.Seed(); f.Actor.UserId = "owner";
        var fault = new FailAfterSave(failFanout ? typeof(NeighborhoodMemberOffer) : typeof(NeighborhoodMemberDelivery));
        await using var db = Open(f, fault);
        await f.Service(db).RegisterAsync("request-1", f.Input(), default);
        Assert.Equal(2, fault.MatchedSaveAttempts);
        Assert.Null(db.Database.CurrentTransaction);
        await f.Service(db).RegisterAsync("request-1", f.Input(), default);
        await using var reread = f.OpenDb();
        Assert.Equal(1, await reread.Set<NeighborhoodMemberDelivery>().CountAsync());
        Assert.Equal(1, await reread.Set<NeighborhoodMemberOffer>().CountAsync());
        Assert.Equal("Pending", (await reread.Set<NeighborhoodMemberOffer>().SingleAsync()).StateCode);
        Assert.True((await reread.운송원장.SingleAsync()).생활배송수락준비완료);
        Assert.Empty(await reread.기사운송대금지급요청.ToListAsync());
    }

    [Fact]
    public async Task 등록커밋뒤변경된정본은추천전에다시읽고종결된배송을추천하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        await f.Enroll("member-a"); await f.Seed(); f.Actor.UserId = "owner";
        var changed = new ChangeAfterFirstCommit(async () =>
        {
            await using var other = f.OpenDb();
            var row = await other.Set<NeighborhoodMemberDelivery>().SingleAsync();
            row.StateCode = "Cancelled"; row.Revision++;
            await other.SaveChangesAsync();
        });
        await using var db = Open(f, changed);
        await f.Service(db).RegisterAsync("request-1", f.Input(), default);
        Assert.True(changed.Fired);
        await using var reread = f.OpenDb();
        Assert.Equal("Cancelled", (await reread.Set<NeighborhoodMemberDelivery>().SingleAsync()).StateCode);
        Assert.Empty(await reread.Set<NeighborhoodMemberOffer>().ToListAsync());
    }

    [Fact]
    public async Task MySql일시저장오류는실행전략에전달되고같은요청을한번만기록한다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        Assert.True(ProviderError(1213).IsTransient);
        var fault = new FailAfterSave(typeof(NeighborhoodMemberParticipation), providerNumber: 1213);
        await using var db = Open(f, fault); var request = f.Setting();
        Assert.Equal(1, (await f.Service(db).SaveParticipationAsync(request)).Revision);
        Assert.Equal(2, fault.MatchedSaveAttempts);
        await using var reread = f.OpenDb();
        Assert.Equal(1, await reread.Set<NeighborhoodMemberParticipation>().CountAsync());
        Assert.Equal(1, await reread.Set<NeighborhoodMemberReceipt>().CountAsync(x => x.ClientRequestId == request.ClientRequestId));
    }

    [Fact]
    public async Task MySql영구저장오류는기존충돌코드를유지하고자동재실행하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        Assert.False(ProviderError(1062).IsTransient);
        var fault = new FailAfterSave(typeof(NeighborhoodMemberParticipation), providerNumber: 1062);
        await using var db = Open(f, fault);
        var error = await Assert.ThrowsAsync<Ssalddel.Services.Community.NeighborhoodMemberDeliveryException>(
            () => f.Service(db).SaveParticipationAsync(f.Setting()));
        Assert.Equal("MemberRevisionConflict", error.Code); Assert.Equal(1, fault.MatchedSaveAttempts);
        await using var reread = f.OpenDb();
        Assert.Empty(await reread.Set<NeighborhoodMemberParticipation>().ToListAsync());
        Assert.Empty(await reread.Set<NeighborhoodMemberReceipt>().ToListAsync());
    }

    [Fact]
    public async Task 요청취소는긴재시도대기도취소하고추가저장을하지않는다()
    {
        await using var f = await NeighborhoodMemberDeliveryTests.Fixture.CreateAsync();
        using var cancelled = new CancellationTokenSource();
        var fault = new FailAfterSave(typeof(NeighborhoodMemberParticipation), onFirstFailure: cancelled.Cancel, retryDelay: TimeSpan.FromSeconds(30));
        await using var db = Open(f, fault);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service(db)
            .SaveParticipationAsync(f.Setting(), cancelled.Token).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, fault.MatchedSaveAttempts);
        await using var reread = f.OpenDb();
        Assert.Empty(await reread.Set<NeighborhoodMemberParticipation>().ToListAsync());
    }

    // MySqlConnector 2.4의 공개 IsTransient 판정을 실제 provider 예외로 검증한다. DB에는 연결하지 않는다.
    private static MySqlException ProviderError(int number)
        => (MySqlException)Activator.CreateInstance(typeof(MySqlException), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            binder: null, args: [(MySqlErrorCode)number, "Synthetic provider fault"], culture: null)!;

    private sealed class RetryStrategyFactory(ExecutionStrategyDependencies dependencies) : IExecutionStrategyFactory
    { public IExecutionStrategy Create() => new RetryStrategy(dependencies); }
    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies) : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryableFailure or MySqlException { IsTransient: true };
        protected override TimeSpan? GetNextDelay(Exception lastException)
        {
            var next = base.GetNextDelay(lastException);
            return next.HasValue && lastException is RetryableFailure { RetryDelay: { } delay } ? delay : next;
        }
    }
    internal sealed class RetryableFailure(TimeSpan? retryDelay = null) : Exception
    { internal TimeSpan? RetryDelay { get; } = retryDelay; }

    private sealed class FailAfterSave(Type entityType, int? providerNumber = null, Action? onFirstFailure = null, TimeSpan? retryDelay = null) : SaveChangesInterceptor
    {
        public int MatchedSaveAttempts { get; private set; }
        private bool pending;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            pending = eventData.Context!.ChangeTracker.Entries().Any(x => x.Entity.GetType() == entityType
                && x.State is EntityState.Added or EntityState.Modified);
            if (pending) MatchedSaveAttempts++;
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (pending && MatchedSaveAttempts == 1)
            {
                onFirstFailure?.Invoke();
                if (providerNumber is { } number) throw new DbUpdateException("Synthetic provider write failure", ProviderError(number));
                throw new RetryableFailure(retryDelay);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FailAfterCommit : DbTransactionInterceptor
    {
        public bool Fired { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (!Fired) { Fired = true; throw new RetryableFailure(); }
            return Task.CompletedTask;
        }
    }

    private sealed class ChangeAfterFirstCommit(Func<Task> change) : DbTransactionInterceptor
    {
        public bool Fired { get; private set; }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (Fired) return;
            Fired = true; await change();
        }
    }
}
