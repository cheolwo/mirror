using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;

namespace Ssalddel.Tests.Services.Dispatch.Continuity;

public sealed class CargoContinuityRiskPrecisionR17Tests
{
    private const string Driver = "risk-driver-r17";
    private static readonly DateTime Now = new(2026, 10, 27, 1, 0, 0, DateTimeKind.Utc);
    // 이 fixture의 개인정보 converter가 다른 시험의 protector를 캡처하지 않도록 모델 캐시를 분리한다.
    private static readonly ServiceProvider Provider = new ServiceCollection()
        .AddEntityFrameworkSqlite().BuildServiceProvider(validateScopes: true);

    [Fact]
    public async Task 완료된_이전약속을_건너뛰고_현재운송의_하차지로_위험을계산한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("previous", 상태값.배차상태.인수완료, deadlineMinutes: -30);
        await f.SeedAsync("current", 상태값.배차상태.운송중);
        f.Locations.Set(Now);

        var result = await f.Service().현재위험조회Async(Driver);

        Assert.Equal("current", result.의뢰Id);
        Assert.Equal(화물시간위험Code.정상, result.위험Code);
        Assert.Equal(new DateTimeOffset(Now.AddMinutes(10)), result.예상도착시각Utc);
        Assert.Equal(new DateTimeOffset(Now.AddMinutes(15)), result.보수예상도착시각Utc);
        Assert.Equal(new 배차경로좌표(37.59m, 127.09m), f.Routes.LastDestination);
        Assert.Equal(1, f.Routes.Calls);
        Assert.Equal(2, await f.Db.화물운송시간약속.CountAsync());
        Assert.Equal(상태값.배차상태.인수완료, (await f.Db.운송원장.SingleAsync(x => x.의뢰Id == "previous")).상태);
        Assert.Empty(f.Db.화물다음콜예약);
        Assert.Empty(f.Db.배차추천알림Outbox);
        Assert.False(f.Db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("인수완료")]
    [InlineData("취소")]
    [InlineData("반환대기")]
    public async Task 종료되거나_취소된운송은_현재위험으로_표시하지않는다(string state)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("closed", state);
        f.Locations.Set(Now);

        var result = await f.Service().현재위험조회Async(Driver);

