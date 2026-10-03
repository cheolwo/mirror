using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Dispatch.Continuity;

namespace Ssalddel.Tests.Application.Driver.DispatchAction;

public sealed class 배차수락CommandHandlerTests
{
    [Fact]
    public async Task 사후처리Event가실패해도_기사운송진행정보는_배차확정과같이저장된다()
    {
        await using var db = CreateContext();
        var queue = new 운송원장
        {
            의뢰Id = "request-1",
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "driver-1",
            추천만료시각 = DateTime.UtcNow.AddMinutes(5)
        };
        var request = new 화주운송의뢰
        {
            의뢰Id = "request-1",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            화물종류 = "사과",
            결제상태 = 상태값.결제상태.결제완료,
            배차상태 = 상태값.배차상태.매칭중,
            픽업_도로명주소 = "서울시 강남구",
            픽업_상세주소 = "1층",
            하차_도로명주소 = "서울시 송파구",
            하차_상세주소 = "2층",
            최종운임 = 42000
        };
        db.운송원장.Add(queue);
        db.화주운송의뢰.Add(request);
        await db.SaveChangesAsync();

        var handler = new 배차수락CommandHandler(
            db,
            new ThrowingPublisher(),
            new TestCurrentUserAccessor("driver-1", "기사"),
            new 참여자실행권한검사(),
            new WorkRelationshipSnapshotCollector(),
            new StubEligibilityService(),
            new StubContinuityUseCase(),
            NullLogger<배차수락CommandHandler>.Instance);

        var result = await handler.Handle(
            new 배차수락Command("driver-1", "request-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(상태값.배차대기상태.확정, queue.상태);
        Assert.Equal("driver-1", queue.기사_운송자);
        Assert.Equal("driver-1", queue.확정기사Id);
        Assert.Equal("request-1", queue.운송번호);
        Assert.Equal(request.픽업_도로명주소, queue.픽업_도로명주소);
        Assert.Equal(request.하차_도로명주소, queue.하차_도로명주소);
        Assert.Equal(request.최종운임, queue.운임);
        Assert.Equal(상태값.배차상태.배차확정, request.배차상태);
    }

    [Fact]
    public async Task 추천라운드가바뀌면_안정오류코드로수락을차단한다()
    {
        await using var db = CreateContext();
        db.운송원장.Add(new 운송원장
        {
            의뢰Id = "request-stale",
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "driver-1",
            추천라운드 = 3,
            추천만료시각 = DateTime.UtcNow.AddMinutes(5)
        });
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-stale",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            결제상태 = 상태값.결제상태.결제완료,
            배차상태 = 상태값.배차상태.매칭중
        });
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, new StubEligibilityService());
        var result = await handler.Handle(
            new 배차수락Command("driver-1", "request-stale", expectedRecommendationRound: 2),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(
            화물배차수락오류코드.추천판본불일치,
            result.Errors.Single().Metadata["ErrorCode"]);
    }

