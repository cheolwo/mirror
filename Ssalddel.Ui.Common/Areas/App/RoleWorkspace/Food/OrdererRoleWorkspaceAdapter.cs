using System.Globalization;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>주문 소유자용 API 투영을 사용하며 역할 선택을 권한으로 판정하지 않습니다.</summary>
public sealed class OrdererRoleWorkspaceAdapter(IRoleWorkspaceApi api, TimeProvider? clock = null) : IRoleWorkspaceAdapter
{
    private readonly Dictionary<string, 주문자음식주문상세응답> details = new(StringComparer.Ordinal);
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    public string RoleKey => "orderer";

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var list = await api.GetAsync<주문자음식주문목록응답>(RoleKey, "api/v1/food-orders?page=1&pageSize=50", cancellationToken);
        var selected = string.IsNullOrWhiteSpace(selectedId) ? list.Items.FirstOrDefault()?.주문번호 : selectedId;
        주문자음식주문상세응답? detail = null;
        if (!string.IsNullOrWhiteSpace(selected))
            detail = await api.GetAsync<주문자음식주문상세응답>(RoleKey, $"api/v1/food-orders/{Uri.EscapeDataString(selected)}", cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var now = time.GetUtcNow();
        details.Clear();
        if (detail is not null && detail.주문.주문번호 == selected) details[selected!] = detail;
        var items = list.Items.Select(order => details.TryGetValue(order.주문번호, out var value) ? Map(value, now) : Map(order)).ToList();
        if (detail is not null && detail.주문.주문번호 == selected && items.All(value => value.Id != selected)) items.Insert(0, Map(detail, now));
        return new(RoleKey, items, list.TotalCount > list.Items.Count ? "최근 50건을 표시합니다. 주문 목록에서 이전 내역을 확인해 주세요." : null,
            selected, [new("food.new-order", "음식 주문", "/workspace/orderer/new", IsPrimary: true, RequiresConfirmation: false)]);
    }

    public async Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
    {
        if (!details.TryGetValue(itemId, out var detail)) throw new InvalidOperationException("선택 주문을 먼저 새로고침해 주세요.");
        FoodWorkspacePresentation.Require(detail.AvailableActions, actionKey);
        if (actionKey != 음식배달가능행동Ids.주문수령확인) throw new InvalidOperationException("필요한 내용을 입력한 뒤 요청해 주세요.");
        if (!detail.배달진행.수령확인가능 || detail.배달진행.주문자수령확인됨)
            throw new InvalidOperationException("최신 전달 상태를 확인한 뒤 수령을 확정해 주세요.");
        await api.PostAsync<음식주문응답>(RoleKey, $"api/v1/food-orders/{Uri.EscapeDataString(itemId)}/receipt-confirmation",
            new 주문자음식주문수령확인요청 { 클라이언트요청Id = requestId }, cancellationToken);
    }

    private static RoleWorkspaceItem Map(주문자음식주문요약응답 order) => new(order.주문번호,
        FoodWorkspacePresentation.Value(order.음식점명, "음식점 확인 필요"), FoodWorkspacePresentation.Value(order.상태, "상태 확인 필요"),
        order.상품요약, [FoodWorkspacePresentation.Section("주문 정보", ("주문번호", order.주문번호),
            ("주문 금액", FoodWorkspacePresentation.Money(order.총주문금액)), ("배차", order.배차상태),
            ("주문 시각", FoodWorkspacePresentation.Time(order.CreatedAtUtc)))], IsCurrent: IsCurrent(order.상태),
        Summary: new("주문 진행", Stage(order.상태),
            [new("조리 예정", FoodWorkspacePresentation.Time(order.조리예상완료시각Utc)), new("주문 금액", FoodWorkspacePresentation.Money(order.총주문금액))],
            new("다음 확인", "주문을 선택하면 배달 진행과 가능한 행동을 확인할 수 있어요.")));

