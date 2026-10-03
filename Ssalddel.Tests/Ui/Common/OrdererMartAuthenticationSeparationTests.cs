using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Mart;
using Ssalddel.Contracts.Mart;
using Ssalddel.Ui.Common.Areas.App.Components.Mart;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererMartAuthenticationSeparationTests
{
    [Fact]
    public async Task 실제세션복원중에는_업무입력과상품대신인증진행상태만표시한다()
    {
        var completion = new TaskCompletionSource<주문자앱인증결과>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var fixture = new RenderFixture(new AuthenticationService { PendingRestore = completion.Task });

        var pendingHtml = await fixture.BeginRenderAsync();
        Assert.Contains("저장된 주문자 로그인 세션을 확인하고 있습니다.", pendingHtml);
        Assert.DoesNotContain("type=\"password\"", pendingHtml);
        AssertNoOrderWorkspace(pendingHtml);

        completion.SetResult(new(주문자앱세션상태.익명));
        var completedHtml = await fixture.WaitForInitializationAsync();
        AssertAuthenticationOnly(completedHtml);
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 익명로그인은_이미조회된상품과영수증도함께노출하지않는다(bool existingRequest)
    {
        await using var fixture = new RenderFixture(new AuthenticationService());
        Guid? requestId = existingRequest ? fixture.Orders.Receipt.주문요청Id : null;
        if (requestId is Guid selected)
        {
            Assert.True(await fixture.Page.요청상세.조회Async(selected));
            Assert.NotNull(fixture.Page.요청상세.상세);
        }

        var html = await fixture.RenderAsync(requestId: requestId);

        Assert.NotNull(fixture.Page.상품.상세);
        AssertAuthenticationOnly(html);
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 복원실패는_업무입력없이로그인오류와복귀동작을표시한다(bool throws)
    {
        const string message = "합성 주문자 세션 복원 실패";
        await using var fixture = new RenderFixture(new AuthenticationService
        {
            RestoreError = throws ? null : message,
            RestoreFailure = throws ? new IOException(message) : null
        });

        var html = await fixture.RenderAsync();

        Assert.False(fixture.Page.인증.초기화됨);
        Assert.False(fixture.Page.인증.처리중);
        AssertAuthenticationOnly(html);
        Assert.Contains(message, html);
        Assert.Contains("로그인 없이 상품 더 보기", html);
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    [Fact]
    public async Task 정상인증후작성화면은_로그인입력없이선택상품과명시적저장행동을표시한다()
    {
        await using var fixture = new RenderFixture(new AuthenticationService { SignedIn = true });
        fixture.Page.작성.수량 = 3;
        fixture.Page.작성.비구속주문요청확인 = true;

        var html = await fixture.RenderAsync();

        Assert.Contains("data-mart-stage=\"workspace\"", html);
        Assert.Contains("합성 공개 상품", html);
        Assert.Contains("요청 수량", html);
        Assert.Contains("주문 요청 저장", html);
        Assert.Contains("로그아웃", html);
        Assert.DoesNotContain("type=\"password\"", html);
        Assert.DoesNotContain("로그인하고 계속", html);
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    [Fact]
    public async Task 정상인증영수증은_같은요청을조회하고_로그인입력없이표시한다()
    {
        await using var fixture = new RenderFixture(new AuthenticationService { SignedIn = true });

        var html = await fixture.RenderAsync(requestId: fixture.Orders.Receipt.주문요청Id);

        Assert.Contains("mart-order-receipt", html);
        Assert.Contains("합성 요청 영수증", html);
        Assert.DoesNotContain("type=\"password\"", html);
        Assert.DoesNotContain("요청 수량", html);
        Assert.Equal(fixture.Orders.Receipt.주문요청Id, Assert.Single(fixture.Orders.DetailRequests));
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    [Fact]
    public async Task 같은컴포넌트의로그인실패후성공은_작성초안과멱등Id를보존하고_자동저장하지않는다()
    {
        var authentication = new AuthenticationService { FailFirstLogin = true };
        await using var fixture = new RenderFixture(authentication);
        fixture.Page.작성.수량 = 4;
        fixture.Page.작성.비구속주문요청확인 = true;
        var requestId = fixture.Page.작성.클라이언트요청Id;
        AssertAuthenticationOnly(await fixture.RenderAsync());

        var failedHtml = await fixture.LoginAsync();
        AssertAuthenticationOnly(failedHtml);
        Assert.Contains("합성 로그인 실패", failedHtml);
        AssertDraft(fixture.Page.작성, requestId);
        Assert.Equal(0, fixture.Orders.SubmitCalls);

        var successfulHtml = await fixture.LoginAsync();
        Assert.Contains("합성 공개 상품", successfulHtml);
        Assert.Contains("요청 수량", successfulHtml);
        Assert.Contains("주문 요청 저장", successfulHtml);
        Assert.DoesNotContain("type=\"password\"", successfulHtml);
        AssertDraft(fixture.Page.작성, requestId);
        Assert.Equal(0, fixture.Orders.SubmitCalls);
        Assert.Equal(2, authentication.LoginCalls);
    }

    [Fact]
    public async Task 선택대상이없으면_로그인을강요하거나상품을자동선택하지않는다()
    {
        await using var fixture = new RenderFixture(new AuthenticationService());

        var html = await fixture.RenderAsync(productId: null);

        Assert.Contains("주문 요청할 상품을 먼저 선택해 주세요.", html);
        Assert.Contains("공개 상품 보기", html);
        Assert.DoesNotContain("type=\"password\"", html);
        AssertNoOrderWorkspace(html);
        Assert.Empty(fixture.Products.DetailRequests);
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 공용접근frame은_인증모드에서만업무탐색을숨기고안전한복귀는유지한다(bool authenticationOnly)
    {
        await using var fixture = new RenderFixture(new AuthenticationService());

        var html = await fixture.RenderFrameAsync(authenticationOnly);

        Assert.Contains("돌아가기", html);
        Assert.Contains("/food/mart?q=rice", html);
        Assert.Contains("합성 본문", html);
        Assert.Equal(!authenticationOnly, html.Contains("mart-product-nav", StringComparison.Ordinal));
        Assert.Equal(!authenticationOnly, html.Contains("공개 상품과 마지막 투영 시각", StringComparison.Ordinal));
        Assert.Equal(0, fixture.Orders.SubmitCalls);
    }

    private static void AssertNoOrderWorkspace(string html)
    {
        Assert.DoesNotContain("합성 공개 상품", html);
        Assert.DoesNotContain("합성 요청 영수증", html);
        Assert.DoesNotContain("요청 수량", html);
        Assert.DoesNotContain("주문 요청 저장", html);
        Assert.DoesNotContain("mart-order-receipt", html);
        Assert.DoesNotContain("mart-order-product-heading", html);
    }

    private static void AssertAuthenticationOnly(string html)
    {
        Assert.Contains("data-mart-stage=\"authentication\"", html);
        Assert.Contains("type=\"password\"", html);
        Assert.Contains("로그인하고 계속", html);
        AssertNoOrderWorkspace(html);
    }

    private static void AssertDraft(마트주문작성ViewModel writer, Guid requestId)
    {
        Assert.Equal(4, writer.수량);
        Assert.True(writer.비구속주문요청확인);
        Assert.Equal(requestId, writer.클라이언트요청Id);
        Assert.Null(writer.등록응답);
    }

    // The real PageViewModel, authentication, writer and Razor remain in one root.
    // Only external data services and JS/navigation are replaced for this render proof.
    private sealed class RenderFixture : IAsyncDisposable
    {
        private readonly ServiceProvider provider;
        private readonly HtmlRenderer renderer;
        private HtmlRootComponent rendered;

        public ProductService Products { get; } = new();
        public OrderService Orders { get; } = new();
        public 마트주문작성PageViewModel Page { get; }

        public RenderFixture(AuthenticationService authentication)
        {
            Page = new(
                new 주문자앱인증ViewModel(authentication),
                new 마트공개상품상세ViewModel(Products),
                new 마트주문작성ViewModel(Orders),
                new 마트주문요청상세ViewModel(Orders));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMudServices();
            services.AddSingleton<IJSRuntime, NoopJsRuntime>();
            services.AddSingleton<NavigationManager, TestNavigationManager>();
            services.AddSingleton(Page);
            services.AddSingleton(new 마트페이지접근ViewModel(new AccessService()));
            provider = services.BuildServiceProvider();
            renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());
        }

        public Task<string> RenderAsync(long? productId = 41, Guid? requestId = null)
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                rendered = await renderer.RenderComponentAsync<OrdererMartOrderRequestWorkspace>(Parameters(productId, requestId));
                return Html();
            });

        public Task<string> BeginRenderAsync()
            => renderer.Dispatcher.InvokeAsync(() =>
            {
                rendered = renderer.BeginRenderingComponent<OrdererMartOrderRequestWorkspace>(Parameters(41, null));
                return Html();
            });

        public async Task<string> WaitForInitializationAsync()
        {
            await rendered.QuiescenceTask;
            return await renderer.Dispatcher.InvokeAsync(Html);
        }

        public Task<string> LoginAsync()
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                await Page.인증.로그인Async("synthetic-orderer", "synthetic-password");
                return Html();
            });

        public Task<string> RenderFrameAsync(bool authenticationOnly)
            => renderer.Dispatcher.InvokeAsync(async () =>
            {
                var frame = await renderer.RenderComponentAsync<MartProductAccessFrame>(ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    [nameof(MartProductAccessFrame.Context)] = new MartProductNavigationContext { From = "/food/mart?q=rice" },
                    [nameof(MartProductAccessFrame.CurrentScreen)] = MartProductScreenKind.Order,
                    [nameof(MartProductAccessFrame.ProductId)] = 41L,
                    [nameof(MartProductAccessFrame.Eyebrow)] = "ORDER REQUEST",
                    [nameof(MartProductAccessFrame.Title)] = "합성 화면",
                    [nameof(MartProductAccessFrame.Description)] = "합성 설명",
                    [nameof(MartProductAccessFrame.HomeHref)] = "/food",
                    [nameof(MartProductAccessFrame.AuthenticationOnly)] = authenticationOnly,
                    [nameof(MartProductAccessFrame.ChildContent)] = (RenderFragment)(content => content.AddContent(0, "합성 본문"))
                }));
                return WebUtility.HtmlDecode(frame.ToHtmlString());
            });

        private static ParameterView Parameters(long? productId, Guid? requestId)
            => ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(OrdererMartOrderRequestWorkspace.ProductId)] = productId,
                [nameof(OrdererMartOrderRequestWorkspace.RequestId)] = requestId,
                [nameof(OrdererMartOrderRequestWorkspace.CatalogHref)] = "/food/mart?q=rice&page=2"
            });

        private string Html() => WebUtility.HtmlDecode(rendered.ToHtmlString());

        public async ValueTask DisposeAsync()
        {
            await renderer.DisposeAsync();
            await provider.DisposeAsync();
        }
    }

    private sealed class AuthenticationService : I주문자앱인증Service
    {
        public bool SignedIn { get; init; }
        public string? RestoreError { get; init; }
        public Exception? RestoreFailure { get; init; }
        public Task<주문자앱인증결과>? PendingRestore { get; init; }
        public bool FailFirstLogin { get; init; }
        public int LoginCalls { get; private set; }

        public Task<주문자앱인증결과> 복원Async(CancellationToken cancellationToken = default)
            => RestoreFailure is not null
                ? Task.FromException<주문자앱인증결과>(RestoreFailure)
                : PendingRestore ?? Task.FromResult(new 주문자앱인증결과(
                    new(SignedIn, SignedIn ? "synthetic-orderer" : null, SignedIn ? "합성 주문자" : null), RestoreError));

        public Task<주문자앱인증결과> 로그인Async(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
        {
            LoginCalls++;
            return Task.FromResult(FailFirstLogin && LoginCalls == 1
                ? new 주문자앱인증결과(주문자앱세션상태.익명, "합성 로그인 실패")
                : new 주문자앱인증결과(new(true, "synthetic-orderer", "합성 주문자")));
        }

        public Task 로그아웃Async(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ProductService : I마트공개상품읽기Service
    {
        public List<long> DetailRequests { get; } = [];

        public Task<마트공개상품상세응답?> 상세Async(long productId, CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(productId);
            return Task.FromResult<마트공개상품상세응답?>(new()
            {
                Id = productId,
                상품명 = "합성 공개 상품",
                판매단위 = "개",
                판매가 = 9000,
                판매가능수량 = 20,
                판매가능여부 = true,
                재고기준시각Utc = DateTime.UnixEpoch
            });
        }

        public Task<마트공개상품목록응답> 목록Async(마트공개상품목록조회요청 request, CancellationToken cancellationToken = default)
            => Task.FromResult(new 마트공개상품목록응답());
    }

    private sealed class OrderService : I마트주문요청Service
    {
        public int SubmitCalls { get; private set; }
        public List<Guid> DetailRequests { get; } = [];
        public 마트주문요청응답 Receipt { get; } = new()
        {
            주문요청Id = Guid.Parse("937b82e0-6053-4d83-a11b-8b45cfe891d1"),
            공개상품Id = 41,
            상품명 = "합성 요청 영수증",
            수량 = 4,
            판매단위 = "개",
            단가 = 9000,
            합계 = 36000,
            재고기준시각Utc = DateTime.UnixEpoch,
            제출일시Utc = DateTime.UnixEpoch
        };

        public Task<마트주문요청응답> 등록Async(마트주문요청등록요청 request, CancellationToken cancellationToken = default)
        {
            SubmitCalls++;
            return Task.FromResult(Receipt);
        }

        public Task<마트주문요청응답?> 상세Async(Guid orderRequestId, CancellationToken cancellationToken = default)
        {
            DetailRequests.Add(orderRequestId);
            return Task.FromResult<마트주문요청응답?>(orderRequestId == Receipt.주문요청Id ? Receipt : null);
        }
    }

    private sealed class AccessService : I마트페이지접근Service
    {
        public Task<bool> 기능활성여부Async(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => ValueTask.FromResult(default(TValue)!);
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/food/mart/order/41");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
