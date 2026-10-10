using FDriverApp.PageModels;
using Ssalddel.Contracts.Food;

namespace Ssalddel.Tests.Clients;

public sealed class FDriverPricingRowPresentationTests
{
    [Fact]
    public void ConfirmedZeroAndMissingAmountsRemainDifferent()
    {
        var row = Stored();
        row.PricingBreakdown.PickupAmount = 0m;
        row.PricingBreakdown.DropoffAmount = null;
        row.PricingBreakdown.DistanceKm = null;

        var item = FDriverDailySettlementItem.From(row);

        Assert.Equal("0원", Value(item, "픽업비"));
        Assert.Equal("확인 전", Value(item, "전달비"));
        Assert.Equal("확인 전", Value(item, "요금 기준 거리"));
        Assert.Equal("요금 기준 거리 확인 전", item.DistanceText);
    }

    [Fact]
    public void FractionalAmountsAndStoredDistanceKeepTheirPrecision()
    {
        var row = Stored();
        row.GrossAmount = 4180.75m;
        row.DeductionAmount = 123.5m;
        row.NetAmount = 4057.25m;
        row.PricingBreakdown.PickupAmount = 700.25m;
        row.PricingBreakdown.DistanceAmount = 2080.5m;
        row.PricingBreakdown.DistanceKm = 2.613m;

        var item = FDriverDailySettlementItem.From(row);

        Assert.Equal(Money(700.25m), Value(item, "픽업비"));
        Assert.Equal(Money(2080.5m), Value(item, "거리비"));
        Assert.Equal(2.613m.ToString("0.###") + "km", Value(item, "요금 기준 거리"));
        Assert.Equal(Money(4180.75m), item.Display.GrossText);
        Assert.Equal(Money(123.5m), item.Display.DeductionText);
        Assert.Equal(Money(4057.25m), item.Display.NetText);
    }

    [Fact]
    public void LegacyUnsplitBaseNeverBecomesInventedPickupAndDropoffAmounts()
    {
        var row = Stored();
        row.PricingBreakdown.BaseSplitCode = "LegacyUnsplit";
        row.PricingBreakdown.BaseAmount = 1400m;
        row.PricingBreakdown.PickupAmount = null;
        row.PricingBreakdown.DropoffAmount = null;

        var item = FDriverDailySettlementItem.From(row);

        Assert.Equal(Money(1400m), Value(item, "기본비 합계"));
        Assert.DoesNotContain(item.PricingRows, value => value.Label is "픽업비" or "전달비");
        Assert.Contains("픽업·전달 구분 미확인", item.PricingNotice);
        Assert.Contains($"기본비 합계 {Money(1400m)} · 픽업·전달 구분 미확인", item.PricingDetails);
    }

    [Theory]
    [InlineData("MissingEvidence")]
    [InlineData("InvalidEvidence")]
    public void UntrustedEvidenceDoesNotExposeApparentlyKnownComponents(string evidence)
    {
        var row = Stored();
        row.PricingBreakdown.EvidenceStatusCode = evidence;

        var item = FDriverDailySettlementItem.From(row);

        Assert.Empty(item.PricingRows);
        Assert.Equal("수락 당시 요금 구성을 확인하지 못했습니다.", item.PricingNotice);
        Assert.Equal(item.PricingNotice, item.PricingDetails);
        Assert.Equal(Money(4180m), item.Display.GrossText);
    }

    [Fact]
    public void PartialEvidenceKeepsKnownAndMissingValuesWithoutHoldingTheMutableDto()
    {
        var row = Stored();
        row.PricingBreakdown.EvidenceStatusCode = "MissingComponents";
        row.PricingBreakdown.TimeSurchargeAmount = null;

        var item = FDriverDailySettlementItem.From(row);
        row.PricingBreakdown.DistanceAmount = 9999m;
        row.GrossAmount = 1m;

        Assert.Equal("수락 당시 요금 · 세부 구성 확인 전", item.PricingNotice);
        Assert.Equal(Money(2080m), Value(item, "거리비"));
        Assert.Equal("확인 전", Value(item, "시간 할증"));
        Assert.Equal(Money(4180m), item.Display.GrossText);
    }

