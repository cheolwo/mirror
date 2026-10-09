using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Services.Fulfillment;

/// <summary>새 자동 접수 조건의 변경 관문입니다. 철회·현재 수행 취소·환불 권한을 부여하지 않습니다.</summary>
public static class AutoFulfillmentConsentRenewalPolicy
{
    public static string? LockedReason(bool sourceClosed, bool hasCookingStarted, string? attemptState, string? currentState = null)
    {
        if (sourceClosed) return "FulfillmentSourceClosed";
        if (hasCookingStarted) return "CookingAlreadyStarted";
        if (currentState is not (null or AutoFulfillmentStates.AwaitingSource or AutoFulfillmentStates.AwaitingRouting
            or AutoFulfillmentStates.NeedsReview or AutoFulfillmentStates.Failed or AutoFulfillmentStates.Cancelled))
            return StateLockedReason(currentState);
        return StateLockedReason(attemptState);
    }

    private static string? StateLockedReason(string? state) => state switch
        {
            null or AutoFulfillmentStates.Failed or AutoFulfillmentStates.Cancelled => null,
            AutoFulfillmentStates.Completed => "FulfillmentCompleted",
            AutoFulfillmentStates.Submitting or AutoFulfillmentStates.ResultUnknown
                or AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned
                or AutoFulfillmentStates.InTransit or AutoFulfillmentStates.CancelPending => "FulfillmentActive",
            _ => "FulfillmentStateUnknown"
        };
}
