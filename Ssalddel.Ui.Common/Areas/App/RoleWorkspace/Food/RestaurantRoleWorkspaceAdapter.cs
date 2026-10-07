using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

public sealed class RestaurantRoleWorkspaceAdapter(IRoleWorkspaceApi api) : IRoleWorkspaceAdapter
{
    private readonly Dictionary<string, 음식주문응답> orders = new(StringComparer.Ordinal);
    public string RoleKey => "restaurant";

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var list = await api.GetAsync<음식점주문수신함응답>(RoleKey,
            "api/v1/food-orders/restaurant/inbox?처리상태=" + Uri.EscapeDataString(음식점주문수신함처리상태코드.미처리) + "&Page=1&PageSize=50", cancellationToken);
        var selected = string.IsNullOrWhiteSpace(selectedId) ? list.Items.FirstOrDefault()?.주문번호 : selectedId;
        var values = list.Items.ToDictionary(value => value.주문번호, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            var detail = await api.GetAsync<음식주문응답>(RoleKey, $"api/v1/food-orders/restaurant/inbox/{Uri.EscapeDataString(selected)}", cancellationToken);
            if (detail.주문번호 != selected) throw new InvalidOperationException("음식점 주문 응답을 확인해 주세요.");
            values[selected] = detail;
        }
        cancellationToken.ThrowIfCancellationRequested(); orders.Clear();
        foreach (var value in values) orders.Add(value.Key, value.Value);
        return new(RoleKey, values.Values.Select(Map).ToArray(),
            list.TotalCount > list.Items.Count ? "미처리 주문 최근 50건을 표시합니다." : null, selected);
    }

    public async Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
    {
        if (!orders.TryGetValue(itemId, out var order)) throw new InvalidOperationException("선택 주문을 먼저 새로고침해 주세요.");
        var allowed = FoodWorkspacePresentation.Require(order.AvailableActions, actionKey);
        var operation = actionKey switch
        {
            음식배달가능행동Ids.음식점조리시작 => 음식점주문진행작업코드.조리시작,
            음식배달가능행동Ids.음식점픽업준비완료 => 음식점주문진행작업코드.픽업준비,
            _ => throw new InvalidOperationException("필요한 내용을 입력한 뒤 요청해 주세요.")
        };
        if (operation == 음식점주문진행작업코드.조리시작 && !order.조리시작가능)
            throw new InvalidOperationException("기사 배정과 현재 조리 상태를 새로고침해 주세요.");
        await api.PostAsync<음식주문응답>(RoleKey, $"api/v1/food-orders/{Uri.EscapeDataString(itemId)}/restaurant-progress",
            new 음식점주문진행변경요청 { 클라이언트요청Id = requestId, 예상Revision = allowed.ExpectedRevision ?? order.Revision, 작업 = operation }, cancellationToken);
    }

    private static RoleWorkspaceItem Map(음식주문응답 order)
    {
        var choices = new[]
        {
            (음식배달가능행동Ids.음식점주문수락, "주문 수락", true),
            (음식배달가능행동Ids.음식점조리시작, "조리 시작", false),
            (음식배달가능행동Ids.음식점픽업준비완료, "픽업 준비 완료", false),
            (음식배달가능행동Ids.음식점조리시간변경, "조리 시간 변경", true),
            (음식배달가능행동Ids.음식점주문거절, "주문 거절", true)
        };
        var actions = choices.Select(value => FoodWorkspacePresentation.Available(order.AvailableActions, value.Item1, value.Item2,
            false, value.Item3 ? FoodWorkspacePresentation.ActionRoute("restaurant", value.Item1, order.주문번호) : null))
            .OfType<RoleWorkspaceAction>().Select(value => value.Key == 음식배달가능행동Ids.음식점조리시작 && !order.조리시작가능
                ? value with { Enabled = false, DisabledReason = "기사 배정과 현재 조리 상태를 확인해 주세요." } : value).ToArray();
        var primary = actions.FirstOrDefault(value => value.Enabled && value.Key is (음식배달가능행동Ids.음식점주문수락
            or 음식배달가능행동Ids.음식점조리시작 or 음식배달가능행동Ids.음식점픽업준비완료))?.Key;
        actions = actions.Select(value => value with { IsPrimary = value.Key == primary }).ToArray();
        var recooking = order.CurrentPreparationRound > 1 || order.RecookingRequestedAtUtc.HasValue;
        var sections = new[]
        {
            FoodWorkspacePresentation.Section("주문 정보", ("주문번호", order.주문번호), ("주문 금액", FoodWorkspacePresentation.Money(order.총주문금액)), ("배차", order.배차상태),
                ("요청사항", FoodWorkspacePresentation.Value(order.수령인정보.요청사항, "별도 요청사항 없음"))),
            FoodWorkspacePresentation.Section("조리·픽업 준비", ("현재 음식", recooking ? "재조리 음식" : "첫 조리 음식"),
                ("조리 회차", order.CurrentPreparationRound > 0 ? $"{order.CurrentPreparationRound}회차" : "미확인"),
                ("준비 예정", FoodWorkspacePresentation.Time(order.조리예상완료시각Utc)),
                ("현재 음식 준비 완료", FoodWorkspacePresentation.Time(order.CurrentPickupReadyAtUtc))),
            FoodWorkspacePresentation.Section("주문 메뉴", order.상품목록.Select(value => (value.상품명, (string?)$"{value.수량}개")).ToArray())
        };
        return new(order.주문번호, "주문 " + order.주문번호, FoodWorkspacePresentation.Value(order.상태, "상태 확인 필요"),
            order.음식점명, sections, actions, FoodWorkspacePresentation.Marker(order.주문번호 + ":pickup", "음식점", NeighborhoodMapMarkerKinds.Pickup,
                order.음식점위도, order.음식점경도), IsCurrent: 음식점주문수신함처리상태코드.미처리여부(order.상태),
            Summary: Summary(order, actions, recooking));
    }

    private static RoleWorkspaceSummary Summary(음식주문응답 order, IReadOnlyList<RoleWorkspaceAction> actions, bool recooking)
    {
        var menu = string.Join(" · ", order.상품목록.Take(2).Select(value => $"{value.상품명} {value.수량}개"));
        if (order.상품목록.Count > 2) menu += $" · 외 {order.상품목록.Count - 2}종";
        var ready = KnownTime(order.CurrentPickupReadyAtUtc) ?? (recooking ? null : KnownTime(order.픽업준비시각Utc));
        var cooking = KnownTime(order.CurrentCookingStartedAtUtc) ?? (recooking ? null : KnownTime(order.조리시작시각Utc));
        var afterPickup = order.상태 is 음식주문상태코드.픽업완료 or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인;
        var stage = afterPickup || !음식점주문수신함처리상태코드.미처리여부(order.상태) ? FoodWorkspacePresentation.Value(order.상태, "상태 확인 필요")
            : recooking ? ready.HasValue ? "재조리 준비 완료" : cooking.HasValue ? "재조리 중" : "재조리 대기"
            : FoodWorkspacePresentation.Value(order.상태, "상태 확인 필요");
        var next = NextGuide(order, actions, ready, cooking);
        return new("주문 메뉴", FoodWorkspacePresentation.Value(menu, "메뉴 확인 필요"),
            [new("조리 단계", stage), new("준비 예정", FoodWorkspacePresentation.Time(order.조리예상완료시각Utc)),
             new("준비 완료", FoodWorkspacePresentation.Time(ready)), new("최근 변경", FoodWorkspacePresentation.Time(order.최근변경시각Utc))],
            new("다음 행동", next));
    }

    private static string NextGuide(음식주문응답 order, IReadOnlyList<RoleWorkspaceAction> actions, DateTime? ready, DateTime? cooking)
    {
        if (order.상태 == 음식주문상태코드.픽업완료) return "기사가 음식을 전달하고 있습니다.";
        if (order.상태 == 음식주문상태코드.전달완료) return "음식 전달이 완료되었습니다. 주문자의 수령 확인을 기다립니다.";
        if (order.상태 == 음식주문상태코드.수령확인) return "주문자가 수령을 확인했습니다. 이 주문은 완료됐습니다.";
        if (!음식점주문수신함처리상태코드.미처리여부(order.상태)) return "처리된 주문 내역을 확인해 주세요.";
        var primaryAction = actions.FirstOrDefault(action => action.IsPrimary && action.Enabled);
        var primary = primaryAction?.Label;
        var recovery = order.배차상태 is 음식주문배차상태코드.배차불가 or "추천만료" or "수락취소" or "배차취소";
        if (recovery)
            return primary is null ? "기사 배정 확인이 필요합니다. 최신 배차 상태를 확인하고 운영자 안내를 기다려 주세요."
                : primary + " · 기사 배정 확인이 필요합니다. 최신 배차 안내를 함께 확인해 주세요.";
        if (primary is not null)
            return primaryAction!.Key == 음식배달가능행동Ids.음식점픽업준비완료 && order.배차상태 != 음식주문배차상태코드.기사배정
                ? primary + " · 기사 재배정을 기다리고 있습니다." : primary;
        if (ready.HasValue)
            return order.배차상태 == 음식주문배차상태코드.기사배정
                ? "기사에게 현재 준비된 음식을 인계해 주세요."
                : "현재 음식은 준비됐습니다. 기사 재배정을 기다리고 최신 배차 상태를 확인해 주세요.";
        if (cooking.HasValue)
            return order.배차상태 == 음식주문배차상태코드.기사배정
                ? "현재 음식의 준비 상태와 가능한 행동을 새로고침해 주세요."
                : "현재 음식은 조리 중입니다. 기사 재배정을 기다리고 준비 상태를 확인해 주세요.";
        if (order.상태 is 음식주문상태코드.주문확인 or 음식주문상태코드.기사배정)
            return "기사 배정과 조리 시작 가능 여부를 확인해 주세요.";
        return "최신 주문 상태와 가능한 행동을 확인해 주세요.";
    }

    private static DateTime? KnownTime(DateTime? value) => value == default(DateTime) ? null : value;

    public void Clear() => orders.Clear();
}
