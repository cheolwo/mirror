using System.Net;
using System.Text.Json;
using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodRoleWorkspaceCardsR16Tests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 3, 0, 0, TimeSpan.Zero);
    private const string OrderNo = "order-a";
    private const string OperatorMap = "api/v1/admin/dispatch/food-delivery-ai-review";

    [Fact]
    public async Task OrdererCarriesMeasuredAndQueryTimesWithThirtySecondExpiry()
    {
        var detail = OrdererDetail();
        detail.기사위치.기록시각Utc = Now.AddSeconds(-12).UtcDateTime;
        detail.기사위치.조회시각Utc = Now.AddSeconds(-2).UtcDateTime;
        var api = OrdererApi(detail);
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(api, new FixedTime()).LoadAsync(null, default)).Items);
        var marker = Assert.Single(item.Markers!);
        Assert.Equal(Now.AddSeconds(-12), marker.MeasuredAt);
        Assert.Equal(Now.AddSeconds(-2), marker.ReceivedAt);
        Assert.Equal(Now.AddSeconds(18), marker.ExpiresAt);
        var summary = Assert.IsType<RoleWorkspaceSummary>(item.Summary);
        Assert.Equal("10/5 11:59:48 (한국)", Field(summary, "위치 기록"));
        Assert.Equal("10/5 11:59:58 (한국)", Field(summary, "위치 조회"));
        Assert.Contains("30초", Field(summary, "위치 안내"));
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(29, 0, true, true, true)]
    [InlineData(30, 0, true, true, false)]
    [InlineData(31, 0, true, true, false)]
    [InlineData(-1, 0, true, true, false)]
    [InlineData(0, 1, true, true, false)]
    [InlineData(1, -1, true, true, false)]
    [InlineData(0, 0, false, true, false)]
    [InlineData(0, 0, true, false, false)]
    [InlineData(0, 0, false, false, false)]
    public async Task OrdererDoesNotClaimExpiredMissingFutureOrInconsistentLocationAsCurrent(
        int measuredAge, int queryAge, bool hasMeasured, bool hasQuery, bool visible)
    {
        var detail = OrdererDetail();
        detail.기사위치.기록시각Utc = hasMeasured ? Now.AddSeconds(-measuredAge).UtcDateTime : null;
        detail.기사위치.조회시각Utc = hasQuery ? Now.AddSeconds(-queryAge).UtcDateTime : default;
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail), new FixedTime()).LoadAsync(null, default)).Items);
        Assert.Equal(visible ? 1 : 0, item.Markers!.Count);
        if (!visible) Assert.Contains("현재 위치 확인 필요", Field(item.Summary!, "위치 안내"));
    }

    [Theory]
    [InlineData(음식배달위치추적상태코드.갱신지연)]
    [InlineData(음식배달위치추적상태코드.종료)]
    [InlineData(음식배달위치추적상태코드.추적전)]
    public async Task OrdererRespectsServerLocationStateEvenWithFreshCoordinates(string trackingState)
    {
        var detail = OrdererDetail();
        detail.기사위치.상태 = trackingState;
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail), new FixedTime()).LoadAsync(null, default)).Items);
        Assert.Empty(item.Markers!);
    }

    [Fact]
    public async Task OrdererDoesNotTurnMissingCoordinatesIntoARecentLocation()
    {
        var detail = OrdererDetail();
        detail.기사위치.위도 = null;
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail), new FixedTime()).LoadAsync(null, default)).Items);
        Assert.Empty(item.Markers!);
        Assert.Contains("현재 위치 확인 필요", Field(item.Summary!, "위치 안내"));
    }

    [Theory]
    [InlineData(음식주문상태코드.전달완료, "수령 확인 대기")]
    [InlineData(음식주문상태코드.수령확인, "수령 확인 완료")]
    [InlineData(음식주문상태코드.취소, "주문 취소")]
    [InlineData(음식주문상태코드.거절, "주문 거절")]
    public async Task OrdererClearsTerminalPinsWithoutInventingReceiptAction(string orderState, string stage)
    {
        var detail = OrdererDetail();
        detail.주문.상태 = orderState;
        detail.수령인정보.수령인명 = "수령인 개인 이름";
        detail.수령인정보.연락처 = "010-1234-5678";
        detail.수령인정보.주소 = "고객 개인 주소";
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail), new FixedTime()).LoadAsync(null, default)).Items);
        Assert.Empty(item.Markers!);
        Assert.Empty(item.Actions!);
        Assert.Equal(stage, item.Summary!.Destination);
        var text = SummaryText(item.Summary);
        Assert.DoesNotContain("수령인 개인 이름", text);
        Assert.DoesNotContain("010-1234-5678", text);
        Assert.DoesNotContain("고객 개인 주소", text);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OrdererNextReceiptDependsOnCurrentAllowedAction(bool expired, bool enabled)
    {
        var detail = OrdererDetail();
        detail.주문.상태 = 음식주문상태코드.전달완료;
        detail.배달진행.수령확인가능 = true;
        detail.배달진행.안내 = "전달·수령 상태를 확인해 주세요.";
        detail.AvailableActions = [new() { ActionId = 음식배달가능행동Ids.주문수령확인,
            ExpiresAtUtc = expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(1) }];
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(OrdererApi(detail), new FixedTime()).LoadAsync(null, default)).Items);
        Assert.Equal(enabled, Assert.Single(item.Actions!).Enabled);
        Assert.Equal(enabled ? "음식을 받은 뒤 수령을 확인해 주세요." : detail.배달진행.안내, item.Summary!.Request!.Value);
    }

    [Fact]
    public async Task OrdererSelectedOutsideRecentListStillGetsDetailedCard()
    {
        var detail = OrdererDetail();
        var api = OrdererApi(detail);
        api.Reads["api/v1/food-orders?page=1&pageSize=50"] = new 주문자음식주문목록응답();
        var item = Assert.Single((await new OrdererRoleWorkspaceAdapter(api, new FixedTime()).LoadAsync(OrderNo, default)).Items);
        Assert.Equal(OrderNo, item.Id);
        Assert.Contains(item.Summary!.Metrics!, field => field.Label == "위치 기록");
        Assert.Single(item.Markers!);
    }

    [Theory]
    [InlineData(false, false, "재조리 대기", "미확인")]
    [InlineData(true, false, "재조리 중", "미확인")]
    [InlineData(true, true, "재조리 준비 완료", "10/5 12:00 (한국)")]
    public async Task RestaurantCurrentRoundDoesNotReuseFirstFoodPreparation(bool cooking, bool ready, string stage, string readyTime)
    {
        var order = RestaurantOrder();
        order.CurrentPreparationRound = 2;
        order.RecookingRequestedAtUtc = Now.AddMinutes(-5).UtcDateTime;
        order.조리시작시각Utc = Now.AddHours(-1).UtcDateTime;
        order.픽업준비시각Utc = Now.AddMinutes(-50).UtcDateTime;
        order.CurrentCookingStartedAtUtc = cooking ? Now.AddMinutes(-2).UtcDateTime : null;
        order.CurrentPickupReadyAtUtc = ready ? Now.UtcDateTime : null;
        var item = Assert.Single((await new RestaurantRoleWorkspaceAdapter(RestaurantApi(order)).LoadAsync(null, default)).Items);
        Assert.Equal(stage, Field(item.Summary!, "조리 단계"));
        Assert.Equal(readyTime, Field(item.Summary!, "준비 완료"));
        Assert.Empty(item.Actions!);
    }

    [Fact]
    public async Task RestaurantBasicMenuIsShortAndKeepsCompleteDetailAndAllowedActions()
    {
        var order = RestaurantOrder();
        order.상품목록 = [new() { 상품명 = "메뉴 하나", 수량 = 2 }, new() { 상품명 = "메뉴 둘", 수량 = 1 }, new() { 상품명 = "메뉴 셋", 수량 = 3 }];
        order.AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점주문수락 }, new() { ActionId = 음식배달가능행동Ids.음식점주문거절 }];
        order.수령인정보.연락처 = "010-1234-5678";
        var api = RestaurantApi(order);
        var item = Assert.Single((await new RestaurantRoleWorkspaceAdapter(api).LoadAsync(null, default)).Items);
        Assert.Equal("메뉴 하나 2개 · 메뉴 둘 1개 · 외 1종", item.Summary!.Destination);
        Assert.Equal("주문 수락", item.Summary.Request!.Value);
        Assert.Equal(3, item.Sections!.Single(section => section.Title == "주문 메뉴").Fields.Count);
        Assert.Equal(new[] { 음식배달가능행동Ids.음식점주문수락, 음식배달가능행동Ids.음식점주문거절 }, item.Actions!.Select(action => action.Key));
        Assert.DoesNotContain("010-1234-5678", SummaryText(item.Summary));
        Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task RestaurantExpiredPrimaryActionIsNotSuggestedAsNextAction()
    {
        var order = RestaurantOrder();
        order.AvailableActions = [new() { ActionId = 음식배달가능행동Ids.음식점주문수락, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) }];
        var item = Assert.Single((await new RestaurantRoleWorkspaceAdapter(RestaurantApi(order)).LoadAsync(null, default)).Items);
        Assert.False(Assert.Single(item.Actions!).Enabled);
        Assert.NotEqual("주문 수락", item.Summary!.Request!.Value);
    }

    [Theory]
    [InlineData("access-503")]
    [InlineData("access-404")]
    [InlineData("http")]
    [InlineData("json")]
    [InlineData("timeout")]
    public async Task OperatorMapFailureKeepsRealOrderCardAndReviewAction(string failure)
    {
        var api = OperatorApi();
        api.Reads[OperatorMap] = failure switch
        {
            "access-503" => new RoleWorkspaceAccessException(503, "unavailable"),
            "access-404" => new RoleWorkspaceAccessException(404, "unavailable"),
            "json" => new JsonException("malformed optional map"),
            "timeout" => new TaskCanceledException("optional map timed out", new TimeoutException()),
            _ => new HttpRequestException("offline")
        };
        var snapshot = await new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, default);
        var item = Assert.Single(snapshot.Items);
        Assert.Equal("전달 중", item.Summary!.Destination);
        Assert.Equal("배달 기사", Field(item.Summary, "담당"));
        Assert.Equal("10/5 12:00 (한국)", Field(item.Summary, "최근 변경"));
        Assert.Equal("배달 중단 검토", item.Summary.Request!.Value);
        Assert.Single(item.Actions!);
        Assert.Empty(item.Markers!);
        Assert.Contains("지도를 불러오지 못했습니다", snapshot.Message);
        Assert.All(api.Roles, role => Assert.Equal("operator", role));
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(401, true)]
    [InlineData(403, true)]
    public async Task OperatorOptionalMapDoesNotSwallowAuthenticationOrPermissionFailure(int status, bool http)
    {
        var api = OperatorApi();
        api.Reads[OperatorMap] = http ? new HttpRequestException("denied", null, (HttpStatusCode)status)
            : new RoleWorkspaceAccessException(status, "denied");
        if (http) await Assert.ThrowsAsync<HttpRequestException>(() => new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, default));
        else await Assert.ThrowsAsync<RoleWorkspaceAccessException>(() => new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, default));
    }

    [Fact]
    public async Task OperatorOptionalMapDoesNotSwallowCancellation()
    {
        var api = OperatorApi();
        api.Reads[OperatorMap] = new OperationCanceledException();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, default));
    }

    [Fact]
    public async Task OperatorOptionalMapTimeoutDoesNotSwallowCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var api = OperatorApi();
        api.Reads[OperatorMap] = new TaskCanceledException("timed out", new TimeoutException());
        api.BeforeResult = path => { if (path == OperatorMap) cancellation.Cancel(); };
        await Assert.ThrowsAsync<TaskCanceledException>(() => new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, cancellation.Token));
    }

    [Fact]
    public async Task OperatorCancellationDuringOptionalMapFailureDoesNotReturnOldCard()
    {
        using var cancellation = new CancellationTokenSource();
        var api = OperatorApi();
        api.Reads[OperatorMap] = new RoleWorkspaceAccessException(503, "unavailable");
        api.BeforeResult = path => { if (path == OperatorMap) cancellation.Cancel(); };
        await Assert.ThrowsAsync<OperationCanceledException>(() => new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, cancellation.Token));
    }

    [Theory]
    [InlineData("actual", 2)]
    [InlineData("actual-with-sample-drivers", 0)]
    [InlineData("jungnang-scope-sample", 0)]
    public async Task OperatorCardDoesNotRelaxActualMapSourceGate(string source, int markerCount)
    {
        var api = OperatorApi();
        api.Reads[OperatorMap] = new FoodDeliveryDispatchAIReviewWorkspaceDto { Source = source,
            Orders = [new() { OrderNo = OrderNo, RestaurantLatitude = 37.57m, RestaurantLongitude = 127.08m,
                CustomerLatitude = 37.58m, CustomerLongitude = 127.09m }] };
        var item = Assert.Single((await new OperatorRoleWorkspaceAdapter(api).LoadAsync(null, default)).Items);
        Assert.Equal(markerCount, item.Markers!.Count);
        Assert.NotNull(item.Summary);
    }

    private static 주문자음식주문상세응답 OrdererDetail() => new()
    {
        주문 = new() { 주문번호 = OrderNo, 음식점명 = "음식점", 상태 = 음식주문상태코드.픽업완료 },
        배달진행 = new() { 안내 = "음식을 전달하고 있습니다.", 최근변경시각Utc = Now.UtcDateTime },
        기사위치 = new() { 상태 = 음식배달위치추적상태코드.추적중, 위도 = 37.57m, 경도 = 127.08m,
            기록시각Utc = Now.UtcDateTime, 조회시각Utc = Now.UtcDateTime }
    };
    private static Api OrdererApi(주문자음식주문상세응답 detail) => new()
    {
        Reads = { ["api/v1/food-orders?page=1&pageSize=50"] = new 주문자음식주문목록응답 { Items = [detail.주문] },
            [$"api/v1/food-orders/{OrderNo}"] = detail }
    };
    private static 음식주문응답 RestaurantOrder() => new() { 주문번호 = OrderNo, 상태 = 음식주문상태코드.주문대기,
        최근변경시각Utc = Now.UtcDateTime };
    private static Api RestaurantApi(음식주문응답 order) => new()
    {
        Reads = { ["api/v1/food-orders/restaurant/inbox?처리상태=" + Uri.EscapeDataString(음식점주문수신함처리상태코드.미처리) + "&Page=1&PageSize=50"]
                = new 음식점주문수신함응답 { Items = [order] },
            [$"api/v1/food-orders/restaurant/inbox/{OrderNo}"] = order }
    };
    private static Api OperatorApi() => new()
    {
        Reads =
        {
            ["api/v1/admin/food-orders/operations?page=1&pageSize=50"] = new AdminFoodOrderListDto { Items = [new() { OrderNo = OrderNo }] },
            [$"api/v1/admin/food-orders/{OrderNo}/operations-trace"] = new 음식주문운영추적응답
            {
                주문번호 = OrderNo, 최근변경시각Utc = Now.UtcDateTime, 조회시각Utc = Now.UtcDateTime,
                생명주기조화 = new() { 현재단계Code = 음식배달운영생명주기단계Codes.배송,
                    현재책임주체Codes = [음식배달운영책임주체Codes.음식배달기사], 운영자확인필요여부 = true },
                배달시도목록 = [new() { 시도StableId = "attempt-a", 상태Code = 음식배달시도상태Code.중단 }]
            },
            [OperatorMap] = new FoodDeliveryDispatchAIReviewWorkspaceDto { Source = "actual" }
        }
    };
    private static string Field(RoleWorkspaceSummary summary, string label) => Assert.Single(summary.Metrics!, field => field.Label == label).Value;
    private static string SummaryText(RoleWorkspaceSummary summary) => string.Join(" · ",
        new[] { summary.Destination, summary.Request?.Value }.Concat(summary.Metrics!.Select(field => field.Value)));
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Api : IRoleWorkspaceApi
    {
        public Dictionary<string, object> Reads { get; } = new(StringComparer.Ordinal);
        public List<string> Roles { get; } = [];
        public List<object> Writes { get; } = [];
        public Action<string>? BeforeResult { get; set; }
        public Task<T> GetAsync<T>(string roleKey, string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Roles.Add(roleKey);
            BeforeResult?.Invoke(path);
            return Reads[path] is Exception error ? Task.FromException<T>(error) : Task.FromResult((T)Reads[path]);
        }
        public Task<T?> PostAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken)
        { Writes.Add(body); throw new NotSupportedException(); }
        public Task<T?> PutAsync<T>(string roleKey, string path, object body, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string roleKey, string path, HttpContent body, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