        AssertInsufficient(result, "TimeCommitmentMissing");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Theory]
    [InlineData("other-driver", "other-driver")]
    [InlineData("risk-driver-r17", "other-driver")]
    [InlineData("other-driver", "risk-driver-r17")]
    public async Task 담당이변경되거나_귀속이충돌한운송의_약속은_사용하지않는다(string operatorId, string assignedId)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("changed-owner", operatorId: operatorId, assignedId: assignedId);
        f.Locations.Set(Now);

        var result = await f.Service().현재위험조회Async(Driver);

        AssertInsufficient(result, "TimeCommitmentMissing");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Theory]
    [InlineData("risk-driver-r17", "")]
    [InlineData("", "risk-driver-r17")]
    public async Task 기존의_한쪽기사필드만_있는_확정운송도_현재기사이면_유지한다(string operatorId, string assignedId)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current", operatorId: operatorId, assignedId: assignedId);
        f.Locations.Set(Now);

        var result = await f.Service().현재위험조회Async(Driver);

        Assert.Equal("current", result.의뢰Id);
        Assert.NotNull(result.예상도착시각Utc);
        Assert.Equal(1, f.Routes.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 미확정또는_다른업무의운송은_화물현재위험에_섞지않는다(bool food)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("not-active-cargo", food: food, confirmed: food);
        f.Locations.Set(Now);

        var result = await f.Service().현재위험조회Async(Driver);

        AssertInsufficient(result, "TimeCommitmentMissing");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Fact]
    public async Task 현재운송이있어도_다른기사의시간약속은_사용하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current", promiseDriver: "other-driver");
        f.Locations.Set(Now);

        AssertInsufficient(await f.Service().현재위험조회Async(Driver), "TimeCommitmentMissing");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Fact]
    public async Task 위치가없으면_예상도착을_만들지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current");

        var result = await f.Service().현재위험조회Async(Driver);

        Assert.Equal("current", result.의뢰Id);
        AssertInsufficient(result, "CurrentLocationMissing");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Theory]
    [InlineData(-601)]
    [InlineData(61)]
    public async Task 오래되거나_허용시계오차를넘은_미래위치로_현재ETA를만들지않는다(int receivedOffsetSeconds)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current");
        f.Locations.Set(Now.AddSeconds(receivedOffsetSeconds));

        var result = await f.Service().현재위험조회Async(Driver);

        AssertInsufficient(result, "CurrentLocationStale");
        Assert.Equal("current", result.의뢰Id);
        Assert.Equal(0, f.Routes.Calls);
    }

    [Theory]
    [InlineData(-600)]
    [InlineData(-1)]
    [InlineData(60)]
    public async Task 기존위치신선도정책의_경계와_허용시계오차를_그대로따른다(int receivedOffsetSeconds)
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current");
        f.Locations.Set(Now.AddSeconds(receivedOffsetSeconds));

        var result = await f.Service().현재위험조회Async(Driver);

        Assert.Equal(화물시간위험Code.정상, result.위험Code);
        Assert.NotNull(result.예상도착시각Utc);
        Assert.Equal(1, f.Routes.Calls);
    }

    [Fact]
    public async Task 위치유효시간은_기존배차큐옵션을_재사용한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current");
        f.Locations.Set(Now.AddSeconds(-121));

        AssertInsufficient(await f.Service(validMinutes: 2).현재위험조회Async(Driver), "CurrentLocationStale");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Fact]
    public async Task 하차좌표가없으면_경로를조회하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current", destination: false);
        f.Locations.Set(Now);

        AssertInsufficient(await f.Service().현재위험조회Async(Driver), "DropoffLocationMissing");
        Assert.Equal(0, f.Routes.Calls);
    }

    [Fact]
    public async Task 경로가없으면_정상위험이나_현재ETA로_꾸미지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.SeedAsync("current");
        f.Locations.Set(Now);
        f.Routes.Missing = true;

        AssertInsufficient(await f.Service().현재위험조회Async(Driver), "CurrentRouteEvidenceMissing");
        Assert.Equal(1, f.Routes.Calls);
    }

    private static void AssertInsufficient(화물경로위험Dto result, string reason)
    {
        Assert.Equal(화물시간위험Code.근거부족, result.위험Code);
        Assert.Equal(reason, result.사유Code);
        Assert.Null(result.예상도착시각Utc);
        Assert.Null(result.보수예상도착시각Utc);
        Assert.Null(result.남은완충분);
        Assert.False(result.보유콜자동반환필요);
    }

    private sealed class Fixture(SqliteConnection connection, SsalddelContext db) : IAsyncDisposable
    {
        public SsalddelContext Db { get; } = db;
        public Locations Locations { get; } = new();
        public Routes Routes { get; } = new();

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new SsalddelContext(new DbContextOptionsBuilder<SsalddelContext>()
                .UseSqlite(connection).UseInternalServiceProvider(Provider).Options, new Encryption());
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }

        public async Task SeedAsync(string request, string state = "운송중", int deadlineMinutes = 60,
            string operatorId = Driver, string assignedId = Driver, bool food = false, bool confirmed = true,
            string promiseDriver = Driver, bool destination = true)
        {
            Db.운송원장.Add(new 운송원장
            {
                의뢰Id = request, 운송번호 = request, 상태 = state, 기사_운송자 = operatorId, 확정기사Id = assignedId,
                배차업무유형 = food ? 상태값.배차업무유형.음식배달 : 상태값.배차업무유형.용달운송,
                배차큐단계 = confirmed ? 상태값.배차큐단계.확정 : 상태값.배차큐단계.배차추천,
                하차_위도 = destination ? 37.59m : null, 하차_경도 = destination ? 127.09m : null,
                CreatedAt = Now.AddHours(-1), UpdatedAt = Now
            });
            Db.화물운송시간약속.Add(new 화물운송시간약속
            {
                의뢰Id = request, 기사Id = promiseDriver, Revision = 1,
                목표도착시각Utc = Now.AddMinutes(deadlineMinutes - 10),
                최종도착한계시각Utc = Now.AddMinutes(deadlineMinutes), 잠금완충분 = 5,
                CreatedAt = Now.AddHours(-1), 잠금시각Utc = Now.AddHours(-1)
            });
            await Db.SaveChangesAsync();
        }

        public 화물연속배차UseCase Service(int validMinutes = 10) => new(Db,
            // 위험 조회에서 호출하지 않는 변경·추천·투영 의존성은 연결하지 않는다.
            null!, null!, null!, Routes, Locations, new 화물연속배차Policy(),
            Options.Create(new 화물연속배차Options()), new Clock(), NullLogger<화물연속배차UseCase>.Instance,
            queueOptions: Options.Create(new 배차큐정책Options { 기사위치유효시간분 = validMinutes }));

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class Encryption : IPersonalDataEncryptionService
    {
        public string? Protect(string? value) => value;
        public string? Unprotect(string? value) => value;
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Locations : IDriverLocationStore
    {
        private DriverLocationSnapshot? current;
        public void Set(DateTime received) => Upsert(new(Driver, 37.58m, 127.08m, 5m, "운행중", received, received));
        public void Upsert(DriverLocationSnapshot snapshot) => current = snapshot;
        public bool TryGetLatest(string driverId, out DriverLocationSnapshot snapshot)
        {
            snapshot = current!;
            return current?.DriverId == driverId;
        }
    }

    private sealed class Routes : I배차추천경로Service
    {
        public int Calls { get; private set; }
        public bool Missing { get; set; }
        public 배차경로좌표? LastDestination { get; private set; }
        public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination)
        {
            Calls++;
            LastDestination = destination;
            return Task.FromResult<배차경로예상결과?>(Missing ? null : new(2m, TimeSpan.FromMinutes(10), 0m, "fixture-r17", true));
        }
        public Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria) => throw new NotSupportedException();
        public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation) => throw new NotSupportedException();
        public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff) => throw new NotSupportedException();
        public decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target) => throw new NotSupportedException();
    }
}
