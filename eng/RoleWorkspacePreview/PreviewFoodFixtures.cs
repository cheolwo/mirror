using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Participants;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace RoleWorkspacePreview;

/// <summary>
/// 음식 제품 adapter의 실제 표시 경로에 공급하는 로컬 DTO 예시입니다.
/// 실제 주문·서버 조회·업무 전이·권한·기사 GPS를 증명하지 않습니다.
/// </summary>
public static class PreviewFoodFixtures
{
    public const string OrderId = "preview-order-1";
    public const string OfferId = "preview-offer-1";
    private const string DriverId = "preview-driver-1";
    private const long Revision = 3;
    private const string MenuSummary = "김밥 2개 · 국물 메뉴 1개";

    public static bool IsScenario(string value) => value is
        "offer" or "pickup" or "delivery" or "pickupdone" or "delivered" or "awaiting-driver" or "assigned"
        or "ready" or "ready-without-permission" or "valid-ready" or "ready-reassigning" or "dispatch-unavailable";

    public static bool RestaurantRequiresSelection(string scenario) => scenario is "complete" or "receipt" or "delivered";

    public static 음식주문응답 Restaurant(string scenario, bool noMap, DateTime recordedAt)
    {
        var waiting = scenario == "waiting";
        var awaitingDriver = scenario == "awaiting-driver";
        var assigned = scenario == "assigned";
        var afterPickup = scenario is "pickupdone" or "delivery";
        var delivered = scenario is "receipt" or "delivered";
        var complete = scenario == "complete";
        var recooking = scenario is "recooking" or "ready-without-permission" or "valid-ready";
        var ready = scenario is "ready" or "ready-without-permission" or "valid-ready" or "ready-reassigning"
            || afterPickup || delivered || complete;
        var actions = waiting
            ? OrderActions(음식배달가능행동Ids.음식점주문수락, 음식배달가능행동Ids.음식점주문거절)
            : awaitingDriver ? OrderActions(음식배달가능행동Ids.음식점조리시간변경)
            : assigned ? OrderActions(음식배달가능행동Ids.음식점조리시작, 음식배달가능행동Ids.음식점조리시간변경)
            : ready ? [] : OrderActions(음식배달가능행동Ids.음식점조리시간변경, 음식배달가능행동Ids.음식점픽업준비완료);
        if (scenario == "expired")
            foreach (var action in actions) action.ExpiresAtUtc = recordedAt.AddMinutes(-1);
        return new()
        {
            주문번호 = OrderId, 음식점Id = 1, 음식점명 = "음식점 1", Revision = Revision,
            음식점주소 = "서울 중랑구 사가정로 00", 음식점상세주소 = "1층",
            음식점위도 = noMap ? null : 37.580m, 음식점경도 = noMap ? null : 127.087m,
            수령인정보 = Recipient(), 상품목록 = Menu(), 총주문금액 = 18000,
            상태 = complete ? 음식주문상태코드.수령확인 : delivered ? 음식주문상태코드.전달완료
                : afterPickup ? 음식주문상태코드.픽업완료 : waiting ? 음식주문상태코드.주문대기
                : awaitingDriver ? 음식주문상태코드.주문확인 : assigned ? 음식주문상태코드.기사배정
                : ready ? 음식주문상태코드.픽업대기 : 음식주문상태코드.조리중,
            배차상태 = waiting ? 음식주문배차상태코드.미요청 : awaitingDriver || scenario == "ready-reassigning"
                ? 음식주문배차상태코드.배차대기 : scenario == "dispatch-unavailable" ? 음식주문배차상태코드.배차불가
                : afterPickup ? 음식주문배차상태코드.배달중 : delivered || complete ? 음식주문배차상태코드.배달완료
                : 음식주문배차상태코드.기사배정,
            CreatedAt = recordedAt.AddMinutes(-20), 최근변경시각Utc = recordedAt,
            CurrentPreparationRound = recooking ? 2 : 1,
            RecookingRequestedAtUtc = recooking ? recordedAt.AddMinutes(-5) : null,
            CurrentCookingStartedAtUtc = waiting || awaitingDriver || assigned ? null : recordedAt.AddMinutes(-5),
            CurrentPickupReadyAtUtc = ready ? recordedAt.AddMinutes(-1) : null,
            // 재조리 이전 첫 음식의 시각이 현재 회차 안내를 덮지 않아야 합니다.
            조리시작시각Utc = waiting || awaitingDriver || assigned ? null : recordedAt.AddMinutes(-15),
            픽업준비시각Utc = recooking ? recordedAt.AddMinutes(-10) : ready ? recordedAt.AddMinutes(-1) : null,
            조리시작가능 = assigned, 조리예상완료시각Utc = ready ? recordedAt.AddMinutes(-1) : recordedAt.AddMinutes(10),
            AvailableActions = actions
        };
    }

