using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Driver.Food;
using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DeliveryExperienceUiTests
{
    [Theory]
    [InlineData("Unknown", null, "Operational")]
    [InlineData("Pending", 4L, "Operational")]
    [InlineData("Confirmed", null, "Operational")]
    [InlineData("Confirmed", -1L, "Operational")]
    [InlineData("Confirmed", 4L, "Unknown")]
    public void UnknownOrUnconfirmedCountNeverBecomesZero(string evidence, long? count, string mode)
    {
        var value = Summary("PickupCompleted", "Restaurant", count);
        value.EvidenceStatusCode = evidence; value.ExecutionModeCode = mode;
        Assert.Empty(DeliveryExperiencePresentation.DriverLines([value], false));
        Assert.Empty(DeliveryExperiencePresentation.RestaurantLines([value]));
    }

    [Fact]
    public void MissingAsOfHidesCountAndConfirmedZeroIsExplicit()
    {
        var summary = Summary("PickupCompleted", "Restaurant", 0);
        Assert.Contains("0건", Assert.Single(DeliveryExperiencePresentation.DriverLines([summary], false)));
        summary.AsOfUtc = null;
        Assert.Empty(DeliveryExperiencePresentation.DriverLines([summary], false));
    }

    [Fact]
    public void PartialHistoryOnlyShowsPositiveVerifiedSubsetWithItsBasis()
    {
        var summary = Summary("DeliveryCompleted", "Road", 3); summary.EvidenceStatusCode = "Partial";
        Assert.Contains("3건 · 확인된 앱 기록", Assert.Single(DeliveryExperiencePresentation.DriverLines([summary], true)));
        summary.CompletedCount = 0; Assert.Empty(DeliveryExperiencePresentation.DriverLines([summary], true));
        summary.CompletedCount = null; Assert.Empty(DeliveryExperiencePresentation.OrdererLines([summary]));
    }

    [Fact]
    public void DriverUsesCompletedOrdersAndStageSpecificPlacesInsteadOfVisitOrdinal()
    {
        var values = new[] { Summary("DeliveryCompleted", "ApartmentComplex", 8), Summary("PickupCompleted", "Restaurant", 3),
            Summary("DeliveryCompleted", "Road", 12), Summary("DeliveryCompleted", "Road", 999) };
        Assert.Equal("이 매장 픽업 완료 3건 · 앱 기록", Assert.Single(DeliveryExperiencePresentation.DriverLines(values, false)));
        var delivery = DeliveryExperiencePresentation.DriverLines(values, true);
        Assert.Equal(2, delivery.Count); Assert.Contains("12건", delivery[0]); Assert.Contains("8건", delivery[1]);
        Assert.DoesNotContain(delivery, value => value.Contains("방문") || value.Contains("번째"));
        values[1].ExecutionModeCode = "Simulation";
        Assert.Contains("모의 기록", Assert.Single(DeliveryExperiencePresentation.RestaurantLines(values)));
    }

    [Theory]
    [InlineData(음식주문상태코드.기사배정, 음식주문배차상태코드.기사배정, 1)]
    [InlineData(음식주문상태코드.조리중, 음식주문배차상태코드.기사배정, 1)]
    [InlineData(음식주문상태코드.조리중, 음식주문배차상태코드.배차대기, 0)]
    [InlineData(음식주문상태코드.픽업완료, 음식주문배차상태코드.배달중, 0)]
    [InlineData(음식주문상태코드.수령확인, 음식주문배차상태코드.배달완료, 0)]
    [InlineData(음식주문상태코드.취소, 음식주문배차상태코드.기사배정, 0)]
    public async Task RestaurantCurrentCardOnlyUsesCurrentAssignedDriver(string state, string dispatch, int count)
    {
        var order = new 음식주문응답 { 주문번호 = "order-a", 상태 = state, 배차상태 = dispatch,
            ExperienceSummaries = [Summary("PickupCompleted", "Restaurant", 4)] };
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/food-orders/restaurant/inbox?처리상태=" + Uri.EscapeDataString("미처리") + "&Page=1&PageSize=50"] = new 음식점주문수신함응답 { Items = [order] };
        api.Reads["api/v1/food-orders/restaurant/inbox/order-a"] = order;
        var adapter = new RestaurantRoleWorkspaceAdapter(api);
        var item = Assert.Single((await adapter.LoadAsync(null, default)).Items);
        Assert.Equal(count, item.Summary!.ExperienceLines!.Count);
        if (count > 0)
        {
            Assert.Contains("4건을 픽업한 기사님입니다", Assert.Single(item.Summary.ExperienceLines));
            Assert.DoesNotContain("오고 있습니다", Assert.Single(item.Summary.ExperienceLines));
        }
        order.ExperienceSummaries = [];
        Assert.Empty(Assert.Single((await adapter.LoadAsync(null, default)).Items).Summary!.ExperienceLines!);
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(음식주문상태코드.기사배정, false, 0)]
    [InlineData(음식주문상태코드.픽업완료, false, 2)]
    [InlineData(음식주문상태코드.픽업완료, true, 0)]
    [InlineData(음식주문상태코드.전달완료, true, 0)]
    [InlineData(음식주문상태코드.취소, false, 0)]
    public async Task OrdererOnlyShowsCurrentDeliveryAndClearsWithdrawnSharing(string state, bool delivered, int count)
    {
        var detail = new 주문자음식주문상세응답 { 주문 = new() { 주문번호 = "order-a", 상태 = state },
            배달진행 = new() { 기사전달완료 = delivered },
            ExperienceSummaries = [Summary("DeliveryCompleted", "Road", 12), Summary("DeliveryCompleted", "ApartmentComplex", 8)] };
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/food-orders?page=1&pageSize=50"] = new 주문자음식주문목록응답 { Items = [detail.주문] };
        api.Reads["api/v1/food-orders/order-a"] = detail;
        var adapter = new OrdererRoleWorkspaceAdapter(api);
        Assert.Equal(count, Assert.Single((await adapter.LoadAsync(null, default)).Items).Summary!.ExperienceLines!.Count);
        detail.ExperienceSummaries = [];
        Assert.Empty(Assert.Single((await adapter.LoadAsync(null, default)).Items).Summary!.ExperienceLines!);
        Assert.Empty(api.Writes);
    }

    [Theory]
    [InlineData(DriverWorkOfferStatus.Accepted, 1)]
    [InlineData(DriverWorkOfferStatus.MovingToDropoff, 2)]
    public async Task DriverCardSeparatesPickupFromDestinationExperience(string status, int count)
    {
        var api = new FoodRoleWorkspaceAdapterTests.Api();
        api.Reads["api/v1/driver/food-deliveries/workspace/current"] = new FoodDeliveryDriverCurrentWorkDto { ActiveDeliveries = [new() {
            OfferId = "offer-a", WorkStatus = status, ExperienceSummaries = [Summary("PickupCompleted", "Restaurant", 3),
                Summary("DeliveryCompleted", "Road", 12), Summary("DeliveryCompleted", "ApartmentComplex", 8)] }] };
        api.Reads["api/v1/driver/food-deliveries/work/status"] = new 기사운행상태응답 { Status = "운행중" };
        api.Reads["api/v1/driver/operational-dispatch/availability"] = new 운영배차수신상태Dto();
        var item = Assert.Single((await new FoodDriverRoleWorkspaceAdapter(api, new NoLocation()).LoadAsync(null, default)).Items);
        Assert.Equal(count, item.Summary!.ExperienceLines!.Count); Assert.Empty(api.Writes);
    }

    [Fact]
    public async Task ExperienceMarkupEncodesTextAndEmptySummaryAddsNoPanelOrAlert()
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        async Task<string> Render(IReadOnlyList<string> lines) => await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<DeliveryExperienceLines>(ParameterView.FromDictionary(new Dictionary<string, object?>
                { [nameof(DeliveryExperienceLines.Lines)] = lines }))).ToHtmlString());
        Assert.DoesNotContain("data-delivery-experience", await Render([]));
        var html = await Render(["도로 배달 완료 12건 <script> · 앱 기록", "단지 배달 완료 8건", "제외할 세 번째 줄"]);
        Assert.Contains("data-delivery-experience", html); Assert.Contains("<script>", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("세 번째", html);
        Assert.DoesNotContain("role=\"alert\"", html); Assert.DoesNotContain("aria-live", html);
    }

    internal static DeliveryExperienceSummaryDto Summary(string metric, string kind, long? count) => new()
    { MetricCode = metric, PlaceKind = kind, PlaceDisplayName = kind == "Road" ? "도로 1" : kind == "ApartmentComplex" ? "단지 1" : "음식점 1",
      CompletedCount = count, EvidenceStatusCode = "Confirmed", AsOfUtc = DateTime.UtcNow, ExecutionModeCode = "Operational" };
    private sealed class NoLocation : IRoleWorkspaceLocationProvider
    { public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken ct) => Task.FromResult<RoleWorkspaceLocation?>(null); }
}
