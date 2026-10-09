using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using static Ssalddel.Tests.Ui.Common.주문자음식주문취소ViewModelTests;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererFoodOrderStateFocusTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ReceiptConfirmationComesBeforeSupportingDetailsOnlyWhenServerAllowsIt(bool allowed, bool stale)
    {
        var service = new FoodService { Detail = new()
        {
            주문 = new() { 주문번호 = "FOOD-A", 상태 = 음식주문상태코드.전달완료 },
            배달진행 = new() { 배차요청됨 = true, 기사전달완료 = true, 안내 = "음식 수령을 확인해 주세요." },
            AvailableActions = allowed ? [new() { ActionId = 음식배달가능행동Ids.주문수령확인 }] : []
        }};
        var detail = new 주문자음식주문상세ViewModel(service, service);
        Assert.True(await detail.조회Async("FOOD-A"));
        if (stale) detail.조회상태무효화();

        var html = await RenderDetailAsync(detail);
        var buttons = Regex.Matches(html, "<button\\b[^>]*>(?:(?!</button>).)*음식 수령 확인(?:(?!</button>).)*</button>", RegexOptions.Singleline);

        Assert.Equal(allowed ? 1 : 0, buttons.Count);
        Assert.Equal(allowed, html.Contains("data-food-order-current-task", StringComparison.Ordinal));
        Assert.Equal(allowed && !stale, detail.수령확인가능);
        Assert.Equal(1, Regex.Matches(html, "aria-label=\"전달과 수령 확인 단계\"").Count);
        Assert.Contains("기사가 전달 완료를 기록했습니다.", html);
        Assert.Contains("실제 음식을 받은 뒤 주문자가 직접 확인합니다.", html);
        if (allowed)
        {
            Assert.True(html.IndexOf("data-food-order-current-task", StringComparison.Ordinal)
                < html.IndexOf("내 음식 주문 상세", StringComparison.Ordinal));
            Assert.True(html.IndexOf("음식 수령 확인", StringComparison.Ordinal)
                < html.IndexOf("주문 상품", StringComparison.Ordinal));
            Assert.Equal(stale, buttons[0].Value.Contains("disabled", StringComparison.Ordinal));
            var memo = Regex.Match(html, "<details\\b[^>]*class=\"food-order-receipt-memo\"[^>]*>");
            Assert.True(memo.Success);
            Assert.DoesNotMatch("\\bopen(?:\\s|=|>)", memo.Value);
            Assert.Contains("수령 확인 메모 (선택)", html);
            if (stale) Assert.Contains("마지막으로 확인한 주문 정보입니다.", html);
        }
        Assert.Empty(service.Requests);
    }

    [Theory]
    [InlineData(음식주문상태코드.주문확인, false, false)]
    [InlineData(음식주문상태코드.픽업완료, false, false)]
    [InlineData(음식주문상태코드.전달완료, true, false)]
    [InlineData(음식주문상태코드.수령확인, true, true)]
    [InlineData(음식주문상태코드.취소, false, false)]
    [InlineData(음식주문상태코드.거절, false, false)]
    public async Task DeliveryAndReceiptInformationMatchesTheCanonicalOrderState(string state, bool delivered, bool received)
    {
        var stopped = state is 음식주문상태코드.취소 or 음식주문상태코드.거절;
        var service = new FoodService { Detail = new()
        {
            주문 = new() { 주문번호 = "FOOD-A", 상태 = state },
            배달진행 = new()
            {
                배차요청됨 = true, 기사전달완료 = delivered, 주문자수령확인됨 = received,
                현재운송상태 = stopped ? "배차취소" : "배달중",
                안내 = stopped ? "주문이 중단되어 배달이 진행되지 않습니다." : "배달 진행을 확인해 주세요."
            },
            AvailableActions = state == 음식주문상태코드.전달완료
                ? [new() { ActionId = 음식배달가능행동Ids.주문수령확인 }] : []
        }};
        var detail = new 주문자음식주문상세ViewModel(service, service);
        Assert.True(await detail.조회Async("FOOD-A"));

        var html = await RenderDetailAsync(detail);

        Assert.Contains("주문 상세 닫기", html);
        if (stopped)
        {
            Assert.Contains("주문이 중단되어 배달이 진행되지 않습니다.", html);
            Assert.DoesNotContain("전달과 수령 확인 단계", html);
            Assert.DoesNotContain("기사의 전달 완료를 기다리고 있습니다.", html);
            Assert.DoesNotContain("실제 음식을 받은 뒤", html);
            Assert.DoesNotContain("수령 확인 메모", html);
            Assert.DoesNotContain("기존 기사 제안이 종료되었습니다.", html);
        }
        else
        {
            Assert.Contains("전달과 수령 확인 단계", html);
            Assert.Contains(delivered ? "기사가 전달 완료를 기록했습니다." : "기사의 전달 완료를 기다리고 있습니다.", html);
            if (received) Assert.DoesNotContain("실제 음식을 받은 뒤 주문자가 직접 확인합니다.", html);
            Assert.Equal(state == 음식주문상태코드.전달완료, html.Contains("수령 확인 메모", StringComparison.Ordinal));
        }
        Assert.Empty(service.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrFailedSelectedOrderStillOffersListReturn(bool failed)
    {
        var service = new FoodService { Detail = null };
        if (failed) service.OnRead = (_, _) => throw new HttpRequestException("연결 오류");
        var detail = new 주문자음식주문상세ViewModel(service, service);
        await detail.조회Async("FOOD-A");

        var html = await RenderDetailAsync(detail);

        Assert.Contains("주문 상세 닫기", html);
        Assert.Contains("주문 목록", html);
        Assert.Contains(failed ? "주문 상세를 불러오지 못했습니다." : "이 계정의 음식 주문을 찾을 수 없습니다.", html);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task LoadingSelectedOrderStillOffersListReturn()
    {
        var pending = new TaskCompletionSource<주문자음식주문상세응답?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FoodService { OnRead = (_, _) => pending.Task };
        var detail = new 주문자음식주문상세ViewModel(service, service);
        var reading = detail.조회Async("FOOD-A");
        try
        {
            var html = await RenderDetailAsync(detail);
            Assert.Contains("주문 상세를 불러오고 있습니다.", html);
            Assert.Contains("주문 상세 닫기", html);
        }
        finally { pending.SetResult(null); await reading; }
    }

    [Fact]
    public async Task WorkspaceSelectionHasAFocusModeAndReturningPreservesListInputWithoutCommands()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service, new AuthenticationService());
        var services = Services();
        services.AddSingleton(page);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = Renderer(provider);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var result = await renderer.RenderComponentAsync<OrdererFoodOrderWorkspace>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(OrdererFoodOrderWorkspace.OrderNo)] = "FOOD-A" }));
            page.목록.검색어 = "내 주문";
            page.목록.상태필터 = 음식주문상태코드.주문대기;
            Assert.Contains("food-order-layout--detail", result.ToHtmlString());

            page.주문선택해제();

            Assert.DoesNotContain("food-order-layout--detail", result.ToHtmlString());
            Assert.Equal("내 주문", page.목록.검색어);
            Assert.Equal(음식주문상태코드.주문대기, page.목록.상태필터);
            Assert.Null(page.상세.요청OrderNo);
            Assert.Empty(service.Requests);
        });
    }

    private static async Task<string> RenderDetailAsync(주문자음식주문상세ViewModel detail)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = Renderer(provider);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var result = await renderer.RenderComponentAsync<OrdererFoodOrderDetailPanel>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(OrdererFoodOrderDetailPanel.Detail)] = detail }));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddCommerceUiFixture();
        services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        return services;
    }
    private static HtmlRenderer Renderer(IServiceProvider provider)
        => new(provider, provider.GetRequiredService<ILoggerFactory>());
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/food?orderNo=FOOD-A");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
}
