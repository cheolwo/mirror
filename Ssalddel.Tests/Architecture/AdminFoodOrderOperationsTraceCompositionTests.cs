namespace Ssalddel.Tests.Architecture;

public sealed class AdminFoodOrderOperationsTraceCompositionTests
{
    [Fact]
    public void 관리자화면은_주문번호상관관계와복구정보를표시하고민감자료를제외한다()
    {
        var root = FindRepositoryRoot();
        var page = File.ReadAllText(Path.Combine(
            root,
            "SsalddelAdmin",
            "Components",
            "Pages",
            "FoodOrderOperationsTrace.razor"));

        Assert.Contains("@page \"/food/order-trace\"", page);
        Assert.Contains("주문 진행", page);
        Assert.Contains("Outbox 상태", page);
        Assert.Contains("복구 안내", page);
        Assert.Contains("수령지 주소·연락처·Outbox payload는 이 화면에 표시하지 않습니다.", page);
        Assert.DoesNotContain("DataJson", page);
        Assert.DoesNotContain("PayloadJson", page);
    }

    [Fact]
    public void 주문진행과정산오류는기본에남기고_진단과지급시험은접힌보조영역에둔다()
    {
        var page = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "SsalddelAdmin", "Components", "Pages", "FoodOrderOperationsTrace.razor"));
        var detailsStart = page.IndexOf("<MudExpansionPanel Text=\"상세 정보\" Expanded=\"false\">", StringComparison.Ordinal);
        var payoutStart = page.IndexOf("<MudExpansionPanel Text=\"지급 테스트\" Expanded=\"false\">", StringComparison.Ordinal);
        Assert.True(detailsStart > 0);
        Assert.True(payoutStart > detailsStart);
        var main = page[..detailsStart];
        var details = page[detailsStart..payoutStart];
        var payoutEnd = page.IndexOf("</MudExpansionPanel>", payoutStart, StringComparison.Ordinal);
        Assert.True(payoutEnd > payoutStart);
        var payout = page[payoutStart..payoutEnd];

        Assert.Contains("@trace.음식점명", main);
        Assert.Contains("주문 진행", main);
        Assert.Contains("@display.GrossText", main);
        Assert.Contains("@display.DeductionText", main);
        Assert.Contains("@display.NetText", main);
        Assert.Contains("@display.ProgressText", main);
        Assert.Contains("@display.NoticeText", main);
        Assert.Contains("@payoutError", main);
        Assert.Contains("trace.경고목록", main);
        Assert.Contains("복구 안내", main);
        Assert.Contains("@if (trace.복구안내목록.Count > 0)", main);
        Assert.DoesNotContain("현재 운영자가 개입해야 할 복구 안내가 없습니다.", main);
        Assert.DoesNotContain("@checkpoint.설명", main);
        Assert.DoesNotContain("배차대기 ID", main);
        Assert.DoesNotContain("Outbox 상태", main);
        Assert.DoesNotContain("원본 의뢰", main);
        Assert.DoesNotContain("공동 원장", main);
        Assert.DoesNotContain("시험 공제액", main);

        Assert.Contains("배차대기 ID", details);
        Assert.Contains("Outbox 상태", details);
        Assert.Contains("원본 의뢰", details);
        Assert.Contains("공동 원장", details);
        Assert.Contains("운송 감사 이벤트", details);
        Assert.Contains("@checkpoint.설명", details);
        Assert.Contains("CanSimulatePayout(testSettlement)", payout);
        Assert.Contains("시험 공제액", payout);
        Assert.Contains("SimulationPayments", payout);
        Assert.Contains("실제 송금은 하지 않습니다.", payout);
        Assert.Contains("settlement.ServerExecutionModeCode == \"Simulation\"", page);
    }

    [Fact]
    public void 관리자V1탐색에_음식주문추적화면을포함한다()
    {
        var root = FindRepositoryRoot();
        var navigation = File.ReadAllText(Path.Combine(
            root,
            "SsalddelAdmin",
            "Services",
            "AdminV1NavigationPolicy.cs"));

        Assert.Contains("\"/admin/food-delivery/order-trace\"", navigation);
    }

    [Fact]
    public void 음식배달워크플로우에_관리자운영추적화면을등록한다()
    {
        var root = FindRepositoryRoot();
        var metadata = File.ReadAllText(Path.Combine(
            root,
            "Ssalddel",
            "ApiMetadata",
            "SsalddelApiVersionAttribute.cs"));

        Assert.Contains("\"음식 주문 운영 추적\"", metadata);
        Assert.Contains("\"/food/order-trace\"", metadata);
        Assert.Contains("\"운영자가 음식 주문번호로 배차·추천·운송·Outbox 상관관계", metadata);
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "Ssalddel.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }

        throw new DirectoryNotFoundException("저장소 루트를 찾을 수 없습니다.");
    }
}