    public static 주문자음식주문상세응답 Orderer(string scenario, bool noMap, DateTime recordedAt)
    {
        var waiting = scenario == "waiting";
        var awaitingDriver = scenario == "awaiting-driver";
        var assigned = scenario == "assigned";
        var recovery = scenario == "dispatch-unavailable";
        var complete = scenario == "complete";
        var receipt = scenario is "receipt" or "delivered";
        var ended = complete || receipt;
        var hasDriver = !waiting && !awaitingDriver && !recovery;
        var stamp = scenario == "expired" ? recordedAt.AddSeconds(-45) : recordedAt;
        var state = ended ? 음식배달위치추적상태코드.종료 : !hasDriver ? 음식배달위치추적상태코드.추적전
            : DateTime.UtcNow - stamp >= TimeSpan.FromSeconds(30) ? 음식배달위치추적상태코드.갱신지연 : 음식배달위치추적상태코드.추적중;
        return new()
        {
            주문 = new()
            {
                주문번호 = OrderId, 음식점Id = 1, 음식점명 = "음식점 1", 상품요약 = MenuSummary, 상품종류수 = 2, 총수량 = 3,
                상태 = complete ? 음식주문상태코드.수령확인 : receipt ? 음식주문상태코드.전달완료
                    : waiting ? 음식주문상태코드.주문대기 : awaitingDriver ? 음식주문상태코드.주문확인
                    : assigned ? 음식주문상태코드.기사배정 : recovery ? 음식주문상태코드.조리중 : 음식주문상태코드.픽업완료,
                배차상태 = waiting ? 음식주문배차상태코드.미요청 : awaitingDriver ? 음식주문배차상태코드.배차대기
                    : recovery ? 음식주문배차상태코드.배차불가 : ended ? 음식주문배차상태코드.배달완료
                    : assigned ? 음식주문배차상태코드.기사배정 : 음식주문배차상태코드.배달중,
                총주문금액 = 18000, 조리예상완료시각Utc = recordedAt.AddMinutes(10), CreatedAtUtc = recordedAt.AddMinutes(-20)
            },
            음식점주소 = "서울 중랑구 사가정로 00", 음식점상세주소 = "1층",
            수령인정보 = Recipient(), 상품목록 = Menu(),
            배달진행 = new()
            {
                배차요청됨 = !waiting, 기사배정됨 = hasDriver, 기사전달완료 = ended, 주문자수령확인됨 = complete,
                수령확인가능 = receipt, 현재운송상태 = recovery ? 음식주문배차상태코드.배차불가
                    : awaitingDriver ? 음식주문배차상태코드.배차대기 : assigned ? DriverWorkOfferStatus.Accepted
                    : ended ? DriverWorkOfferStatus.Completed : hasDriver ? DriverWorkOfferStatus.MovingToDropoff : 음식주문배차상태코드.미요청,
                최근변경시각Utc = recordedAt,
                안내 = complete ? "음식 수령을 확인했습니다." : receipt ? "음식을 받은 뒤 수령을 확인해 주세요."
                    : waiting ? "음식점의 주문 응답을 기다리고 있습니다." : awaitingDriver ? "기사를 배정하고 있습니다."
                    : recovery ? "기사 배정 확인이 필요합니다." : assigned ? "기사 배정 후 음식 준비 상태를 확인하고 있습니다." : "음식을 전달하고 있습니다."
            },
            // 화면 배치용 좌표 예시이며 실제 기기의 GPS 위치가 아닙니다.
            기사위치 = new()
            {
                상태 = state, 기록시각Utc = !hasDriver || ended ? null : stamp, 조회시각Utc = DateTime.UtcNow,
                위도 = noMap || !hasDriver || ended ? null : 37.581m, 경도 = noMap || !hasDriver || ended ? null : 127.087m,
                안내 = state == 음식배달위치추적상태코드.갱신지연 ? "미리보기 위치 예시의 수신 시각이 오래됐습니다."
                    : !hasDriver ? "기사를 배정하면 위치를 확인할 수 있습니다." : ended ? "배달이 종료되었습니다." : "화면 배치용 기사 위치 예시입니다."
            },
            AvailableActions = receipt ? OrderActions(음식배달가능행동Ids.주문수령확인)
                : waiting ? OrderActions(음식배달가능행동Ids.주문취소) : []
        };
    }

