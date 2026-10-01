using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public partial class CommunityVoteService
{
    private static GroupPurchaseParticipation ValidateGroupPurchaseParticipation(
        CommunityVoteRecord vote,
        CommunityVoteCastRequest request,
        string voterHash)
    {
        var settings = vote.GroupPurchase;
        if (settings is null)
        {
            return new GroupPurchaseParticipation(1, string.Empty, null);
        }

        if (request.RequestedQuantity is < 1 or > 10_000)
        {
            throw new InvalidOperationException("희망 수량은 1개 이상 10,000개 이하여야 합니다.");
        }

        var methodCode = NormalizeOptional(request.ParticipationMethodCode)
            ?? throw new InvalidOperationException("공동구매 참여 방법을 선택해야 합니다.");
        var methodAllowed = settings.ParticipationPolicyCode switch
        {
            CommunityVoteParticipationPolicyCodes.CommunityOnly => methodCode == CommunityVoteParticipationMethodCodes.CommunityMember,
            CommunityVoteParticipationPolicyCodes.ServiceAreaOnly => methodCode == CommunityVoteParticipationMethodCodes.ServiceArea,
            CommunityVoteParticipationPolicyCodes.PickupPoint => methodCode == CommunityVoteParticipationMethodCodes.PickupPoint,
            CommunityVoteParticipationPolicyCodes.Hybrid => methodCode is CommunityVoteParticipationMethodCodes.CommunityMember
                or CommunityVoteParticipationMethodCodes.ServiceArea
                or CommunityVoteParticipationMethodCodes.PickupPoint,
            _ => false
        };
        if (!methodAllowed)
        {
            throw new InvalidOperationException("이 공동구매에서 허용하지 않는 참여 방법입니다.");
        }

        if (methodCode == CommunityVoteParticipationMethodCodes.CommunityMember
            && !string.Equals(request.CommunityMembershipReference?.Trim(), vote.CommunityScope, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("현재 커뮤니티의 확인된 구성원 참조가 필요합니다.");
        }

        if (methodCode == CommunityVoteParticipationMethodCodes.ServiceArea
            && !string.Equals(request.ServiceAreaReference?.Trim(), settings.ServiceAreaKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("공동구매 서비스 지역과 일치하는 생활권 참조가 필요합니다.");
        }

        var pickupPointId = NormalizeOptional(request.PickupPointId);
        var pickupPoint = pickupPointId is null
            ? null
            : settings.PickupPoints.FirstOrDefault(x => string.Equals(x.PickupPointId, pickupPointId, StringComparison.OrdinalIgnoreCase));
        if (pickupPointId is not null && pickupPoint is null)
        {
            throw new InvalidOperationException("선택한 공동수령 거점을 찾을 수 없습니다.");
        }

        if (methodCode == CommunityVoteParticipationMethodCodes.PickupPoint && pickupPoint is null)
        {
            throw new InvalidOperationException("픽업 참여자는 공동수령 거점을 선택해야 합니다.");
        }

        if (pickupPoint?.CapacityQuantity is int capacityQuantity)
        {
            var assignedQuantity = vote.Votes
                .Where(x => !string.Equals(x.VoterHash, voterHash, StringComparison.Ordinal))
                .Where(x => string.Equals(x.PickupPointId, pickupPoint.PickupPointId, StringComparison.OrdinalIgnoreCase))
                .Sum(x => x.RequestedQuantity);
            if (assignedQuantity + request.RequestedQuantity > capacityQuantity)
            {
                throw new InvalidOperationException("선택한 공동수령 거점의 보관 가능 수량을 초과합니다.");
            }
        }

        return new GroupPurchaseParticipation(request.RequestedQuantity, methodCode, pickupPoint?.PickupPointId);
    }

    private static string? QueueGroupPurchaseDemand(
        CommunityVoteRecord vote,
        CommunityVoteCastRequest request,
        string optionId,
        string voterHash,
        GroupPurchaseParticipation participation)
    {
        var settings = vote.GroupPurchase;
        if (settings is null)
        {
            return null;
        }

        var option = vote.Options.Single(x => string.Equals(x.OptionId, optionId, StringComparison.OrdinalIgnoreCase));
        var pickupPoint = participation.PickupPointId is null
            ? null
            : settings.PickupPoints.Single(x => string.Equals(
                x.PickupPointId,
                participation.PickupPointId,
                StringComparison.OrdinalIgnoreCase));
        var deliveryScopeKey = pickupPoint is not null
            ? $"pickup-point:{pickupPoint.PickupPointId}"
            : participation.ParticipationMethodCode == CommunityVoteParticipationMethodCodes.ServiceArea
                ? settings.ServiceAreaKey
                : vote.CommunityScope;
        var deliveryScopeName = pickupPoint?.Name
            ?? (participation.ParticipationMethodCode == CommunityVoteParticipationMethodCodes.ServiceArea
                ? settings.ServiceAreaLabel
                : vote.CommunityScope);

        var handoffRequest = new CommunityGroupPurchaseDemandHandoffRequest
        {
            VoteId = vote.Id,
            SourcePostId = vote.SourcePostId,
            CommunityLedgerId = vote.CommunityLedgerId,
            VoterHash = voterHash,
            VoterDisplayName = Normalize(request.VoterDisplayName, "익명 참여자"),
            OptionId = option.OptionId,
            ProductKey = string.IsNullOrWhiteSpace(option.ProductKey)
                ? $"community-vote:{vote.Id:N}:{option.OptionId}"
                : option.ProductKey,
            ProductName = option.Text,
            HsCode = string.IsNullOrWhiteSpace(option.HsCode) ? settings.HsCode : option.HsCode,
            TemperatureCode = string.IsNullOrWhiteSpace(option.TemperatureCode) ? settings.TemperatureCode : option.TemperatureCode,
            LogisticsMode = string.IsNullOrWhiteSpace(option.LogisticsMode) ? settings.LogisticsMode : option.LogisticsMode,
            DeliveryScopeKey = deliveryScopeKey,
            DeliveryScopeName = deliveryScopeName,
            RequestedQuantity = participation.RequestedQuantity,
            QuantityUnit = string.IsNullOrWhiteSpace(option.QuantityUnit) ? settings.QuantityUnit : option.QuantityUnit,
            MinimumParticipantCount = pickupPoint?.MinimumParticipantCount ?? settings.MinimumParticipantCount,
            MinimumTotalQuantity = pickupPoint?.MinimumTotalQuantity ?? settings.MinimumTotalQuantity
        };
        var outboxId = $"community-vote:{vote.Id:N}:{voterHash}";
        vote.DemandHandoffOutbox.RemoveAll(x =>
            string.Equals(x.OutboxId, outboxId, StringComparison.Ordinal));
        vote.DemandHandoffOutbox.Add(new CommunityVoteDemandHandoffOutboxRecord
        {
            OutboxId = outboxId,
            Request = handoffRequest,
            Status = CommunityVoteDemandHandoffStatusCodes.Pending,
            UpdatedAtUtc = DateTime.UtcNow
        });
        return outboxId;
    }

    private static CommunityGroupPurchaseVoteResponse? ToGroupPurchaseResponse(CommunityVoteRecord vote)
    {
        var settings = vote.GroupPurchase;
        if (settings is null)
        {
            return null;
        }

        var totalRequestedQuantity = vote.Votes.Sum(x => x.RequestedQuantity);
        var unassignedVotes = vote.Votes.Where(x => x.PickupPointId is null).ToArray();
        var hasExplicitTradeRoute = !string.IsNullOrWhiteSpace(settings.TradeRouteCode);
        var tradeRouteDecision = hasExplicitTradeRoute
            ? CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
                new CommunityGroupPurchaseTradeRouteInput(
                    settings.SellerCountryCode,
                    settings.ShipFromCountryCode,
                    settings.DeliveryCountryCode,
                    settings.CustomsClearanceStatusCode,
                    settings.OperatingMarketCountryCode))
            : null;
        return new CommunityGroupPurchaseVoteResponse
        {
            ProposerRoleCode = CommunityGroupPurchaseProposerRoleCodes.IsSupported(settings.ProposerRoleCode)
                ? settings.ProposerRoleCode
                : CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative,
            AgreementPolicyCode = string.IsNullOrWhiteSpace(settings.AgreementPolicyCode)
                ? CommunityGroupPurchaseAgreementPolicy.PolicyCode
                : settings.AgreementPolicyCode,
            ProposalOriginLegalEffectNotice = string.IsNullOrWhiteSpace(settings.ProposalOriginLegalEffectNotice)
                ? CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice
                : settings.ProposalOriginLegalEffectNotice,
            OperatingMarketCountryCode = CommunityGroupPurchaseTradeRoutePolicy
                .NormalizeOperatingMarketCountryCode(settings.OperatingMarketCountryCode),
            SellerCountryCode = settings.SellerCountryCode,
            ShipFromCountryCode = settings.ShipFromCountryCode,
            DeliveryCountryCode = settings.DeliveryCountryCode,
            CustomsClearanceStatusCode = settings.CustomsClearanceStatusCode,
            TradeRouteCode = tradeRouteDecision?.RouteCode ?? string.Empty,
            IsGroupImportCandidate = tradeRouteDecision?.IsGroupImportCandidate == true,
            RequiresTradeRouteReview = tradeRouteDecision?.RequiresManualReview == true,
            RecommendedLedgerTemplateKey = tradeRouteDecision?.IsGroupImportCandidate == true
                ? CommunityLedgerTemplateKeys.GroupImport
                : string.Empty,
            TradeRouteReasonCodes = tradeRouteDecision?.ReasonCodes ?? [],
            TradeRouteMissingFieldCodes = tradeRouteDecision?.MissingFieldCodes ?? [],
            TradeRouteInvalidFieldCodes = tradeRouteDecision?.InvalidFieldCodes ?? [],
            ParticipationPolicyCode = settings.ParticipationPolicyCode,
            HsCode = settings.HsCode,
            TemperatureCode = settings.TemperatureCode,
            LogisticsMode = settings.LogisticsMode,
            QuantityUnit = settings.QuantityUnit,
            TargetUnitPriceKrwPerKg = settings.TargetUnitPriceKrwPerKg,
            ServiceAreaKey = settings.ServiceAreaKey,
            ServiceAreaLabel = settings.ServiceAreaLabel,
            RadiusMeters = settings.RadiusMeters,
            MinimumParticipantCount = settings.MinimumParticipantCount,
            MinimumTotalQuantity = settings.MinimumTotalQuantity,
            TotalRequestedQuantity = totalRequestedQuantity,
            UnassignedPickupParticipantCount = unassignedVotes.Length,
            UnassignedPickupQuantity = unassignedVotes.Sum(x => x.RequestedQuantity),
            DemandHandoffPendingCount = vote.DemandHandoffOutbox.Count(x =>
                x.Status is CommunityVoteDemandHandoffStatusCodes.Pending
                    or CommunityVoteDemandHandoffStatusCodes.Processing
                    or CommunityVoteDemandHandoffStatusCodes.RetryPending),
            DemandHandoffFailedCount = vote.DemandHandoffOutbox.Count(x =>
                x.Status is CommunityVoteDemandHandoffStatusCodes.Failed),
            IsMinimumReached = vote.Votes.Count >= settings.MinimumParticipantCount
                && totalRequestedQuantity >= settings.MinimumTotalQuantity,
            PickupPoints = settings.PickupPoints.Select(point =>
            {
                var assignedVotes = vote.Votes
                    .Where(x => string.Equals(x.PickupPointId, point.PickupPointId, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var requestedQuantity = assignedVotes.Sum(x => x.RequestedQuantity);
                var minimumParticipantCount = point.MinimumParticipantCount ?? settings.MinimumParticipantCount;
                var minimumTotalQuantity = point.MinimumTotalQuantity ?? settings.MinimumTotalQuantity;
                return new CommunityVotePickupPointResponse
                {
                    PickupPointId = point.PickupPointId,
                    Name = point.Name,
                    AddressSummary = point.AddressSummary,
                    Latitude = point.Latitude,
                    Longitude = point.Longitude,
                    StorageTypeCode = point.StorageTypeCode,
                    PickupStartsAtUtc = point.PickupStartsAtUtc,
                    PickupEndsAtUtc = point.PickupEndsAtUtc,
                    CapacityQuantity = point.CapacityQuantity,
                    MinimumParticipantCount = point.MinimumParticipantCount,
                    MinimumTotalQuantity = point.MinimumTotalQuantity,
                    PickupFee = point.PickupFee,
                    ParticipantCount = assignedVotes.Length,
                    RequestedQuantity = requestedQuantity,
                    IsMinimumReached = assignedVotes.Length >= minimumParticipantCount
                        && requestedQuantity >= minimumTotalQuantity,
                    IsCapacityReached = point.CapacityQuantity is int capacityQuantity
                        && requestedQuantity >= capacityQuantity
                };
            }).ToArray()
        };
    }
}