    private static RoleWorkspaceItem Map(주문자음식주문상세응답 detail, DateTimeOffset now)
    {
        var order = detail.주문;
        var item = Map(order);
        var actions = new List<RoleWorkspaceAction>();
        if (FoodWorkspacePresentation.Available(detail.AvailableActions, 음식배달가능행동Ids.주문수령확인, "음식 수령 확인", true) is { } receipt)
            actions.Add(receipt with
            {
                Enabled = receipt.Enabled && detail.배달진행.수령확인가능 && !detail.배달진행.주문자수령확인됨,
                DisabledReason = detail.배달진행.수령확인가능 && !detail.배달진행.주문자수령확인됨
                    ? receipt.DisabledReason : "최신 전달·수령 상태를 확인해 주세요."
            });
        if (FoodWorkspacePresentation.Available(detail.AvailableActions, 음식배달가능행동Ids.주문취소, "주문 취소",
                route: FoodWorkspacePresentation.ActionRoute("orderer", 음식배달가능행동Ids.주문취소, order.주문번호)) is { } cancel) actions.Add(cancel);
        var sections = item.Sections!.Concat([FoodWorkspacePresentation.Section("배달 진행", ("진행 안내", detail.배달진행.안내),
            ("기사 전달", detail.배달진행.기사전달완료 ? "전달 완료" : "전달 대기"),
            ("내 수령 확인", detail.배달진행.주문자수령확인됨 ? "확인 완료" : "미확인")),
            FoodWorkspacePresentation.Section("주문 메뉴", detail.상품목록.Select(value =>
                (value.상품명, (string?)$"{value.수량}개 · {FoodWorkspacePresentation.Money(value.단가)}")).ToArray())]).ToArray();
        var tracking = detail.기사위치;
        var ended = tracking.상태 == 음식배달위치추적상태코드.종료 || !IsCurrent(order.상태)
            || order.상태 == 음식주문상태코드.전달완료 || detail.배달진행.기사전달완료;
        var measured = Utc(tracking.기록시각Utc);
        var received = Utc(tracking.조회시각Utc);
        var fresh = !ended && tracking.상태 == 음식배달위치추적상태코드.추적중
            && measured is { } measuredAt && received is { } receivedAt && measuredAt <= receivedAt
            && receivedAt <= now && now - measuredAt < TimeSpan.FromSeconds(30)
            && FoodWorkspacePresentation.Coordinates(tracking.위도, tracking.경도);
        IReadOnlyList<NeighborhoodMapMarker> markers = fresh
            ? FoodWorkspacePresentation.Marker(order.주문번호 + ":driver", "배달 기사 · 최근 수신 위치", NeighborhoodMapMarkerKinds.Driver, tracking.위도, tracking.경도)
                .Select(marker => marker with { MeasuredAt = measured, ReceivedAt = received, ExpiresAt = measured!.Value.AddSeconds(30) }).ToArray() : [];
        var locationNotice = ended ? "위치 추적 종료" : tracking.상태 == 음식배달위치추적상태코드.갱신지연
            || tracking.상태 == 음식배달위치추적상태코드.추적중 && !fresh ? "갱신 지연 · 현재 위치 확인 필요"
            : fresh ? "위치 기록 후 30초가 지나면 위치를 숨깁니다." : FoodWorkspacePresentation.Value(tracking.안내, "위치 확인 필요");
        if (!fresh && !ended && tracking.상태 is (음식배달위치추적상태코드.추적중 or 음식배달위치추적상태코드.갱신지연))
            sections = sections.Concat([FoodWorkspacePresentation.Section("위치 확인", ("안내", locationNotice))]).ToArray();
        var next = actions.Any(action => action.Key == 음식배달가능행동Ids.주문수령확인 && action.Enabled)
            ? "음식을 받은 뒤 수령을 확인해 주세요." : detail.배달진행.주문자수령확인됨 ? "수령 확인을 마쳤습니다."
            : !IsCurrent(order.상태) ? "처리된 주문 내역을 확인해 주세요."
            : order.상태 != 음식주문상태코드.전달완료 && RecoveryProgress(detail) is { } recovery
                ? OrdererFoodOrderPresentation.DeliveryRecoveryGuide(recovery)
            : FoodWorkspacePresentation.Value(detail.배달진행.안내, "최신 배달 진행을 확인해 주세요.");
        return item with { Sections = sections, Actions = actions, Markers = markers,
            Summary = new("주문 진행", Stage(order.상태),
                [new("조리 예정", FoodWorkspacePresentation.Time(order.조리예상완료시각Utc)),
                 new("최근 변경", FoodWorkspacePresentation.Time(detail.배달진행.최근변경시각Utc)),
                 new("위치 기록", LocationTime(tracking.기록시각Utc)), new("위치 조회", LocationTime(tracking.조회시각Utc)),
                 new("위치 안내", locationNotice)], new("다음 확인", next)) };
    }

    private static string Stage(string status) => status switch
    {
        음식주문상태코드.주문대기 => "음식점 응답 대기",
        음식주문상태코드.주문확인 => "기사 배정 대기",
        음식주문상태코드.기사배정 => "기사 배정됨 · 준비 상태 확인",
        음식주문상태코드.조리중 => "조리 중",
        음식주문상태코드.픽업대기 => "픽업 대기",
        음식주문상태코드.픽업완료 => "전달 중",
        음식주문상태코드.전달완료 => "수령 확인 대기",
        음식주문상태코드.수령확인 => "수령 확인 완료",
        음식주문상태코드.취소 => "주문 취소",
        음식주문상태코드.거절 => "주문 거절",
        _ => "진행 상태 확인 필요"
    };
    private static 주문자음식배달진행응답? RecoveryProgress(주문자음식주문상세응답 detail)
    {
        if (OrdererFoodOrderPresentation.NeedsDeliveryRecovery(detail.배달진행)) return detail.배달진행;
        var progress = new 주문자음식배달진행응답 { 현재운송상태 = detail.주문.배차상태 };
        return OrdererFoodOrderPresentation.NeedsDeliveryRecovery(progress) ? progress : null;
    }
    private static DateTimeOffset? Utc(DateTime? value) => value is null || value == default(DateTime)
        ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
    private static string LocationTime(DateTime? value) => Utc(value) is { } utc
        ? utc.AddHours(9).ToString("M/d HH:mm:ss", CultureInfo.InvariantCulture) + " (한국)" : "미확인";

    private static bool IsCurrent(string status) => status is not (음식주문상태코드.수령확인 or 음식주문상태코드.취소 or 음식주문상태코드.거절);
    public void Clear() => details.Clear();
}
