using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Infrastructure.Storage.Memory;
using Ssalddel.Services.Community;
using Ssalddel.Services.External.Naver;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Transport;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using Microsoft.Extensions.Options;

namespace Ssalddel.Tests.Application.Driver.Work;

public sealed class 위치갱신CommandHandlerTests
{
    [Theory]
    [InlineData("latitude")]
    [InlineData("longitude")]
    [InlineData("zero")]
    [InlineData("accuracy")]
    [InlineData("future")]
    [InlineData("stale")]
    public async Task 무효이거나오래된GPS는_원장과최신위치에저장하지않는다(string condition)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync(상태값.기사운행상태.운행중);
        var request = Request(기사앱식별자.CargoYongdalDriverApp);
        request = condition switch
        {
            "latitude" => request with { 위도 = 91m },
            "longitude" => request with { 경도 = -181m },
            "zero" => request with { 위도 = 0m, 경도 = 0m },
            "accuracy" => request with { 정확도_m = -1m },
            "future" => request with { 기록시각 = DateTime.UtcNow.AddSeconds(5) },
            _ => request with { 기록시각 = DateTime.UtcNow.AddMinutes(-11) }
        };
        Assert.True((await f.Handler.Handle(request, default)).IsFailed);
        await f.AssertNoLocationAsync();
        Assert.Null(await f.StateStore.GetAsync("driver"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task 동일하거나역순으로도착한GPS가_이미확인한최신위치를덮어쓰지않는다(int seconds)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync(상태값.기사운행상태.운행중);
        var recorded = DateTime.UtcNow.AddSeconds(-3);
        var first = Request(기사앱식별자.CargoYongdalDriverApp) with { 기록시각 = recorded };
        Assert.True((await f.Handler.Handle(first, default)).IsSuccess);
        var previous = first with { 위도 = 38m, 기록시각 = recorded.AddSeconds(seconds) };
        Assert.True((await f.Handler.Handle(previous, default)).IsFailed);
        Assert.Single(await f.Db.기사위치기록.ToArrayAsync());
        Assert.True(f.Locations.TryGetLatest("driver", out var latest));
        Assert.Equal(first.위도, latest.Latitude);
        Assert.Equal(recorded, latest.RecordedAtUtc);
    }

    [Theory]
    [InlineData(null, 30)]
    [InlineData(90, 90)]
    public async Task 운행중_기본전송간격은30초이고_명시한서버정책을존중한다(int? configured, int expected)
    {
        await using var f = await Fixture.CreateAsync(); f.IntervalSeconds = configured;
        await f.SeedAsync(상태값.기사운행상태.운행중);
        var result = await f.Handler.Handle(Request(기사앱식별자.CargoYongdalDriverApp), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.권장위치전송간격초);
        Assert.Equal(30, new Ssalddel.Contracts.Driver.Work.기사위치갱신응답().권장위치전송간격초);
    }

    [Fact]
    public async Task 다른기사의위치를전송하려는요청은저장하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync(상태값.기사운행상태.운행중);
        var request = new 위치갱신Command("other-driver", 기사앱식별자.CargoYongdalDriverApp,
            37m, 127m, 5m, null, 상태값.기사운행상태.운행중, DateTime.UtcNow);
        Assert.True((await f.Handler.Handle(request, default)).IsFailed);
        await f.AssertNoLocationAsync();
    }

