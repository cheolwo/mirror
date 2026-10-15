using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Components.Settlement;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 활동정산RenderTests
{
    [Fact]
    public async Task 미확정기본보수와_지급신고수령확인을_구별하고_원증빙을노출하지않는다()
    {
        var fixture = new 활동정산TestFixture(); var html = await Render(fixture, "agreement-1");
        Assert.Contains("금액 미확정", html); Assert.Contains("입고 기본보수", html);
        Assert.Contains("지급 신고액", html); Assert.Contains("누적 수령 확인액", html);
        Assert.Contains("은행 입금 검증", html); Assert.DoesNotContain("private-bank-original", html);
        Assert.DoesNotContain("private-evidence-original", html); Assert.DoesNotContain(">owner<", html);
    }

    [Fact]
    public async Task 근거대기는_예정금액0원으로대체하지않는다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Agreement.Terms.ActivityRewardTerms = new();
        fixture.Value.ActivityReward = new() { ReasonCodes = ["ActivityRewardCustomerReceiptRequired"] };
        var html = await Render(fixture, "agreement-1");
        Assert.Contains("근거 대기", html); Assert.Contains("성과금 금액 미확정", html);
        Assert.Contains("최종 배송과 주문자의 수령 확인", html);
        Assert.DoesNotContain("내 성과금 예정액</dt><dd>0원", html);
        Assert.Contains("확정 이익·실제 입금을 인증하지 않습니다", html);
    }

    [Fact]
    public async Task 성과금은_본인배분과활동근거만표시한다()
    {
        var fixture = new 활동정산TestFixture(); fixture.Value.Agreement.Terms.ActivityRewardTerms = new();
        fixture.Value.ActivityReward = new() { StatusCode = NeighborhoodActivityRewardCodes.Ready, PoolAmountKrw = 300,
            Allocations = [new() { ActorUserId = "worker", AmountKrw = 100 }, new() { ActorUserId = "another-private-user", AmountKrw = 200 }],
            ContributionShares = [new() { Contribution = new() { ActorUserId = "worker", Kind = NeighborhoodSettlementCodes.Inbound, Quantity = 1,
                UnitCode = NeighborhoodActivityRewardCodes.Unit, EvidenceReferences = ["private-actor-evidence"] }, AmountKrw = 100, AgreedWeight = 1 },
                new() { Contribution = new() { ActorUserId = "another-private-user", Kind = NeighborhoodSettlementCodes.Picking, Quantity = 99 }, AmountKrw = 200 }] };
        var html = await Render(fixture, "agreement-1");
        Assert.Contains("내 성과금 예정액", html); Assert.Contains("100원", html);
        Assert.Contains("내 활동별 성과금 계산", html); Assert.Contains("입고 활동", html);
        Assert.DoesNotContain("내 성과금 배분 항목은 확인되지 않았습니다", html);
        Assert.DoesNotContain("another-private-user", html); Assert.DoesNotContain("private-actor-evidence", html);
        Assert.DoesNotContain("완료량 99", html); Assert.DoesNotContain(">200원<", html);
    }

    [Fact]
    public async Task 익명화면은_활동내역과명령을표시하지않는다()
    {
        var fixture = new 활동정산TestFixture(null); var html = await Render(fixture, "agreement-1");
        Assert.Contains("로그인이 필요해요", html); Assert.DoesNotContain("발생·확정 내역", html);
        Assert.DoesNotContain("이 항목 확인", html); Assert.Equal(0, fixture.ReadCalls);
    }

    [Fact]
    public async Task 목록은_선택한약정상세와_복귀문맥으로연결한다()
    {
        var html = await Render(new 활동정산TestFixture());
        Assert.Contains("activity-settlements/agreement-1", html); Assert.Contains("returnUrl=", html);
        Assert.DoesNotContain("이 항목 확인", html); Assert.DoesNotContain("private-bank-original", html);
    }

    [Fact]
    public async Task 입고보관과출고배송목록은_각각의섹션에서만표시하고_기존약정을추정분류하지않는다()
    {
        var inbound = ListItem("inbound-agreement", NeighborhoodSettlementWorkflowCodes.InboundStorage);
        var outbound = ListItem("outbound-agreement", NeighborhoodSettlementWorkflowCodes.OutboundDelivery);
        var legacy = ListItem("legacy-agreement", null);
        legacy.Agreement.Terms.Lines = [new() { Kind = NeighborhoodSettlementCodes.Inbound }, new() { Kind = NeighborhoodSettlementCodes.Delivery }];
        var fixture = new 활동정산TestFixture { ListValues = [outbound, legacy, inbound] };
        var html = await Render(fixture);
        var inboundSection = Section(html, "입고·보관 정산 목록");
        var outboundSection = Section(html, "출고·배송 정산 목록");
        var legacySection = Section(html, "기타·기존 약정 정산 목록");
        Assert.Contains("inbound-agreement", inboundSection); Assert.DoesNotContain("outbound-agreement", inboundSection); Assert.DoesNotContain("legacy-agreement", inboundSection);
        Assert.Contains("outbound-agreement", outboundSection); Assert.DoesNotContain("inbound-agreement", outboundSection);
        Assert.Contains("legacy-agreement", legacySection); Assert.Contains("기존 약정 · 업무 구분 확인 필요", legacySection);
        Assert.DoesNotContain("이 항목 확인", html);
    }

    [Fact]
    public async Task 입고완료지급조건은_배송수령조건과구분하고_재고원시식별자를표시하지않는다()
    {
        var fixture = new 활동정산TestFixture();
        fixture.Value.Agreement.Terms.WorkflowKind = NeighborhoodSettlementWorkflowCodes.InboundStorage;
        fixture.Value.Agreement.Terms.PaymentReleaseCondition = NeighborhoodSettlementWorkflowCodes.WorkCompleted;
        fixture.Value.Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.InboundStorage, StateCode = "Received",
            CompletionConfirmed = true, PaymentReleaseReady = true, InventoryLotIds = ["private-lot-id"] };
        var html = await Render(fixture, "agreement-1");
        var section = Section(html, "업무 진행과 지급 기준");
        Assert.Contains("입고·보관", section); Assert.Contains("입고 확인 완료", section); Assert.Contains("작업 완료 확인 후", section);
        Assert.Contains("업무 완료 조건 충족", section); Assert.Contains("입고·출고 연결 재고 1 건", section);
        Assert.DoesNotContain("고객 수령 확인 후", section); Assert.DoesNotContain("private-lot-id", html);
        Assert.Contains("업무 완료 조건 충족은 금액 확정이나 실제 지급 완료를 뜻하지 않습니다", section);
    }

    [Fact]
    public async Task 배송수령대기중에도_발생보수와기존지급기록을유지하고_다음행동은서버허용으로표시한다()
    {
        var fixture = new 활동정산TestFixture();
        fixture.Value.Agreement.Terms.WorkflowKind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery;
        fixture.Value.Agreement.Terms.PaymentReleaseCondition = NeighborhoodSettlementWorkflowCodes.CustomerReceived;
        fixture.Value.Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery, StateCode = "DeliveredAwaitingReceipt",
            CompletionConfirmed = false, PaymentReleaseReady = false, WaitingReasonCode = "DeliveryReceiptRequired" };
        var html = await Render(fixture, "agreement-1");
        var section = Section(html, "업무 진행과 지급 기준");
        Assert.Contains("출고·배송", section); Assert.Contains("고객 수령 확인 후", section); Assert.Contains("전달 완료 · 고객 수령 확인 대기", section);
        Assert.Contains("최종 배송과 고객의 수령 확인을", section);
        Assert.Contains("작업 보수의 발생 내역은 유지됩니다", section);
        Assert.Contains("발생 · 당사자 확인 대기", html); Assert.Contains("1,000원", html);
        Assert.Contains("지급 신고액", html); Assert.Contains("내 수령액 확인", html); Assert.Contains("이 항목 확인", html);
    }

    [Fact]
    public async Task 기존혼합약정은_과거조건유지를안내하고_미설정값을완료로표시하지않는다()
    {
        var fixture = new 활동정산TestFixture();
        fixture.Value.Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.LegacyMixed, StateCode = "private-state-code",
            WaitingReasonCode = "LegacyWorkflowAgreement" };
        var html = await Render(fixture, "agreement-1");
        Assert.Contains("기존 혼합 약정 · 업무 구분 확인 필요", html);
        Assert.Contains("기존 약정의 지급 조건 확인 필요", html); Assert.Contains("과거 지급 조건을 바꾸지 않으며", html);
        Assert.Contains("현재 단계 확인 필요", html); Assert.DoesNotContain("private-state-code", html);
        Assert.Contains("1,000원", html); Assert.Contains("내 수령액 확인", html);
    }

    [Fact]
    public async Task 항목별지급대기는_당시약정조건에따른대상항목에만표시한다()
    {
        var fixture = new 활동정산TestFixture();
        fixture.Value.Agreement.TermsVersion = 3;
        fixture.Value.Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery,
            PaymentReleaseReady = false, WaitingReasonCode = "DeliveryReceiptRequired" };
        fixture.Value.Entries[0].TermsVersion = 2;
        fixture.Value.Entries[0].PaymentReleaseReady = false;
        fixture.Value.Entries[0].PaymentWaitingReasonCode = "DeliveryReceiptRequired";
        fixture.Value.Entries = [fixture.Value.Entries[0], new() { EntryId = "entry-2", AgreementId = "agreement-1", TermsVersion = 1,
            Kind = NeighborhoodSettlementCodes.Inbound, AmountKrw = 2000, StatusCode = NeighborhoodSettlementCodes.Confirmed,
            PayerUserId = "owner", PayeeUserId = "worker", PaymentReleaseReady = true }];
        var html = await Render(fixture, "agreement-1");
        var entries = Section(html, "발생 정산 항목");
        Assert.Equal(1, entries.Split("고객 수령 확인 후 정산", StringSplitOptions.None).Length - 1);
        Assert.Contains("1,000원", entries); Assert.Contains("2,000원", entries);
        Assert.Contains("발생 항목은 각 항목에 적용된 약정의 지급 조건을 따릅니다", html);
    }

    [Fact]
    public async Task 기존약정의진단용대기상태는_지급조건을충족한과거항목의지급보류로표시하지않는다()
    {
        var fixture = new 활동정산TestFixture();
        fixture.Value.Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.LegacyMixed,
            PaymentReleaseReady = false, WaitingReasonCode = "LegacyWorkflowAgreement" };
        fixture.Value.Entries[0].TermsVersion = 1;
        fixture.Value.Entries[0].PaymentReleaseReady = true;
        var html = await Render(fixture, "agreement-1");
        var workflow = Section(html, "업무 진행과 지급 기준");
        var entries = Section(html, "발생 정산 항목");
        Assert.Contains("기존 약정 조건 유지", workflow); Assert.DoesNotContain("조건 확인 대기", workflow);
        Assert.DoesNotContain("고객 수령 확인 후 정산", entries); Assert.DoesNotContain("완료 조건 확인 후 정산", entries);
        Assert.Contains("1,000원", entries); Assert.Contains("내 수령액 확인", html);
    }

    private static NeighborhoodSettlementSummaryDto ListItem(string id, string? workflowKind) => new()
    {
        Agreement = new() { AgreementId = id, StatusCode = NeighborhoodSettlementCodes.Active,
            Terms = new() { ActivityKind = NeighborhoodSettlementCodes.StoredGoods, WorkflowKind = workflowKind } },
        Workflow = workflowKind is null ? null : new() { Kind = workflowKind }
    };
    private static string Section(string html, string label)
        => html.Split($"aria-label=\"{label}\"", StringSplitOptions.None)[1].Split("</section>", StringSplitOptions.None)[0];

    private static async Task<string> Render(활동정산TestFixture fixture, string? id = null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        services.AddSingleton<NavigationManager, Navigation>(); services.AddSingleton<ISsalddel현재사용자Context>(fixture);
        services.AddSingleton<I활동정산Client>(fixture); services.AddSingleton<활동정산Session>(); services.AddTransient<활동정산PageViewModel>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<활동정산Screen>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(활동정산Screen.AgreementId)] = id, [nameof(활동정산Screen.ReturnUrl)] = "/community/exchange/work" }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
        Assert.Empty(fixture.Commands); return html;
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("http://localhost/", "http://localhost/activity-settlements");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
}
