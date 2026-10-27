using System.Text.Json;
using RestaurantDeskApp.Models.Restaurant;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp;

// 별도 circuit의 합성 자료다. 서버 장애 fallback이나 실제 업무 원장이 아니다.
public sealed class Pos미리보기자료(Pos미리보기Realtime realtime)
{
    private readonly List<음식점주문DeskItem> orders = Seed();
    public string ControlOrderNo { get; set; } = "DEMO-001";
    public string ControlMessage { get; private set; } = "실제 배차·픽업·인쇄 아님";
    public bool ReadFailure { get; private set; }
    public bool PrintFailure { get; set; }
    public int PrintRequests { get; set; }

    public IReadOnlyList<음식점주문DeskItem> 목록()
        => orders.Select(Clone).ToArray();

    public 음식점주문DeskItem? 조회(string orderNo)
        => orders.FirstOrDefault(x => x.주문번호 == orderNo) is { } item ? Clone(item) : null;

    internal 음식점주문DeskItem? Mutate(string orderNo, string actionId, Action<음식점주문DeskItem> action)
    {
        var order = orders.FirstOrDefault(x => x.주문번호 == orderNo);
        if (order is null) return null;
        if (!order.AvailableActions.Any(x => x.ActionId == actionId))
            throw new InvalidOperationException("현재 상태에서는 이 작업을 할 수 없습니다.");
        action(order);
        Synchronize(order);
        return Clone(order);
    }

    internal void PrintRequested(string orderNo)
    {
        var order = orders.Single(x => x.주문번호 == orderNo);
        order.전표출력요청시각 = DateTimeOffset.UtcNow;
    }

    public async Task 새주문추가Async()
    {
        var number = orders.Count + 1;
        var order = New(number, "따뜻한 채소 덮밥", 2, 8000, 음식주문상태코드.주문대기);
        var receivedAt = DateTimeOffset.UtcNow;
        order.접수시각 = receivedAt;
        order.상세주문!.CreatedAt = receivedAt.UtcDateTime;
        orders.Add(order);
        ControlMessage = $"{order.주문번호} 추가";
        await realtime.EmitNewAsync(order);
    }

    public async Task 기사배정Async()
    {
        var order = orders.FirstOrDefault(x => x.주문번호 == ControlOrderNo);
        if (order?.상태 != 음식주문상태코드.주문확인)
        { ControlMessage = "먼저 주문을 수락해 주세요."; return; }
        order.상태 = 음식주문상태코드.기사배정;
        order.배차상태 = 음식주문배차상태코드.기사배정;
        order.최근메시지 = null;
        Synchronize(order);
        ControlMessage = $"{order.주문번호} 기사 배정 반영";
        await realtime.EmitStateAsync(order);
    }

    public async Task 픽업확인Async()
    {
        var order = orders.FirstOrDefault(x => x.주문번호 == ControlOrderNo);
        if (order?.상태 != 음식주문상태코드.픽업대기)
        { ControlMessage = "먼저 픽업 준비를 완료해 주세요."; return; }
        order.상태 = 음식주문상태코드.픽업완료;
        order.배차상태 = 음식주문배차상태코드.배달중;
        Synchronize(order);
        ControlMessage = $"{order.주문번호} 기사 픽업 확인 반영 · 배송 완료 아님";
        await realtime.EmitStateAsync(order);
    }

    public async Task 조회실패Async()
    {
        ReadFailure = true;
        ControlMessage = "조회 실패 주입 · 주문은 변경하지 않음";
        await realtime.EmitRefreshAsync();
    }

    public async Task 조회복구Async()
    {
        ReadFailure = false;
        ControlMessage = "조회 복구 · 정본 재조회 신호";
        await realtime.EmitRefreshAsync();
    }

    private static List<음식점주문DeskItem> Seed()
        => [New(1, "든든한 비빔밥", 2, 9000, 음식주문상태코드.주문대기),
            New(2, "구수한 된장찌개", 1, 8500, 음식주문상태코드.주문대기),
            New(3, "따뜻한 채소 덮밥", 3, 8000, 음식주문상태코드.주문대기),
            New(4, "매콤한 떡볶이", 2, 7000, 음식주문상태코드.주문확인),
            New(5, "바삭한 돈가스", 1, 11000, 음식주문상태코드.기사배정),
            New(6, "담백한 닭곰탕", 2, 10000, 음식주문상태코드.조리중),
            New(7, "따뜻한 채소 덮밥", 1, 8000, 음식주문상태코드.픽업대기),
            New(8, "든든한 비빔밥", 1, 9000, 음식주문상태코드.픽업완료),
            New(9, "구수한 된장찌개", 2, 8500, 음식주문상태코드.주문확인, failedDispatch: true)];

