using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Components.Recovery;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Contracts.Shipper.Request;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkErrorRecoveryTests
{
    [Theory]
    [InlineData("http-404 Not Found RAW_PRIVATE", "not-found")]
    [InlineData("운송 정보를 찾지 못했습니다.", "not-found")]
    [InlineData("로그인 세션 조회 실패: RAW_PRIVATE", "session")]
    [InlineData("HTTP 500 traceId=RAW_PRIVATE", "read-failure")]
    [InlineData("GET /requests/401abc HTTP 404", "not-found")]
    [InlineData("GET /requests/401abc-403-road404 HTTP 500", "read-failure")]
    [InlineData("HTTP 401", "session")]
    [InlineData("HTTP 403", "session")]
    public void 기술원문은_분류에만쓰고_안내로복제하지않는다(string raw, string category)
    {
        var notice = 업무오류안내Presentation.생성(raw)!;
        Assert.Equal(category, notice.분류);
        Assert.DoesNotContain("RAW_PRIVATE", notice.제목 + notice.설명);
    }

    [Fact]
    public void 미확정은_404나로그인문구보다_우선하고_재전송을유도하지않는다()
    {
        var notice = 업무오류안내Presentation.생성("HTTP404 세션 RAW_PRIVATE", outcomeUnknown: true)!;
        Assert.Equal("unknown-outcome", notice.분류);
        Assert.Equal("결과 확인", notice.재조회문구);
        Assert.Contains("다시 요청하지 말고", notice.설명);
        Assert.DoesNotContain("사진", notice.설명);
    }

    [Theory]
    [InlineData("transport-failure", null, false, true)]
    [InlineData("http-503", 503, false, true)]
    [InlineData("timeout", null, true, false)]
    [InlineData("business-rule", 422, false, false)]
    [InlineData("conflict", 409, false, false)]
    public void 기술오류만_공통다이얼로그로분리한다(string code, int? status, bool fieldError, bool expected)
    {
        var error = new Api작업오류(code, "RAW", status, 충돌: status == 409,
            필드오류: fieldError ? new Dictionary<string, IReadOnlyList<string>> { ["field"] = ["required"] } : null);
        Assert.Equal(expected, 업무오류안내Presentation.기술실패인가(error));
    }

    [Theory]
    [InlineData("BusinessRuleBlocked")]
    [InlineData("RevisionConflict")]
    [InlineData("TerminalManualReview")]
    public void 서버전문실패분류가있으면_http상태보다우선해_기존안내를유지한다(string classification)
        => Assert.False(업무오류안내Presentation.기술실패인가(new Api작업오류("http-503", "업무 안내", 503,
            실패분류Code: classification)));

    [Fact]
    public void 같은분류는_trace문구가변해도_자동조회와빈Busy중간상태로재개방하지않는다()
    {
        var session = new 업무오류안내Session();
        session.갱신("A", "read-failure", false);
        var revision = session.Revision;
        Assert.True(session.열기());
        Assert.False(session.열기());
        Assert.False(session.갱신("A", null, true));
        Assert.False(session.갱신("A", "read-failure", false));
        Assert.Equal(revision, session.Revision);
        Assert.False(session.열기());
        Assert.True(session.열기(manual: true));
    }

    [Fact]
    public void 명시재조회_실제해소_대상변경은_각각새안내를허용하고_문맥세대를구분한다()
    {
        var session = new 업무오류안내Session();
        session.갱신("A", "read-failure", false); Assert.True(session.열기());
        session.재조회시작(); Assert.True(session.열기());
        var revision = session.Revision;
        Assert.True(session.갱신("B", "read-failure", false));
        Assert.NotEqual(revision, session.Revision); Assert.True(session.열기());
        Assert.True(session.갱신("B", null, false)); Assert.False(session.열기());
        session.갱신("B", "read-failure", false); Assert.True(session.열기());
    }

    [Fact]
    public async Task 조회오류프레임은_기술원문을숨기고_닫은뒤에도_미완료와다시열기를남긴다()
    {
        var html = await Render(new ShipperRequestDetailPageState
        {
            LookupRequestId = "private-id",
            StatusMessage = "운송 의뢰 원장 조회 실패: RAW_PRIVATE trace",
            StatusTone = ShipperRequestDetailMessageTone.Error
        });
        Assert.Contains("연결 확인 필요", html);
        Assert.Contains("안내 보기", html);
        Assert.DoesNotContain("RAW_PRIVATE", html);
        Assert.DoesNotContain("private-id", html);
    }

    [Theory]
    [InlineData("테스트 결제 완료 · 실제 결제 없음", ShipperRequestDetailMessageTone.Success)]
    [InlineData("인수증 등록 완료 · 입금·정산은 별도 확인", ShipperRequestDetailMessageTone.Success)]
    [InlineData("등록 결과가 미확인입니다. 결과 확인 후 다시 진행해 주세요.", ShipperRequestDetailMessageTone.Warning)]
    public async Task 명령의성공과미확정안내는_조회오류로숨기지않는다(string message, ShipperRequestDetailMessageTone tone)
    {
        var html = await Render(new ShipperRequestDetailPageState { StatusMessage = message, StatusTone = tone });
        Assert.Contains(message, html);
        Assert.DoesNotContain("연결 확인 필요", html);
    }

    [Fact]
    public async Task 구조화조회오류는_의뢰맥락안내를렌더하고_코드나기술원문을표시하지않는다()
    {
        var html = await Render(new ShipperRequestDetailPageState
        {
            LookupRequestId = "private-id",
            QueryError = new Api작업오류("transport-failure", "RAW_PRIVATE HTTP404"),
            StatusMessage = "RAW_PRIVATE", StatusTone = ShipperRequestDetailMessageTone.Error
        });
        Assert.Contains("운송 의뢰 정보 확인 필요", html);
        Assert.Contains("안내 보기", html);
        Assert.DoesNotContain("RAW_PRIVATE", html);
        Assert.DoesNotContain("transport-failure", html);
        Assert.DoesNotContain("private-id", html);
    }

    private static async Task<string> Render(ShipperRequestDetailPageState state)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager, TestNavigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ShipperRequestDetailScreenFrame>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(ShipperRequestDetailScreenFrame.State)] = state,
                    [nameof(ShipperRequestDetailScreenFrame.Context)] = new ShipperRequestDetailNavigationContext(),
                    [nameof(ShipperRequestDetailScreenFrame.Title)] = "진행 이력",
                    [nameof(ShipperRequestDetailScreenFrame.HomeHref)] = "/shipper",
                    [nameof(ShipperRequestDetailScreenFrame.NewRequestHref)] = "/shipper/request",
                    [nameof(ShipperRequestDetailScreenFrame.ChildContent)] = (RenderFragment)(_ => { })
                }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!);
        public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!);
    }
    private sealed class TestNavigation : NavigationManager
    {
        public TestNavigation() => Initialize("http://localhost/", "http://localhost/shipper");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
