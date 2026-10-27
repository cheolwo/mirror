using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Tests.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Tests.Application.Food;

/// <summary>기존 격리 SQLite fixture에서 후보 선정 뒤의 화물 추천 저장·알림 관문만 검사한다.</summary>
public sealed partial class 음식배달정상수직생명주기Tests
{
    [Theory]
    [InlineData(운송자격Codes.Pending, false)]
    [InlineData(운송자격Codes.Suspended, false)]
    [InlineData("MissingGuard", false)]
    [InlineData(운송자격Codes.Valid, true)]
    public async Task DispatchQualification_화물원장전환은저장직전자격을확인하고_부적격추천과알림을만들지않는다(string status, bool expected)
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context; const string id = "qualification-transition";
        await QualificationSeed(db, id);
        var guard = status == "MissingGuard" ? null : new QualificationFixtureGuard(status);
        var notification = new QualificationNotification(new 배차추천알림Service(db, null!, null!,
            NullLogger<배차추천알림Service>.Instance, 신규업무자격: guard));
        var states = new QualificationDriverState();
        var service = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()), null!, notification,
            states, new 음식배달배차흐름Resolver(), 운영배차: 운영배차시험수신UseCase.허용,
            화물대상후보: new 용달대상후보시험Service(), 신규업무자격: guard);
        var result = await service.추천시작Async(id, "driver");
        Assert.Equal(expected, result.전환여부);
        db.ChangeTracker.Clear(); var row = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == id);
        Assert.Equal(expected ? "driver" : null, row.현재추천대상기사Id);
        Assert.Equal(expected ? 1 : 0, row.추천라운드);
        Assert.Equal(expected ? 1 : 0, notification.Calls);
        Assert.Equal(expected ? 1 : 0, states.Recommendations);
        Assert.Equal(expected ? 1 : 0, await db.배차추천알림Outbox.CountAsync());
        Assert.Equal(expected ? 1 : 0, await db.운영배차활동사건.CountAsync());
        if (guard is not null) Assert.True(guard.Calls >= 1);
        if (!expected) Assert.Equal(상태값.배차노출상태.계획대기, row.배차노출상태);
    }

    [Theory]
    [InlineData(운송자격Codes.Pending, false)]
    [InlineData(운송자격Codes.Suspended, false)]
    [InlineData("MissingGuard", false)]
    [InlineData(운송자격Codes.Valid, false)]
    public async Task DispatchQualification_근거와검증계획없는구형AI결과는_유효자격만으로추천저장하지않는다(string status, bool expected)
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context; const string id = "qualification-coordination";
        await QualificationSeed(db, id);
        var guard = status == "MissingGuard" ? null : new QualificationFixtureGuard(status);
        var notification = new QualificationNotification(new 배차추천알림Service(db, null!, null!,
            NullLogger<배차추천알림Service>.Instance, 신규업무자격: guard));
        var states = new QualificationDriverState();
        var store = new InMemory국내화물배달권실행공간Store();
        await store.Upsert운송의뢰Async("fixture-region", id, []);
        var service = new 국내화물배차조율적용Service(db, Options.Create(new 배차큐정책Options()), notification, states,
            store, NullLogger<국내화물배차조율적용Service>.Instance, 신규업무자격: guard);
        var plan = new 국내화물배차조율결과(DateTime.UtcNow,
            [new 국내화물배차제안(1, id, "driver", 1, 100, 3000, 4000, 1000, "격리 시험의 이전 AI 계획", [])],
            [], [], 3000, 4000, 1000);
        var result = await service.추천잠금적용Async(plan);
        Assert.Equal(expected ? 1 : 0, result.잠금건수);
        Assert.Equal(expected ? 0 : 1, result.실패건수);
        db.ChangeTracker.Clear(); var row = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == id);
        Assert.Equal(expected ? "driver" : null, row.현재추천대상기사Id);
        Assert.Equal(expected ? 1 : 0, row.추천라운드);
        Assert.Equal(expected ? 1 : 0, notification.Calls);
        Assert.Equal(expected ? 1 : 0, states.Recommendations);
        Assert.Equal(expected ? 1 : 0, await db.배차추천알림Outbox.CountAsync());
        var regions = await store.SnapshotAsync();
        Assert.Equal(!expected, regions.Any(x => x.미처리운송의뢰Ids.Contains(id)));
    }

    [Fact]
    public async Task DispatchQualification_요금계산중정지된기사는_계산한금액도복원하고_추천저장과알림을보류한다()
    {
        await using var database = await TestDatabase.CreateAsync();
        var db = database.Context; const string id = "qualification-during-pricing";
        await QualificationSeed(db, id);
        var guard = new QualificationFixtureGuard(운송자격Codes.Valid);
        var notification = new QualificationNotification(new 배차추천알림Service(db, null!, null!,
            NullLogger<배차추천알림Service>.Instance, 신규업무자격: guard));
        var service = new 배차대기원장전환Service(db, Options.Create(new 배차큐정책Options()), null!, notification,
            new QualificationDriverState(), new 음식배달배차흐름Resolver(), smallGoodsPricing: new QualificationRevokingPricing(guard),
            운영배차: 운영배차시험수신UseCase.허용, 화물대상후보: new 용달대상후보시험Service(), 신규업무자격: guard);
        var result = await service.추천시작Async(id, "driver");
        Assert.False(result.전환여부); Assert.Equal(0, notification.Calls);
        Assert.True(guard.Calls >= 1);
        db.ChangeTracker.Clear(); var row = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == id);
        Assert.Null(row.현재추천대상기사Id); Assert.Null(row.기사지급예정액); Assert.Null(row.기사제안요금계산근거Json);
        Assert.Equal(0, row.추천라운드);
        Assert.Empty(await db.배차추천알림Outbox.AsNoTracking().ToArrayAsync());
        Assert.Empty(await db.운영배차활동사건.AsNoTracking().ToArrayAsync());
    }

    private static async Task QualificationSeed(살뜰.Data.SsalddelContext db, string id)
    {
        db.운송원장.Add(new 운송원장 { 의뢰Id = id, 운송번호 = id, 원본의뢰Id = id,
            배차업무유형 = 상태값.배차업무유형.용달운송, 상태 = 상태값.배차대기상태.대기,
            배차큐단계 = 상태값.배차큐단계.계획배차, 배차노출상태 = 상태값.배차노출상태.계획대기 });
        db.화주운송의뢰.Add(new 화주운송의뢰 { 의뢰Id = id, 차량종류 = "1톤 카고", 화물종류 = "시험 상자",
            화물중량Kg = 10, 화물길이Mm = 300, 화물폭Mm = 200, 화물높이Mm = 100,
            픽업_도로명주소 = "격리 시험 픽업지", 하차_도로명주소 = "격리 시험 전달지" });
        await db.SaveChangesAsync();
    }
    private sealed class QualificationFixtureGuard(string status) : I신규화물업무자격Guard
    {
        public string Status { get; set; } = status;
        public int Calls { get; private set; }
        public Task<운송자격평가결과> 평가Async(string driverId, CancellationToken ct = default)
        { Calls++; return Task.FromResult(new 운송자격평가결과(Status == 운송자격Codes.Valid, "Qualification." + Status,
            "격리 시험 자격 상태", Status)); }
    }
    private sealed class QualificationNotification(I배차추천알림Service actual) : I배차추천알림Service
    {
        public int Calls { get; private set; }
        public Task 추천알림요청생성Async(long id, string request, string driver, int round, CancellationToken ct = default)
        { Calls++; return actual.추천알림요청생성Async(id, request, driver, round, ct); }
        public Task<int> 대기알림발송Async(int take = 100, CancellationToken ct = default) => throw new NotSupportedException("외부 푸시는 실행하지 않습니다.");
    }
    private sealed class QualificationDriverState : I국내화물운송기사상태Service
    {
        public int Recommendations { get; private set; }
        public Task<국내화물운송기사상태Snapshot?> 추천기록Async(string id, DateTime now, CancellationToken ct = default)
        { Recommendations++; return Task.FromResult<국내화물운송기사상태Snapshot?>(null); }
        public Task<국내화물운송기사상태Snapshot> 운행시작Async(string id, long shift, DateTime started, string mode, string location,
            string? destination, string? preference = null, string? appKey = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot> 위치갱신Async(DriverLocationSnapshot location, long? shift = null,
            decimal? radius = null, string? appKey = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<국내화물운송기사상태Snapshot?> 후보없음기록Async(string id, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
        public Task 운행종료Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class QualificationRevokingPricing(QualificationFixtureGuard guard) : I소형배송기사제안요금Service
    {
        public Task<Result> 준비Async(운송원장 queue, CancellationToken ct = default)
        { queue.기사지급예정액 = 9999; queue.기사제안요금계산근거Json = "test-priced"; guard.Status = 운송자격Codes.Suspended; return Task.FromResult(Result.Ok()); }
        public Task<Result> 조회검증Async(운송원장 queue, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> 수락검증Async(운송원장 queue, string driverId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
