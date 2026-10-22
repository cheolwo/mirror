using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Admin.Settlement;
using SsalddelAdmin.Components.Pages;
using SsalddelAdmin.Services;
using SsalddelAdmin.ViewModels;
using 기사월정산관리응답 = SsalddelAdmin.Services.기사월정산관리응답;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>관리자 기존 조회 소비자의 수납/월 이용료/기사 지급 경계를 검증합니다. 실제 금융 실행은 하지 않습니다.</summary>
public sealed class AdminTransportSettlementR20Tests
{
    private const string RequestId = "r20-request";
    private const long TransportId = 207;

    [Theory]
    [InlineData("입금확인완료")]
    [InlineData("정산완료")]
    [InlineData("완료")]
    public void 화주입금과월이용료완료만으로기사실지급단계가완료되지않는다(string settlementState)
    {
        var request = Request(); request.정산상태 = settlementState;
        var detail = Build(request, monthlyFees: [new() { 기사Id = "driver", 이용료 = 9900, 결제완료 = true }]);
        var model = TransportWorkflowSettlementViewModel.Create(detail);

        Assert.False(model.HasActualTransferConfirmation);
        Assert.Equal("관리자 조회 미연결", model.PreparationStatus);
        Assert.Equal("실지급 미확인", model.ActualTransferStatus);
        Assert.False(Assert.Single(detail.단계목록, x => x.단계코드 == "settlement").완료됨);
        Assert.Contains(detail.운영확인목록, x => x.구분 == "지급확인");
    }