    public static object ReadDriver(string path, string scenario, bool noMap, DateTime recordedAt) => path switch
    {
        "api/v1/driver/food-deliveries/workspace" => Driver(scenario, noMap, recordedAt),
        // 위치 공급자와 운행 응답 모두 기사 위치를 만들지 않습니다.
        "api/v1/driver/food-deliveries/work/status" => new 기사운행상태응답
            { DriverId = DriverId, Status = "운행중", UpdatedAt = recordedAt },
        "api/v1/driver/operational-dispatch/availability" => new 운영배차수신상태Dto
        {
            주체Id = DriverId, 수신의사Code = 운영배차수신의사Code.On,
            실효상태Code = 운영배차실효상태Code.배차가능, 서버관측시각Utc = new DateTimeOffset(recordedAt)
        },
        _ => throw new InvalidOperationException("미리보기에서 지원하지 않는 음식 기사 조회입니다.")
    };

    public static FoodDeliveryDriverWorkspaceDto Driver(string scenario, bool noMap, DateTime recordedAt)
    {
        var offer = scenario is "offer" or "expired";
        var empty = scenario is "waiting" or "complete";
        var expiry = scenario == "expired" ? recordedAt.AddMinutes(-1) : recordedAt.AddMinutes(10);
        return new()
        {
            DriverId = DriverId, MaxActiveDeliveries = 3, UpdatedAtUtc = recordedAt,
            Recommendations = offer ? [new()
            {
                OfferId = OfferId, RestaurantName = "음식점 1", OrderSummary = MenuSummary,
                Pickup = Pickup(noMap, recordedAt), Dropoff = Dropoff(noMap), DriverPayout = 4720, DistanceKm = 3.414m,
                ExpiresAtUtc = expiry, RecommendationReason = "화면 배치용 제안 예시",
                AvailableActions = [new() { ActionId = 음식배달가능행동Ids.기사제안수락, ExpiresAtUtc = expiry },
                    new() { ActionId = 음식배달가능행동Ids.기사제안거절, ExpiresAtUtc = expiry }]
            }] : [],
            ActiveDeliveries = offer || empty ? [] : [Active(scenario, noMap, recordedAt)]
        };
    }

