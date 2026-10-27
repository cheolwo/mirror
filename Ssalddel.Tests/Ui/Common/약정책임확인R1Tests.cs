using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Ui.Common.Areas.App.Components.Settlement;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 약정책임확인R1Tests
{
    [Fact]
    public void 현재판본의필요당사자만세고_최신철회를반영한다()
    {
        var agreement = Agreement();
        agreement.RequiredConsentUserIds = ["worker", "seller", "seller"];
        agreement.Consents =
        [
            Consent("worker", 2, true, 1), Consent("seller", 1, true, 9),
            Consent("seller", 2, true, 2), Consent("seller", 2, false, 3),
            Consent("not-required", 2, true, 9), Consent("seller", 3, true, 10)
        ];
        var result = 약정책임확인표시.현재판본동의(agreement);
        Assert.Equal(2, result.필요인원);
        Assert.Equal(1, result.동의인원);
        Assert.False(result.당사자확인필요);
        Assert.False(result.동일판본기록충족);
    }

    [Fact]
    public void 같은시각의상반된동의는_임의선택하거나입력순서로완료하지않는다()
    {
        var agreement = Agreement();
        agreement.RequiredConsentUserIds = ["worker"];
        agreement.Consents = [Consent("worker", 2, true, 2), Consent("worker", 2, false, 2)];
        Assert.False(약정책임확인표시.현재판본동의(agreement).동일판본기록충족);
        agreement.Consents = agreement.Consents.Reverse().ToArray();
        Assert.Equal(0, 약정책임확인표시.현재판본동의(agreement).동의인원);
    }

    [Fact]
    public void 표시된항목의지급담당이나배송부담자가필요당사자에서누락되면_완료하지않는다()
    {
        var agreement = Agreement();
        agreement.RequiredConsentUserIds = ["worker"];
        agreement.Consents = [Consent("worker", 2, true, 1)];
        agreement.Terms.Lines = [Line("missing-payer", "worker", 100)];
        Assert.True(약정책임확인표시.현재판본동의(agreement).당사자확인필요);
        Assert.False(약정책임확인표시.현재판본동의(agreement).동일판본기록충족);
        agreement.RequiredConsentUserIds = ["worker", "missing-payer"];
        agreement.Consents = [.. agreement.Consents, Consent("missing-payer", 2, true, 1)];
        agreement.Terms.Lines[0].DeliveryCostShares = [new() { PayerUserId = "missing-delivery-payer", AmountKrw = 100 }];
        Assert.False(약정책임확인표시.현재판본동의(agreement).동일판본기록충족);
    }

    [Fact]
    public void 필요당사자가없거나일부누락되거나판본이미상이면_완료로표시하지않는다()
    {
        var agreement = Agreement();
        agreement.Consents = [Consent("worker", 2, true, 1)];
        foreach (var required in new IReadOnlyList<string>[] { [], [" "], ["worker", " "] })
        {
            agreement.RequiredConsentUserIds = required;
            var result = 약정책임확인표시.현재판본동의(agreement);
            Assert.True(result.당사자확인필요);
            Assert.False(result.동일판본기록충족);
        }
        agreement.RequiredConsentUserIds = ["worker"];
        agreement.TermsVersion = 0;
        var unknown = 약정책임확인표시.현재판본동의(agreement);
        Assert.True(unknown.당사자확인필요);
        Assert.False(unknown.동일판본기록충족);
        Assert.Equal(0, unknown.동의인원);
    }

    [Fact]
    public void 필요한현재동의기록을모두확인해도_법적책임이나지급권한을생성하지않는다()
    {
        var agreement = Agreement();
        agreement.RequiredConsentUserIds = ["worker", "seller"];
        agreement.Consents = [Consent("worker", 2, true, 1), Consent("seller", 2, true, 1)];
        var before = JsonSerializer.Serialize(agreement);
        Assert.True(약정책임확인표시.현재판본동의(agreement).동일판본기록충족);
        Assert.Equal(before, JsonSerializer.Serialize(agreement));
        Assert.Empty(agreement.AllowedActions);
        Assert.Equal(NeighborhoodSettlementCodes.AwaitingConsent, agreement.StatusCode);
    }

    [Fact]
    public void 본인연결은대소문자를구분하고_내부항목과미상지급담당을분리한다()
    {
        var own = Line("payer-private", "worker", 100);
        var payable = Line("worker", "recipient-private", 200);
        var internalLine = Line("worker", "worker", 300);
        var missing = Line("", "worker", null);
        var agreement = Agreement();
        agreement.Terms.Lines = [own, payable, internalLine, missing, Line("payer-private", "other-private", 97775)];
        Assert.Equal(new[] { own, payable, internalLine, missing }, 약정책임확인표시.본인항목(agreement, "worker"));
        Assert.Empty(약정책임확인표시.본인항목(agreement, "Worker"));
        Assert.Empty(약정책임확인표시.본인항목(agreement, null));
        Assert.Equal("내가 받을 약정", 약정책임확인표시.지급관계(own, "worker"));
        Assert.Equal("내가 지급할 약정", 약정책임확인표시.지급관계(payable, "worker"));
        Assert.Contains("내부", 약정책임확인표시.지급관계(internalLine, "worker"));
        Assert.Equal("지급·수령 담당 확인 필요", 약정책임확인표시.지급관계(missing, "worker"));
    }

    [Fact]
    public void 단가수량미정과명시무료와알수없는수금방식을_무료나확정으로바꾸지않는다()
    {
        var line = new NeighborhoodSettlementTermLine { Billing = NeighborhoodSettlementCodes.PerUnit };
        Assert.Equal("단가·수량 미확정", 약정책임확인표시.미확정조건(line));
        line.Quantity = 2;
        Assert.Equal("단가 미확정", 약정책임확인표시.미확정조건(line));
        line.UnitPriceKrw = 0;
        Assert.Null(약정책임확인표시.미확정조건(line));
        line.Billing = NeighborhoodSettlementCodes.Free;
        Assert.Equal("무료 조건 확인 필요", 약정책임확인표시.미확정조건(line));
        line.AmountKrw = 0;
        Assert.Null(약정책임확인표시.미확정조건(line));
        Assert.Equal("상품대금 수금 방식 확인 필요", 약정책임확인표시.수금방식("private-unknown-route"));
        Assert.Equal("연결 정보 확인 필요", 약정책임확인표시.담당연결(" ", "worker"));
        Assert.Equal("본인으로 연결됨", 약정책임확인표시.담당연결("worker", "worker"));
        Assert.Equal("담당 연결 기록 있음", 약정책임확인표시.담당연결("private-owner", "worker"));
    }

    [Fact]
    public async Task 렌더는본인항목과확인한관계만보여주고_식별자와타인금액과새명령을노출하지않는다()
    {
        var agreement = Agreement();
        agreement.SourceKind = NeighborhoodSettlementCodes.MartOrder;
        agreement.Terms.GoodsOwnerUserId = "private-goods-owner";
        agreement.Terms.SellerUserId = "worker";
        agreement.Terms.GoodsPaymentRoute = NeighborhoodSettlementCodes.SellerCollect;
        var line = Line("private-payer", "worker", null);
        line.Billing = NeighborhoodSettlementCodes.PerUnit;
        line.Quantity = 2;
        line.IncludedInDelivery = true;
        agreement.Terms.Lines = [line, Line("private-payer", "private-other", 97775)];
        agreement.RequiredConsentUserIds = ["worker", "private-payer", "private-other"];
        agreement.Consents = [Consent("worker", 1, true, 1), Consent("worker", 2, true, 2)];
        var value = new NeighborhoodSettlementSummaryDto { Agreement = agreement };
        var before = JsonSerializer.Serialize(value);

        var html = await Render(value, "worker");
        Assert.Contains("담당 연결 기록 있음", html);
        Assert.Contains("본인으로 연결됨", html);
        Assert.Contains("판매 담당자 수금·소유자 전달 약정", html);
        Assert.Contains("동의 1 / 3 명", html);
        Assert.Contains("본인에게 표시된 약정 항목의 조회 범위", html);
        Assert.Contains("단가 미확정", html);
        Assert.Contains("배송비에 포함된 참고 항목", html);
        Assert.Contains("법적 계약 당사자는", html);
        Assert.Contains("확정할 수 없습니다", html);
        Assert.Contains("원래 업무의 협의 화면", html);
        Assert.DoesNotContain("private-", html);
        Assert.DoesNotContain("97,775", html);
        Assert.DoesNotContain("97775", html);
        Assert.DoesNotContain("<button", html);
        Assert.DoesNotContain("href=", html);
        Assert.Equal(before, JsonSerializer.Serialize(value));
    }

    [Fact]
    public async Task 빈필요당사자는미확인으로표시하고_음식약정에상품역할을추정하지않는다()
    {
        var agreement = Agreement();
        agreement.SourceKind = NeighborhoodSettlementCodes.FoodOrder;
        agreement.Terms.ActivityKind = NeighborhoodSettlementCodes.PreparedFood;
        var html = await Render(new() { Agreement = agreement }, "worker");
        Assert.Contains("필요 당사자·판본 확인 필요", html);
        Assert.DoesNotContain("동의 0 / 0", html);
        Assert.DoesNotContain("필요 당사자의 동일 판본 동의 기록이 있습니다", html);
        Assert.DoesNotContain("<dt>물품 소유자</dt>", html);
        Assert.DoesNotContain("<dt>판매 담당자</dt>", html);
    }

    private static NeighborhoodSettlementAgreementDto Agreement() => new()
    {
        TermsVersion = 2, StatusCode = NeighborhoodSettlementCodes.AwaitingConsent,
        Terms = new() { ActivityKind = NeighborhoodSettlementCodes.StoredGoods }
    };

    private static NeighborhoodSettlementConsentDto Consent(string user, int version, bool granted, int minute)
        => new() { UserId = user, TermsVersion = version, Granted = granted, RecordedAtUtc = new DateTime(2026, 10, 23, 0, minute, 0, DateTimeKind.Utc) };

    private static NeighborhoodSettlementTermLine Line(string payer, string payee, decimal? amount)
        => new() { LineKey = "private-line", Kind = NeighborhoodSettlementCodes.Picking,
            PayerUserId = payer, PayeeUserId = payee, AmountKrw = amount };

    private static async Task<string> Render(NeighborhoodSettlementSummaryDto value, string? owner)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<약정책임확인Panel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(약정책임확인Panel.Value)] = value, [nameof(약정책임확인Panel.CurrentUserId)] = owner }));
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }
}
