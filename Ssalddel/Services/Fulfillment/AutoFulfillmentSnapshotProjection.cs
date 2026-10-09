using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Domain.Community;

namespace Ssalddel.Services.Fulfillment;

/// <summary>조회용 금액·현재 상태 계산입니다. 계산 결과는 접수·PG·정산 증거를 만들지 않습니다.</summary>
public static class AutoFulfillmentSnapshotProjection
{
    public static AutoFulfillmentSnapshot Paused(FulfillmentSourceKey key, bool sourceClosed = false) => new()
    {
        SourceKind = key.SourceKind, SourceId = key.SourceId, SourceClosed = sourceClosed,
        ExecutionPaused = true, StateCode = AutoFulfillmentStates.NeedsReview, ReviewCode = "FulfillmentRoutingDisabled",
        MaxUserChargeKrw = null, TotalCostKrw = null, UserChargeKrw = null,
        RestaurantContributionKrw = 0, FrozenRestaurantSupportKrw = 0,
        CanCancel = false, CanRenewConsent = false, IsSimulation = true, IsOperationalSettlementEligible = false,
        ConsentRenewalLockedReasonCode = "FulfillmentRoutingDisabled",
        Notice = "자동 수행 기능이 중지되어 저장된 접수·비용·상한의 현재 상태를 조회하지 않습니다. 기존 요청 기록은 유지합니다."
    };

    public static AutoFulfillmentSnapshot Create(NeighborhoodFulfillmentRecord current, AutoFulfillmentConsentRecord consent,
        NeighborhoodFulfillmentAttemptRecord? attempt, IReadOnlyList<NeighborhoodFulfillmentAttemptRecord> attempts,
        bool sourceClosed, bool sourceCanCancel, bool hasCookingStarted)
    {
        var unknownCost = attempts.Any(x => Terminal(x.StateCode) && x.ConfirmedIncurredCostKrw is null);
        decimal? total = unknownCost ? null : attempts.Where(x => Terminal(x.StateCode)).Sum(x => x.ConfirmedIncurredCostKrw ?? 0)
            + (attempt is not null && !Terminal(attempt.StateCode) ? attempt.CostKrw : 0);
        var policy = DeliveryCostSharingStorage.Read(consent.FrozenCostSharingPolicyJson, consent.FrozenRestaurantSupportKrw);
        var allocation = total is { } amount ? DeliveryCostSharingCalculator.Calculate(amount, policy) : null;
        var allocationDenied = allocation?.IsAllowed == false || current.ReviewCode == "RestaurantChargeLimitExceeded";
        var renewalLockedReason = AutoFulfillmentConsentRenewalPolicy.LockedReason(sourceClosed, hasCookingStarted, attempt?.StateCode, current.StateCode);
        return new()
        {
            SourceKind = current.SourceKind, SourceId = current.RequestId, SourceClosed = sourceClosed,
            StateCode = sourceClosed && attempt is null ? AutoFulfillmentStates.Cancelled : current.StateCode,
            Revision = current.Revision, ConsentRevision = consent.Revision, AttemptId = attempt?.AttemptId,
            ProviderId = attempt?.ProviderId, ProviderName = AutoFulfillmentNotice.Providers.FirstOrDefault(x => x.ProviderId == attempt?.ProviderId)?.ProviderName,
            ExternalRequestId = attempt?.ExternalRequestId, Leg = attempt?.Leg, MaxUserChargeKrw = consent.MaxUserChargeKrw,
            TotalCostKrw = total, UserChargeKrw = !allocationDenied && allocation?.IsAllowed == true ? allocation.CustomerChargeKrw : null,
            RestaurantContributionKrw = !allocationDenied && allocation?.IsAllowed == true ? allocation.RestaurantChargeKrw ?? 0 : 0,
            FrozenRestaurantSupportKrw = consent.FrozenRestaurantSupportKrw, RestaurantSupportRevision = consent.RestaurantSupportRevision,
            CostSharingPolicy = current.SourceKind == FulfillmentSourceKinds.FoodOrder ? DeliveryCostSharingCalculator.Clone(policy) : null,
            CostSharingAllowed = current.SourceKind == FulfillmentSourceKinds.FoodOrder ? allocationDenied ? false : allocation?.IsAllowed : null,
            EstimatedCompletionUtc = attempt?.EstimatedCompletionUtc, UpdatedAtUtc = DateTime.SpecifyKind(current.UpdatedAtUtc, DateTimeKind.Utc),
            SelectionReason = current.SelectionReason, ReviewCode = allocation?.IsAllowed == false ? allocation.ReasonCode : current.ReviewCode,
            CanCancel = !sourceClosed && sourceCanCancel && !hasCookingStarted && attempt?.StateCode is AutoFulfillmentStates.Accepted or AutoFulfillmentStates.DriverAssigned,
            CanRenewConsent = renewalLockedReason is null,
            ConsentRenewalLockedReasonCode = renewalLockedReason,
            IsSimulation = true, IsOperationalSettlementEligible = false
        };
    }

    public static bool Terminal(string state) => state is AutoFulfillmentStates.Failed or AutoFulfillmentStates.Cancelled or AutoFulfillmentStates.Completed;
}