    private static FoodDeliveryDriverActiveDeliveryDto Active(string scenario, bool noMap, DateTime recordedAt)
    {
        var delivery = scenario is "delivery" or "pickupdone";
        var recooking = scenario is "recooking" or "ready-without-permission" or "valid-ready";
        var arrived = delivery || recooking || scenario == "ready";
        var ready = delivery || scenario is "ready" or "ready-without-permission" or "valid-ready";
        var actions = new List<업무가능행동Dto>();
        if (delivery) actions.Add(new() { ActionId = 음식배달가능행동Ids.기사전달완료 });
        else if (!arrived) actions.Add(AttemptAction(음식배달가능행동Ids.기사가게도착));
        else if (ready && scenario != "ready-without-permission") actions.Add(new() { ActionId = 음식배달가능행동Ids.기사픽업확인 });
        actions.Add(AttemptAction(음식배달가능행동Ids.기사배달중단));
        return new()
        {
            TransportId = 101, OfferId = OfferId, RestaurantName = "음식점 1", OrderSummary = MenuSummary,
            Pickup = Pickup(noMap, recordedAt), Dropoff = Dropoff(noMap), DriverPayout = 4720,
            TransportStatus = "운송중", WorkStatus = delivery ? DriverWorkOfferStatus.MovingToDropoff
                : arrived ? DriverWorkOfferStatus.MovingToPickup : DriverWorkOfferStatus.Accepted,
            DeliveryAttemptId = "preview-attempt-1", AttemptRevision = Revision,
            RestaurantArrivedAtUtc = arrived ? recordedAt.AddMinutes(-3) : null,
            // 예정 시각이 지나도 현재 음식의 준비 완료·픽업 권한을 대신하지 않습니다.
            DisplayedPreparationReadyAtUtc = recooking ? recordedAt.AddMinutes(-1) : recordedAt.AddMinutes(10),
            CurrentPreparationRound = recooking ? 2 : 1, RecookingRequestedAtUtc = recooking ? recordedAt.AddMinutes(-5) : null,
            CurrentPickupReadyAtUtc = ready ? recordedAt.AddMinutes(-1) : null,
            Recipient = new() { DisplayName = "예시 수령인", ContactPhone = "•••• 0000", OrdererIsRecipient = true,
                DeliveryInstructions = "초인종을 누르지 말고 문 앞에 놓아 주세요." },
            AvailableActions = actions, UpdatedAtUtc = recordedAt
        };
    }

    private static 업무가능행동Dto[] OrderActions(params string[] ids)
        // 실제 projector의 revision 계약을 따른 DTO 형식 예시이며 권한을 발행하지 않습니다.
        => ids.Select(id => id is 음식배달가능행동Ids.음식점주문수락 or 음식배달가능행동Ids.주문수령확인
            ? new 업무가능행동Dto { ActionId = id }
            : new 업무가능행동Dto { ActionId = id, RevisionKindCode = 업무Revision종류Codes.음식주문, ExpectedRevision = Revision }).ToArray();
    private static 업무가능행동Dto AttemptAction(string id)
        => new() { ActionId = id, RevisionKindCode = 업무Revision종류Codes.음식배달시도, ExpectedRevision = Revision };
    private static 음식주문상품Dto[] Menu()
        => [new() { 상품명 = "김밥", 수량 = 2, 단가 = 5000 }, new() { 상품명 = "국물 메뉴", 수량 = 1, 단가 = 8000 }];
    private static 음식주문수령인정보Dto Recipient()
        => new() { 수령인명 = "예시 수령인", 연락처 = "•••• 0000", 주소 = "서울 중랑구 면목로 00", 상세주소 = "1층 출입문 앞",
            요청사항 = "국물은 별도 포장해 주세요.", 주문자본인수령여부 = true };
    private static FoodDeliveryDriverStopDto Pickup(bool noMap, DateTime recordedAt)
        => new() { Label = "음식점 1", Address = "서울 중랑구 사가정로 00, 1층", Latitude = noMap ? null : 37.580m,
            Longitude = noMap ? null : 127.087m, TargetAtUtc = recordedAt.AddMinutes(10) };
    private static FoodDeliveryDriverStopDto Dropoff(bool noMap)
        => new() { Label = "전달지", Address = "서울 중랑구 면목로 00, 1층 출입문 앞", Latitude = noMap ? null : 37.583m, Longitude = noMap ? null : 127.089m };
}

/// <summary>미리보기에서 기기 위치를 읽거나 가상 GPS를 공급하지 않습니다.</summary>
public sealed class PreviewFoodLocationProvider : IRoleWorkspaceLocationProvider
{
    public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<RoleWorkspaceLocation?>(null);
    }
}
