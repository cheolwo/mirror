using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Ssalddel.Contracts.Driver.Profile;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Qualification;
using Ssalddel.Ui.Common.Areas.BackOffice.Components.Operations;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class TransportQualificationRenderTests
{
    [Fact]
    public async Task 본인화면은_비공개참조와검토요청만제공하고_기관검증으로표현하지않는다()
    {
        using var model = new 운송자격ViewModel(new Client()); await model.LoadAsync();
        var html = await RenderAsync<TransportQualificationPanel>(new() { ["Model"] = model });
        Assert.Contains("검토 요청", html); Assert.Contains("공식기관 확인 미완료", html);
        Assert.Contains("비공개 증빙 참조코드", html); Assert.DoesNotContain("검토 유효로 기록", html);
        Assert.Contains("유상 배송 보장", html); Assert.DoesNotContain("화물운송 종사자격", html);
    }
    [Fact]
    public async Task 관리자화면은_자체검토와기관확인을구분하며_보험기간을확인한다()
    {
        using var model = new 운송자격ViewModel(new Client(), true); await model.LoadAsync();
        var html = await RenderAsync<TransportQualificationPanel>(new() { ["Model"] = model });
        Assert.Contains("검토 유효로 기록", html); Assert.Contains("보완 요청", html); Assert.Contains("이용 정지", html);
        Assert.Contains("자료와 적용 범위를 확인했습니다", html); Assert.Contains("보험은 필수", html);
        Assert.Contains("공식기관 조회 완료나 플랫폼 등록 승인을 대신하지 않습니다", html);
    }
    [Fact]
    public async Task 플랫폼현황은_미확인과준비상태를보여주고_운영활성화버튼을제공하지않는다()
    {
        using var model = new 플랫폼등록준비ViewModel(_ => Task.FromResult(new FreightPlatformReadinessDto
        { Requirements = [new() { Code = "office", Title = "사무실", NextAction = "근거 자료를 준비해 주세요." }] }));
        await model.LoadAsync();
        var html = await RenderAsync<FreightPlatformReadinessPanel>(new() { ["Model"] = model });
        Assert.Contains("추가 준비 필요", html); Assert.Contains("사무실", html); Assert.Contains("미확인", html);
        Assert.DoesNotContain("영업 활성화</button>", html); Assert.Contains("준비 현황 다시 확인", html);
    }

    [Fact]
    public async Task 정액정보계획은_현재무료설정과_후보요금_기관미확인을_구분한다()
    {
        using var model = new 플랫폼등록준비ViewModel(_ => Task.FromResult(new FreightPlatformReadinessDto
        {
            FreeConnectionConfigured = true,
            PlannedInformationSubscription = new()
            {
                ModelCode = "MonthlyInformationSubscriptionDraft",
                CandidateMonthlyAmountsKrw = [5000m, 10000m],
                RealtimeRecommendationsPlanned = true, PartyFinalDecisionRequired = true,
                Notice = "검토 후보이며 확정·청구된 금액이 아닙니다."
            }
        }));
        await model.LoadAsync();
        var html = await RenderAsync<FreightPlatformReadinessPanel>(new() { ["Model"] = model });
        Assert.Contains("무료·0원 설정", html);
        Assert.Contains("향후 월정액 정보추천", html);
        Assert.Contains("5,000원 / 10,000원", html);
        Assert.Contains("요금 미확정", html);
        Assert.Contains("건별 중개수수료 계획", html);
        Assert.Contains("기사 수락·거절", html);
        Assert.Contains("계약 당사자 최종 결정", html);
        Assert.Contains("법적 분류 확인", html);
        Assert.Contains("미확인", html);
        Assert.Contains("월정액 청구 미구현", html);
        Assert.DoesNotContain("결제하기", html);
        Assert.DoesNotContain("입금하기", html);
    }

    [Fact]
    public async Task 이전서버의_요금계획없는응답에_정액요금을_임의로_붙이지_않는다()
    {
        using var model = new 플랫폼등록준비ViewModel(_ => Task.FromResult(new FreightPlatformReadinessDto()));
        await model.LoadAsync();
        var html = await RenderAsync<FreightPlatformReadinessPanel>(new() { ["Model"] = model });
        Assert.DoesNotContain("향후 월정액 정보추천", html);
        Assert.DoesNotContain("5,000원", html);
        Assert.DoesNotContain("10,000원", html);
    }

    [Fact]
    public async Task 서버의_향후계획상태가_바뀌면_미확정이나_미구현으로_고정표시하지_않는다()
    {
        using var model = new 플랫폼등록준비ViewModel(_ => Task.FromResult(new FreightPlatformReadinessDto
        {
            PlannedInformationSubscription = new() { ConfirmedMonthlyAmountKrw = 7000m, BillingImplemented = true }
        }));
        await model.LoadAsync();
        var html = await RenderAsync<FreightPlatformReadinessPanel>(new() { ["Model"] = model });
        Assert.Contains("계획 요금 확정", html);
        Assert.Contains("7,000원", html);
        Assert.DoesNotContain("요금 미확정", html);
        Assert.DoesNotContain("월정액 청구 미구현", html);
        Assert.Contains("실제 활성화는 별도 확인", html);
        Assert.DoesNotContain("결제하기", html);
    }
    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parameters) where T : IComponent
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddMudServices();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode((await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parameters))).ToHtmlString()));
    }
    private sealed class Client : I운송자격Client
    {
        public Task<운송자격상세Dto> 조회Async(CancellationToken ct) => Task.FromResult(new 운송자격상세Dto
        { DriverId = "driver", VehicleKind = 운송자격Codes.Motorcycle,
            RequiredEvidenceCodes = 운송자격Codes.RequiredFor(운송자격Codes.Motorcycle) });
        public Task<운송자격상세Dto> 제출Async(운송자격제출요청 request, CancellationToken ct) => throw new NotSupportedException();
        public Task<운송자격상세Dto> 검토Async(운송자격검토요청 request, CancellationToken ct) => throw new NotSupportedException();
    }
}