    private static 음식점주문DeskItem New(int number, string menu, int quantity, decimal price,
        string status, bool failedDispatch = false)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-number * 2);
        var product = new 음식주문상품Dto { 메뉴Id = number, 상품명 = menu, 수량 = quantity, 단가 = price };
        var order = new 음식점주문DeskItem
        {
            Id = number, 주문번호 = $"DEMO-{number:000}", 음식점Id = 66001,
            고객명 = $"주문자 {number}", 메뉴요약 = $"{menu} {quantity}개", 상품목록 = [product],
            상품별조리기준 = [new(menu, quantity, 20, true)], 주문금액 = price * quantity,
            접수시각 = new DateTimeOffset(createdAt), 추천조리예상분 = 20,
            선택조리예상분 = status == 음식주문상태코드.주문대기 ? null : 20,
            상태 = status, 미확인 = status == 음식주문상태코드.주문대기,
            수락시각 = status == 음식주문상태코드.주문대기 ? null : new DateTimeOffset(createdAt.AddSeconds(30)),
            배차상태 = status switch
            {
                음식주문상태코드.주문대기 => 음식주문배차상태코드.미요청,
                음식주문상태코드.주문확인 => failedDispatch ? 음식주문배차상태코드.배차불가 : 음식주문배차상태코드.배차대기,
                음식주문상태코드.픽업완료 => 음식주문배차상태코드.배달중,
                _ => 음식주문배차상태코드.기사배정
            },
            최근메시지 = failedDispatch ? "기사를 찾지 못했습니다. 배차 상태를 확인해 주세요." : null,
            상세주문 = new()
            {
                주문번호 = $"DEMO-{number:000}", 음식점Id = 66001, 음식점명 = "살뜰 식당", CreatedAt = createdAt,
                수령인정보 = new() { 수령인명 = $"주문자 {number}", 요청사항 = number == 1 ? "일회용 수저는 빼 주세요.\n맵지 않게 부탁드립니다." : "요청사항 없음" },
                상품목록 = [product], 총주문금액 = price * quantity, Revision = 1,
                조리예상분 = status == 음식주문상태코드.주문대기 ? null : 20,
                음식점수락시각Utc = status == 음식주문상태코드.주문대기 ? null : createdAt.AddSeconds(30),
                조리시작시각Utc = status is 음식주문상태코드.조리중 or 음식주문상태코드.픽업대기 or 음식주문상태코드.픽업완료 ? createdAt.AddMinutes(1) : null,
                픽업준비시각Utc = status is 음식주문상태코드.픽업대기 or 음식주문상태코드.픽업완료 ? createdAt.AddMinutes(2) : null
            }
        };
        Synchronize(order);
        return order;
    }

    internal static void Synchronize(음식점주문DeskItem order)
    {
        var detail = order.상세주문!;
        detail.상태 = order.상태;
        detail.배차상태 = order.배차상태;
        detail.Revision++;
        detail.조리시작가능 = order.상태 == 음식주문상태코드.기사배정;
        detail.CurrentCookingStartedAtUtc = detail.조리시작시각Utc;
        detail.CurrentPickupReadyAtUtc = detail.픽업준비시각Utc;
        detail.최근변경시각Utc = DateTime.UtcNow;
        string[] ids = order.상태 switch
        {
            음식주문상태코드.주문대기 => [음식배달가능행동Ids.음식점주문수락, 음식배달가능행동Ids.음식점주문거절],
            음식주문상태코드.주문확인 => [음식배달가능행동Ids.음식점조리시간변경],
            음식주문상태코드.기사배정 => [음식배달가능행동Ids.음식점조리시작, 음식배달가능행동Ids.음식점조리시간변경],
            음식주문상태코드.조리중 => [음식배달가능행동Ids.음식점픽업준비완료, 음식배달가능행동Ids.음식점조리시간변경],
            _ => []
        };
        order.AvailableActions = ids.Select(id => new 업무가능행동Dto
        { ActionId = id, ExpectedRevision = detail.Revision, RevisionKindCode = 업무Revision종류Codes.음식주문 }).ToArray();
        detail.AvailableActions = order.AvailableActions;
        order.최근복구시각 = DateTimeOffset.UtcNow;
    }

    private static 음식점주문DeskItem Clone(음식점주문DeskItem value)
        => JsonSerializer.Deserialize<음식점주문DeskItem>(JsonSerializer.Serialize(value))!;
}
