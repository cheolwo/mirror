using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Xunit.Abstractions;

namespace Ssalddel.Tests.Ui.Common;

public sealed class ShipperMobileContextScreenRenderTests
{
    private readonly ITestOutputHelper output;
    public ShipperMobileContextScreenRenderTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task 실제Web페이지의_동일컴포넌트경로변경은_입력없이새단계를표시하고_해제시구독을정리한다()
    {
        var navigation = new LiveContextNavigation();
        var state = new 운송의뢰작성ViewModel { 화물종류 = "원래 물품", 화물중량Kg = 3 };
        var js = new NoopJs();
        var pageType = typeof(global::Ssalddel.WebApp.Pages.ShipperRequestMobileContextPage);
        var modelType = pageType.Assembly.GetType("Ssalddel.WebApp.ViewModels.ShipperRequestAuthoringPageViewModel", throwOnError: true)!;
        using var http = new HttpClient(new RejectHttp());
        object Dependency(Type type)
        {
            if (type == typeof(운송의뢰작성ViewModel)) return state;
            if (type == typeof(NavigationManager)) return navigation;
            if (type == typeof(IJSRuntime)) return js;
            if (type == typeof(HttpClient)) return http;
            if (type == typeof(Ssalddel.Client.Infrastructure.Security.IClientSessionGuard))
                return new Ssalddel.Client.Infrastructure.Security.ClientSessionGuard();
            if (type.IsInterface) return DispatchProxy.Create(type, typeof(NoWorkflowCalls));
            // Tests의 링크된 동명 VM이 아니라 실제 Razor 페이지 어셈블리의 생성자를 사용한다.
            if (type == modelType || type.FullName is "Ssalddel.WebApp.Services.운송의뢰자동저장Service"
                or "Ssalddel.WebApp.Services.커뮤니티화물글초안가져오기Service"
                or "Ssalddel.WebApp.Services.WebAuthSessionService")
            {
                var constructor = type.GetConstructors().Single();
                return constructor.Invoke(constructor.GetParameters().Select(parameter => Dependency(parameter.ParameterType)).ToArray());
            }
            throw new InvalidOperationException("이 화면 시험에서 지원하지 않는 의존성: " + type.FullName);
        }
        using var model = (IDisposable)Dependency(modelType);
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime>(js); services.AddSingleton<NavigationManager>(navigation);
        services.AddSingleton(modelType, model);
        await using var provider = services.BuildServiceProvider();
        var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        Delegate[] externalSubscribersBeforePage = [];
        bool IsPageSubscription(Delegate subscription)
            => subscription.Target?.GetType() == pageType || subscription.Method.DeclaringType == pageType;
        void ReportSubscriptions(string phase)
            => output.WriteLine(phase + ": " + string.Join("; ", navigation.LocationSubscribers.Select(subscription =>
                (subscription.Target?.GetType().FullName ?? "<static>") + " -> "
                + subscription.Method.DeclaringType?.FullName + "." + subscription.Method.Name)));
        try
        {
            await renderer.Dispatcher.InvokeAsync(async () =>
            {
                await renderer.RenderComponentAsync<RoleAppProviders>();
                externalSubscribersBeforePage = navigation.LocationSubscribers.ToArray();
                ReportSubscriptions("providers-before-page");
                var view = await renderer.RenderComponentAsync(pageType, ParameterView.Empty);
                ReportSubscriptions("page-rendered");
                Assert.Single(navigation.LocationSubscribers.Where(IsPageSubscription));
                Assert.Equal(externalSubscribersBeforePage.Length + 1, navigation.LocationSubscriberCount);
                Assert.Contains("data-mobile-step=\"package\"", view.ToHtmlString());
                foreach (var step in new[] { ShipperRequestMobileStep.Pickup, ShipperRequestMobileStep.Dropoff,
                    ShipperRequestMobileStep.Schedule, ShipperRequestMobileStep.Vehicle, ShipperRequestMobileStep.Package })
                {
                    navigation.Move(ShipperRequestMobileStepCatalog.Get(step).Path + "?sourcePostId=12");
                    var html = WebUtility.HtmlDecode(view.ToHtmlString());
                    Assert.Contains($"data-mobile-step=\"{step.ToString().ToLowerInvariant()}\"", html);
                    Assert.Contains(ShipperRequestMobileStepCatalog.Get(step).Title, html);
                }
                Assert.Equal("원래 물품", state.화물종류);
                Assert.Equal(3m, state.화물중량Kg);
                // 다른 페이지로 나가는 동안 이전 adapter를 렌더하지 않는다.
                navigation.Move("/shipper/request/review");
            });
        }
        finally { await renderer.DisposeAsync(); }
        ReportSubscriptions("renderer-disposed");
        Assert.DoesNotContain(navigation.LocationSubscribers, IsPageSubscription);
        // 외부 provider가 먼저 등록한 콜백을 페이지 누수로 계산하지 않는다. 새 콜백이 남으면 여전히 실패한다.
        Assert.All(navigation.LocationSubscribers, subscription => Assert.Contains(subscription, externalSubscribersBeforePage));
        await provider.DisposeAsync();
        ReportSubscriptions("provider-scope-disposed");
        Assert.DoesNotContain(navigation.LocationSubscribers, IsPageSubscription);
        navigation.Move("/shipper/request/pickup");
    }