    [Theory]
    [InlineData("결제완료", "입금대기", true)]
    [InlineData("입금대기", "입금확인완료", true)]
    [InlineData("승인대기", "입금대기", false)]
    [InlineData("입금미확인", "입금대기", false)]
    [InlineData("결제취소완료", "입금확인완료", false)]
    public void 수납확인은알려진근거만사용하고승인대기나취소완료를성공으로읽지않는다(string paymentState, string settlementState, bool expected)
    {
        var request = Request(); request.결제상태 = paymentState; request.정산상태 = settlementState;
        var detail = Build(request);
        var model = TransportWorkflowSettlementViewModel.Create(detail);
        Assert.Equal(expected, model.CollectionConfirmed);
        Assert.Equal(expected, Assert.Single(detail.단계목록, x => x.단계코드 == "payment").완료됨);
        Assert.False(Assert.Single(detail.단계목록, x => x.단계코드 == "settlement").완료됨);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 시험지급검증은잘못된실지급플래그가섞여도실지급완료가아니다(bool actualFlag)
    {
        var payout = Payout(); payout.StatusCode = "SimulationVerified";
        payout.IsActualTransferCompleted = actualFlag;
        var detail = Build(Request(), Query(payout));
        var model = TransportWorkflowSettlementViewModel.Create(detail);
        Assert.Equal("시험 지급 검증", model.RequestStatus);
        Assert.Equal("실지급 미확인", model.ActualTransferStatus);
        Assert.False(Assert.Single(detail.단계목록, x => x.단계코드 == "settlement").완료됨);
    }

    [Fact]
    public void 같은운송과원천의뢰의운영실지급확인근거만지급완료로표시한다()
    {
        var payout = Payout(); payout.ExecutionModeCode = "Operational"; payout.IsActualTransferCompleted = true;
        var detail = Build(Request(), Query(payout));
        Assert.True(TransportWorkflowSettlementViewModel.Create(detail).HasActualTransferConfirmation);
        Assert.True(Assert.Single(detail.단계목록, x => x.단계코드 == "settlement").완료됨);
    }

    [Theory]
    [InlineData(TransportId + 1, RequestId)]
    [InlineData(TransportId, "other-request")]
    [InlineData(TransportId, "R20-REQUEST")]
    public void 다른운송이나원천의뢰의지급은선택운송의실지급근거로새지않는다(long transportId, string requestId)
    {
        var payout = Payout(); payout.TransportId = transportId; payout.RequestId = requestId;
        payout.ExecutionModeCode = "Operational"; payout.IsActualTransferCompleted = true;
        var detail = Build(Request(), Query(payout));
        Assert.Empty(detail.기사운송료지급조회!.지급요청목록);
        Assert.False(TransportWorkflowSettlementViewModel.Create(detail).HasActualTransferConfirmation);
    }

    [Fact]
    public void 실제빈조회는승인월범위를남기고월이용료를지급후보로섞지않는다()
    {
        var detail = Build(Request(), Query(), [new() { 기사Id = "driver", 이용료 = 9900, 결제완료 = false }]);
        var model = TransportWorkflowSettlementViewModel.Create(detail);
        Assert.True(model.QueryCompleted);
        Assert.Equal("2026-10 승인월 · UTC", model.QueryScope);
        Assert.Equal("조회월 기록 없음", model.RequestStatus);
        Assert.Empty(model.PayoutRequests);
        Assert.DoesNotContain(detail.운영확인목록, x => x.조치안내.Contains("기사 정산 후보", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[null]")]
    [InlineData("malformed")]
    [InlineData("forbidden")]
    [InlineData("timeout")]
    public async Task 지급조회오류는수납과운송을보존하며정상0건으로바꾸지않는다(string failure)
    {
        using var fixture = new Fixture(failure);
        var detail = Assert.IsType<운송워크플로우관제상세응답>(await fixture.Service.운송워크플로우관제상세조회Async(RequestId));
        var model = TransportWorkflowSettlementViewModel.Create(detail);
        Assert.True(model.CollectionConfirmed);
        Assert.NotNull(detail.운송);
        Assert.True(model.QueryFailed);
        Assert.False(model.QueryCompleted);
        Assert.Equal("조회 실패", model.RequestStatus);
        Assert.Contains(detail.운영확인목록, x => x.구분 == "지급조회");
        Assert.All(fixture.Handler.Calls, call => Assert.Equal(HttpMethod.Get, call.Method));
    }

    [Fact]
    public async Task 실제관리자소비자는UTC승인월과운송원천의뢰를결속하고기사본인준비나월이용료API를호출하지않는다()
    {
        using var fixture = new Fixture();
        var detail = Assert.IsType<운송워크플로우관제상세응답>(await fixture.Service.운송워크플로우관제상세조회Async(RequestId));
        var query = Assert.IsType<기사운송료지급조회응답>(detail.기사운송료지급조회);
        Assert.True(query.조회완료);
        Assert.Equal(6200m, Assert.Single(query.지급요청목록).ExpectedPayoutAmount);
        var call = Assert.Single(fixture.Handler.Calls, call => call.Uri.AbsolutePath == "/api/v1/admin/driver-payouts");
        var args = QueryHelpers.ParseQuery(call.Uri.Query);
        Assert.Equal(query.조회년!.Value.ToString(), args["year"]);
        Assert.Equal(query.조회월!.Value.ToString(), args["month"]);
        Assert.False(args.ContainsKey("driverId")); // 운송 응답의 기사_운송자는 계정 ID가 아닌 표시명일 수 있습니다.
        Assert.All(fixture.Handler.Calls, call =>
        {
            Assert.Equal(HttpMethod.Get, call.Method);
            Assert.Equal("r20-admin-token", call.Bearer);
            Assert.DoesNotContain("payout-preparations", call.Uri.AbsolutePath);
            Assert.DoesNotContain("driver-settlements", call.Uri.AbsolutePath);
        });
        Assert.Empty(detail.정산후보목록);
    }

    [Fact]
    public async Task 운송미생성시에는지급조회연결을꾸미지않고금융API를호출하지않는다()
    {
        using var fixture = new Fixture(noTransport: true);
        var detail = Assert.IsType<운송워크플로우관제상세응답>(await fixture.Service.운송워크플로우관제상세조회Async(RequestId));
        Assert.Null(detail.기사운송료지급조회);
        Assert.Equal("조회 미연결", TransportWorkflowSettlementViewModel.Create(detail).RequestStatus);
        Assert.DoesNotContain(fixture.Handler.Calls, call => call.Uri.AbsolutePath.Contains("driver-payouts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 화면이탈취소는지급조회실패로덮지않고호출자에게전파한다()
    {
        using var fixture = new Fixture("caller-cancel");
        using var lifetime = new CancellationTokenSource();
        fixture.Handler.CancelOnPayout = lifetime.Cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.운송워크플로우관제상세조회Async(RequestId, lifetime.Token));
        Assert.All(fixture.Handler.Calls, call => Assert.Equal(HttpMethod.Get, call.Method));
    }

    [Fact]
    public async Task 실제운송정산화면은화주운임과기사예정액을구분하고시험검증과미연결을보인다()
    {
        using var fixture = new Fixture();
        var html = await RenderAsync(fixture);
        Assert.Contains("화주 운임", html);
        Assert.Contains("18,000원", html);
        Assert.Contains("6,200원", html);
        Assert.Contains("시험 지급 검증", html);
        Assert.Contains("실지급 미확인", html);
        Assert.Contains("관리자 조회 미연결", html);
        Assert.Contains("승인월 · UTC", html);
        Assert.Contains("월 이용료 조회", html);
        Assert.DoesNotContain("기사 정산 후보", html);
        Assert.DoesNotContain("99,999원", html);
        Assert.All(fixture.Handler.Calls, call => Assert.Equal(HttpMethod.Get, call.Method));
    }

    [Fact]
    public async Task 실제화면의지급조회실패는수납정보와재조회및기존복귀를남긴다()
    {
        using var fixture = new Fixture("forbidden");
        var html = await RenderAsync(fixture);
        Assert.Contains("수납 확인", html);
        Assert.Contains("지급요청 조회 실패", html);
        Assert.Contains("실지급 미확인", html);
        Assert.Contains("새로고침", html);
        Assert.Contains("관제 개요", html);
        Assert.DoesNotContain("0건", html);
        Assert.DoesNotContain("이 승인월에 연결된 지급요청이 없습니다", html);
        Assert.DoesNotContain("private-provider-error", html);
    }

    private static 화주운송의뢰응답 Request() => new()
    {
        의뢰Id = RequestId, 결제상태 = "결제완료", 정산상태 = "입금확인완료", 최종운임 = 18000,
        운송방식 = "퀵", 픽업지 = "픽업 장소", 하차지 = "전달 장소", 생성일시 = DateTime.UtcNow
    };

    private static 운송진행응답 Transport() => new()
    {
        Id = TransportId, 운송번호 = RequestId, 상태 = "인수완료", 기사_운송자 = "기사 표시명",
        운임 = 18000, 도착 = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static 기사지급승인응답 Payout() => new()
    {
        PayoutRequestId = 701, TransportId = TransportId, RequestId = RequestId, DriverId = "driver-id",
        ExpectedPayoutAmount = 6200, CurrencyCode = "KRW", StatusCode = "SimulationVerified",
        ExecutionModeCode = "Simulation", ApprovedAtUtc = DateTime.UtcNow,
        SimulationVerifiedAtUtc = DateTime.UtcNow, IsActualTransferCompleted = false
    };

    private static 기사운송료지급조회응답 Query(params 기사지급승인응답[] payouts)
        => new() { 조회년 = 2026, 조회월 = 10, 조회완료 = true, 지급요청목록 = payouts };

    private static 운송워크플로우관제상세응답 Build(화주운송의뢰응답 request,
        기사운송료지급조회응답? query = null, IReadOnlyList<기사월정산관리응답>? monthlyFees = null)
        => 운송워크플로우관제상세Factory.Build(RequestId, request, [], [], [Transport()], [], [], monthlyFees ?? [], query)!;

    private static async Task<string> RenderAsync(Fixture fixture)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<I백오피스Service>(fixture.Service);
        services.AddSingleton<NavigationManager, NavigationStub>();
        services.AddSingleton<IJSRuntime, NoopJs>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TransportWorkflowSettlement>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(TransportWorkflowSettlement.RequestId)] = RequestId }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient http;
        public Handler Handler { get; }
        public 백오피스조회Service Service { get; }
        public Fixture(string? failure = null, bool noTransport = false)
        {
            Handler = new(failure, noTransport);
            http = new(Handler) { BaseAddress = new("https://r20-admin.invalid/") };
            var session = new 관리자인증세션Service(new ConfigurationBuilder().Build(), new EnvironmentStub());
            session.로그인적용(new 토큰응답 { AccessToken = "r20-admin-token", UserId = "admin", Roles = ["서버관리자"] });
            Service = new(http, session, new TransportRequestLedgerObserver());
        }
        public void Dispose() => http.Dispose();
    }

    private sealed class Handler(string? failure, bool noTransport) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string? Bearer)> Calls { get; } = [];
        public Action? CancelOnPayout { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.Parameter));
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/api/v1/admin/driver-payouts")
            {
                CancelOnPayout?.Invoke(); cancellationToken.ThrowIfCancellationRequested();
                if (failure == "timeout") throw new TaskCanceledException("private-provider-error");
                if (failure == "forbidden") return Task.FromResult(Reply("private-provider-error", HttpStatusCode.Forbidden));
                if (failure is "null" or "[null]" or "malformed") return Task.FromResult(Reply(failure));
                var other = Payout(); other.TransportId++; other.ExpectedPayoutAmount = 99999;
                return Task.FromResult(Reply(JsonSerializer.Serialize(new[] { Payout(), other })));
            }
            if (path.StartsWith("/api/v1/shipper/requests/", StringComparison.Ordinal))
                return Task.FromResult(Reply("""
                    {"의뢰Id":"r20-request","의뢰상태":"인수완료","결제상태":"결제완료","정산상태":"입금확인완료",
                     "배차상태":"확정","최종운임":18000,"생성일시":"2026-10-22T00:00:00Z","픽업지":"픽업 장소","하차지":"전달 장소"}
                    """));
            if (path == "/api/v1/admin/transports") return Task.FromResult(Reply(JsonSerializer.Serialize(noTransport ? Array.Empty<운송진행응답>() : [Transport()])));
            if (path is "/api/v1/payments" or "/api/v1/dispatch/wait" or "/api/v1/admin/transports/events" or "/api/v1/admin/files/pod")
                return Task.FromResult(Reply("[]"));
            return Task.FromResult(Reply("unexpected GET: " + path, HttpStatusCode.NotFound));
        }
        private static HttpResponseMessage Reply(string body, HttpStatusCode status = HttpStatusCode.OK)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class NavigationStub : NavigationManager
    {
        public NavigationStub() => Initialize("https://r20-admin.invalid/", "https://r20-admin.invalid/transports/r20-request/settlement");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
    private sealed class EnvironmentStub : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "AdminTransportR20Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
