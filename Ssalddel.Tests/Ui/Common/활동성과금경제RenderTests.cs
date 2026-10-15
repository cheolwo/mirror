using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Components.Settlement;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 활동성과금경제RenderTests
{
    [Theory]
    [InlineData(5, 500, 9500)]
    [InlineData(10, 1000, 9000)]
    public async Task 합의한플랫폼비율과서버계산몫을_연결원가및참여자풀과구분한다(int rate, int platformAmount, int participantPool)
    {
        var value = Value(rate);
        value.ActivityReward = Ready(platformAmount, participantPool);
        value.ActivityReward.PlatformRetentionRatePercent = rate;
        var html = await Render(value);

        AssertDefinition(html, "플랫폼 보유 비율", $"{rate}%");
        AssertDefinition(html, "연결된 입고·보관 비용", "600원");
        AssertDefinition(html, "비용 차감 후 잔여금", "10,000원");
        AssertDefinition(html, "플랫폼 몫", 활동정산표시.Money(platformAmount));
        AssertDefinition(html, "참여자 성과금 풀", 활동정산표시.Money(participantPool));
        AssertDefinition(html, "출고 기본 작업 비용", "3,000원");
        Assert.Contains("출고 기본 작업 비용과 별도로 반영하는 연결 비용입니다", html);
        Assert.Contains("실제 영업이익률이나 입금 확인을 뜻하지 않습니다", html);
        Assert.Contains("금액 확정·지급 신고·수령 확인", html);
    }

    [Fact]
    public async Task 원가연결과플랫폼몫을표시해도_본인성과금만보이고_타인과원증빙은노출하지않는다()
    {
        var value = Value();
        value.ActivityReward = Ready();
        value.ActivityReward.PlatformPayeeUserId = "private-platform-payee";
        value.ActivityReward.FundingPayerUserId = "private-funding-payer";
        value.ActivityReward.CalculationDigest = "private-calculation-digest";
        value.ActivityReward.CostAttributions = [new() { SourceEntryId = "private-cost-entry", SourceAgreementId = "private-cost-agreement",
            LotId = "private-cost-lot", OrderId = "private-cost-order", Kind = NeighborhoodSettlementCodes.Inbound,
            SourceEntryRevision = 8, SourceQuantity = 10, AllocatedQuantity = 2, AmountKrw = 600, EvidenceDigest = "private-cost-evidence-digest" }];
        value.ActivityReward.Allocations = [new() { ActorUserId = "worker", AmountKrw = 123 },
            new() { ActorUserId = "private-other-worker", AmountKrw = 8787 }];
        value.ActivityReward.ContributionShares = [new() { AgreedWeight = 1, AmountKrw = 123,
            Contribution = new() { ActorUserId = "worker", Kind = NeighborhoodSettlementCodes.Picking,
                Quantity = 1, UnitCode = NeighborhoodActivityRewardCodes.Unit,
                ContributionId = "private-contribution-id", OriginId = "private-history-id", EvidenceReferences = ["private-photo-reference"] } }];

        var html = await Render(value);
        AssertDefinition(html, "내 성과금 예정액", "123원");
        Assert.Contains("피킹 활동", html);
        Assert.Contains("연결된 원가 1 건", html);
        Assert.DoesNotContain("8,787원", html);
        Assert.DoesNotContain("private-", html);
    }

    [Theory]
    [InlineData("InventoryCostEvidenceRequired", "입고·보관 비용 연결 근거")]
    [InlineData("InventoryCostSelectionChanged", "현재 약정을 다시 확인")]
    [InlineData("InventoryCostAmbiguous", "중복되는 입고·보관 비용")]
    [InlineData("InventoryCostNotConfirmed", "금액 확정이나 이의 내역")]
    [InlineData("InventoryCostQuantityExceeded", "수량이 실제 수량을 넘지 않는지")]
    [InlineData("InventoryCostIncomplete", "입고·보관 비용 연결 근거")]
    [InlineData("InventoryStoragePeriodNotAttributable", "보관 기간의 근거")]
    [InlineData("InventoryCostBudgetUnderstated", "약정의 비용 기준을 다시 확인")]
    [InlineData("PlatformShareConfigurationRequired", "플랫폼 보유 비율과 분배 조건")]
    public async Task 원가나분배조건대기중이면_실패이유를보여주고_기본보수와미확정성과금을구분한다(string reason, string message)
    {
        var value = Value();
        value.ActivityReward = new() { ReasonCodes = [reason] };
        var html = await Render(value);

        Assert.Contains(message, html);
        Assert.Contains("성과금 금액 미확정 · 기본보수는 위 약정과 발생 내역에서 확인합니다", html);
        Assert.DoesNotContain(reason, html);
        Assert.DoesNotContain(Definitions(html), value => value.Label == "플랫폼 몫" && value.Value == "0원");
        Assert.DoesNotContain(Definitions(html), value => value.Label == "참여자 성과금 풀" && value.Value == "0원");
    }

    [Fact]
    public async Task 과거약정의미배분잔액을_플랫폼몫으로추정하지않는다()
    {
        var value = Value();
        value.Agreement.Terms.ActivityRewardTerms!.Economics = null;
        value.ActivityReward = Ready();
        var html = await Render(value);

        Assert.Contains("초과성과 환원 비율", html);
        AssertDefinition(html, "공동 성과금 풀", "9,000원");
        Assert.DoesNotContain("플랫폼 보유 비율", html);
        Assert.DoesNotContain("플랫폼 몫", html);
        Assert.DoesNotContain("연결된 입고·보관 비용", html);
    }

    [Fact]
    public async Task 새경제옵션의미확정비율과금액은_0으로바꾸지않는다()
    {
        var value = Value();
        value.Agreement.Terms.ActivityRewardTerms!.Economics!.PlatformRetentionRatePercent = null;
        value.ActivityReward = new() { StatusCode = NeighborhoodActivityRewardCodes.Ready };
        var html = await Render(value);

        AssertDefinition(html, "플랫폼 보유 비율", "미합의");
        AssertDefinition(html, "연결된 입고·보관 비용", "금액 미확정");
        AssertDefinition(html, "플랫폼 몫", "금액 미확정");
        AssertDefinition(html, "참여자 성과금 풀", "금액 미확정");
        Assert.DoesNotContain(Definitions(html), value => value.Value == "0원");
        Assert.DoesNotContain("연결된 원가 0 건", html);
    }

    [Fact]
    public async Task 원가상세가권한필터로숨겨져도_총액을유지하고_0건으로표시하지않는다()
    {
        var value = Value();
        value.ActivityReward = Ready();
        value.ActivityReward.CostAttributions = [];
        var html = await Render(value);

        AssertDefinition(html, "연결된 입고·보관 비용", "600원");
        Assert.DoesNotContain("연결된 원가 0 건", html);
        Assert.DoesNotContain("private-selected-cost-entry", html);
    }

    [Fact]
    public async Task 발생한성과금은_예정액이나실제입금으로바꾸지않는다()
    {
        var value = Value();
        value.ActivityReward = Ready();
        value.ActivityReward.AlreadyAccrued = true;
        value.ActivityReward.Allocations = [new() { ActorUserId = "worker", AmountKrw = 123 }];
        var html = await Render(value);

        Assert.Contains("발생 내역에 기록된 계산", html);
        Assert.Contains("내 성과금 발생액", html);
        Assert.DoesNotContain("내 성과금 예정액", html);
        Assert.Contains("실재원·확정 이익·실제 입금을 인증하지 않습니다", html);
    }

    [Fact]
    public async Task 이미발생한기존계산은_최신약정의경제옵션으로덮어표시하지않는다()
    {
        var value = Value();
        value.ActivityReward = new() { StatusCode = NeighborhoodActivityRewardCodes.Ready, AlreadyAccrued = true, PoolAmountKrw = 9000 };
        var html = await Render(value);

        Assert.Contains("발생 내역에 기록된 계산", html);
        AssertDefinition(html, "공동 성과금 풀", "9,000원");
        Assert.DoesNotContain("플랫폼 보유 비율", html);
        Assert.DoesNotContain("플랫폼 몫", html);
        Assert.DoesNotContain("연결된 입고·보관 비용", html);
        Assert.Contains("현재 약정 조건 · 발생 내역은 당시 계산 보존", html);
        Assert.Contains("현재 약정의 초과성과 환원 비율", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 계산에적용한플랫폼비율은_현재약정보다서버계산판본을우선한다(bool recorded)
    {
        var value = Value(5);
        value.Agreement.Terms.ActivityRewardTerms!.Policy.Weights = [new() { Kind = NeighborhoodSettlementCodes.Picking,
            UnitCode = NeighborhoodActivityRewardCodes.Unit, Weight = 3 }];
        value.ActivityReward = Ready();
        value.ActivityReward.AlreadyAccrued = recorded;
        var html = await Render(value);

        AssertDefinition(html, recorded ? "계산에 적용한 플랫폼 보유 비율" : "플랫폼 보유 비율", "10%");
        Assert.DoesNotContain(Definitions(html), value => value.Label.EndsWith("플랫폼 보유 비율", StringComparison.Ordinal) && value.Value == "5%");
        AssertDefinition(html, "플랫폼 몫", "1,000원");
        if (recorded)
        {
            AssertDefinition(html, "계산에 적용한 초과성과 환원 비율", "90%");
            Assert.DoesNotContain(Definitions(html), value => value.Label.EndsWith("초과성과 환원 비율", StringComparison.Ordinal) && value.Value == "95%");
            Assert.Contains("현재 약정 조건 · 발생 내역은 당시 계산 보존", html);
            Assert.Contains("아래 수입·비용·가중치는 현재 약정 조건", html);
            Assert.Contains("이미 발생한 금액은 당시 계산을 보존", html);
            Assert.Contains("피킹 기본보수 · 개당 가중치 3", html);
        }
    }

    [Fact]
    public async Task 현재성과금약정의당사자가아니어도_이전본인발생계산을보존하고_없는금융입력은추정하지않는다()
    {
        var value = Value();
        value.Agreement.Terms.ActivityRewardTerms = null;
        value.ActivityReward = Ready();
        value.ActivityReward.AlreadyAccrued = true;
        value.ActivityReward.PlatformPayeeUserId = "private-platform-payee";
        value.ActivityReward.Allocations = [new() { ActorUserId = "worker", AmountKrw = 123 },
            new() { ActorUserId = "private-other-worker", AmountKrw = 8787 }];
        value.ActivityReward.ContributionShares = [new() { AgreedWeight = 2, AmountKrw = 123,
            Contribution = new() { ActorUserId = "worker", Kind = NeighborhoodSettlementCodes.Picking,
                Quantity = 1, UnitCode = NeighborhoodActivityRewardCodes.Unit, ContributionId = "private-mine-contribution",
                EvidenceReferences = ["private-mine-photo"] } }, new() { AgreedWeight = 4, AmountKrw = 8787,
            Contribution = new() { ActorUserId = "private-other-worker", Kind = NeighborhoodSettlementCodes.Inbound,
                Quantity = 99, UnitCode = NeighborhoodActivityRewardCodes.Unit } }];

        var html = await Render(value);
        Assert.Contains("이전 약정의 발생 계산입니다. 현재 약정과 구분해 보관합니다", html);
        AssertDefinition(html, "내 성과금 발생액", "123원");
        AssertDefinition(html, "당시 플랫폼 보유 비율", "10%");
        AssertDefinition(html, "당시 플랫폼 몫", "1,000원");
        Assert.Contains("피킹 활동", html); Assert.Contains("당시 가중치 2", html);
        Assert.DoesNotContain("등록된 공동 초과성과금 약정이 없습니다", html);
        Assert.DoesNotContain("합의 수입", html); Assert.DoesNotContain("출고 기본 작업 비용", html);
        Assert.DoesNotContain("현재 약정 조건", html); Assert.DoesNotContain("참여자 성과금 풀", html);
        Assert.DoesNotContain("9,000원", html); Assert.DoesNotContain("8,787원", html);
        Assert.DoesNotContain("완료량 99", html); Assert.DoesNotContain("private-", html);
        Assert.Contains("금액 확정·지급 신고·수령 확인", html);
        Assert.Contains("실제 입금을 인증하지 않습니다", html);
    }

    [Fact]
    public void 플랫폼발생항목은_일반활동보수로표시하지않는다()
        => Assert.Equal("플랫폼 몫", 활동정산표시.Kind("PlatformShare"));

    private static void AssertDefinition(string html, string label, string expected)
    {
        var value = Assert.Single(Definitions(html).Where(value => value.Label == label));
        Assert.Equal(expected, value.Value);
    }

    private static IReadOnlyList<(string Label, string Value)> Definitions(string html)
        => Regex.Matches(html, @"<dt\b[^>]*>(?<label>.*?)</dt>\s*<dd\b[^>]*>(?<value>.*?)</dd>", RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .Cast<Match>().Select(match => (
                Label: Regex.Replace(match.Groups["label"].Value, "<[^>]*>", "").Trim(),
                Value: Regex.Replace(match.Groups["value"].Value, "<[^>]*>", "").Trim())).ToArray();

    private static NeighborhoodSettlementSummaryDto Value(decimal rate = 10) => new()
    {
        Agreement = new()
        {
            Terms = new()
            {
                ActivityRewardTerms = new()
                {
                    PayerUserId = "private-funding-payer",
                    ParticipantUserIds = ["worker", "private-other-worker"],
                    Policy = new() { ReturnRatePercent = 100 - rate },
                    FinancialBasis = new() { RevenueAmountKrw = 15600, BasicWorkCostAmountKrw = 3000,
                        OtherCostAmountKrw = 1000, ReserveAmountKrw = 1000, BasisReference = "private-financial-reference" },
                    Economics = new() { PlatformPayeeUserId = "private-platform-payee", PlatformRetentionRatePercent = rate,
                        CostAllocations = [new() { SourceEntryId = "private-selected-cost-entry", ExpectedEntryRevision = 8 }] }
                }
            }
        }
    };

    private static NeighborhoodActivityRewardPreviewDto Ready(decimal platformAmount = 1000, decimal participantPool = 9000) => new()
    {
        StatusCode = NeighborhoodActivityRewardCodes.Ready,
        CostAttributionAmountKrw = 600, SurplusAmountKrw = 10000,
        PlatformRetentionRatePercent = 10, PlatformRetentionAmountKrw = platformAmount,
        PoolAmountKrw = participantPool
    };

    private static async Task<string> Render(NeighborhoodSettlementSummaryDto value)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<활동성과금Panel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(활동성과금Panel.Value)] = value,
                [nameof(활동성과금Panel.CurrentUserId)] = "worker"
            }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
}