    [Fact]
    public async Task 서버재평가가차단하면_원장을확정하지않는다()
    {
        await using var db = CreateContext();
        var queue = new 운송원장
        {
            의뢰Id = "request-blocked",
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "driver-1",
            추천라운드 = 1,
            추천만료시각 = DateTime.UtcNow.AddMinutes(5)
        };
        db.운송원장.Add(queue);
        db.화주운송의뢰.Add(new 화주운송의뢰
        {
            의뢰Id = "request-blocked",
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            결제상태 = 상태값.결제상태.결제완료,
            배차상태 = 상태값.배차상태.매칭중
        });
        await db.SaveChangesAsync();

        var evaluator = new StubEligibilityService(new(
            false,
            화물배차수락오류코드.경고확인필요,
            "주의사항 확인 필요",
            [화물배차수락경고코드.차량주의사항], [], [], 30m, 5m));
        var result = await CreateHandler(db, evaluator).Handle(
            new 배차수락Command("driver-1", "request-blocked", 1),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(상태값.배차대기상태.대기, queue.상태);
        Assert.Equal(화물배차수락오류코드.경고확인필요, result.Errors.Single().Metadata["ErrorCode"]);
        Assert.Equal(
            new[] { 화물배차수락경고코드.차량주의사항 },
            Assert.IsAssignableFrom<IReadOnlyList<string>>(result.Errors.Single().Metadata["WarningCodes"]));
    }

    [Theory]
    [InlineData("운송완료후정산", "후불승인완료")]
    [InlineData("월말정산", "후불승인완료")]
    [InlineData("현장지급", "현장수금예정")]
    public async Task 후불이나현장지급조건이확인된의뢰는_수락해도실제결제상태를변경하지않는다(
        string settlementTime,
        string settlementStatus)
    {
        await using var db = CreateContext();
        var (queue, request) = await SeedUnpaidRequestAsync(db, settlementTime, settlementStatus);

        var result = await CreateHandler(db, new StubEligibilityService()).Handle(
            new 배차수락Command("driver-1", request.의뢰Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        db.ChangeTracker.Clear();
        var savedTransport = await db.운송원장.SingleAsync();
        var savedRequest = await db.화주운송의뢰.SingleAsync();
        Assert.Equal(상태값.배차대기상태.확정, savedTransport.상태);
        Assert.Equal("driver-1", savedTransport.기사_운송자);
        Assert.Equal("driver-1", savedTransport.확정기사Id);
        Assert.Equal(queue.의뢰Id, savedTransport.의뢰Id);
        Assert.Equal(상태값.배차상태.배차확정, savedRequest.배차상태);
        Assert.Equal(상태값.결제상태.결제대기, savedRequest.결제상태);
        Assert.Equal(settlementStatus, savedRequest.정산상태);
        Assert.Empty(await db.결제.ToListAsync());
        Assert.Empty(await db.결제승인완료Outbox.ToListAsync());
    }

    [Theory]
    [InlineData("선결제", "결제대기")]
    [InlineData("미등록정산시점", "후불승인완료")]
    [InlineData("운송완료후정산", "청구대기")]
    [InlineData("운송완료후정산", "인수증대기")]
    [InlineData("월말정산", "후불승인대기")]
    [InlineData("운송완료후정산", "비정상운송검토보류")]
    public async Task 승인전이거나보류된의뢰는_수락과원장변경을409로차단한다(
        string settlementTime,
        string settlementStatus)
    {
        await using var db = CreateContext();
        var (_, request) = await SeedUnpaidRequestAsync(db, settlementTime, settlementStatus);

        var result = await CreateHandler(db, new StubEligibilityService()).Handle(
            new 배차수락Command("driver-1", request.의뢰Id),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(409, result.Errors.Single().Metadata["StatusCode"]);
        Assert.Equal(화주운송배차진입정책.진입불가오류코드, result.Errors.Single().Metadata["ErrorCode"]);
        db.ChangeTracker.Clear();
        var savedTransport = await db.운송원장.SingleAsync();
        var savedRequest = await db.화주운송의뢰.SingleAsync();
        Assert.Equal(상태값.배차대기상태.대기, savedTransport.상태);
        Assert.Null(savedTransport.확정기사Id);
        Assert.Equal(상태값.배차상태.매칭중, savedRequest.배차상태);
        Assert.Equal(상태값.결제상태.결제대기, savedRequest.결제상태);
        Assert.Equal(settlementStatus, savedRequest.정산상태);
    }

    [Fact]
    public async Task 관계형저장중일시실패는_롤백후다시수락하고_같은기사재수락은사후처리하지않는다()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection)
            .ReplaceService<IExecutionStrategyFactory, RetryStrategyFactory>()
            .Options;
        await using var db = new SsalddelContext(options, new DummyPersonalDataEncryptionService());
        await db.Database.EnsureCreatedAsync();
        var (_, request) = await SeedUnpaidRequestAsync(db, "운송완료후정산", "후불승인완료");
        var continuity = new StubContinuityUseCase(failFirstCompletion: true);
        var publisher = new RecordingPublisher();
        var snapshots = new WorkRelationshipSnapshotCollector();
        var handler = new 배차수락CommandHandler(
            db, publisher, new TestCurrentUserAccessor("driver-1", "기사"), new 참여자실행권한검사(),
            snapshots, new StubEligibilityService(), continuity, NullLogger<배차수락CommandHandler>.Instance);

        var accepted = await handler.Handle(new 배차수락Command("driver-1", request.의뢰Id), CancellationToken.None);
        var replay = await handler.Handle(new 배차수락Command("driver-1", request.의뢰Id), CancellationToken.None);

        Assert.True(accepted.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(2, continuity.CompletionCalls);
        Assert.Equal(1, continuity.PromiseCalls);
        Assert.Single(publisher.Notifications);
        Assert.Single(snapshots.Drain());
        await using var read = new SsalddelContext(options, new DummyPersonalDataEncryptionService());
        Assert.Equal("driver-1", (await read.운송원장.SingleAsync()).확정기사Id);
        var savedRequest = await read.화주운송의뢰.SingleAsync();
        Assert.Equal(상태값.배차상태.배차확정, savedRequest.배차상태);
        Assert.Equal(상태값.결제상태.결제대기, savedRequest.결제상태);
    }

    private static async Task<(운송원장 Queue, 화주운송의뢰 Request)> SeedUnpaidRequestAsync(
        SsalddelContext db,
        string settlementTime,
        string settlementStatus)
    {
        var queue = new 운송원장
        {
            의뢰Id = "warehouse-outbound-approved",
            상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.배차추천,
            배차노출상태 = 상태값.배차노출상태.추천중,
            현재추천대상기사Id = "driver-1",
            추천만료시각 = DateTime.UtcNow.AddMinutes(5)
        };
        var request = new 화주운송의뢰
        {
            의뢰Id = queue.의뢰Id,
            화주Id = "shipper-1",
            주문자UserId = "shipper-1",
            결제상태 = 상태값.결제상태.결제대기,
            배차상태 = 상태값.배차상태.매칭중,
            정산시점 = settlementTime,
            정산상태 = settlementStatus
        };
        db.운송원장.Add(queue);
        db.화주운송의뢰.Add(request);
        await db.SaveChangesAsync();
        return (queue, request);
    }

    private static 배차수락CommandHandler CreateHandler(
        SsalddelContext db,
        I화물배차수락적격성Service eligibility)
        => new(
            db,
            new ThrowingPublisher(),
            new TestCurrentUserAccessor("driver-1", "기사"),
            new 참여자실행권한검사(),
            new WorkRelationshipSnapshotCollector(),
            eligibility,
            new StubContinuityUseCase(),
            NullLogger<배차수락CommandHandler>.Instance);

    private static SsalddelContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase($"dispatch-accept-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new SsalddelContext(options, new DummyPersonalDataEncryptionService());
    }

    private sealed record TestCurrentUserAccessor(string? UserId, string? Role) : ICurrentUserAccessor;

    private sealed class ThrowingPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.FromException(new InvalidOperationException("temporary event failure"));

        public Task Publish<TNotification>(
            TNotification notification,
            CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.FromException(new InvalidOperationException("temporary event failure"));
    }

    private sealed class StubEligibilityService : I화물배차수락적격성Service
    {
        private readonly 화물배차수락적격성결과 _result;

        public StubEligibilityService(화물배차수락적격성결과? result = null)
        {
            _result = result ?? new(true, null, null, [], [], [], null, null);
        }

        public Task<화물배차수락적격성결과> 평가Async(
            string 기사Id,
            화주운송의뢰 후보의뢰,
            IReadOnlyCollection<string> 확인한경고코드,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_result);
    }

    private sealed class StubContinuityUseCase(bool failFirstCompletion = false) : I화물연속배차UseCase
    {
        public int CompletionCalls { get; private set; }
        public int PromiseCalls { get; private set; }

        public Task<화물연속배차상태Dto> 조회Async(string 기사Id, CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물연속배차상태Dto { 기사Id = 기사Id });

        public Task<화물연속배차상태Dto> 의사변경Async(
            string 기사Id,
            화물연속배차의사변경요청 요청,
            CancellationToken cancellationToken = default)
            => 조회Async(기사Id, cancellationToken);

        public Task<화물다음콜예약Dto?> 추천탐색Async(string 기사Id, CancellationToken cancellationToken = default)
            => Task.FromResult<화물다음콜예약Dto?>(null);

        public Task<화물예약검증결과> 수락예약검증Async(
            string 기사Id,
            string 의뢰Id,
            string? reservationId,
            long? expectedRevision,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물예약검증결과(true));

        public Task 수락완료Async(string 기사Id, string 의뢰Id, CancellationToken cancellationToken = default)
        {
            CompletionCalls++;
            return failFirstCompletion && CompletionCalls == 1
                ? Task.FromException(new RetryableWriteException())
                : Task.CompletedTask;
        }

        public Task 시간약속잠금Async(
            string 기사Id,
            화주운송의뢰 의뢰,
            CancellationToken cancellationToken = default)
        {
            PromiseCalls++;
            return Task.CompletedTask;
        }

        public Task 완료기록Async(string 기사Id, DateTime 완료시각Utc, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(
            string 기사Id,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<화물운송시간약속Dto>>([]);

        public Task<화물경로위험Dto> 현재위험조회Async(string 기사Id, CancellationToken cancellationToken = default)
            => Task.FromResult(new 화물경로위험Dto());
    }

    private sealed class RecordingPublisher : IPublisher
    {
        public List<object> Notifications { get; } = [];

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Publish((object)notification, cancellationToken);
    }

    private sealed class RetryableWriteException : Exception { }

    private sealed class RetryStrategyFactory : IExecutionStrategyFactory
    {
        private readonly ExecutionStrategyDependencies _dependencies;

        public RetryStrategyFactory(ExecutionStrategyDependencies dependencies) => _dependencies = dependencies;

        public IExecutionStrategy Create() => new RetryStrategy(_dependencies);
    }

    private sealed class RetryStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, 1, TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is RetryableWriteException;
    }

    private sealed class DummyPersonalDataEncryptionService : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }
}
