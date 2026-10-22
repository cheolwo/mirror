using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using System.Runtime.CompilerServices;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Food;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Food;

public sealed class RelatedDriverLocationR8Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CurrentExplicitAssignmentExposesOnlyRecentUtcLocation(bool cargo)
    {
        await using var f = await Fixture.CreateAsync(cargo);
        f.Location.기록시각 = DateTime.SpecifyKind(f.Location.기록시각, DateTimeKind.Unspecified);
        f.Location.CreatedAt = DateTime.SpecifyKind(f.Location.CreatedAt, DateTimeKind.Unspecified);
        await f.Db.SaveChangesAsync();
        if (cargo)
        {
            var result = await f.CargoAsync();
            Assert.NotNull(result);
            Assert.Equal(37.58m, result.기사최근위도);
            Assert.Equal(127.08m, result.기사최근경도);
            Assert.Equal(DateTimeKind.Utc, result.기사최근위치시각Utc!.Value.Kind);
        }
        else
        {
            var result = await f.FoodAsync();
            Assert.Equal(음식배달위치추적상태코드.추적중, result.상태);
            Assert.Equal(37.58m, result.위도);
            Assert.Equal(DateTimeKind.Utc, result.기록시각Utc!.Value.Kind);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inferred")]
    [InlineData("round")]
    [InlineData("driver")]
    [InlineData("offer")]
    [InlineData("interrupted")]
    [InlineData("latest-interrupted")]
    [InlineData("future-assignment")]
    [InlineData("before-assignment")]
    [InlineData("received-before-assignment")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("future-received")]
    [InlineData("invalid-coordinates")]
    [InlineData("negative-accuracy")]
    [InlineData("ended-queue")]
    public async Task FoodHidesUnprovenStaleInvalidAndPreviousAssignmentLocations(string condition)
    {
        await using var f = await Fixture.CreateAsync(false);
        switch (condition)
        {
            case "missing": f.Db.음식배달시도.Remove(f.Attempt!); break;
            case "inferred": f.Attempt!.유산추정여부 = true; break;
            case "round": f.Attempt!.추천라운드++; break;
            case "driver": f.Attempt!.기사Id = "previous-driver"; break;
            case "offer": f.Attempt!.제안Id = "previous-offer"; break;
            case "interrupted": f.Attempt!.상태Code = 음식배달시도상태Code.중단; break;
            case "latest-interrupted":
                f.Db.음식배달시도.Add(new 음식배달시도 { 시도StableId = "new-interrupted", 주문번호 = "food-r8",
                    제안Id = "offer-r8", 기사Id = "driver-r8", 추천라운드 = 2, 시도순번 = 2, Revision = 1,
                    상태Code = 음식배달시도상태Code.중단, 수락시각Utc = f.AssignedAt.AddSeconds(1) }); break;
            case "future-assignment": f.Attempt!.수락시각Utc = DateTime.UtcNow.AddMinutes(1); break;
            case "before-assignment": f.Location.기록시각 = f.AssignedAt.AddSeconds(-1); break;
            case "received-before-assignment": f.Location.CreatedAt = f.AssignedAt.AddSeconds(-1); break;
            case "stale":
                f.Attempt!.수락시각Utc = DateTime.UtcNow.AddMinutes(-5);
                f.Location.기록시각 = DateTime.UtcNow.AddSeconds(-31); break;
            case "future": f.Location.기록시각 = DateTime.UtcNow.AddSeconds(1); break;
            case "future-received": f.Location.CreatedAt = DateTime.UtcNow.AddSeconds(1); break;
            case "invalid-coordinates": f.Location.위도 = 91; break;
            case "negative-accuracy": f.Location.정확도_m = -1; break;
            case "ended-queue": f.Queue.배차큐단계 = 상태값.배차큐단계.종료; break;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.FoodAsync();
        Assert.NotEqual(음식배달위치추적상태코드.추적중, result.상태);
        Assert.Null(result.위도); Assert.Null(result.경도); Assert.Null(result.정확도_m);
        if (condition is "missing" or "inferred") Assert.Contains("배정", result.안내);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("round")]
    [InlineData("driver")]
    [InlineData("latest-corrupt")]
    [InlineData("future-assignment")]
    [InlineData("before-assignment")]
    [InlineData("received-before-assignment")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("future-received")]
    [InlineData("invalid-coordinates")]
    [InlineData("negative-accuracy")]
    [InlineData("ended-queue")]
    [InlineData("cancelled-request")]
    public async Task CargoHidesUnprovenStaleInvalidAndPreviousAssignmentLocations(string condition)
    {
        await using var f = await Fixture.CreateAsync(true);
        switch (condition)
        {
            case "missing": f.Db.운송이벤트.Remove(f.Assignment!); break;
            case "corrupt": f.Assignment!.메타데이터 = "{invalid"; break;
            case "round": f.Queue.추천라운드++; break;
            case "driver": f.Queue.확정기사Id = "new-driver"; break;
            case "latest-corrupt":
                f.Db.운송이벤트.Add(new 운송이벤트 { 의뢰Id = "cargo-r8", 이벤트타입 = CargoAcceptanceRecoveryMarker.EventType,
                    이벤트시각 = f.AssignedAt.AddSeconds(1), 메타데이터 = "{invalid" }); break;
            case "future-assignment": f.Assignment!.이벤트시각 = DateTime.UtcNow.AddMinutes(1); break;
            case "before-assignment": f.Location.기록시각 = f.AssignedAt.AddSeconds(-1); break;
            case "received-before-assignment": f.Location.CreatedAt = f.AssignedAt.AddSeconds(-1); break;
            case "stale":
                f.Assignment!.이벤트시각 = DateTime.UtcNow.AddMinutes(-20);
                f.Location.기록시각 = DateTime.UtcNow.AddMinutes(-11); break;
            case "future": f.Location.기록시각 = DateTime.UtcNow.AddSeconds(1); break;
            case "future-received": f.Location.CreatedAt = DateTime.UtcNow.AddSeconds(1); break;
            case "invalid-coordinates": f.Location.경도 = 181; break;
            case "negative-accuracy": f.Location.정확도_m = -1; break;
            case "ended-queue": f.Queue.배차큐단계 = 상태값.배차큐단계.종료; break;
            case "cancelled-request": f.Cargo!.상태 = 상태값.의뢰상태.취소; break;
        }
        await f.Db.SaveChangesAsync();
        var result = await f.CargoAsync();
        Assert.NotNull(result); // 위치를 숨겨도 소유자의 업무 조회는 계속 가능하다.
        Assert.Null(result.기사최근위도); Assert.Null(result.기사최근경도); Assert.Null(result.기사최근위치시각Utc);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ChangedAssignmentOrCompletionDuringLocationReadDiscardsCoordinates(bool cargo, bool complete)
    {
        await using var f = await Fixture.CreateAsync(cargo);
        f.Interceptor.OnLocation = () =>
        {
            using var other = f.OtherContext();
            if (complete && cargo) other.화주운송의뢰.Single().상태 = 상태값.의뢰상태.취소;
            else if (complete) other.음식주문.Single().상태 = 음식주문상태코드.전달완료;
            else
            {
                var queue = other.운송원장.Single(); queue.확정기사Id = "new-driver"; queue.추천라운드++;
            }
            other.SaveChanges();
        };
        if (cargo) Assert.Null((await f.CargoAsync())!.기사최근위도);
        else Assert.Null((await f.FoodAsync()).위도);
        Assert.True(f.Interceptor.Triggered);
    }

    [Fact]
    public async Task OtherOrderOwnerCannotReadCurrentAssignedLocation()
    {
        await using var f = await Fixture.CreateAsync(false);
        var result = await new 주문자음식주문조회UseCase(f.Db).상세Async("food-r8", "other-owner", default);
        Assert.True(result.IsFailed);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string database = Guid.NewGuid().ToString("N");
        // 저장소 root도 provider singleton이다. 이름은 매 fixture GUID라 데이터는 계속 격리한다.
        private static readonly InMemoryDatabaseRoot DatabaseRoot = new();
        private static readonly LocationInterceptor SharedInterceptor = new();
        public readonly LocationObservation Interceptor = new();
        public SsalddelContext Db { get; }
        public DateTime AssignedAt { get; } = DateTime.UtcNow.AddSeconds(-20);
        public 운송원장 Queue { get; private set; } = null!;
        public 기사위치기록 Location { get; private set; } = null!;
        public 음식배달시도? Attempt { get; private set; }
        public 운송이벤트? Assignment { get; private set; }
        public 화주운송의뢰? Cargo { get; private set; }
        private Fixture()
        {
            Db = new(new DbContextOptionsBuilder<SsalddelContext>()
                .UseInMemoryDatabase(database, DatabaseRoot).AddInterceptors(SharedInterceptor).Options, new Encryption());
            SharedInterceptor.Register(Db, Interceptor);
        }
        public SsalddelContext OtherContext() => new(new DbContextOptionsBuilder<SsalddelContext>()
            .UseInMemoryDatabase(database, DatabaseRoot).Options, new Encryption());
        public static async Task<Fixture> CreateAsync(bool cargo)
        {
            var f = new Fixture();
            f.Queue = new 운송원장 { 의뢰Id = cargo ? "cargo-r8" : "offer-r8", 원본의뢰Id = cargo ? "cargo-r8" : "food-r8",
                원본의뢰유형 = cargo ? "CargoTransport" : "FoodOrder", 배차업무유형 = cargo ? 상태값.배차업무유형.용달운송 : 상태값.배차업무유형.음식배달,
                확정기사Id = "driver-r8", 기사_운송자 = "driver-r8", 추천라운드 = 2,
                배차큐단계 = 상태값.배차큐단계.확정, 상태 = "운송중", UpdatedAt = f.AssignedAt };
            f.Location = new 기사위치기록 { 기사Id = "driver-r8", 위도 = 37.58m, 경도 = 127.08m, 정확도_m = 5,
                기록시각 = DateTime.UtcNow.AddSeconds(-5), CreatedAt = DateTime.UtcNow.AddSeconds(-4) };
            f.Db.운송원장.Add(f.Queue); f.Db.기사위치기록.Add(f.Location);
            if (cargo)
            {
                f.Cargo = new 화주운송의뢰 { 의뢰Id = "cargo-r8", 화주Id = "owner-r8", 주문자UserId = "owner-r8",
                    상태 = 상태값.의뢰상태.생성됨, 배차상태 = 상태값.배차상태.배차확정 };
                f.Db.화주운송의뢰.Add(f.Cargo);
                await f.Db.SaveChangesAsync();
                f.Assignment = CargoAcceptanceRecoveryMarker.Create(f.Queue, f.AssignedAt);
                f.Db.운송이벤트.Add(f.Assignment);
            }
            else
            {
                f.Db.음식주문.Add(new 음식주문 { 주문번호 = "food-r8", 음식점Id = 1, 주문자UserId = "owner-r8",
                    음식점명 = "시험 식당", 상태 = 음식주문상태코드.기사배정, 배차상태 = 음식주문배차상태코드.기사배정 });
                f.Attempt = new 음식배달시도 { 시도StableId = "food-r8-attempt-1", 주문번호 = "food-r8", 제안Id = "offer-r8",
                    기사Id = "driver-r8", 추천라운드 = 2, 시도순번 = 1, Revision = 1,
                    상태Code = 음식배달시도상태Code.수락, 수락시각Utc = f.AssignedAt };
                f.Db.음식배달시도.Add(f.Attempt);
            }
            await f.Db.SaveChangesAsync();
            return f;
        }
        public async Task<주문자음식배달위치추적응답> FoodAsync()
        {
            var result = await new 주문자음식주문조회UseCase(Db).상세Async("food-r8", "owner-r8", default);
            Assert.True(result.IsSuccess); return result.Value.기사위치;
        }
        public Task<Ssalddel.Contracts.Shipper.Request.화주운송의뢰응답?> CargoAsync()
            => new 의뢰단건조회QueryHandler(Db, new 화주운송업무담당자UseCase(Db, new Actor(), TimeProvider.System))
                .Handle(new 의뢰단건조회Query("cargo-r8"), default);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed record Actor : ICurrentUserAccessor
    { public string UserId => "owner-r8"; public string Role => "화주"; }
    private sealed class Encryption : IPersonalDataEncryptionService
    { public string? Protect(string? value) => value; public string? Unprotect(string? value) => value; }
    private sealed class LocationObservation
    {
        public Action? OnLocation;
        public bool Triggered;
    }
    // 같은 EF provider를 재사용하되 실제 읽기 중 변경 신호는 해당 context에만 결속한다.
    private sealed class LocationInterceptor : IMaterializationInterceptor
    {
        private readonly ConditionalWeakTable<DbContext, LocationObservation> observations = new();
        public void Register(DbContext context, LocationObservation observation) => observations.Add(context, observation);
        public object InitializedInstance(MaterializationInterceptionData data, object entity)
        {
            if (entity is 기사위치기록 && observations.TryGetValue(data.Context, out var observation)
                && observation.OnLocation is { } action && !observation.Triggered)
            { observation.Triggered = true; action(); }
            return entity;
        }
    }
}
