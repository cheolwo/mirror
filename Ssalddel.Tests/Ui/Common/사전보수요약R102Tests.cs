using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Components.Settlement;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 사전보수요약R102Tests
{
    [Fact]
    public void 본인수령기본보수만_선택하고_지급항목과타인보수와내부작업을제외한다()
    {
        var own = Line("own", NeighborhoodSettlementCodes.Picking, "payer", "worker", 100);
        var packing = Line("packing", NeighborhoodSettlementCodes.Packing, "payer", "worker", 200);
        var agreement = new NeighborhoodSettlementAgreementDto { Terms = new() { Lines =
            [own, packing, Line("payable", NeighborhoodSettlementCodes.Picking, "worker", "other", 300),
                Line("other", NeighborhoodSettlementCodes.Picking, "payer", "other", 400),
                Line("internal", NeighborhoodSettlementCodes.Picking, "worker", "worker", 500),
                Line("sale", NeighborhoodSettlementCodes.ProductSale, "payer", "worker", 600)] } };

        Assert.Equal(new[] { own, packing }, 사전보수요약표시.본인수령항목(agreement, "worker"));
        Assert.Empty(사전보수요약표시.본인수령항목(agreement, null));
        Assert.Empty(사전보수요약표시.본인수령항목(agreement, "Worker"));
        Assert.Equal("포장 기본보수", 사전보수요약표시.항목명(packing));
    }

    [Fact]
    public void 배송비포함항목은_별도보수와구분하고_원본약정을변경하지않는다()
    {
        var line = Line("delivery", NeighborhoodSettlementCodes.Delivery, "payer", "worker", 4720);
        line.IncludedInDelivery = true;
        var agreement = new NeighborhoodSettlementAgreementDto { Terms = new() { Lines = [line] } };

        Assert.Empty(사전보수요약표시.본인수령항목(agreement, "worker"));
        Assert.Same(line, Assert.Single(사전보수요약표시.본인수령항목(agreement, "worker", includedInDelivery: true)));
        Assert.True(line.IncludedInDelivery);
        Assert.Equal(4720m, line.AmountKrw);
    }

    [Fact]
    public void 미확정과명시적무료와알수없는산정방식을구별하고_수량을반올림하지않는다()
    {
        var line = new NeighborhoodSettlementTermLine();
        Assert.Equal("금액 미확정", 사전보수요약표시.금액(line));
        line.Billing = NeighborhoodSettlementCodes.PerUnit;
        Assert.Equal("금액 미확정 × 수량 미확정", 사전보수요약표시.금액(line));
        line.UnitPriceKrw = 100; line.Quantity = 1.125m;
        Assert.Equal("100원 × 1.125", 사전보수요약표시.금액(line));
        line.Billing = NeighborhoodSettlementCodes.Free;
        Assert.Equal("무료 조건 확인 필요", 사전보수요약표시.금액(line));
        line.AmountKrw = 0;
        Assert.Equal("0원 · 무료 합의", 사전보수요약표시.금액(line));
        line.Billing = "FutureBilling"; line.AmountKrw = 999;
        Assert.Equal("산정 방식 확인 필요", 사전보수요약표시.금액(line));
    }

    [Fact]
    public async Task 요약은_본인약정조건과배송비참고만표시하며_타인금액과식별자와명령을노출하지않는다()
    {
        var unknown = Line("private-line-id", NeighborhoodSettlementCodes.Packing, "private-payer", "worker", null);
        var delivery = Line("private-delivery-id", NeighborhoodSettlementCodes.Delivery, "private-payer", "worker", 4720);
        delivery.IncludedInDelivery = true;
        var value = new NeighborhoodSettlementSummaryDto { Agreement = new() { AllowedActions = ["consent"], Terms = new()
            { ActivityKind = NeighborhoodSettlementCodes.StoredGoods, PaymentReleaseCondition = NeighborhoodSettlementWorkflowCodes.CustomerReceived,
                Lines = [unknown, delivery, Line("private-other-line", NeighborhoodSettlementCodes.Picking, "private-payer", "other-private-user", 987654)] } },
            Workflow = new() { Kind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery } };

        var html = await Render(value, "worker");
        Assert.Contains("내가 받을 기본보수", html);
        Assert.Contains("포장 기본보수", html);
        Assert.Contains("금액 미확정", html);
        Assert.Contains("유효기한 미확정", html);
        Assert.Contains("고객 수령 확인 후", html);
        Assert.Contains("배송비에 포함된 보수 1 건", html);
        Assert.Contains("별도 지급액으로 더하지 않습니다", html);
        Assert.DoesNotContain("987,654", html);
        Assert.DoesNotContain("private-", html);
        Assert.DoesNotContain("other-private-user", html);
        Assert.DoesNotContain("<button", html);
        Assert.DoesNotContain("현재 약정에 동의", html);
    }

    private static NeighborhoodSettlementTermLine Line(string key, string kind, string payer, string payee, decimal? amount)
        => new() { LineKey = key, Kind = kind, PayerUserId = payer, PayeeUserId = payee, AmountKrw = amount };

    private static async Task<string> Render(NeighborhoodSettlementSummaryDto value, string? owner)
    {
        var services = new ServiceCollection(); services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<사전보수요약Panel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(사전보수요약Panel.Value)] = value, [nameof(사전보수요약Panel.CurrentUserId)] = owner }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
}
