using Ssalddel.Contracts.Admin.Settlement;
using SsalddelAdmin.Services;
using SsalddelAdmin.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class AdminSettlementResponsibilitiesR21Tests
{
    [Theory]
    [InlineData("결제완료", true)]
    [InlineData("미결제", false)]
    [InlineData("환불완료", false)]
    public void 지급조회실패는수납결과를덮지않고빈목록으로확정하지않는다(string paymentStatus, bool collected)
    {
        var request = new 화주운송의뢰응답 { 의뢰Id = "request", 결제상태 = paymentStatus };
        var model = TransportWorkflowSettlementViewModel.Create("request", request, null, Transport(),
            new() { 조회년 = 2026, 조회월 = 10, 조회실패 = true, 지급요청목록 = [Paid()] });
        Assert.Equal(collected, model.Collection.Confirmed);
        Assert.True(model.Payout.QueryFailed); Assert.False(model.Payout.QueryCompleted);
        Assert.Equal("조회 실패", model.Payout.RequestStatus);
        Assert.Equal("실지급 미확인", model.Payout.ActualTransferStatus);
        Assert.Equal("관리자 조회 미연결", model.Payout.PreparationStatus);
    }

    [Fact]
    public void 수납취소가이미확인된실지급기록을임의로취소하지않는다()
    {
        var model = TransportWorkflowSettlementViewModel.Create("request", new() { 결제상태 = "취소완료" }, null,
            Transport(), Query(Paid()));
        Assert.False(model.Collection.Confirmed);
        Assert.True(model.Payout.HasActualTransferConfirmation);
        Assert.Equal("실지급 확인", model.ActualTransferStatus);
    }

    [Theory]
    [InlineData("other", 12)]
    [InlineData("request", 13)]
    [InlineData("REQUEST", 12)]
    public void 다른의뢰나운송의실지급을선택운송의근거로가져오지않는다(string requestId, long transportId)
    {
        var record = Paid(); record.RequestId = requestId; record.TransportId = transportId;
        var model = TransportPayoutPresentation.Create("request", Transport(), Query(record));
        Assert.Empty(model.Requests); Assert.False(model.HasActualTransferConfirmation);
    }

    [Fact]
    public void 지급미연결과조회월없음은서로다르게표시한다()
    {
        var missing = TransportPayoutPresentation.Create("request", Transport(), null);
        var empty = TransportPayoutPresentation.Create("request", Transport(), Query());
        Assert.False(missing.QueryCompleted); Assert.Equal("조회 미연결", missing.RequestStatus);
        Assert.True(empty.QueryCompleted); Assert.Equal("조회월 기록 없음", empty.RequestStatus);
        Assert.Equal("실지급 미확인", empty.ActualTransferStatus);
    }

    [Fact]
    public void 시험지급플래그가있어도실지급완료로승격하지않는다()
    {
        var record = Paid(); record.ExecutionModeCode = "Simulation"; record.StatusCode = "SimulationVerified";
        var model = TransportPayoutPresentation.Create("request", Transport(), Query(record));
        Assert.Equal("시험 지급 검증", model.RequestStatus);
        Assert.False(model.HasActualTransferConfirmation);
    }

    private static 운송진행응답 Transport() => new() { Id = 12, 운송번호 = "request" };
    private static 기사운송료지급조회응답 Query(params 기사지급승인응답[] items)
        => new() { 조회년 = 2026, 조회월 = 10, 조회완료 = true, 지급요청목록 = items };
    private static 기사지급승인응답 Paid() => new()
    {
        RequestId = "request", TransportId = 12, ExpectedPayoutAmount = 6200, CurrencyCode = "KRW",
        ExecutionModeCode = "Operational", IsActualTransferCompleted = true, StatusCode = "Approved",
        ApprovedAtUtc = new DateTime(2026, 10, 22, 1, 0, 0, DateTimeKind.Utc)
    };
}