    [Fact]
    public async Task 종료후_늦은운행중위치요청은_근무와배달권을_되살리지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.운행중, 운송실행유형코드.음식배달);
        await fixture.States.운행시작Async("driver", 1, DateTime.UtcNow, "immediate", "서울특별시 중랑구", null,
            appKey: 기사앱식별자.FoodDeliveryDriverApp);
        await fixture.FoodScopes.Upsert기사Async("food-scope:test", "driver", []);
        await using var stopDb = fixture.NewContext();
        var stop = new 운행종료CommandHandler(stopDb, fixture.WorkQueue, fixture.States,
            fixture.FoodScopes, fixture.CargoScopes, new CurrentUser(), new 참여자실행권한검사(),
            fixture.Community, NullLogger<운행종료CommandHandler>.Instance);
        await stop.Handle(new 운행종료Command("driver"), CancellationToken.None);

        var result = await fixture.Handler.Handle(Request(기사앱식별자.FoodDeliveryDriverApp), CancellationToken.None);

        Assert.True(result.IsFailed);
        await fixture.AssertNoLocationAsync();
        Assert.Null(await fixture.StateStore.GetAsync("driver"));
        Assert.All(await fixture.FoodScopes.SnapshotAsync(), scope => Assert.DoesNotContain("driver", scope.운행중기사Ids));
        Assert.All(await fixture.CargoScopes.SnapshotAsync(), scope => Assert.DoesNotContain("driver", scope.운행중기사Ids));
        await using var read = fixture.NewContext();
        Assert.Equal(상태값.기사운행상태.대기, (await read.용달기사.SingleAsync()).운행상태);
    }

    [Fact]
    public async Task 근무를시작하지않은기사의_위치요청은_운행을시작하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.대기);

        var result = await fixture.Handler.Handle(Request(기사앱식별자.CargoYongdalDriverApp), CancellationToken.None);

        Assert.True(result.IsFailed);
        await fixture.AssertNoLocationAsync();
        Assert.Null(await fixture.StateStore.GetAsync("driver"));
        Assert.Equal(상태값.기사운행상태.대기, (await fixture.Db.용달기사.SingleAsync()).운행상태);
    }

    [Theory]
    [InlineData(운송실행유형코드.음식배달, 기사앱식별자.FoodDeliveryDriverApp, "운행중")]
    [InlineData(운송실행유형코드.화물운송, 기사앱식별자.CargoYongdalDriverApp, null)]
    [InlineData(null, 기사앱식별자.CargoYongdalDriverApp, "운행중")]
    public async Task 기존운행중기사는_실행유형과근무유무에맞게_위치를갱신한다(
        string? executionType, string appKey, string? requestStatus)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.운행중, executionType);

        var result = await fixture.Handler.Handle(Request(appKey, requestStatus), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(상태값.기사운행상태.운행중, result.Value.Status);
        var location = await fixture.Db.기사위치기록.SingleAsync();
        Assert.Equal(37.6063m, location.위도);
        Assert.True(fixture.Locations.TryGetLatest("driver", out _));
        Assert.Equal(appKey, (await fixture.StateStore.GetAsync("driver"))?.AppKey);
        if (appKey == 기사앱식별자.FoodDeliveryDriverApp)
        {
            Assert.Contains("driver", Assert.Single(await fixture.FoodScopes.SnapshotAsync()).운행중기사Ids);
            Assert.Empty(await fixture.CargoScopes.SnapshotAsync());
        }
        else
        {
            Assert.Contains("driver", Assert.Single(await fixture.CargoScopes.SnapshotAsync()).운행중기사Ids);
            Assert.Empty(await fixture.FoodScopes.SnapshotAsync());
        }
        await using var read = fixture.NewContext();
        Assert.Equal(상태값.기사운행상태.운행중, (await read.용달기사.SingleAsync()).운행상태);
    }

    [Fact]
    public async Task 운행중이어도_다른실행유형의앱위치는_수락하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.운행중, 운송실행유형코드.음식배달);

        var result = await fixture.Handler.Handle(Request(기사앱식별자.CargoYongdalDriverApp), CancellationToken.None);

        Assert.True(result.IsFailed);
        await fixture.AssertNoLocationAsync();
    }

    [Fact]
    public async Task 요청의대기상태는_기존처럼위치전송을거절하고_운행을종료하지않는다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.운행중);

        var result = await fixture.Handler.Handle(Request(기사앱식별자.CargoYongdalDriverApp, 상태값.기사운행상태.대기),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        await fixture.AssertNoLocationAsync();
        Assert.Equal(상태값.기사운행상태.운행중, (await fixture.Db.용달기사.SingleAsync()).운행상태);
    }

    [Fact]
    public async Task 위치갱신이먼저진입하면_종료는인덱스반영후_정리한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.운행중, 운송실행유형코드.음식배달);
        var saving = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeLocationSave.Callback = async () =>
        {
            saving.SetResult();
            await resume.Task;
        };

        var locationTask = fixture.Handler.Handle(Request(기사앱식별자.FoodDeliveryDriverApp), CancellationToken.None);
        await saving.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await using var stopDb = fixture.NewContext();
        var stopTask = fixture.Stop(stopDb).Handle(new 운행종료Command("driver"), CancellationToken.None);
        try
        {
            Assert.False(stopTask.IsCompleted);
        }
        finally
        {
            resume.TrySetResult();
        }
        var result = await locationTask.WaitAsync(TimeSpan.FromSeconds(5));
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        Assert.True(fixture.BeforeLocationSave.WasCalled);
        await using var read = fixture.NewContext();
        Assert.Equal(상태값.기사운행상태.대기, (await read.용달기사.SingleAsync()).운행상태);
        Assert.Single(await read.기사위치기록.ToListAsync());
        Assert.Null(await fixture.StateStore.GetAsync("driver"));
        Assert.Empty(await fixture.WorkQueue.SnapshotAsync());
        Assert.All(await fixture.FoodScopes.SnapshotAsync(), scope => Assert.DoesNotContain("driver", scope.운행중기사Ids));
        Assert.All(await fixture.CargoScopes.SnapshotAsync(), scope => Assert.DoesNotContain("driver", scope.운행중기사Ids));
    }

    [Fact]
    public async Task 종료가먼저진입하면_추적중인운행상태가낡아도_늦은위치를거절한다()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedAsync(상태값.기사운행상태.운행중, 운송실행유형코드.음식배달);
        // 다른 Context가 종료하더라도 위치 Context에는 이전 운행중 객체가 남아 있습니다.
        await fixture.Db.용달기사.SingleAsync();
        await fixture.States.운행시작Async("driver", 1, DateTime.UtcNow, "immediate", "서울특별시 중랑구", null,
            appKey: 기사앱식별자.FoodDeliveryDriverApp);
        await fixture.FoodScopes.Upsert기사Async("food-scope:test", "driver", []);
        var stopping = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.WorkQueue.BeforeRemove = async () =>
        {
            stopping.SetResult();
            await resume.Task;
        };
        await using var stopDb = fixture.NewContext();
        var stopTask = fixture.Stop(stopDb).Handle(new 운행종료Command("driver"), CancellationToken.None);
        await stopping.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var locationTask = fixture.Handler.Handle(Request(기사앱식별자.FoodDeliveryDriverApp), CancellationToken.None);
        try
        {
            Assert.False(locationTask.IsCompleted);
        }
        finally
        {
            resume.TrySetResult();
        }
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));
        var result = await locationTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsFailed);
        await fixture.AssertNoLocationAsync();
        Assert.Null(await fixture.StateStore.GetAsync("driver"));
        Assert.All(await fixture.FoodScopes.SnapshotAsync(), scope => Assert.DoesNotContain("driver", scope.운행중기사Ids));
        Assert.All(await fixture.CargoScopes.SnapshotAsync(), scope => Assert.DoesNotContain("driver", scope.운행중기사Ids));
        await using var read = fixture.NewContext();
        Assert.Equal(상태값.기사운행상태.대기, (await read.용달기사.SingleAsync()).운행상태);
    }

    private static 위치갱신Command Request(string appKey, string? status = "운행중")
        => new("driver", appKey, 37.6063m, 127.0927m, 10m, null, status, DateTime.UtcNow);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public SsalddelContext Db { get; private set; } = null!;
        public InMemoryDriverLocationStore Locations { get; } = new();
        public DelayedWorkQueue WorkQueue { get; } = new();
        public InMemory국내화물운송기사상태Store StateStore { get; } = new();
        public InMemory음식배달권실행공간Store FoodScopes { get; } = new();
        public InMemory국내화물배달권실행공간Store CargoScopes { get; } = new();
        public CommunityDriverAvailabilityService Community { get; } = new(new SystemCommunityDriverAvailabilityClock());
        public BeforeLocationSave BeforeLocationSave { get; } = new();
        public int? IntervalSeconds { get; set; }
        public 국내화물운송기사상태Service States => new(StateStore);
        public 위치갱신CommandHandler Handler => new(Db, Locations, States, FoodScopes, CargoScopes,
            new ApproachNotifications(), new CurrentUser(), new 참여자실행권한검사(), Community,
            new ReverseGeocoding(), new LedgerRealtime(), NullLogger<위치갱신CommandHandler>.Instance,
            IntervalSeconds is { } interval ? Options.Create(new 배차큐정책Options { 기사위치전송간격초 = interval }) : null);
        public 운행종료CommandHandler Stop(SsalddelContext db) => new(db, WorkQueue, States,
            FoodScopes, CargoScopes, new CurrentUser(), new 참여자실행권한검사(), Community,
            NullLogger<운행종료CommandHandler>.Instance);

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await fixture.connection.OpenAsync();
            fixture.Db = new(new DbContextOptionsBuilder<SsalddelContext>().UseSqlite(fixture.connection)
                .AddInterceptors(fixture.BeforeLocationSave).Options, new Encryption());
            await fixture.Db.Database.EnsureCreatedAsync();
            return fixture;
        }

        public SsalddelContext NewContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseSqlite(connection).Options, new Encryption());

        public async Task SeedAsync(string status, string? executionType = null)
        {
            Db.용달기사.Add(new 용달기사
            {
                기사Id = "driver", 기사명 = "기사", 운행상태 = status, 주_활동지역 = "서울특별시 중랑구"
            });
            if (executionType is not null)
                Db.기사근무.Add(new 기사근무 { 기사Id = "driver", 시작모드 = "immediate",
                    시작위치 = "서울특별시 중랑구", 시작시각 = DateTime.UtcNow, 운송실행유형 = executionType });
            await Db.SaveChangesAsync();
            Db.ChangeTracker.Clear();
        }

        public async Task AssertNoLocationAsync()
        {
            Assert.Empty(await Db.기사위치기록.ToListAsync());
            Assert.False(Locations.TryGetLatest("driver", out _));
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class DelayedWorkQueue : IDriverWorkQueueStore
    {
        private readonly InMemoryDriverWorkQueueStore inner = new();
        public Func<Task>? BeforeRemove { get; set; }
        public Task UpsertAsync(DriverWorkQueueEntry entry, CancellationToken ct = default)
            => inner.UpsertAsync(entry, ct);
        public async Task RemoveAsync(string driverId, CancellationToken ct = default)
        {
            if (BeforeRemove is { } callback)
            {
                BeforeRemove = null;
                await callback();
            }
            await inner.RemoveAsync(driverId, ct);
        }
        public Task<IReadOnlyList<DriverWorkQueueEntry>> SnapshotAsync(CancellationToken ct = default)
            => inner.SnapshotAsync(ct);
    }

    private sealed class BeforeLocationSave : SaveChangesInterceptor
    {
        public Func<Task>? Callback { get; set; }
        public bool WasCalled { get; private set; }
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Callback is { } callback && eventData.Context!.ChangeTracker.Entries<기사위치기록>()
                    .Any(entry => entry.State == EntityState.Added))
            {
                Callback = null;
                WasCalled = true;
                await callback();
            }
            return result;
        }
    }

    private sealed class CurrentUser : ICurrentUserAccessor
    {
        public string UserId => "driver";
        public string Role => 역할명.기사;
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class ApproachNotifications : I상차접근알림Service
    {
        public Task<int> 상차지접근알림검사Async(DriverLocationSnapshot location, decimal 접근반경Km = 10m,
            CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class ReverseGeocoding : INaverMapsReverseGeocodingService
    {
        public Task<NaverDistrictRegion?> ResolveDistrictAsync(decimal latitude, decimal longitude,
            CancellationToken cancellationToken = default) => Task.FromResult<NaverDistrictRegion?>(null);
    }

    private sealed class LedgerRealtime : ITransportRequestLedgerRealtimeService
    {
        public Task PublishAsync(string requestId, string eventType, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