    [Fact]
    public async Task 물품과포장화면은_입력을분리하고_현재단계필수만다음이동을판정한다()
    {
        var state = new 운송의뢰작성ViewModel();
        var cargo = await RenderAsync<ShipperRequestCargoScreen>(state, ShipperRequestMobileStep.Cargo);
        Assert.Contains("화물 종류", cargo); Assert.DoesNotContain("전체 중량 kg (포장·깔판 포함)", cargo);
        Assert.True(NextDisabled(cargo)); state.화물종류 = "포장 물품";
        cargo = await RenderAsync<ShipperRequestCargoScreen>(state, ShipperRequestMobileStep.Cargo);
        Assert.False(NextDisabled(cargo));
        var package = await RenderAsync<ShipperRequestCargoScreen>(state, ShipperRequestMobileStep.Package);
        Assert.Contains("포장 1개 길이 mm", package); Assert.DoesNotContain("화물 적재 형태", package);
        Assert.True(NextDisabled(package));
        state.화물중량Kg = 3; state.화물길이Mm = 300; state.화물폭Mm = 200; state.화물높이Mm = 100;
        package = await RenderAsync<ShipperRequestCargoScreen>(state, ShipperRequestMobileStep.Package);
        Assert.False(NextDisabled(package)); Assert.Contains("inputmode=\"numeric\"", package);
    }

    [Theory]
    [InlineData(ShipperRequestMobileStep.Pickup, "상차 도로명 주소", "하차 도로명 주소")]
    [InlineData(ShipperRequestMobileStep.Dropoff, "하차 도로명 주소", "상차 도로명 주소")]
    public async Task 주소화면은_각장소입력만보이고_상대장소와시각입력을섞지않는다(ShipperRequestMobileStep step, string shown, string hidden)
    {
        var state = new 운송의뢰작성ViewModel { 상차도로명주소 = "예시 상차지역", 하차도로명주소 = "예시 전달지역" };
        var html = await RenderAsync<ShipperRequestTransportScreen>(state, step);
        Assert.Contains(shown, html); Assert.DoesNotContain(hidden, html);
        Assert.DoesNotContain("상차 시작 (한국시간)", html); Assert.DoesNotContain("하차 종료 (한국시간", html);
        Assert.Contains("type=\"tel\"", html); Assert.False(NextDisabled(html));
    }

    [Fact]
    public async Task 시간화면은_시간과요청을보이고_주소와운송방식은표시하지않는다()
    {
        var html = await RenderAsync<ShipperRequestTransportScreen>(new(), ShipperRequestMobileStep.Schedule);
        Assert.Contains("상차 시작 (한국시간)", html); Assert.Contains("하차 시간 지정 (선택)", html);
        Assert.Contains("요청 사항", html); Assert.DoesNotContain("상차 도로명 주소", html);
        Assert.DoesNotContain("운송 방식", html); Assert.True(NextDisabled(html));
    }

    [Fact]
    public async Task 차량과비용화면은_선택과입력문맥을나누고_보조절차를보존한다()
    {
        var state = new 운송의뢰작성ViewModel { 차량종류 = "오토바이 퀵" };
        var vehicle = await RenderAsync<ShipperRequestProcedureScreen>(state, ShipperRequestMobileStep.Vehicle);
        Assert.Contains("radiogroup", vehicle); Assert.Contains("운송 방식", vehicle);
        Assert.Contains("권장 운송 방식", vehicle);
        Assert.DoesNotContain("결제 예정 금액", vehicle); Assert.False(NextDisabled(vehicle));
        var payment = await RenderAsync<ShipperRequestProcedureScreen>(state, ShipperRequestMobileStep.Payment);
        Assert.Contains("결제 예정 금액", payment); Assert.DoesNotContain("radiogroup", payment);
        Assert.Contains("추가 절차·알선 조건 (선택)", payment); Assert.Contains("알선소 ID", payment);
    }

