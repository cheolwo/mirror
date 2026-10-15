using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Hubs;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;

namespace Ssalddel.Tests.Application.Driver.Work;

public sealed class CargoWorkStartRecommendationFailureR74Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 추천실패전확정한화물근무는성공으로반환하고같은요청은기존근무를재사용한다(bool legacyRequest)
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = Request(legacyRequest ? null : Guid.NewGuid());
        var independentlyCommitted = false;
        fixture.Recommendations.Send = async () =>
        {
            await using var read = fixture.Db();
            var saved = await read.기사근무.AsNoTracking().SingleAsync();
            Assert.Equal(운송실행유형코드.화물운송, saved.운송실행유형);
            Assert.Equal(상태값.기사운행상태.운행중, (await read.용달기사.AsNoTracking().SingleAsync()).운행상태);
            independentlyCommitted = true;
            throw new InvalidOperationException("Synthetic recommendation provider unavailable.");
        };

        await using var firstDb = fixture.Db();
        var first = await fixture.Start(firstDb).Handle(request, default);
        Assert.True(first.IsSuccess);
        Assert.True(independentlyCommitted);
        Assert.Equal(상태값.기사운행상태.운행중, first.Value.Status);
        Assert.False(first.Value.FollowupPending); // 인덱스 후속 계약을 알림 재처리 예약으로 바꾸지 않습니다.
        Assert.Single(fixture.Log.Warnings);
        Assert.Contains("추천·알림 완료를 확인하지 못했습니다", fixture.Log.Warnings[0]);

        await using var replayDb = fixture.Db();
        var replay = await fixture.Start(replayDb).Handle(request, default);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.ShiftId, replay.Value.ShiftId);
        Assert.Equal(1, fixture.Recommendations.Attempts);
        Assert.Equal(0, fixture.Recommendations.Successes);
        Assert.Single(fixture.Log.Warnings);

        await using var finalRead = fixture.Db();
        var shift = Assert.Single(await finalRead.기사근무.AsNoTracking().ToArrayAsync());
        Assert.Equal(first.Value.ShiftId, shift.Id);
        Assert.Equal(request.ClientRequestId, shift.WorkStartClientRequestId);
        Assert.Empty(await finalRead.Set<기사운행시작후속>().ToArrayAsync());
        Assert.Equal(first.Value.ShiftId, Assert.Single(await fixture.Queue.SnapshotAsync()).ShiftId);
        Assert.Equal(first.Value.ShiftId, (await fixture.StateStore.GetAsync("driver"))!.ShiftId);
    }

    [Fact]
    public async Task 추천의취소예외는기존대로전파하되확정근무를롤백하거나재시작하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Recommendations.Send = () => throw new OperationCanceledException("Synthetic cancellation.");
        await using var db = fixture.Db();
        await Assert.ThrowsAsync<OperationCanceledException>(() => fixture.Start(db).Handle(Request(Guid.NewGuid()), default));
        await using var read = fixture.Db();
        Assert.Single(await read.기사근무.AsNoTracking().ToArrayAsync());
        Assert.Equal(상태값.기사운행상태.운행중, (await read.용달기사.AsNoTracking().SingleAsync()).운행상태);
        Assert.Equal(1, fixture.Recommendations.Attempts);
        Assert.Equal(0, fixture.Recommendations.Successes);
        Assert.Empty(fixture.Log.Warnings);
    }

    private static 운행시작Command Request(Guid? requestId) => new("driver", "바로시작", null,
        "합성 시험 시작 지역", null, null, null, null, false, null, null, false, false,
        운송실행유형코드.화물운송, requestId);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _connection = $"Data Source=cargo-start-r74-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        private SqliteConnection _anchor = null!;
        public InMemoryDriverWorkQueueStore Queue { get; } = new();
        public InMemory국내화물운송기사상태Store StateStore { get; } = new();
        public Recommendations Recommendations { get; } = new();
        public CaptureLog Log { get; } = new();
        public SsalddelContext Db() => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(_connection).Options, new Encryption());
        public 운행시작CommandHandler Start(SsalddelContext db) => new(db, Recommendations, Queue,
            new 국내화물운송기사상태Service(StateStore), new InMemory음식배달권실행공간Store(),
            new InMemory국내화물배달권실행공간Store(), new Actor(), new 참여자실행권한검사(),
            new CommunityDriverAvailabilityService(new SystemCommunityDriverAvailabilityClock()), Log);
        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            fixture._anchor = new(fixture._connection);
            await fixture._anchor.OpenAsync();
            await using var db = fixture.Db();
            await db.Database.EnsureCreatedAsync();
            db.용달기사.Add(new 용달기사 { 기사Id = "driver", 기사명 = "합성 기사", 운행상태 = 상태값.기사운행상태.대기 });
            await db.SaveChangesAsync();
            return fixture;
        }
        public ValueTask DisposeAsync() => _anchor.DisposeAsync();
    }

    private sealed class Recommendations : I배차추천Service
    {
        public Func<Task> Send { get; set; } = () => Task.CompletedTask;
        public int Attempts { get; private set; }
        public int Successes { get; private set; }
        public async Task SendToDriverAsync(string driverId) { Attempts++; await Send(); Successes++; }
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string driverId)
            => throw new InvalidOperationException("This regression does not synthesize recommendation results.");
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string driverId, 배차추천검색조건? criteria)
            => GetRecommendationsAsync(driverId);
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null)
            => GetRecommendationsAsync(driverId);
        public Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null)
            => GetRecommendationsAsync(driverId);
    }

    private sealed class CaptureLog : ILogger<운행시작CommandHandler>
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        { if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception)); }
    }
    private sealed class Actor : ICurrentUserAccessor { public string? UserId => "driver"; public string? Role => "기사"; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
}
