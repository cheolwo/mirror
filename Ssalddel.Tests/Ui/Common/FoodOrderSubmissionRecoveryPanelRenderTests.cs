using System.Net;
using Microsoft.AspNetCore.Components;
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

/// <summary>실제 복구 panel의 정적 HTML. 브라우저 동작·클릭·기기 화면 검증과 구별합니다.</summary>
public sealed class FoodOrderSubmissionRecoveryPanelRenderTests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("accepted")]
    [InlineData("error")]
    [InlineData("loading")]
    public async Task 복구상태별화면은_확인과명시적행동만표시하며렌더만으로재제출하지않는다(string state)
    {
        var store = new PendingStore(new("synthetic-owner", new()
        {
            클라이언트요청Id = Guid.NewGuid(), 음식점Id = 7,
            상품목록 = [new() { 메뉴Id = 8, 상품명 = "기존 선택 메뉴", 수량 = 2, 단가 = 3000 }]
        }, DateTime.UtcNow));
        var api = new Api(state);
        using var recovery = new FoodOrderSubmissionRecoveryViewModel(store, api, api);
        var initialization = recovery.계정설정Async("synthetic-owner");
        if (state == "loading") await api.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        else await initialization;
        var callbacks = 0;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>();
        services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var rendered = await renderer.RenderComponentAsync<OrdererFoodSubmissionRecoveryPanel>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(OrdererFoodSubmissionRecoveryPanel.Recovery)] = recovery,
                    [nameof(OrdererFoodSubmissionRecoveryPanel.CheckRequested)] = EventCallback.Factory.Create(this, () => { callbacks++; }),
                    [nameof(OrdererFoodSubmissionRecoveryPanel.ResubmitRequested)] = EventCallback.Factory.Create(this, () => { callbacks++; }),
                    [nameof(OrdererFoodSubmissionRecoveryPanel.OpenOrderRequested)] = EventCallback.Factory.Create<string>(this, _ => { callbacks++; })
                }));
            return WebUtility.HtmlDecode(rendered.ToHtmlString());
        });

        Assert.Contains("주문 접수 확인", html);
        Assert.Equal(1, api.Reads);
        Assert.Equal(0, api.Posts);
        Assert.Equal(0, callbacks);
        switch (state)
        {
            case "missing":
                Assert.Contains("기존 선택 메뉴 · 2개", html);
                Assert.Contains("현재 접수된 주문을 찾지 못했습니다", html);
                Assert.Contains("같은 주문 다시 제출", html);
                Assert.Contains("접수 결과 확인", html);
                Assert.DoesNotContain("주문 상세 보기", html);
                break;
            case "accepted":
                Assert.Contains("주문이 접수됐습니다", html);
                Assert.Contains("주문 상세 보기", html);
                Assert.DoesNotContain("같은 주문 다시 제출", html);
                Assert.DoesNotContain("접수 결과 확인", html);
                Assert.DoesNotContain("기존 선택 메뉴", html);
                break;
            case "error":
                Assert.Contains("접수 결과를 확인하지 못했습니다", html);
                Assert.Contains("접수 결과 확인", html);
                Assert.Contains("기존 선택 메뉴 · 2개", html);
                Assert.DoesNotContain("같은 주문 다시 제출", html);
                Assert.DoesNotContain("주문 상세 보기", html);
                break;
            case "loading":
                Assert.Contains("이전에 제출한 주문을 확인하고 있습니다", html);
                Assert.DoesNotContain("접수 결과 확인", html);
                Assert.DoesNotContain("같은 주문 다시 제출", html);
                Assert.DoesNotContain("주문 상세 보기", html);
                api.ReadCompletion.SetResult(null);
                await initialization.WaitAsync(TimeSpan.FromSeconds(5));
                break;
        }
        Assert.Equal(0, api.Posts);
        Assert.Equal(0, callbacks);
    }

    private sealed class PendingStore(FoodOrderPendingSubmission? pending) : IFoodOrderPendingSubmissionStore
    {
        public Task<FoodOrderPendingSubmission?> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(pending);
        public Task SaveAsync(FoodOrderPendingSubmission snapshot, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("조회와 렌더 중 저장 요청을 재제출하면 안 됩니다.");
        public Task ClearAsync(Guid requestId, CancellationToken cancellationToken = default)
        {
            if (pending?.Request.클라이언트요청Id == requestId) pending = null;
            return Task.CompletedTask;
        }
    }
    private sealed class Api(string state) : I주문자음식주문접수결과Service, I주문자음식주문쓰기Service
    {
        public int Reads { get; private set; }
        public int Posts { get; private set; }
        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<음식주문접수결과응답?> ReadCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<음식주문접수결과응답?> 접수결과Async(Guid requestId, CancellationToken cancellationToken = default)
        {
            Reads++;
            ReadStarted.TrySetResult();
            return state switch
            {
                "accepted" => Task.FromResult<음식주문접수결과응답?>(new() { 주문번호 = "FOOD-EXISTING" }),
                "error" => Task.FromException<음식주문접수결과응답?>(new SsalddelApiException("synthetic 500", 500, "접수 결과", "", null)),
                "loading" => ReadCompletion.Task,
                _ => Task.FromResult<음식주문접수결과응답?>(null)
            };
        }
        public Task<음식주문응답> 등록Async(음식주문등록요청 request, CancellationToken cancellationToken = default)
        {
            Posts++;
            throw new InvalidOperationException("렌더나 결과 조회가 주문을 자동 재제출하면 안 됩니다.");
        }
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/food/restaurants");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
