using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Components.Fulfillment;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using static Ssalddel.Tests.Ui.Common.주문자음식주문취소ViewModelTests;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodOrderConsentRenewalRenderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 조리잠금사유가있으면조건변경입력을표시하지않고협의가필요함을안내한다(bool legacyFlag)
    {
        var snapshot = new AutoFulfillmentSnapshot
        {
            SourceKind = FulfillmentSourceKinds.FoodOrder, SourceId = "FOOD-A",
            StateCode = AutoFulfillmentStates.NeedsReview, CanRenewConsent = legacyFlag,
            ConsentRenewalLockedReasonCode = "CookingAlreadyStarted"
        };
        var html = await Render<AutoFulfillmentStatus>(new()
        {
            [nameof(AutoFulfillmentStatus.Value)] = snapshot,
            [nameof(AutoFulfillmentStatus.Renewal)] = (RenderFragment)(builder => builder.AddContent(0, "renewal-input-marker"))
        });

        Assert.Contains("조리가 시작되어 자동 접수 조건과 처리처를 바꿀 수 없습니다", html);
        Assert.DoesNotContain("renewal-input-marker", html);
        Assert.Contains("실제 외부 배송·청구·입금·기사 지급 없음", html);
        Assert.False(AutoFulfillmentPresentation.CanRenew(snapshot));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 수락후협의요청은기존보호지원화면으로연결하고주문명령을실행하지않는다(bool accepted)
    {
        const string orderNo = "FOOD/A?x=1";
        var service = new FoodService { Detail = new()
        {
            주문 = new() { 주문번호 = orderNo, 상태 = accepted ? 음식주문상태코드.조리중 : 음식주문상태코드.주문대기 },
            음식점수락시각Utc = accepted ? DateTime.UtcNow : null
        }};
        var detail = new 주문자음식주문상세ViewModel(service, service);
        Assert.True(await detail.조회Async(orderNo));

        var html = await Render<OrdererFoodOrderDetailPanel>(new() { [nameof(OrdererFoodOrderDetailPanel.Detail)] = detail });

        Assert.Equal(accepted, html.Contains("취소·변경 협의 요청 및 처리 확인"));
        if (accepted)
        {
            Assert.Contains("commerce/disputes?sourceKind=food-order&sourceId=FOOD%2FA%3Fx%3D1", html);
            Assert.Contains("요청 접수만으로 주문·배송이 취소되거나 환불이 완료되지 않습니다", html);
        }
        Assert.Empty(service.Requests);
    }

    [Fact]
    public void 잠금사유의추가계약은기존허용Snapshot직렬화를변경하지않는다()
    {
        var snapshot = new AutoFulfillmentSnapshot { CanRenewConsent = true };
        Assert.DoesNotContain("ConsentRenewalLockedReasonCode", JsonSerializer.Serialize(snapshot));
        snapshot.ConsentRenewalLockedReasonCode = "FulfillmentStateUnknown";
        Assert.Contains("FulfillmentStateUnknown", JsonSerializer.Serialize(snapshot));
        Assert.False(AutoFulfillmentPresentation.CanRenew(snapshot));
        Assert.Contains("현재 수행 상태가 확인되지 않아", AutoFulfillmentPresentation.ConsentRenewalLockedReason("future-code"));
    }

    private static async Task<string> Render<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices(); services.AddCommerceUiFixture();
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var result = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }

    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/food");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
}
