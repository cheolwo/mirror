using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Models;

namespace Ssalddel.Tests.Clients;

public sealed class FoodDeliverySettlementDisplayTests
{
    [Fact]
    public void 소수시험공제를_각각_반올림하여_합계를_왜곡하지_않는다()
    {
        var result = FoodDeliverySettlementDisplay.From(new()
        {
            GrossAmount = 4_000m, DeductionAmount = 123.5m, NetAmount = 3_876.5m
        });
        Assert.Equal(123.5m.ToString("#,0.##") + "원", result.DeductionText);
        Assert.Equal(3_876.5m.ToString("#,0.##") + "원", result.NetText);
    }

    [Fact]
    public void 공제근거가_없으면_미확정을_0원으로_보여주지_않는다()
    {
        var result = FoodDeliverySettlementDisplay.From(new()
        {
            GrossAmount = 4_720m, SettlementStatusCode = "AwaitingDeductions"
        });
        Assert.Contains("4", result.GrossText);
        Assert.Equal("공제 미확정", result.DeductionText);
        Assert.Equal("수령액 미확정", result.NetText);
        Assert.Contains("입금 확인 전", result.NoticeText);
    }

    [Fact]
    public void 모의성공과_시험공제는_실제입금으로_표시하지_않는다()
    {
        var result = FoodDeliverySettlementDisplay.From(new FoodDeliveryOrderSettlementDto
        {
            GrossAmount = 4_720m, DeductionAmount = 100m, NetAmount = 4_620m,
            DeductionEvidenceScopeCode = "SimulationFixture", PayoutStatusCode = "SimulationSucceeded",
            SettlementStatusCode = "ReadyForSimulation"
        });
        Assert.Equal("지급 테스트 완료", result.PaymentText);
        Assert.Contains("실제 입금 아님", result.NoticeText);
        Assert.Contains("테스트 정산", result.NoticeText);
    }

    [Fact]
    public void 모의실패는_재시도상태이며_금액누락은_확인필요로_보여준다()
    {
        var result = FoodDeliverySettlementDisplay.From(new()
        {
            SettlementStatusCode = "BlockedMissingQuote", PayoutStatusCode = "SimulationFailed"
        });
        Assert.Equal("배달료 확인 필요", result.GrossText);
        Assert.Contains("재시도 필요", result.PaymentText);
        Assert.Contains("확인 필요", result.StatusText);
    }
}
