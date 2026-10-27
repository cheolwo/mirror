using System.Globalization;
using System.Text.Json;
using Ssalddel.Contracts.Admin.Dispatch;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;

namespace Ssalddel.Tests.Services.Dispatch.Coordination;

public sealed class FoodDeliveryReviewCatalogTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void 다섯샘플주문의_순서_원문_좌표_시간_권역_golden을보존한다()
    {
        var orders = 음식배달AI검토Catalog.CreateJungnangScenarioOrders(Now);
        var golden = orders.Select(x => FormattableString.Invariant(
            $"{x.OrderNo}|{x.RestaurantId}|{x.RestaurantName}|{x.MenuSummary}|{x.OrderAmount}|{x.RestaurantAddress}|{x.RestaurantLatitude}|{x.RestaurantLongitude}|{x.CustomerAddress}|{x.CustomerLatitude}|{x.CustomerLongitude}|{(x.PickupReadyAtUtc!.Value - Now).TotalMinutes}|{x.MaxDeliveryMinutesAfterReady}|{x.PickupScopeKey}|{x.PickupScopeName}|{x.PickupScopeRole}|{x.DropoffScopeKey}|{x.DropoffScopeName}|{x.DropoffScopeRole}")).ToArray();

        Assert.Equal(new[]
        {
            "FOOD-JN-001|101|살뜰분식 면목점|제육덮밥 2|19000|서울특별시 중랑구 면목로 352|37.5888|127.0874|서울특별시 중랑구 면목동 15-1|37.5867|127.0885|14|42|bjd-sigungu:11260|중랑구|주요 배달권|bjd-sigungu:11260|중랑구|주요 배달권",
            "FOOD-JN-002|101|살뜰분식 면목점|비빔밥 1, 만두 1|16500|서울특별시 중랑구 면목로 352|37.5888|127.0874|서울특별시 동대문구 장안동 335-1|37.5684|127.0717|18|42|bjd-sigungu:11260|중랑구|주요 배달권|bjd-sigungu:11230|동대문구|인접 배달권",
            "FOOD-JN-003|102|살뜰도시락 상봉점|돈까스 1, 우동 1|19000|서울특별시 중랑구 망우로 353|37.5969|127.0851|서울특별시 광진구 중곡동 130-1|37.5586|127.0817|21|42|bjd-sigungu:11260|중랑구|주요 배달권|bjd-sigungu:11215|광진구|인접 배달권",
            "FOOD-JN-004|103|살뜰치킨 신내점|후라이드치킨 1|21000|서울특별시 중랑구 신내로 128|37.6088|127.0961|서울특별시 노원구 공릉동 375-1|37.6246|127.0730|24|42|bjd-sigungu:11260|중랑구|주요 배달권|bjd-sigungu:11350|노원구|인접 배달권",
            "FOOD-JN-005|104|살뜰국밥 망우점|순대국 2|22000|서울특별시 중랑구 망우로 470|37.5996|127.1012|경기도 구리시 교문동 230-1|37.5943|127.1296|28|42|bjd-sigungu:11260|중랑구|주요 배달권|bjd-sigungu:41310|구리시|인접 배달권"
        }, golden);
        Assert.All(orders, x =>
        {
            Assert.Equal(음식주문상태코드.픽업대기, x.OrderStatus);
            Assert.Equal(음식주문배차상태코드.배차대기, x.DispatchStatus);
            Assert.Equal(DateTimeKind.Utc, x.PickupReadyAtUtc!.Value.Kind);
        });
    }

    [Fact]
    public void 기사샘플은_별도입력시각에서위치와상태및수신시각golden을보존한다()
    {
        var driverNow = Now.AddMinutes(3);
        var drivers = 음식배달AI검토Catalog.CreateSampleDrivers(driverNow);

        Assert.Equal(new[]
        {
            "f-driver-sample-a|F드라이버 A|운행중|37.5907|127.0869|bjd-sigungu:11260|중랑구|주요 배달권|0|-2",
            "f-driver-sample-b|F드라이버 B|운행중|37.5712|127.0732|bjd-sigungu:11230|동대문구|인접 배달권|1|-5",
            "f-driver-sample-c|F드라이버 C|운행중|37.5949|127.1267|bjd-sigungu:41310|구리시|인접 배달권|0|-7"
        }, drivers.Select(x => FormattableString.Invariant(
            $"{x.DriverId}|{x.DriverName}|{x.DrivingStatus}|{x.Latitude}|{x.Longitude}|{x.DeliveryScopeKey}|{x.DeliveryScopeName}|{x.DeliveryScopeRole}|{x.CurrentAcceptedDeliveryCount}|{(x.LastLocationReceivedAtUtc!.Value - driverNow).TotalMinutes}")));
        Assert.Equal(new[] { "동대문구", "광진구", "노원구", "구리시" }, 음식배달AI검토Catalog.AdjacentReviewScopes.Select(x => x.Name));
    }

    [Theory]
    [InlineData("중랑구 면목동", "37.5928", "127.0879", "중랑구")]
    [InlineData("동대문구 장안동", "37.5684", "127.0717", "동대문구")]
    [InlineData("광진구 중곡동", "37.5586", "127.0817", "광진구")]
    [InlineData("노원구 공릉동", "37.6246", "127.0730", "노원구")]
    [InlineData("구리시 교문동", "37.5943", "127.1296", "구리시")]
    public void 실제주문의주소키워드근사는_기존좌표와권역을보존한다(string address, string latitude, string longitude, string scope)
    {
        var order = Order("ACTUAL-1");
        order.수령인정보.주소 = $" {address} ";
        var before = JsonSerializer.Serialize(order);

        var result = 음식배달AI검토Catalog.EnrichOrder(order);

        Assert.Equal(address, result.CustomerAddress);
        Assert.Equal(decimal.Parse(latitude, CultureInfo.InvariantCulture), result.CustomerLatitude);
        Assert.Equal(decimal.Parse(longitude, CultureInfo.InvariantCulture), result.CustomerLongitude);
        Assert.Equal(scope, result.DropoffScopeName);
        Assert.Equal(before, JsonSerializer.Serialize(order));
    }

    [Fact]
    public void 누락매장과미확인지역은_현재기본매장과주문번호offset으로보강한다()
    {
        var order = Order("ACTUAL-1");
        order.음식점Id = 999;
        order.음식점명 = " ";
        order.음식점주소 = " ";
        order.수령인정보.주소 = " 확인 안 된 지역 ";

        var result = 음식배달AI검토Catalog.EnrichOrder(order);

        Assert.Equal("살뜰음식점 중랑점", result.RestaurantName);
        Assert.Equal("서울특별시 중랑구 봉화산로 179", result.RestaurantAddress);
        Assert.Equal(37.6066m, result.RestaurantLatitude);
        Assert.Equal(127.0927m, result.RestaurantLongitude);
        Assert.Equal(37.6166m, result.CustomerLatitude);
        Assert.Equal(127.0967m, result.CustomerLongitude);
        Assert.Equal("확인 안 된 지역", result.CustomerAddress);
        Assert.Equal(42m, result.MaxDeliveryMinutesAfterReady);
    }

    [Theory]
    [InlineData("ready", 7)]
    [InlineData("accepted", 25)]
    [InlineData("created", 20)]
    public void 조리예상시각은_예정값_수락후20분_생성후20분순서를보존한다(string source, int minutes)
    {
        var order = Order("ACTUAL-1");
        order.음식점수락시각Utc = source == "created" ? null : Now.AddMinutes(5);
        order.조리예상완료시각Utc = source == "ready" ? Now.AddMinutes(7) : null;
        order.음식점명 = " 직접 관측 매장 ";
        order.음식점주소 = " 중랑구 관측 주소 ";
        order.음식점위도 = 37.1m;
        order.음식점경도 = null;

        var result = 음식배달AI검토Catalog.EnrichOrder(order);

        Assert.Equal(Now.AddMinutes(minutes), result.PickupReadyAtUtc);
        Assert.Equal("직접 관측 매장", result.RestaurantName);
        Assert.Equal("중랑구 관측 주소", result.RestaurantAddress);
        Assert.Equal(37.1m, result.RestaurantLatitude);
        Assert.Equal(127.0874m, result.RestaurantLongitude);
    }

    [Fact]
    public void 권역의주소우선순위와키trim및ordinal동등성을유지한다()
    {
        Assert.Equal("중랑구", 음식배달AI검토Catalog.ResolveFoodDeliveryScope("중랑구 광진구", null).Name);
        Assert.Equal("동대문구", 음식배달AI검토Catalog.ResolveFoodDeliveryScope("동대문구 광진구", null).Name);
        Assert.Same(음식배달AI검토Catalog.PrimaryReviewScope, 음식배달AI검토Catalog.ResolveScopeByKey(" bjd-sigungu:11260 "));
        Assert.Null(음식배달AI검토Catalog.ResolveScopeByKey("BJD-SIGUNGU:11260"));
        Assert.Equal("미정", 음식배달AI검토Catalog.ResolveScopeDisplay(" "));
        Assert.Equal("unlisted", 음식배달AI검토Catalog.ResolveScopeDisplay(" unlisted "));
        Assert.Null(음식배달AI검토Catalog.CreatePoint(37m, null));
    }

    [Theory]
    [InlineData("empty", "jungnang-scope-sample", 5, 3)]
    [InlineData("default", "jungnang-scope-sample", 5, 3)]
    [InlineData("mixed", "actual-with-sample-drivers", 2, 3)]
    [InlineData("actual", "actual-with-sample-drivers", 1, 3)]
    [InlineData("actual-driver", "actual", 1, 1)]
    public async Task 실제와샘플선택및기사fallback은_기존Source와근사를그대로유지한다(
        string scenario, string source, int orderCount, int driverCount)
    {
        var actual = Order("ACTUAL-1");
        var defaultOrder = Order("FOOD-20260701-001");
        IReadOnlyList<음식주문응답> orders = scenario switch
        {
            "empty" => [], "default" => [defaultOrder], "mixed" => [defaultOrder, actual], _ => [actual]
        };
        var bundle = new CapturingBundles();
        var spaces = new ScopeStore(scenario == "actual-driver"
            ? [new("bjd-sigungu:11230", [], ["driver-real"], [], Now)] : []);
        var before = DateTime.UtcNow;
        var workspace = await new FoodDeliveryDispatchAIReviewService(new OrderStore(orders), bundle, spaces,
            new LocationStore(), new ThrowingLedger()).GetWorkspaceAsync();
        var after = DateTime.UtcNow;

        Assert.Equal(source, workspace.Source);
        Assert.Equal(orderCount, workspace.Orders.Count);
        Assert.Equal(driverCount, workspace.Drivers.Count);
        Assert.Equal(1, spaces.Reads);
        Assert.Equal(2, bundle.LastRequest!.최대묶음크기);
        Assert.Equal(24, bundle.LastRequest.최대조합수);
        Assert.Empty(workspace.Bundles);
        Assert.Empty(workspace.Assignments);
        Assert.InRange(workspace.GeneratedAt.UtcDateTime, before, after);
        if (scenario == "mixed")
        {
            var legacy = Assert.Single(workspace.Orders.Where(x => x.OrderNo == defaultOrder.주문번호));
            Assert.Equal("서울특별시 중랑구 면목동 15-1", legacy.CustomerAddress);
            Assert.Equal(37.5867m, legacy.CustomerLatitude);
        }
        if (scenario == "actual-driver")
        {
            var driver = Assert.Single(workspace.Drivers);
            Assert.Equal("driver-real", driver.DriverId);
            Assert.Equal("동대문구", driver.DeliveryScopeName);
            Assert.Equal(37.5928m, driver.Latitude);
            Assert.Equal(Now, driver.LastLocationReceivedAtUtc);
        }
        if (source == "jungnang-scope-sample")
            Assert.InRange(workspace.Orders[0].PickupReadyAtUtc!.Value, before.AddMinutes(14), after.AddMinutes(14));
        if (scenario != "actual-driver")
            Assert.InRange(workspace.Drivers[0].LastLocationReceivedAtUtc!.Value, before.AddMinutes(-2), after.AddMinutes(-2));
    }

    [Fact]
    public async Task 실제조회는_종료상태를빼고예정시각순최대12건을보강한다()
    {
        var orders = Enumerable.Range(0, 14).Reverse().Select(i =>
        {
            var order = Order($"ACTUAL-{i:D2}");
            order.조리예상완료시각Utc = Now.AddMinutes(i);
            return order;
        }).ToList();
        var terminal = Order("TERMINAL"); terminal.상태 = 음식주문상태코드.전달완료;
        terminal.조리예상완료시각Utc = Now.AddMinutes(-1); orders.Add(terminal);
        var bundle = new CapturingBundles();

        var workspace = await new FoodDeliveryDispatchAIReviewService(new OrderStore(orders), bundle,
            new ScopeStore([]), new LocationStore(), new ThrowingLedger()).GetWorkspaceAsync();

        Assert.Equal(Enumerable.Range(0, 12).Select(i => $"ACTUAL-{i:D2}"), workspace.Orders.Select(x => x.OrderNo));
        Assert.Equal(12, bundle.LastRequest!.작업목록.Count);
    }

    private static 음식주문응답 Order(string orderNo) => new()
    {
        주문번호 = orderNo, 음식점Id = 101, 상태 = 음식주문상태코드.조리중,
        CreatedAt = Now, 수령인정보 = new() { 주소 = "중랑구 면목동" }
    };
    private sealed class OrderStore(IReadOnlyList<음식주문응답> orders) : ISsalddelFoodOrderStore
    {
        public 음식주문목록응답 GetOrders() => new() { Items = orders };
        public 음식주문응답? GetOrder(string id) => throw new NotSupportedException();
        public 음식주문응답 AddOrder(음식주문등록요청 request) => throw new NotSupportedException();
        public 음식주문응답? 음식점수락(string id, 음식점주문수락요청 request) => throw new NotSupportedException();
        public 음식주문응답? 배차대기반영(string id, long queueId, DateTime at) => throw new NotSupportedException();
    }
    private sealed class CapturingBundles : I음식멀티배차조합Service
    {
        internal 멀티배차조합요청? LastRequest { get; private set; }
        public IReadOnlyList<멀티배차조합후보> 조합생성(멀티배차조합요청 request) { LastRequest = request; return []; }
    }
    private sealed class ScopeStore(IReadOnlyList<음식배달권실행공간Snapshot> spaces) : I음식배달권실행공간Store
    {
        internal int Reads { get; private set; }
        public Task<IReadOnlyList<음식배달권실행공간Snapshot>> SnapshotAsync(CancellationToken ct = default) { Reads++; return Task.FromResult(spaces); }
        public Task Upsert기사Async(string key, string id, IReadOnlyList<string> adjacent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Remove기사Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Upsert운송의뢰Async(string key, string id, IReadOnlyList<string> adjacent, CancellationToken ct = default) => throw new NotSupportedException();
        public Task Remove운송의뢰Async(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<음식배달권실행공간Snapshot?> GetAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class LocationStore : IDriverLocationStore
    {
        public void Upsert(DriverLocationSnapshot snapshot) => throw new NotSupportedException();
        public bool TryGetLatest(string id, out DriverLocationSnapshot snapshot)
        { snapshot = new(id, 37.5928m, 127.0879m, null, "운행중", Now, Now); return true; }
    }
    private sealed class ThrowingLedger : I배차AI판단사례LedgerStore
    {
        public Task<DispatchAIJudgmentCaseCatalogDto> GetCatalogAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DispatchAIJudgmentCaseDto> CreateAsync(DispatchAIJudgmentCaseCreateRequest request, string? user, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<DispatchAIJudgmentCaseDto> PromoteSuggestionAsync(string key, DispatchAIJudgmentCasePromoteSuggestionRequest request, string? user, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