    [Fact]
    public void StoredTotalAndDeductionsAreNotRecalculatedFromVisibleComponents()
    {
        var row = Stored();
        row.GrossAmount = 5000m;
        row.DeductionAmount = null;
        row.NetAmount = null;

        var item = FDriverDailySettlementItem.From(row);

        Assert.Equal(Money(5000m), item.Display.GrossText);
        Assert.Equal("공제 미확정", item.Display.DeductionText);
        Assert.Equal("수령액 미확정", item.Display.NetText);
        Assert.Equal("공제 미확정", item.DeductionSummaryText);
        Assert.Equal("수령액 미확정", item.NetSummaryText);
        Assert.Equal(Money(2080m), Value(item, "거리비"));
    }

    [Fact]
    public async Task ExistingDescriptionAndCommandsRemainCompatible()
    {
        var calls = 0;
        var item = FDriverDailySettlementItem.From(Stored(), _ => { calls++; return Task.CompletedTask; });
        var expected = string.Join(Environment.NewLine,
            "수락 당시 저장된 요금 구성",
            $"픽업 {Money(700m)} · 전달 {Money(700m)}",
            $"거리비 {Money(2080m)} · 최소요금 조정 {Money(0m)}",
            $"시간 할증 {Money(700m)} · 날씨 할증 {Money(0m)} · 수요 할증 {Money(0m)}",
            $"요금 기준 거리 {2.6m:0.###}km");

        Assert.Equal(expected, item.PricingDetails);
        Assert.Equal($"요금 기준 거리 {2.6m:0.###}km", item.DistanceText);
        Assert.Equal("수락 당시 저장된 요금 구성", item.PricingNotice);
        item.TogglePricingCommand.Execute(null);
        Assert.True(item.IsPricingExpanded);
        await item.OpenDetailsCommand!.ExecuteAsync(null);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SimulationPaymentNoticeRemainsSeparateFromFinancialAmounts()
    {
        var row = Stored();
        row.DeductionAmount = 100m;
        row.NetAmount = 4080m;
        row.DeductionEvidenceScopeCode = "SimulationFixture";
        row.SettlementStatusCode = "ReadyForSimulation";
        row.PayoutStatusCode = "SimulationSucceeded";

        var item = FDriverDailySettlementItem.From(row);

        Assert.Equal(Money(4180m), item.Display.GrossText);
        Assert.Equal(Money(4080m), item.Display.NetText);
        Assert.Equal("지급 테스트 완료", item.Display.PaymentText);
        Assert.Equal("테스트 정산 · 실제 입금 아님", item.Display.NoticeText);
        Assert.False(row.IsActualTransferCompleted);
    }

    private static string Value(FDriverDailySettlementItem item, string label)
        => Assert.Single(item.PricingRows, value => value.Label == label).Value;

    private static string Money(decimal value) => value.ToString("#,0.##") + "원";

    private static FoodDeliveryOrderSettlementDto Stored() => new()
    {
        SettlementId = "settlement-1", OrderNo = "order-1", DeliveryAttemptId = "attempt-1",
        DriverId = "driver-1", RestaurantName = "음식점 1", GrossAmount = 4180m,
        SettlementStatusCode = "AwaitingDeductions",
        PricingBreakdown = new()
        {
            EvidenceStatusCode = "Stored", BaseSplitCode = "Split", BaseAmount = 1400m,
            PickupAmount = 700m, DropoffAmount = 700m, DistanceAmount = 2080m,
            MinimumAdjustmentAmount = 0m, TimeSurchargeAmount = 700m,
            WeatherSurchargeAmount = 0m, DemandSurchargeAmount = 0m, DistanceKm = 2.6m
        }
    };
}
