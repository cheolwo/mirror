using System.Net;
using System.Text.Json;
using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

/// <summary>관리자 전용 원장과 실제 배차 검토 좌표를 별도 관리자 자격으로 읽습니다.</summary>
public sealed class OperatorRoleWorkspaceAdapter(IRoleWorkspaceApi api) : IRoleWorkspaceAdapter
{
    public string RoleKey => "operator";
    public const string ReviewInterruption = "food-operator.interruption-review";

    public async Task<RoleWorkspaceSnapshot> LoadAsync(string? selectedId, CancellationToken cancellationToken)
    {
        var list = await api.GetAsync<AdminFoodOrderListDto>(RoleKey, "api/v1/admin/food-orders/operations?page=1&pageSize=50", cancellationToken);
        var selected = string.IsNullOrWhiteSpace(selectedId) ? list.Items.FirstOrDefault()?.OrderNo : selectedId;
        음식주문운영추적응답? detail = null;
        if (!string.IsNullOrWhiteSpace(selected))
            detail = await api.GetAsync<음식주문운영추적응답>(RoleKey, $"api/v1/admin/food-orders/{Uri.EscapeDataString(selected)}/operations-trace", cancellationToken);
        if (detail is not null && detail.주문번호 != selected) throw new InvalidOperationException("운영 주문 응답을 확인해 주세요.");
        FoodDeliveryDispatchAIReviewWorkspaceDto? map = null;
        string? mapNotice = null;
        try
        {
            map = await api.GetAsync<FoodDeliveryDispatchAIReviewWorkspaceDto>(RoleKey, "api/v1/admin/dispatch/food-delivery-ai-review", cancellationToken);
        }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is not (401 or 403)) { mapNotice = MapUnavailable; }
        catch (HttpRequestException ex) when (ex.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)) { mapNotice = MapUnavailable; }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested && ex.InnerException is TimeoutException) { mapNotice = MapUnavailable; }
        catch (JsonException) { mapNotice = MapUnavailable; }
        var actualMap = string.Equals(map?.Source, "actual", StringComparison.Ordinal);
        cancellationToken.ThrowIfCancellationRequested();
        var items = list.Items.Select(order => Map(order, detail?.주문번호 == order.OrderNo ? detail : null,
            actualMap ? map!.Orders.FirstOrDefault(value => value.OrderNo == order.OrderNo) : null)).ToList();
        if (detail is not null && items.All(value => value.Id != detail.주문번호))
            items.Insert(0, Map(new() { OrderNo = detail.주문번호, RestaurantName = detail.음식점명, OrderStatus = detail.주문상태, DispatchStatus = detail.배차상태 }, detail,
                actualMap ? map!.Orders.FirstOrDefault(value => value.OrderNo == detail.주문번호) : null));
        return new(RoleKey, items, mapNotice ?? (actualMap ? null : map is null ? MapUnavailable
            : "현재 배차 검토 자료는 예시를 포함하므로 지도에 표시하지 않습니다."), selected);
    }

    private static RoleWorkspaceItem Map(AdminFoodOrderListItemDto order, 음식주문운영추적응답? trace, FoodDeliveryDispatchAIReviewOrderDto? map)
    {
        var sections = new List<RoleWorkspaceSection>
        {
            FoodWorkspacePresentation.Section("음식 운영", ("주문번호", order.OrderNo), ("음식점", order.RestaurantName),
                ("주문 상태", order.OrderStatus), ("배차", order.DispatchStatus), ("최근 변경", FoodWorkspacePresentation.Time(order.UpdatedAtUtc)))
        };
        var actions = new List<RoleWorkspaceAction>();
        if (trace is not null)
        {
            sections.Add(FoodWorkspacePresentation.Section("현재 인계", ("단계", AdminFoodOperationsState.StageText(trace.생명주기조화.현재단계Code)),
                ("담당", string.Join(" · ", trace.생명주기조화.현재책임주체Codes.Select(AdminFoodOperationsState.ActorText))),
                ("안내", trace.생명주기조화.운영자확인필요여부 ? "운영자 확인 필요" : "현재 상태 확인")));
            foreach (var warning in trace.경고목록) sections.Add(FoodWorkspacePresentation.Section("확인 필요", ("안내", warning)));
            foreach (var guide in trace.복구안내목록) sections.Add(FoodWorkspacePresentation.Section("복구 안내", ("안내", guide)));
            foreach (var attempt in trace.배달시도목록.Where(value => value.상태Code == 음식배달시도상태Code.중단))
                actions.Add(new(ReviewInterruption, "배달 중단 검토", FoodWorkspacePresentation.ActionRoute("operator", ReviewInterruption, order.OrderNo)
                    + "&attemptId=" + Uri.EscapeDataString(attempt.시도StableId), IsPrimary: actions.Count == 0));
        }
        var markers = map is null ? [] : FoodWorkspacePresentation.Marker(order.OrderNo + ":pickup", "음식점", NeighborhoodMapMarkerKinds.Pickup,
                map.RestaurantLatitude, map.RestaurantLongitude).Concat(FoodWorkspacePresentation.Marker(order.OrderNo + ":dropoff", "전달", NeighborhoodMapMarkerKinds.Dropoff,
                map.CustomerLatitude, map.CustomerLongitude)).ToArray();
        return new(order.OrderNo, FoodWorkspacePresentation.Value(order.RestaurantName, "음식점 확인 필요"),
            trace is null ? FoodWorkspacePresentation.Value(order.OrderStatus, "상태 확인 필요") : AdminFoodOperationsState.StageText(trace.생명주기조화.현재단계Code),
            order.OrderNo, sections, actions, markers, IsCurrent: order.OrderStatus is not (음식주문상태코드.수령확인 or 음식주문상태코드.취소 or 음식주문상태코드.거절),
            Summary: Summary(order, trace, actions));
    }

    private const string MapUnavailable = "배차 지도를 불러오지 못했습니다. 주문 정보는 확인할 수 있습니다. 새로고침해 주세요.";
    private static RoleWorkspaceSummary Summary(AdminFoodOrderListItemDto order, 음식주문운영추적응답? trace, IReadOnlyList<RoleWorkspaceAction> actions)
    {
        var stage = trace is null ? FoodWorkspacePresentation.Value(order.OrderStatus, "상태 확인 필요")
            : AdminFoodOperationsState.StageText(trace.생명주기조화.현재단계Code);
        var updated = trace is { 최근변경시각Utc: var latest } && latest != default ? latest : order.UpdatedAtUtc;
        var owner = trace is null ? "담당 확인 필요" : FoodWorkspacePresentation.Value(
            string.Join(" · ", trace.생명주기조화.현재책임주체Codes.Select(AdminFoodOperationsState.ActorText)), "담당 확인 필요");
        var attention = trace is null ? "상세 확인 필요" : trace.생명주기조화.운영자확인필요여부 ? "운영자 확인 필요"
            : trace.경고목록.Count > 0 || trace.복구안내목록.Count > 0 ? "확인 안내 있음" : "현재 인계 확인";
        var metrics = new List<RoleWorkspaceField>
        {
            new("담당", owner), new("최근 변경", FoodWorkspacePresentation.Time(updated)), new("확인 사항", attention)
        };
        if (trace is not null) metrics.Add(new("조회 시각", FoodWorkspacePresentation.Time(trace.조회시각Utc)));
        var next = actions.FirstOrDefault(action => action.IsPrimary && action.Enabled)?.Label
            ?? (trace?.생명주기조화.운영자확인필요여부 == true || trace?.경고목록.Count > 0 || trace?.복구안내목록.Count > 0
                ? "확인 필요 사항을 상세에서 검토해 주세요." : "현재 인계와 주문 상태를 확인해 주세요.");
        return new("현재 인계", stage, metrics, new("다음 확인", next));
    }

    public Task PerformAsync(string itemId, string actionKey, Guid requestId, CancellationToken cancellationToken)
        => throw new InvalidOperationException("중단 검토 화면에서 판정과 근거를 입력해 주세요.");
    public void Clear() { }
}
