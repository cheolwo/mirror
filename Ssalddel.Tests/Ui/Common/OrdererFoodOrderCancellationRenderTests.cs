using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Models.Auth;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using static Ssalddel.Tests.Ui.Common.주문자음식주문취소ViewModelTests;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererFoodOrderCancellationRenderTests
{
    [Fact]
    public async Task 익명내역은_자격입력만보이고_검색상세와취소입력을보이지않는다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service, new AuthenticationService { SignedIn = false });
        await RenderAsync(page, async html =>
        {
            Assert.True(page.인증화면표시);
            Assert.Contains("type=\"password\"", html());
            Assert.DoesNotContain("주문 취소 검토", html());
            Assert.DoesNotContain("주문 상세 닫기", html());
            Assert.DoesNotContain("주문 검색", html());
            Assert.Empty(service.Requests);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task 취소검토는_같은주문상세의보조행동이고_선택과사유를명시확인한다()
    {
        var service = new FoodService();
        using var page = await CreatePageAsync(service, new AuthenticationService());
        await RenderAsync(page, async html =>
        {
            Assert.Contains("주문 취소 검토", html());
            Assert.DoesNotContain("type=\"password\"", html());
            Assert.DoesNotContain("취소 사유", html());
            page.취소!.작성시작();
            Assert.Contains("취소 사유", html());
            Assert.Contains("선택한 주문과 취소 사유를 확인했습니다.", html());
            Assert.Contains("환불 완료를 의미하지 않습니다", html());
            Assert.Contains("FOOD-A 주문 취소", html());
            Assert.False(page.취소.제출가능);
            Assert.Empty(service.Requests);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task 경합은_정본상세와오류복구를보존하고_취소완료로표시하지않는다()
    {
        var service = new FoodService
        {
            OnCancel = (_, _, _) => throw new HttpRequestException("상태 변경", null, HttpStatusCode.Conflict)
        };
        using var page = await CreatePageAsync(service, new AuthenticationService());
        await RenderAsync(page, async html =>
        {
            Review(page.취소!);
            service.Detail = Ready(revision: 8);
            Assert.False(await page.주문취소Async());
            Assert.Contains("주문 상태가 변경됐습니다", html());
            Assert.Contains("주문 상세 닫기", html());
            Assert.DoesNotContain("주문 취소를 확인했습니다.", html());
            Assert.False(page.취소!.검토확인);
            Assert.Single(service.Requests);
        });
    }

    [Fact]
    public async Task 최종401은_개인상세와취소입력을제거하고_로그인후같은검토로복귀한다()
    {
        var service = new FoodService
        {
            OnCancel = (_, _, _) => throw new HttpRequestException("세션 만료", null, HttpStatusCode.Unauthorized)
        };
        using var page = await CreatePageAsync(service, new AuthenticationService());
        await RenderAsync(page, async html =>
        {
            Review(page.취소!);
            Assert.False(await page.주문취소Async());
            var id = page.취소!.현재시도요청Id;
            await page.인증오류복구Async();
            Assert.Contains("type=\"password\"", html());
            Assert.DoesNotContain("취소 사유", html());
            Assert.DoesNotContain("주문 상세 닫기", html());
            Assert.True(await page.로그인Async(new 공통로그인요청("user-a", "test-password"), "FOOD-A"));
            Assert.DoesNotContain("type=\"password\"", html());
            Assert.Contains("취소 결과 다시 확인", html());
            Assert.Contains("같은 주문을 두 번 넣었습니다.", html());
            Assert.Equal(id, page.취소.현재시도요청Id);
            Assert.Single(service.Requests);
        });
    }

    private static async Task RenderAsync(주문자음식주문PageViewModel page, Func<Func<string>, Task> verify)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddCommerceUiFixture();
        services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton(page);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var result = await renderer.RenderComponentAsync<OrdererFoodOrderWorkspace>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(OrdererFoodOrderWorkspace.OrderNo)] = "FOOD-A"
                }));
            Assert.False(page.목록.오류발생, page.목록.오류메시지);
            Assert.False(page.상세.오류발생, page.상세.오류메시지);
            await verify(() => WebUtility.HtmlDecode(result.ToHtmlString()));
        });
    }

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/food?orderNo=FOOD-A");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
