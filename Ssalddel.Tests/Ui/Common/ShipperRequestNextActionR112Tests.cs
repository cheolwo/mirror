using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Tests.Clients;
using Ssalddel.Ui.Common.Areas.App.Components;
using Ssalddel.Ui.Common.Areas.App.Components.Transport;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Models.Shipper;

namespace Ssalddel.Tests.Ui.Common;

/// <summary>조회 상태와 실제 공용 화면의 표시 회귀입니다. 결제·배차 실행이나 기기 검증이 아닙니다.</summary>
public sealed class ShipperRequestNextActionR112Tests
{
    [Theory]
    [InlineData("미시작")]
    [InlineData("배차대기")]
    [InlineData("매칭중")]
    public void 새선결제의뢰는_등록과배차대기를구분하고_결제확인이먼저다(string dispatch)
    {
        var item = Pending() with { DispatchStatus = dispatch };
        var next = ShipperRequestDetailPresentation.ResolveNextAction(item);
        Assert.Equal("접수 준비 · 결제 대기", next.Title);
        Assert.Contains("결제 후 배차 접수", next.Reason);
        Assert.Equal(ShipperRequestDetailScreenKind.Payment, next.RelatedScreen);
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveDispatchState(item));
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveAcceptedState(item));
        Assert.False(ShipperRequestDetailPresentation.CanPay(item.DispatchStatus, item.PaymentStatus));
    }

    [Theory]
    [InlineData("운송완료후정산", "후불승인대기")]
    [InlineData("월말정산", "청구대기")]
    [InlineData("운송완료후정산", "인수증등록완료")]
    public void 후불등록이나인수증은_승인과결제완료를대신하지않는다(string timing, string settlement)
    {
        var item = Pending() with { SettlementTiming = timing, SettlementStatus = settlement, DispatchStatus = "매칭중" };
        Assert.Equal("후불 승인 확인 필요", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveDispatchState(item));
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolvePaymentState(item));
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolveSettlementState(item));
    }

    [Theory]
    [InlineData("운송완료후정산", "후불승인완료")]
    [InlineData("월말정산", "후불승인완료")]
    [InlineData("현장지급", "현장수금예정")]
    public void 확인된후불현장조건과실제대기상태가함께있어야_배차대기로안내한다(string timing, string settlement)
    {
        var item = Pending() with { SettlementTiming = timing, SettlementStatus = settlement, DispatchStatus = "매칭중" };
        Assert.Equal("기사 배차 대기", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Active, ShipperRequestDetailPresentation.ResolveDispatchState(item));
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolvePaymentState(item));
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolveSettlementState(item));
    }

    [Fact]
    public void 현장지급등록만으로_수금예정이나수금완료를만들지않는다()
    {
        var item = Pending() with { SettlementTiming = "현장지급", SettlementStatus = "정산조건작성됨" };
        Assert.Equal("현장 지급 조건 확인 필요", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveDispatchState(item));
    }

    [Theory]
    [InlineData("선결제", "미시작")]
    [InlineData("현장지급", "미시작")]
    [InlineData("운송완료후정산", "미시작")]
    [InlineData("월말정산", "미시작")]
    [InlineData("선결제", "인수완료")]
    public void 서버의명시모의수행기록은_실제결제승인정산의대기로안내하지않는다(string timing, string transport)
    {
        var item = Pending() with { SettlementTiming = timing, SettlementStatus = "모의 수행·정산 제외", TransportStatus = transport };
        var next = ShipperRequestDetailPresentation.ResolveNextAction(item);
        Assert.Equal("모의 수행 기록·정산 제외", next.Title);
        Assert.Equal(ShipperRequestDetailScreenKind.Timeline, next.RelatedScreen);
        Assert.Contains("대상이 아닙니다", next.Reason);
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolvePaymentState(item));
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveSettlementState(item));
        Assert.NotEqual(ShipperRequestProgressState.Completed, next.State);
        Assert.Equal("모의 수행·정산 제외", ShipperRequestDetailPresentation.BuildTimeline(item).Single(x => x.Title == "정산").Status);
    }

    [Theory]
    [InlineData("취소", "의뢰 취소 상태")]
    [InlineData("검토보류", "진행 확인·처리 대기")]
    [InlineData("아직모르는배차값", "진행 상태 확인 필요")]
    [InlineData("배차대기", "접수 준비 · 결제 대기")]
    public void 배차이력은_대기이유와별개로_서버가보낸원래상태를보존한다(string dispatch, string expectedGuidance)
    {
        var item = Pending() with { DispatchStatus = dispatch };
        var step = ShipperRequestDetailPresentation.BuildTimeline(item).Single(x => x.Title == "배차");
        Assert.Equal(dispatch, step.Status);
        Assert.Contains(expectedGuidance, step.Description);
    }

    [Theory]
    [InlineData("대기")]
    [InlineData("배차대기")]
    [InlineData("매칭중")]
    public void 결제확보후_실제서버대기상태는_기사수락전으로구분한다(string dispatch)
    {
        var item = Pending() with { PaymentStatus = "결제완료", SettlementStatus = "결제완료", DispatchStatus = dispatch };
        Assert.Equal("기사 배차 대기", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolveAcceptedState(item));
    }

    [Fact]
    public void 결제기록만있고아직미시작이면_배차가시작된것처럼안내하지않는다()
    {
        var item = Pending() with { PaymentStatus = "결제완료", SettlementStatus = "결제완료" };
        Assert.Equal("배차 접수 확인 필요", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveDispatchState(item));
    }

    [Theory]
    [InlineData("", "결제완료")]
    [InlineData("0", "결제완료")]
    [InlineData("선결제", "")]
    [InlineData("선결제", "미확인상태")]
    public void 알수없는정산원천값을_결제기록만으로확인된배차조건으로승격하지않는다(string timing, string settlement)
    {
        var item = Pending() with { PaymentStatus = "결제완료", DispatchStatus = "매칭중", SettlementTiming = timing, SettlementStatus = settlement };
        Assert.Equal("진행 상태 확인 필요", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Pending, ShipperRequestDetailPresentation.ResolveDispatchState(item));
    }

    [Theory]
    [InlineData("상차완료", "운송 중")]
    [InlineData("운송중", "운송 중")]
    [InlineData("하차지도착", "하차 확인 진행")]
    public void 상차완료이후상태는_하차완료나도착완료로부분일치하지않는다(string transport, string title)
    {
        var item = Pending() with { TransportStatus = transport, DispatchStatus = "배차확정" };
        Assert.Equal(title, ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolvePickupState(item));
        Assert.Equal(ShipperRequestProgressState.Active, ShipperRequestDetailPresentation.ResolveDropoffState(item));
        Assert.StartsWith("미산정", ShipperRequestDetailPresentation.ResolveEtaStatus(item));
    }

    [Theory]
    [InlineData("배차확정")]
    [InlineData("이동중")]
    [InlineData("상차중")]
    [InlineData("상차지도착")]
    public void 상차지로가는확인된수행상태는_기사배정뒤상차전으로안내한다(string transport)
    {
        var item = Pending() with { TransportStatus = transport };
        Assert.Equal("기사 배정 완료", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Active, ShipperRequestDetailPresentation.ResolvePickupState(item));
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolveDropoffState(item));
    }

    [Fact]
    public void 전문운송원장이있으면_다른원장의오래된완료표시보다우선한다()
    {
        var item = Pending() with { TransportStatus = "상차지도착", DispatchStatus = "인수완료", RequestStatus = "하차완료" };
        var timeline = ShipperRequestDetailPresentation.BuildTimeline(item);
        Assert.Equal("기사 배정 완료", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal("상차지 도착", timeline.Single(x => x.Title == "상차").Status);
        Assert.NotEqual(ShipperRequestProgressState.Completed, timeline.Single(x => x.Title == "하차").State);
        Assert.Contains("상차지도착", timeline.Single(x => x.Title == "상차").Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("미래단계")]
    [InlineData("예측완료")]
    public void 비어있거나알수없는운송값을_완료로보충하지않는다(string transport)
    {
        var item = new ShipperRequestDetailSnapshot { RequestId = "unknown", TransportStatus = transport };
        Assert.Equal("진행 상태 확인 필요", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.All(ShipperRequestDetailPresentation.BuildTimeline(item), step => Assert.NotEqual(ShipperRequestProgressState.Completed, step.State));
        Assert.Equal("하차 상태 미확인", ShipperRequestDetailPresentation.BuildTimeline(item).Single(x => x.Title == "하차").Status);
        Assert.Null(item.ExpectedPaymentAmount);
        Assert.Null(item.FinalFare);
        Assert.Equal("-", ShipperRequestDetailPresentation.Money(item.FinalFare));
    }

    [Fact]
    public void 알수없는전문운송상태를_배차원장의완료로덮지않는다()
    {
        var item = Pending() with { TransportStatus = "미래단계", DispatchStatus = "인수완료" };
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolveDropoffState(item));
        Assert.Equal("진행 상태 확인 필요", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
    }

    [Theory]
    [InlineData("후불승인완료")]
    [InlineData("인수증등록완료")]
    [InlineData("현장수금완료")]
    [InlineData("입금확인완료")]
    [InlineData("결제완료")]
    public void 승인증빙수금결제의완료는_기사정산완료를뜻하지않는다(string settlement)
    {
        var item = Pending() with { SettlementStatus = settlement, TransportStatus = "인수완료" };
        Assert.Equal("운송 완료", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.NotEqual(ShipperRequestProgressState.Completed, ShipperRequestDetailPresentation.ResolveSettlementState(item));
        Assert.Contains("기사 입금은 별도 확인", ShipperRequestDetailPresentation.ResolveNextAction(item).Reason);
    }

    [Fact]
    public void 명시정산완료도_실제지급과분리해서표시한다()
    {
        var item = Pending() with { SettlementStatus = "정산완료", TransportStatus = "인수완료" };
        var next = ShipperRequestDetailPresentation.ResolveNextAction(item);
        Assert.Equal("정산 완료 기록", next.Title);
        Assert.Contains("실제 지급은 별도 확인", next.Reason);
    }

    [Fact]
    public void 검토보류는_완료안내보다우선하고_취소는환불완료와구분한다()
    {
        var item = Pending() with { OperationalReviewHeld = true, TransportStatus = "인수완료", SettlementStatus = "정산완료" };
        Assert.Equal("진행 확인·처리 대기", ShipperRequestDetailPresentation.ResolveNextAction(item).Title);
        Assert.Equal(ShipperRequestProgressState.Attention, ShipperRequestDetailPresentation.ResolveSettlementState(item));
        var cancelled = ShipperRequestDetailPresentation.ResolveNextAction(item with { RequestStatus = "취소" });
        Assert.Equal("의뢰 취소 상태", cancelled.Title);
        Assert.Contains("환불·정산은 별도 확인", cancelled.Reason);
    }

    [Fact]
    public async Task Web계약과모바일상세Mapper는_서버의보류사실과미확인금액을보존한다()
    {
        var web = ShipperRequestDetailSnapshot.FromContract(new 화주운송의뢰응답 { 의뢰Id = "web-held", 비정상운송검토보류중 = true });
        Assert.True(web.OperationalReviewHeld);
        Assert.Null(web.ExpectedPaymentAmount);
        using var fixture = new ShipperRecoveryFixture();
        await fixture.Auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        var load = fixture.Model.LoadAsync("mobile-held");
        (await fixture.Operations.NextAsync()).Completion.SetResult(new ShipperRequestItem { 의뢰Id = "mobile-held", 비정상운송검토보류중 = true });
        await load;
        var mobile = Assert.IsType<ShipperRequestDetailSnapshot>(fixture.Model.State.Request);
        Assert.True(mobile.OperationalReviewHeld);
        Assert.Null(mobile.ExpectedPaymentAmount);
        Assert.Equal(ShipperRequestDetailPresentation.ResolveNextAction(web).Title, ShipperRequestDetailPresentation.ResolveNextAction(mobile).Title);
    }

    [Theory]
    [InlineData("summary", "payment", "접수 준비 · 결제 대기", "/payment")]
    [InlineData("timeline", "approval", "후불 승인 확인 필요", "/payment")]
    [InlineData("summary", "dispatch", "기사 배차 대기", "/timeline")]
    [InlineData("summary", "held", "진행 확인·처리 대기", "/timeline")]
    [InlineData("timeline", "completed", "운송 완료", "/proofs")]
    [InlineData("summary", "simulation", "모의 수행 기록·정산 제외", "/timeline")]
    public async Task 실제공용화면은_현재이유담당과기존상세이동만제공한다(string screen, string scenario, string expected, string suffix)
    {
        var item = scenario switch
        {
            "approval" => Pending() with { SettlementTiming = "운송완료후정산", SettlementStatus = "후불승인대기" },
            "dispatch" => Pending() with { PaymentStatus = "결제완료", SettlementStatus = "결제완료", DispatchStatus = "매칭중" },
            "held" => Pending() with { OperationalReviewHeld = true },
            "completed" => Pending() with { TransportStatus = "인수완료" },
            "simulation" => Pending() with { SettlementStatus = "모의 수행·정산 제외" },
            _ => Pending()
        };
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ShipperRequestSummaryScreen.State)] = new ShipperRequestDetailPageState { Request = item },
            [nameof(ShipperRequestSummaryScreen.Context)] = new ShipperRequestDetailNavigationContext { ReturnPath = "/shipper/home" },
            [nameof(ShipperRequestSummaryScreen.HomeHref)] = "/shipper/home",
            [nameof(ShipperRequestSummaryScreen.NewRequestHref)] = "/shipper/request/cargo"
        };
        var html = screen == "summary" ? await RenderAsync<ShipperRequestSummaryScreen>(parameters) : await RenderAsync<ShipperRequestTimelineScreen>(parameters);
        var panel = Regex.Match(html, "<section[^>]*data-testid=\"shipper-request-next-action\"[^>]*>(.*?)</section>", RegexOptions.Singleline);
        Assert.True(panel.Success, html);
        Assert.Contains(expected, panel.Value);
        Assert.Contains("담당", panel.Value);
        Assert.Contains($"/shipper/request/quick-r112{suffix}", panel.Value);
        Assert.DoesNotContain("<button", panel.Value);
        Assert.DoesNotContain("승인 실행", panel.Value);
        Assert.DoesNotContain("결제 실행", panel.Value);
        Assert.DoesNotContain("0원", html);
    }

    private static ShipperRequestDetailSnapshot Pending() => new()
    {
        RequestId = "quick-r112", RequestStatus = "생성됨", PaymentStatus = "결제대기", DispatchStatus = "미시작",
        SettlementTiming = "선결제", SettlementStatus = "결제대기", CollectionOwner = "플랫폼"
    };

    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        services.AddSingleton<IJSRuntime, NoopJs>(); services.AddSingleton<NavigationManager, Navigation>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.RenderComponentAsync<RoleAppProviders>();
            var page = await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters));
            return WebUtility.HtmlDecode(page.ToHtmlString());
        });
    }
    private sealed class NoopJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/shipper/request/quick-r112");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
