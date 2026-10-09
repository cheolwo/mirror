using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.Services.Dispatch.Engine;

namespace Ssalddel.Services.Development.MobileFieldTest;

/// <summary>명시적으로 등록한 시험 자료다. 지도 조회, GPS 또는 실제 주행 근거가 아니다.</summary>
public sealed record 모바일음식배차시험경로(
    string FixtureId, string SourceCode, string ExecutionModeCode,
    string PickupAddress, decimal PickupLatitude, decimal PickupLongitude,
    string DropoffAddress, decimal DropoffLatitude, decimal DropoffLongitude,
    decimal DistanceKm, bool IsEstimated, bool ActualMapRequest);

public sealed class 모바일음식배차시험자료
{
    public const string FixtureRevision = "sagajeong-food-mobile-dispatch.r1";
    public const string DistanceSourceCode = "MobileFieldTestSimulationRouteFixture";
    public const string DriverUserName = "driver1@ssalddel.local";
    public const string OrdererUserName = "field-orderer";
    public const string RestaurantUserName = "restaurant101";
    public const long RestaurantId = 101;
    public const string RestaurantName = "사가정 한상 샘플";
    public const string PickupAddress = "서울특별시 중랑구 사가정역 내부 검증권역";
    public const string DropoffAddress = "모바일 음식 배달 시험 수령지";
    public const string DriverStartLocation = "모바일 음식 배달 시험 대기점";
    public const decimal DriverLatitude = 37.588m;
    public const decimal DriverLongitude = 127.084m;
    public const string DriverMemo = "합성 기사 · GPS/실제 운행 아님 · " + FixtureRevision;
    public static 모바일음식배차시험경로 ConfiguredRoute { get; } = new(
        FixtureRevision, DistanceSourceCode, "Simulation", PickupAddress, 37.580450m, 127.087950m,
        DropoffAddress, 37.582000m, 127.090000m, 0.350m, IsEstimated: true, ActualMapRequest: false);

    private 모바일음식배차시험계정? _accounts;
    public 모바일음식배차시험계정? Accounts => Volatile.Read(ref _accounts);
    public bool IsReady => Accounts is not null;
    internal void Bind(모바일음식배차시험계정 accounts)
    {
        if (string.IsNullOrWhiteSpace(accounts.DriverId) || string.IsNullOrWhiteSpace(accounts.OrdererId)
            || string.IsNullOrWhiteSpace(accounts.RestaurantUserId))
            throw new InvalidOperationException("MobileFoodDispatchIdentityMissing");
        var previous = Interlocked.CompareExchange(ref _accounts, accounts, null);
        if (previous is not null && previous != accounts)
            throw new InvalidOperationException("MobileFoodDispatchIdentityChanged");
    }

    internal bool MatchesRoute(운송원장 queue)
    {
        var route = ConfiguredRoute;
        return queue.배차업무유형 == 상태값.배차업무유형.음식배달
               && queue.원본의뢰유형 == 운송의뢰배차원천유형.음식점주문
               && queue.원본의뢰Id == queue.의뢰Id && !string.IsNullOrWhiteSpace(queue.의뢰Id)
               && queue.화주Id == "restaurant:101"
               && queue.픽업_도로명주소 == route.PickupAddress
               && queue.하차_도로명주소 == route.DropoffAddress
               && queue.픽업_위도 == route.PickupLatitude && queue.픽업_경도 == route.PickupLongitude
               && queue.하차_위도 == route.DropoffLatitude && queue.하차_경도 == route.DropoffLongitude;
    }

    internal async Task<bool> IsFixtureQueueAsync(SsalddelContext db, 운송원장 queue, CancellationToken ct)
    {
        var accounts = Accounts;
        if (accounts is null || !MatchesRoute(queue)) return false;
        return await db.음식주문.AsNoTracking().AnyAsync(order => order.주문번호 == queue.원본의뢰Id
            && order.주문자UserId == accounts.OrdererId && order.음식점Id == RestaurantId
            && order.음식점명 == RestaurantName && order.음식점주소 == PickupAddress
            && order.수령지주소 == DropoffAddress && !order.모의처리처수행
            && (order.상태 == "주문확인" || order.상태 == "조리중" || order.상태 == "픽업대기" || order.상태 == "기사배정")
            // 공통 주문 수락폼에는 좌표 입력이 없다. 양쪽 미입력만 허용하고
            // 명시적인 시험 경로에 결속된 원장의 양끝 좌표 검사는 MatchesRoute가 유지한다.
            && ((order.음식점위도 == null && order.음식점경도 == null)
                || (order.음식점위도 == ConfiguredRoute.PickupLatitude
                    && order.음식점경도 == ConfiguredRoute.PickupLongitude)), ct);
    }
}

public sealed record 모바일음식배차시험계정(string DriverId, string OrdererId, string RestaurantUserId);
