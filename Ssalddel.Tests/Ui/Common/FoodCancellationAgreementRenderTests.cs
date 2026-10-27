using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Food;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class FoodCancellationAgreementRenderTests
{
    [Fact]
    public async Task 실제패널은_조건조회와양측동의및별도실행을구분하고_자동조회나자동동의하지않는다()
    {
        var client = new Client { Listing = [Ready()] };
        await RenderAsync(client, async (panel, html, refresh) =>
        {
            Assert.Contains("조리 전 취소 조건 협의", html());
            Assert.Contains("취소 조건 확인", html());
            Assert.DoesNotContain("새 조건 제안", html());
            Assert.Equal(0, client.Reads);
            var model = Model(panel);
            await model.LoadAsync(); refresh();
            Assert.Contains("주문자 부담 0원 · 음식점 부담 700.25원", html());
            Assert.Contains("양측이 동의한 조건으로 주문 취소를 반영합니다.", html());
            Assert.Contains("환불·지급 완료가 아닙니다", html());
            Assert.True(ButtonIsDisabled(html(), "합의한 취소 반영"));
            Assert.Equal(0, client.Commands);
            model.ApplyConfirmed = true; refresh();
            Assert.False(ButtonIsDisabled(html(), "합의한 취소 반영"));
            model.SetOrder("food", 2); refresh();
            Assert.True(ButtonIsDisabled(html(), "합의한 취소 반영"));
            Assert.Contains("주문 또는 수행 상태가 달라졌습니다", html());
        });
    }

    [Fact]
    public async Task 응답유실의이유와같은요청확인은_접힌협의영역밖에남고_다시POST하지않는다()
    {
        var client = new Client { LoseProposal = true };
        await RenderAsync(client, async (panel, html, refresh) =>
        {
            var model = Model(panel); await model.LoadAsync();
            model.OrdererCostKrw = model.RestaurantCostKrw = 0; model.CostBasisReference = "명시적 무비용 합의";
            await model.ProposeAsync(); refresh();
            var view = html(); var details = view.IndexOf("<details", StringComparison.Ordinal);
            Assert.InRange(view.IndexOf("처리 결과가 아직 확인되지 않았습니다", StringComparison.Ordinal), 0, details - 1);
            Assert.InRange(view.IndexOf("처리 결과 확인", StringComparison.Ordinal), 0, details - 1);
            Assert.True(ButtonIsDisabled(view, "조건 제안"));
            await model.ProposeAsync(); Assert.Equal(1, client.Commands);
        });
    }

    [Fact]
    public async Task 취소된조건은_환불이나지급완료대신_별도금융확인을표시한다()
    {
        var value = Ready(); value.StateCode = "Cancelled"; value.CanApply = value.CanConsent = false;
        var client = new Client { Listing = [value] };
        await RenderAsync(client, async (panel, html, refresh) =>
        {
            await Model(panel).LoadAsync(); refresh();
            Assert.Contains("주문 취소가 반영됐습니다. 합의한 비용의 수납·환불·지급은 별도로 확인합니다.", html());
            Assert.DoesNotContain("새 조건 제안", html());
            Assert.DoesNotContain("합의한 취소 반영", html());
            Assert.Equal(0, client.Commands);
        });
    }

    private static 음식주문취소합의응답 Ready() => new()
    {
        OrderNo = "food", AgreementId = "agreement", Revision = 3, FrozenOrderRevision = 1,
        CurrentOrderRevision = 1, StateCode = "ReadyToApply", OrdererCostKrw = 0, RestaurantCostKrw = 700.25m,
        CostBasisReference = "증빙 cost-reference", OrdererAgreed = true, RestaurantAgreed = true,
        CanApply = true, CanConsent = true, CurrentPartyCode = "Orderer"
    };
    private static 음식주문취소합의ViewModel Model(FoodCancellationAgreementPanel panel)
        => (음식주문취소합의ViewModel)typeof(FoodCancellationAgreementPanel)
            .GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
    private static bool ButtonIsDisabled(string html, string label)
    {
        var labelAt = html.IndexOf(label, StringComparison.Ordinal);
        Assert.True(labelAt >= 0, html);
        var start = html.LastIndexOf("<button", labelAt, StringComparison.Ordinal);
        Assert.True(start >= 0, html);
        var end = html.IndexOf('>', start);
        return html[start..end].Contains("disabled", StringComparison.Ordinal);
    }
    private static async Task RenderAsync(Client client, Func<FoodCancellationAgreementPanel, Func<string>, Action, Task> verify)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddCommerceUiFixture(); services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        services.AddSingleton<I음식주문취소합의Client>(client);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            FoodCancellationAgreementPanel? panel = null;
            var rendered = await renderer.RenderComponentAsync<Host>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(Host.Capture)] = (Action<FoodCancellationAgreementPanel>)(value => panel = value) }));
            await verify(panel!, () => WebUtility.HtmlDecode(rendered.ToHtmlString()), () =>
                typeof(ComponentBase).GetMethod("StateHasChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, null));
        });
    }
    private sealed class Host : ComponentBase
    {
        [Parameter] public Action<FoodCancellationAgreementPanel>? Capture { get; set; }
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<FoodCancellationAgreementPanel>(0);
            builder.AddAttribute(1, nameof(FoodCancellationAgreementPanel.OrderNo), "food");
            builder.AddAttribute(2, nameof(FoodCancellationAgreementPanel.OrderRevision), 1L);
            builder.AddComponentReferenceCapture(3, component => Capture?.Invoke((FoodCancellationAgreementPanel)component));
            builder.CloseComponent();
        }
    }
    private sealed class Client : I음식주문취소합의Client
    {
        public IReadOnlyList<음식주문취소합의응답> Listing { get; init; } = [];
        public bool LoseProposal { get; init; }
        public int Reads { get; private set; }
        public int Commands { get; private set; }
        public Task<IReadOnlyList<음식주문취소합의응답>> 목록Async(string orderNo, CancellationToken ct)
        { Reads++; return Task.FromResult(Listing); }
        public Task<음식주문취소합의응답?> 조회Async(string orderNo, string agreementId, Guid requestId, CancellationToken ct)
            => Task.FromResult<음식주문취소합의응답?>(null);
        public Task<음식주문취소합의응답> 제안Async(string orderNo, 음식주문취소합의제안요청 request, CancellationToken ct)
        { Commands++; return LoseProposal ? Task.FromException<음식주문취소합의응답>(new HttpRequestException("response lost")) : throw new NotSupportedException(); }
        public Task<음식주문취소합의응답> 동의Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct)
        { Commands++; throw new NotSupportedException(); }
        public Task<음식주문취소합의응답> 실행Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct)
        { Commands++; throw new NotSupportedException(); }
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/orders/food?orderNo=food");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
}
