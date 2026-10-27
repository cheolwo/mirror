using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

namespace Ssalddel.Tests.Ui.Common;

public sealed class CargoPublicBrowseTests
{
    [Fact]
    public async Task 공개조회는_기존익명GET만_사용하고_신청이나_개인상세를_호출하지않는다()
    {
        var handler = new Handler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };
        var client = new 공개화물Client(http);

        var items = await client.조회Async(2, 20, CancellationToken.None);

        Assert.Single(items);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("/api/v1/shipper/requests/public?page=2&pageSize=20", handler.Path);
        Assert.Null(handler.Authorization);
    }

    [Fact]
    public async Task 늦은_이전페이지는_현재목록을_덮지않는다()
    {
        var first = new TaskCompletionSource<IReadOnlyList<공개화물요약응답>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Read = (page, _) => page == 1 ? first.Task : Task.FromResult<IReadOnlyList<공개화물요약응답>>([Item("현재 페이지")]) };
        using var model = new 공개화물목록ViewModel(client);
        var stale = model.LoadAsync();
        await model.LoadAsync(2);
        first.SetResult([Item("이전 페이지")]);
        await stale;

        Assert.Equal(2, model.Page);
        Assert.Equal("현재 페이지", Assert.Single(model.Items).화물종류);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task 조회실패를_빈목록으로_확정하지않고_재시도뒤_실제결과를_표시한다()
    {
        var client = new Client { Read = (_, _) => throw new InvalidOperationException("private address/token") };
        using var model = new 공개화물목록ViewModel(client);
        await model.LoadAsync();
        Assert.False(model.HasLoaded);
        Assert.Empty(model.Items);
        Assert.Contains("다시 시도", model.Error);
        Assert.DoesNotContain("private", model.Error);
        Assert.False(model.CanReadNext);

        client.Read = (_, _) => Task.FromResult<IReadOnlyList<공개화물요약응답>>([]);
        await model.LoadAsync();
        Assert.True(model.HasLoaded);
        Assert.Null(model.Error);
    }

    [Fact]
    public async Task 페이지종료뒤_늦은응답은_보관하지않는다()
    {
        var pending = new TaskCompletionSource<IReadOnlyList<공개화물요약응답>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new Client { Read = (_, _) => pending.Task };
        var model = new 공개화물목록ViewModel(client);
        var read = model.LoadAsync();
        model.Dispose();
        pending.SetResult([Item("이탈한 화면")]);
        await read;
        Assert.Empty(model.Items);
    }

    [Fact]
    public async Task 공개화면은_조건과_로그인행동만_보이고_개인상세링크를_제공하지않는다()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient(_ => new 공개화물목록ViewModel(new Client()));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<CargoPublicBrowseScreen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CargoPublicBrowseScreen.PrimaryActionHref)] = CargoPublicBrowseNavigation.ShipperRequestLogin
            }));
            return rendered.ToHtmlString();
        });

        Assert.Contains("공개 화물 목록", WebUtility.HtmlDecode(html));
        Assert.Contains("로그인하고 운송 의뢰", WebUtility.HtmlDecode(html));
        Assert.Contains("/workspace-login/shipper?returnUrl=%2Fshipper%2Frequest", html);
        Assert.DoesNotContain("/shipper/requests/", html);
        Assert.DoesNotContain("synthetic-private-id", html);
        Assert.DoesNotContain("수락", html);
        Assert.DoesNotContain(CargoNoticeNavigation.Public, html);
        Assert.DoesNotContain("화물 정보 공고 둘러보기", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task 공고경로를_명시한_호스트에서만_공고_둘러보기_링크를_표시한다()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient(_ => new 공개화물목록ViewModel(new Client()));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await renderer.RenderComponentAsync<CargoPublicBrowseScreen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(CargoPublicBrowseScreen.InformationNoticesHref)] = CargoNoticeNavigation.Public
            }));
            return rendered.ToHtmlString();
        });

        Assert.Contains($"href=\"{CargoNoticeNavigation.Public}\"", html);
        Assert.Contains("화물 정보 공고 둘러보기", WebUtility.HtmlDecode(html));
        Assert.Contains("로그인하고 운송 의뢰", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("synthetic-private-id", html);
    }

    [Theory]
    [InlineData("shipper", "/shipper/request", "/shipper/request")]
    [InlineData("cargo-driver", "/workspace/cargo-driver?selected=work-a", "/workspace/cargo-driver?selected=work-a")]
    [InlineData("cargo-driver", "/shipper/request", null)]
    [InlineData("shipper", "/shipper/request/private", null)]
    [InlineData("shipper", "https://example.invalid/", null)]
    [InlineData("shipper", "//example.invalid/", null)]
    [InlineData("shipper", "/workspace/../private", null)]
    [InlineData("shipper", "/workspace/%2e%2e/private", null)]
    public void 로그인복귀는_기존업무와_화주작성루트만_허용한다(string role, string candidate, string? expected)
        => Assert.Equal(expected, CargoPublicBrowseNavigation.LoginReturn(role, candidate));

    private static 공개화물요약응답 Item(string name) => new()
    {
        의뢰Id = "synthetic-private-id", 화물종류 = name, 화물수량 = 2,
        화물중량Kg = 30, 운송방식 = "단독", 차량종류 = "1톤 카고", 의뢰상태 = "접수", 배차상태 = "대기"
    };

    private sealed class Client : I공개화물Client
    {
        public Func<int, CancellationToken, Task<IReadOnlyList<공개화물요약응답>>> Read { get; set; }
            = (_, _) => Task.FromResult<IReadOnlyList<공개화물요약응답>>([Item("행사 비품")]);
        public Task<IReadOnlyList<공개화물요약응답>> 조회Async(int page, int pageSize, CancellationToken cancellationToken)
            => Read(page, cancellationToken);
    }

    private sealed class Handler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Path = request.RequestUri!.PathAndQuery;
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { Item("공개 화물") }) });
        }
    }
}
