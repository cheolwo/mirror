using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>기존 메뉴/Identity 시드에 결속한 시험 준비. 주문 수락·조리·배달은 수행하지 않는다.</summary>
public sealed class 모바일음식배차시험준비Service(
    SsalddelContext db, 모바일음식배차시험Options options, 모바일음식배차시험Guard guard,
    모바일음식배차시험자료 fixtures, I국내화물운송기사상태Service driverState,
    I국내화물운송기사상태Store stateStore, IDriverLocationStore locations,
    IDriverWorkQueueStore workQueue, I음식배달권실행공간Store foodScopes,
    I운영배차공통UseCase receiving, TimeProvider timeProvider)
{
    public async Task PrepareAsync(CancellationToken ct)
    {
        guard.EnsureEnabled();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var driver = await IdentityAsync(모바일음식배차시험자료.DriverUserName, 역할명.기사, ct);
        var orderer = await IdentityAsync(모바일음식배차시험자료.OrdererUserName, 역할명.커뮤니티회원, ct);
        var restaurant = await IdentityAsync(모바일음식배차시험자료.RestaurantUserName, 역할명.음식점, ct);
        if (!await db.UserClaims.AsNoTracking().AnyAsync(x => x.UserId == restaurant
            && x.ClaimType == 음식점접근ClaimTypes.음식점Id && x.ClaimValue == "101", ct))
            throw new InvalidOperationException("MobileFoodDispatchRestaurantClaimMissing");

        await 모바일현장검증자료Seeder.SeedAsync(db, now, ct);
        var profile = await db.음식점공개프로필.AsNoTracking().SingleAsync(x => x.Id == 101, ct);
        var route = 모바일음식배차시험자료.ConfiguredRoute;
        if (profile.상호명 != 모바일음식배차시험자료.RestaurantName || profile.공개주소 != route.PickupAddress
            || profile.위도 != route.PickupLatitude || profile.경도 != route.PickupLongitude
            || !profile.소개.Contains(모바일현장검증자료Seeder.FixtureRevision, StringComparison.Ordinal))
            throw new InvalidOperationException("MobileFoodDispatchRestaurantFixtureConflict");

        var sharedDriver = await db.용달기사.SingleOrDefaultAsync(x => x.기사Id == driver, ct)
                           ?? throw new InvalidOperationException("MobileFoodDispatchDriverProfileMissing");
        if (sharedDriver.상태 != "활동중" || sharedDriver.차량 != "오토바이")
            throw new InvalidOperationException("MobileFoodDispatchDriverProfileConflict");
        var foodDriver = await db.Set<배달기사>().SingleOrDefaultAsync(x => x.기사Id == driver, ct);
        if (foodDriver is null)
        {
            foodDriver = new 배달기사
            {
                기사Id = driver, 기사명 = "모바일 시험 기사", 상태 = "활동중", 차량 = "오토바이",
                메모 = 모바일음식배차시험자료.DriverMemo, CreatedAt = now, UpdatedAt = now
            };
            db.Set<배달기사>().Add(foodDriver);
        }
        else if (foodDriver.메모 != 모바일음식배차시험자료.DriverMemo || foodDriver.차량 != "오토바이"
                 || foodDriver.상태 != "활동중")
            throw new InvalidOperationException("MobileFoodDispatchFoodDriverFixtureConflict");

        // 이전 근무/수신 OFF를 재시작 때 다시 켜지 않는다. 최초 합성 근무만 명시적 opt-in으로 만든다.
        var latestShift = await db.기사근무.Where(x => x.기사Id == driver)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var previousIntent = await receiving.수신상태조회Async(driver, ct);
        if (options.BootstrapSyntheticDriverState && latestShift is null && !previousIntent.수신의사변경시각Utc.HasValue)
        {
            latestShift = new 기사근무
            {
                기사Id = driver, 시작모드 = "바로시작", 시작시각 = now,
                시작위치 = 모바일음식배차시험자료.DriverStartLocation,
                운송실행유형 = 운송실행유형코드.음식배달, CreatedAt = now, UpdatedAt = now
            };
            db.기사근무.Add(latestShift);
            sharedDriver.운행상태 = 상태값.기사운행상태.운행중;
            sharedDriver.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            await receiving.기사의사변경Async(driver, new 운영배차수신의사변경요청
            {
                클라이언트요청Id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                    모바일음식배차시험자료.FixtureRevision + ":bootstrap:" + driver))[..16]),
                수신의사Code = 운영배차수신의사Code.On
            }, ct);
        }
        await db.SaveChangesAsync(ct);
        fixtures.Bind(new(driver, orderer, restaurant));
        await PrepareLocationAsync(latestShift, sharedDriver.운행상태, ct);
    }

    public async Task RefreshLocationAsync(CancellationToken ct)
    {
        guard.EnsureEnabled();
        if (!options.RefreshSyntheticDriverLocation || fixtures.Accounts is not { } accounts) return;
        var status = await db.용달기사.AsNoTracking().Where(x => x.기사Id == accounts.DriverId)
            .Select(x => x.운행상태).SingleOrDefaultAsync(ct);
        var shift = await db.기사근무.AsNoTracking().Where(x => x.기사Id == accounts.DriverId)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        await PrepareLocationAsync(shift, status, ct);
    }

    private async Task PrepareLocationAsync(기사근무? shift, string? status, CancellationToken ct)
    {
        if (fixtures.Accounts is not { } accounts || shift is null
            || status != 상태값.기사운행상태.운행중 || shift.운송실행유형 != 운송실행유형코드.음식배달
            || shift.시작위치 != 모바일음식배차시험자료.DriverStartLocation) return;
        var availability = await receiving.수신상태조회Async(accounts.DriverId, ct);
        if (!살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(availability)) return;
        // 앱에서 보낸 다른 위치·화물 상태를 합성 위치로 덮어쓰지 않는다.
        var existing = await stateStore.GetAsync(accounts.DriverId, ct);
        if (existing is not null && (existing.AppKey != 기사앱식별자.FoodDeliveryDriverApp
            || existing.운행상태 != 상태값.기사운행상태.운행중)) return;
        if (locations.TryGetLatest(accounts.DriverId, out var current)
            && (current.Latitude != 모바일음식배차시험자료.DriverLatitude
                || current.Longitude != 모바일음식배차시험자료.DriverLongitude || current.AccuracyM.HasValue)) return;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (existing is null)
        {
            await driverState.운행시작Async(accounts.DriverId, shift.Id, shift.시작시각 ?? shift.CreatedAt,
                shift.시작모드, shift.시작위치, null, appKey: 기사앱식별자.FoodDeliveryDriverApp, cancellationToken: ct);
        }
        await workQueue.UpsertAsync(new(accounts.DriverId, shift.Id, shift.시작시각 ?? shift.CreatedAt,
            shift.시작모드, shift.시작위치, null), ct);
        var location = new DriverLocationSnapshot(accounts.DriverId, 모바일음식배차시험자료.DriverLatitude,
            모바일음식배차시험자료.DriverLongitude, AccuracyM: null, 상태값.기사운행상태.운행중, now, now);
        // 시험 fixture의 갱신 시각이며 GPS 관측 행/정확도 또는 실제 운행 이력을 만들지 않는다.
        locations.Upsert(location);
        await driverState.위치갱신Async(location, shift.Id, 5m, 기사앱식별자.FoodDeliveryDriverApp, ct);
        var scope = 음식배달권정책.판정(new 배차경로좌표(location.Latitude, location.Longitude), null);
        await foodScopes.Upsert기사Async(scope.배달권키, accounts.DriverId,
            음식배달권정책.인접배달권키조회(scope.배달권키), ct);
    }

    private async Task<string> IdentityAsync(string userName, string role, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserName == userName, ct)
                   ?? throw new InvalidOperationException("MobileFoodDispatchSeedIdentityMissing");
        if (!await (from membership in db.UserRoles.AsNoTracking()
                    join definition in db.Roles.AsNoTracking() on membership.RoleId equals definition.Id
                    where membership.UserId == user.Id && definition.Name == role
                    select membership.UserId).AnyAsync(ct))
            throw new InvalidOperationException("MobileFoodDispatchSeedRoleMissing");
        return user.Id;
    }
}
