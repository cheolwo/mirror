using System.Net;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Food;

namespace SsalddelAdmin.Services;

/// <summary>모의 지급의 시험 입력, 명시적 요청과 실패 수명을 추적 조회와 분리합니다.</summary>
internal sealed class 음식주문모의지급Workflow(
    Func<string, FoodDeliverySimulatedPayoutRequest, CancellationToken, Task<FoodDeliveryOrderSettlementDto>> simulate)
{
    private FoodDeliverySimulatedPayoutRequest? payoutRequest;

    public decimal? FixtureDeduction { get; set; }
    public string FixtureEvidence { get; set; } = string.Empty;
    public bool IsBusy { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool CanSimulatePayout(string orderNo, string? tracedOrderNo, FoodDeliveryOrderSettlementDto settlement)
        => string.Equals(orderNo.Trim(), tracedOrderNo, StringComparison.Ordinal)
           && settlement.ServerExecutionModeCode == "Simulation"
           && settlement.GrossAmount.HasValue
           && settlement.ReceiptConfirmedAtUtc.HasValue
           && settlement.PayoutStatusCode != "SimulationSucceeded"
           && settlement.SettlementStatusCode != "BlockedMissingQuote";

    public void InvalidatePending() => IsBusy = false;

    public void ChangeOrder()
    {
        payoutRequest = null;
        ErrorMessage = null;
    }

    public void ResetForTraceLoad()
    {
        ChangeOrder();
        FixtureDeduction = null;
        FixtureEvidence = string.Empty;
    }

    public void RestoreFixture(FoodDeliveryOrderSettlementDto? settlement)
    {
        if (settlement is not { DeductionEvidenceScopeCode: "SimulationFixture" }) return;
        FixtureDeduction = settlement.DeductionAmount;
        FixtureEvidence = settlement.DeductionEvidenceReference;
    }

    public async Task SimulateAsync(
        string requestedOrderNo,
        FoodDeliveryOrderSettlementDto settlement,
        Action<FoodDeliveryOrderSettlementDto> applySettlement,
        Action requireLogin,
        Func<bool> isCurrent,
        Func<bool> ownsPendingState,
        CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            if (payoutRequest is null || payoutRequest.ExpectedSettlementRevision != settlement.Revision
                || payoutRequest.ConfirmedDeductionAmount != FixtureDeduction || payoutRequest.DeductionEvidenceReference != FixtureEvidence.Trim())
            {
                payoutRequest = new()
                {
                    IdempotencyKey = Guid.NewGuid().ToString("N"), ExpectedSettlementRevision = settlement.Revision,
                    ConfirmedGrossAmount = settlement.GrossAmount!.Value, ConfirmedDeductionAmount = FixtureDeduction,
                    DeductionEvidenceReference = FixtureEvidence.Trim()
                };
            }
            var result = await simulate(requestedOrderNo, payoutRequest, cancellationToken);
            if (isCurrent()) applySettlement(result);
        }
        catch (Exception) when (!isCurrent() || cancellationToken.IsCancellationRequested) { }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            requireLogin();
            ErrorMessage = "로그인이 만료되었습니다. 다시 로그인해 주세요.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            if (ownsPendingState()) IsBusy = false;
        }
    }
}
