using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Home;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class HomeParticipationDetailsR12Tests
{
    [Fact]
    public async Task 상세client는공개고정경로에선택페이지만GET하고자격이나개별식별자를보내지않는다()
    {
        using var factory = new PublicFactory(Valid(2));
        var result = await new HomeParticipationDetailsClient(factory).LoadAsync(2);
        Assert.Equal(SsalddelHttpClientNames.OperationalApi, factory.ClientName);
        Assert.Equal(HttpMethod.Get, factory.Method);
        Assert.Equal("https://example.test/api/v1/common/home/participation-details?page=2", factory.Uri?.AbsoluteUri);
        Assert.Null(factory.Authorization);
        Assert.Null(factory.Body);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(2, result.Page);
        Assert.Null(result.Summary.ConfirmedPlatformProfitKrw);
        Assert.Null(result.Summary.ConfirmedSharedPayoutKrw);
    }

    [Theory]
    [InlineData("ascending-months")]
    [InlineData("duplicate-month")]
    [InlineData("invalid-month")]
    [InlineData("negative-count")]
    [InlineData("zero-completed-month")]
    [InlineData("account-exceeds-month")]
    [InlineData("account-exceeds-total")]
    [InlineData("month-sum-exceeds-total")]
    [InlineData("unknown-total")]
    [InlineData("wrong-basis")]
    [InlineData("wrong-page")]
    [InlineData("wrong-page-size")]
    [InlineData("false-next-page")]
    [InlineData("partial-first-page")]
    [InlineData("understated-month-total")]
    [InlineData("null-months")]
    public async Task 잘못된월별응답을정상참여실적으로사용하지않는다(string defect)
    {
        var response = Valid();
        var months = response.MonthlyItems.ToArray();
        switch (defect)
        {
            case "ascending-months": response.MonthlyItems = months.Reverse().ToArray(); break;
            case "duplicate-month": months[1].MonthCode = months[0].MonthCode; break;
            case "invalid-month": months[0].MonthCode = "2026-13"; break;
            case "negative-count": months[0].FreightDriverAccountCount = -1; break;
            case "zero-completed-month": months[0].CompletedFreightTransportCount = 0; break;
            case "account-exceeds-month": months[1].FreightShipperAccountCount = 8; response.Summary.FreightShipperAccountCount = 8; break;
            case "account-exceeds-total": months[0].FreightDriverAccountCount = 6; break;
            case "month-sum-exceeds-total": months[0].CompletedFreightTransportCount = 11; break;
            case "unknown-total": response.Summary.CompletedFreightTransportCount = null; break;
            case "wrong-basis": response.MonthlyPeriodBasisCode = "payment-month-utc"; break;
            case "wrong-page": response.Page = 2; break;
            case "wrong-page-size": response.PageSize = 200; break;
            case "false-next-page": response.HasMore = true; break;
            case "partial-first-page": response.MonthlyItems = []; break;
            case "understated-month-total": months[0].CompletedFreightTransportCount = 9; break;
            case "null-months": response.MonthlyItems = null!; break;
        }
        using var factory = new PublicFactory(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => new HomeParticipationDetailsClient(factory).LoadAsync(1));
        Assert.Equal(1, factory.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public async Task 잘못된페이지는HTTP요청전에거절한다(int page)
    {
        using var factory = new PublicFactory(Valid());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new HomeParticipationDetailsClient(factory).LoadAsync(page));
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task 빈본문은0건성공으로바꾸지않는다()
    {
        using var factory = new PublicFactory(null);
        await Assert.ThrowsAsync<InvalidDataException>(() => new HomeParticipationDetailsClient(factory).LoadAsync(1));
    }

    [Fact]
    public async Task HTTP실패를관리자API나추가요청으로우회하지않는다()
    {
        using var factory = new PublicFactory(Valid(), HttpStatusCode.ServiceUnavailable);
        await Assert.ThrowsAsync<HttpRequestException>(() => new HomeParticipationDetailsClient(factory).LoadAsync(1));
        Assert.Equal(1, factory.Calls);
        Assert.Null(factory.Authorization);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 페이지전환은이전요청을취소하고늦게온성공이나실패가현재페이지를덮지못한다(bool lateFailure)
    {
        var old = new TaskCompletionSource<HomeParticipationDetailsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken firstToken = default;
        var current = Valid(2);
        var client = new ScriptedClient((page, call, ct) =>
        {
            if (call == 1) { firstToken = ct; return old.Task; }
            return Task.FromResult(current);
        });
        using var model = Model(client);
        var first = model.LoadAsync(1);
        Assert.True(model.IsLoading);
        await model.LoadAsync(2);
        Assert.True(firstToken.IsCancellationRequested);
        if (lateFailure) old.SetException(new HttpRequestException("private-old-page-trace"));
        else old.SetResult(Valid(1));
        await first;
        Assert.Equal(2, model.Page);
        Assert.Same(current, model.Response);
        Assert.False(model.HasError);
        Assert.False(model.IsLoading);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 현재조회실패나시간초과는과거수치를지우고같은페이지재시도로복구한다(bool timeout)
    {
        var recovered = Valid(2);
        var client = new ScriptedClient((page, call, ct) => call switch
        {
            1 => Task.FromResult(Valid()),
            2 => Task.FromException<HomeParticipationDetailsResponse>(timeout
                ? new TaskCanceledException("private-timeout") : new HttpRequestException("private-server-trace")),
            _ => Task.FromResult(recovered)
        });
        using var model = Model(client);
        await model.LoadAsync(1);
        Assert.NotNull(model.Response);
        await model.LoadAsync(2);
        Assert.Null(model.Response);
        Assert.True(model.HasError);
        Assert.False(model.IsLoading);
        Assert.Equal(2, model.Page);
        await model.LoadAsync(model.Page);
        Assert.Same(recovered, model.Response);
        Assert.False(model.HasError);
        Assert.Equal(new[] { 1, 2, 2 }, client.Pages);
    }

    [Fact]
    public async Task 수명종료는조회를취소하고늦은실패를무시하며다시조회하지않는다()
    {
        var pending = new TaskCompletionSource<HomeParticipationDetailsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var client = new ScriptedClient((page, call, ct) => { token = ct; return pending.Task; });
        var model = Model(client);
        var load = model.LoadAsync(1);
        model.Dispose();
        Assert.True(token.IsCancellationRequested);
        pending.SetException(new HttpRequestException("private-disposed-response"));
        await load;
        await model.LoadAsync(2);
        Assert.Equal(1, client.Calls);
        Assert.Null(model.Response);
        Assert.False(model.HasError);
    }

    [Fact]
    public async Task 실제세부화면은월별계정의비가산기준과시험표시페이지및역할진입을보인다()
    {
        var response = FullPage();
        response.Summary.HasIncompleteParticipantBindings = true;
        response.MonthlyItems[0].HasIncompleteParticipantBindings = true;
        var html = await RenderPageAsync(new ScriptedClient((page, call, ct) => Task.FromResult(response)));
        Assert.Contains("참여 내역", html);
        Assert.Contains("시험 집계", html);
        Assert.Contains("전체 누적", html);
        Assert.Contains("2026년 10월", html);
        Assert.Contains("월별 계정 수의 합은 전체 계정 수와 다를 수 있습니다.", html);
        Assert.Contains("확인되지 않은 일부 참여 계정은 제외했습니다.", html);
        Assert.Contains("일부 계정 미집계", html);
        Assert.Contains("수령 확인·정산·실제 지급과 별도", html);
        Assert.Contains("2026.10.22 12:00", html);
        Assert.Contains("href=\"http://localhost/preview/overview/participation?page=2\"", html);
        Assert.Contains("href=\"http://localhost/preview/workspace/shipper\"", html);
        Assert.Contains("href=\"http://localhost/preview/workspace/cargo-driver\"", html);
        Assert.DoesNotContain("/admin/", html);
        Assert.DoesNotContain("결제키", html);
    }

    [Fact]
    public async Task 비어있는후속페이지는누적실적을0으로바꾸지않고이전페이지와홈복귀를보인다()
    {
        var response = Valid(2);
        response.MonthlyItems = [];
        var html = await RenderPageAsync(new ScriptedClient((page, call, ct) => Task.FromResult(response)), "2");
        Assert.Contains("17건", html);
        Assert.Contains("이 페이지에 표시할 완료 기록이 없습니다.", html);
        Assert.Contains("2 페이지", html);
        Assert.Contains("href=\"http://localhost/preview/overview/participation\"", html);
        Assert.Contains("href=\"http://localhost/preview/\"", html);
        Assert.DoesNotContain("?page=3", html);
        Assert.DoesNotContain("0건", html);
    }

    [Fact]
    public async Task 실제세부오류화면은원문예외나가짜0을보이지않고재조회와업무복귀를유지한다()
    {
        var html = await RenderPageAsync(new ScriptedClient((page, call, ct) =>
            Task.FromException<HomeParticipationDetailsResponse>(new HttpRequestException("private-server-host-and-key"))));
        Assert.Contains("참여 내역을 불러오지 못했습니다.", html);
        Assert.Contains("다시 확인", html);
        Assert.Contains("운영 요약", html);
        Assert.Contains("화주 업무", html);
        Assert.Contains("기사 업무", html);
        Assert.DoesNotContain("private-server-host-and-key", html);
        Assert.DoesNotContain("0건", html);
        Assert.DoesNotContain("2026.10.22", html);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("https://external.test/")]
    public async Task 화면의잘못된페이지입력은첫공개페이지로정규화한다(string pageInput)
    {
        var client = new ScriptedClient((page, call, ct) => Task.FromResult(Valid(page)));
        await RenderPageAsync(client, pageInput);
        Assert.Equal(new[] { 1 }, client.Pages);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("http://localhost/preview/overview/participation", true)]
    public async Task 기존요약은명시된세부진입만표시하며미집계금액을유지한다(string? href, bool visible)
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddMudServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<HomeParticipationSummary>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(HomeParticipationSummary.Summary)] = Valid().Summary,
                [nameof(HomeParticipationSummary.DetailsHref)] = href
            }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Equal(visible, html.Contains("내역 보기", StringComparison.Ordinal));
        if (visible) Assert.Contains("href=\"http://localhost/preview/overview/participation\"", html);
        Assert.Contains("17건", html);
        Assert.Contains("미집계", html);
        Assert.DoesNotContain("0원", html);
    }

    private static HomeParticipationDetailsResponse Valid(int page = 1) => new()
    {
        Page = page,
        Summary = new()
        {
            PeriodCode = "all-time", StatusCode = "recorded", ExecutionMode = "Simulation",
            AsOfUtc = new(2026, 10, 22, 3, 0, 0, TimeSpan.Zero),
            CompletedFreightTransportCount = 17, FreightShipperAccountCount = 3, FreightDriverAccountCount = 5
        },
        MonthlyItems =
        [
            new() { MonthCode = "2026-10", CompletedFreightTransportCount = 10, FreightShipperAccountCount = 3, FreightDriverAccountCount = 4 },
            new() { MonthCode = "2026-09", CompletedFreightTransportCount = 7, FreightShipperAccountCount = 2, FreightDriverAccountCount = 2 }
        ]
    };

    private static HomeParticipationDetailsResponse FullPage() => new()
    {
        Summary = new() { AsOfUtc = new(2026, 10, 22, 3, 0, 0, TimeSpan.Zero), ExecutionMode = "Simulation",
            CompletedFreightTransportCount = 13, FreightShipperAccountCount = 1, FreightDriverAccountCount = 1 },
        HasMore = true,
        MonthlyItems = Enumerable.Range(0, 12).Select(index => new HomeParticipationMonthSummary
        {
            MonthCode = new DateTime(2026, 10, 1).AddMonths(-index).ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture),
            CompletedFreightTransportCount = 1, FreightShipperAccountCount = 1, FreightDriverAccountCount = 1
        }).ToArray()
    };

    private static HomeParticipationDetailsViewModel Model(IHomeParticipationDetailsClient client)
        => new(client, NullLogger<HomeParticipationDetailsViewModel>.Instance);

    private static async Task<string> RenderPageAsync(IHomeParticipationDetailsClient client, string? page = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NavigationManager>(new TestNavigation());
        services.AddSingleton(client);
        services.AddSingleton<HomeParticipationDetailsViewModel>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<HomeParticipationDetailsPage>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(HomeParticipationDetailsPage.Page)] = page }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class TestNavigation : NavigationManager
    {
        internal TestNavigation() => Initialize("http://localhost/preview/", "http://localhost/preview/overview/participation");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class ScriptedClient(Func<int, int, CancellationToken, Task<HomeParticipationDetailsResponse>> load) : IHomeParticipationDetailsClient
    {
        internal int Calls { get; private set; }
        internal List<int> Pages { get; } = [];
        public Task<HomeParticipationDetailsResponse> LoadAsync(int page, CancellationToken cancellationToken = default)
        { Calls++; Pages.Add(page); return load(page, Calls, cancellationToken); }
    }

    private sealed class PublicFactory(HomeParticipationDetailsResponse? response, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler, IHttpClientFactory
    {
        private HttpClient? client;
        internal string? ClientName { get; private set; }
        internal HttpMethod? Method { get; private set; }
        internal Uri? Uri { get; private set; }
        internal string? Authorization { get; private set; }
        internal string? Body { get; private set; }
        internal int Calls { get; private set; }
        public HttpClient CreateClient(string name)
        { ClientName = name; return client ??= new HttpClient(this, false) { BaseAddress = new Uri("https://example.test/") }; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Method = request.Method; Uri = request.RequestUri; Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = JsonContent.Create(response) };
        }
        protected override void Dispose(bool disposing) { if (disposing) client?.Dispose(); base.Dispose(disposing); }
    }
}