    [Fact]
    public async Task 추천차량의선택과미선택은_보조기술에도true와false문자열로표시한다()
    {
        var state = new 운송의뢰작성ViewModel
        { 화물종류 = "시험 상자", 화물중량Kg = 8, 화물길이Mm = 400, 화물폭Mm = 300, 화물높이Mm = 200 };
        await state.차량추천조회Async((request, _) => Task.FromResult(new 차량추천응답
        {
            적재조건 = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(request)),
            후보목록 = [new() { 차량종류 = "1톤 카고", 단일운송가능여부 = true },
                new() { 차량종류 = "1.4톤 카고", 단일운송가능여부 = true }]
        }));
        Assert.True(state.서버추천차량선택("1톤 카고"));
        var html = await RenderAsync<ShipperRequestProcedureScreen>(state, ShipperRequestMobileStep.Vehicle);
        Assert.Contains("aria-checked=\"true\"", html);
        Assert.Contains("aria-checked=\"false\"", html);
    }

    [Fact]
    public async Task 모바일최종확인은_누락입력의정확한단계로보완링크를제공한다()
    {
        var state = new 운송의뢰작성ViewModel { 화물종류 = "예시 물품" };
        var html = await RenderAsync<ShipperRequestReviewScreen>(state, ShipperRequestMobileStep.Review);
        Assert.Contains("shipper/request/package", html); Assert.Contains("shipper/request/pickup", html);
        Assert.Contains("shipper/request/dropoff", html); Assert.Contains("shipper/request/schedule", html);
        Assert.Contains("shipper/request/vehicle", html); Assert.DoesNotContain("같은 ViewModel", html);
        Assert.Contains("접수 안내", html); Assert.DoesNotContain("shipper-mobile-navigation__next", html);
    }

    [Fact]
    public async Task 기존null모드는_PC통합화면의모든입력을그대로제공한다()
    {
        var cargo = await RenderAsync<ShipperRequestCargoScreen>(new(), null);
        Assert.Contains("화물 종류", cargo); Assert.Contains("포장 1개 길이 mm", cargo);
        Assert.DoesNotContain("shipper-mobile-navigation", cargo);
        var transport = await RenderAsync<ShipperRequestTransportScreen>(new(), null);
        Assert.Contains("상차 도로명 주소", transport); Assert.Contains("하차 도로명 주소", transport);
        Assert.Contains("상차 시작 (한국시간)", transport); Assert.Contains("운송 방식", transport);
    }

    private static bool NextDisabled(string html)
    {
        var match = Regex.Match(html, "<button[^>]*class=\"shipper-mobile-navigation__next\"[^>]*>");
        Assert.True(match.Success, html); return match.Value.Contains("disabled", StringComparison.Ordinal);
    }
    private static async Task<string> RenderAsync<T>(운송의뢰작성ViewModel state, ShipperRequestMobileStep? step) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var parameters = new Dictionary<string, object?> { ["ViewModel"] = state, ["MobileStep"] = step,
                ["NavigationContext"] = ShipperRequestNavigationContext.Parse("/shipper/request?sourcePostId=12&returnPath=%2Fshipper") };
            var view = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(view.ToHtmlString());
        });
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/shipper/request/cargo");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class LiveContextNavigation : NavigationManager
    {
        public LiveContextNavigation() => Initialize("http://localhost/", "http://localhost/shipper/request/package");
        public IReadOnlyList<Delegate> LocationSubscribers => ((MulticastDelegate?)typeof(NavigationManager)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(eventField => eventField.FieldType == typeof(EventHandler<LocationChangedEventArgs>)).GetValue(this))?.GetInvocationList() ?? [];
        public int LocationSubscriberCount => LocationSubscribers.Count;
        public void Move(string uri) => NavigateTo(uri);
        protected override void NavigateToCore(string uri, bool forceLoad)
        { Uri = ToAbsoluteUri(uri).AbsoluteUri; NotifyLocationChanged(false); }
    }
    public class NoWorkflowCalls : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException("화면 경로 시험에서 업무 API를 호출하면 안 됩니다: " + targetMethod?.Name);
    }
    private sealed class RejectHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new InvalidOperationException("화면 경로 시험에서 HTTP를 호출하면 안 됩니다.");
    }
}
