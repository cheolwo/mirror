using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ContractManagement;

namespace Hongdal.Services.Community;

public partial class CommunityVoteService
{
    private static CommunityGroupPurchaseVoteSettingsRecord? CreateGroupPurchaseSettings(
        CommunityVoteCreateRequest request,
        string voteKind,
        string operatingMarketCountryCode)
    {
        if (voteKind != CommunityVoteKindCodes.GroupPurchaseDemand)
        {
            if (request.GroupPurchase is not null)
            {
                throw new InvalidOperationException("공동구매 수요 투표가 아닌 경우 공동구매 설정을 지정할 수 없습니다.");
            }

            return null;
        }

        if (request.AllowMultipleSelection)
        {
            throw new InvalidOperationException("공동구매 수요 투표의 첫 버전은 하나의 상품 선택지만 선택할 수 있습니다.");
        }

        var settings = request.GroupPurchase
            ?? throw new InvalidOperationException("공동구매 수요 투표 설정이 필요합니다.");
        var proposerRoleCode = Normalize(
            settings.ProposerRoleCode,
            CommunityGroupPurchaseProposerRoleCodes.GroupPurchaseRepresentative);
        if (!CommunityGroupPurchaseProposerRoleCodes.IsSupported(proposerRoleCode))
        {
            throw new InvalidOperationException("공동구매 제안 주체는 생산자 또는 공동구매 대표여야 합니다.");
        }

        var tradeRouteDecision = CommunityGroupPurchaseTradeRoutePolicy.Evaluate(
            new CommunityGroupPurchaseTradeRouteInput(
                settings.SellerCountryCode,
                settings.ShipFromCountryCode,
                settings.DeliveryCountryCode,
                settings.CustomsClearanceStatusCode,
                operatingMarketCountryCode));
        if (tradeRouteDecision.InvalidFieldCodes.Count > 0)
        {
            throw new InvalidOperationException(
                "판매자·상품 출발·배송 국가 코드는 ISO 알파-2 두 자리이고, 통관 상태는 지원하는 코드여야 합니다.");
        }

        var sellerCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            settings.SellerCountryCode);
        var shipFromCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            settings.ShipFromCountryCode);
        var deliveryCountryCode = CommunityGroupPurchaseTradeRoutePolicy.NormalizeCountryCode(
            settings.DeliveryCountryCode);
        var customsClearanceStatusCode = CommunityGroupPurchaseTradeRoutePolicy
            .NormalizeCustomsClearanceStatusCode(settings.CustomsClearanceStatusCode);
        var hasExplicitTradeRouteInput = !string.IsNullOrWhiteSpace(sellerCountryCode)
            || !string.IsNullOrWhiteSpace(shipFromCountryCode)
            || !string.IsNullOrWhiteSpace(deliveryCountryCode)
            || !string.Equals(
                customsClearanceStatusCode,
                CommunityGroupPurchaseCustomsClearanceStatusCodes.Unknown,
                StringComparison.OrdinalIgnoreCase);

        var policyCode = Normalize(settings.ParticipationPolicyCode, CommunityVoteParticipationPolicyCodes.Hybrid);
        if (policyCode is not CommunityVoteParticipationPolicyCodes.CommunityOnly
            and not CommunityVoteParticipationPolicyCodes.ServiceAreaOnly
            and not CommunityVoteParticipationPolicyCodes.PickupPoint
            and not CommunityVoteParticipationPolicyCodes.Hybrid)
        {
            throw new InvalidOperationException("지원하지 않는 공동구매 참여 정책입니다.");
        }

        if (settings.MinimumParticipantCount is < 1 or > 100_000)
        {
            throw new InvalidOperationException("최소 참여 인원은 1명 이상 100,000명 이하여야 합니다.");
        }

        if (settings.MinimumTotalQuantity is < 1 or > 1_000_000)
        {
            throw new InvalidOperationException("최소 주문 수량은 1개 이상 1,000,000개 이하여야 합니다.");
        }

        if (settings.TargetUnitPriceKrwPerKg is <= 0 or > 1_000_000_000m)
        {
            throw new InvalidOperationException("공동구매 목표단가는 0원/kg 초과 10억원/kg 이하여야 합니다.");
        }

        if (settings.RadiusMeters is < 100 or > 200_000)
        {
            throw new InvalidOperationException("생활권 반경은 100m 이상 200km 이하여야 합니다.");
        }

        var serviceAreaKey = NormalizeOptional(settings.ServiceAreaKey);
        if (policyCode is CommunityVoteParticipationPolicyCodes.ServiceAreaOnly or CommunityVoteParticipationPolicyCodes.Hybrid
            && serviceAreaKey is null)
        {
            throw new InvalidOperationException("생활권 참여 정책에는 서비스 지역 키가 필요합니다.");
        }

        var pickupPoints = settings.PickupPoints
            .Select((point, index) => CreatePickupPoint(point, index))
            .ToArray();
        if (policyCode is CommunityVoteParticipationPolicyCodes.PickupPoint or CommunityVoteParticipationPolicyCodes.Hybrid
            && pickupPoints.Length == 0)
        {
            throw new InvalidOperationException("픽업 참여 정책에는 공동수령 거점이 하나 이상 필요합니다.");
        }

        if (pickupPoints.Select(x => x.PickupPointId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != pickupPoints.Length)
        {
            throw new InvalidOperationException("공동수령 거점 ID는 중복될 수 없습니다.");
        }

        return new CommunityGroupPurchaseVoteSettingsRecord
        {
            ProposerRoleCode = proposerRoleCode,
            AgreementPolicyCode = CommunityGroupPurchaseAgreementPolicy.PolicyCode,
            ProposalOriginLegalEffectNotice = CommunityGroupPurchaseAgreementPolicy.FullLegalEffectNotice,
            OperatingMarketCountryCode = operatingMarketCountryCode,
            SellerCountryCode = sellerCountryCode,
            ShipFromCountryCode = shipFromCountryCode,
            DeliveryCountryCode = deliveryCountryCode,
            CustomsClearanceStatusCode = customsClearanceStatusCode,
            TradeRouteCode = hasExplicitTradeRouteInput
                ? tradeRouteDecision.RouteCode
                : string.Empty,
            ParticipationPolicyCode = policyCode,
            HsCode = NormalizeOptional(settings.HsCode) ?? string.Empty,
            TemperatureCode = Normalize(settings.TemperatureCode, "상온"),
            LogisticsMode = Normalize(settings.LogisticsMode, "LCL"),
            QuantityUnit = Normalize(settings.QuantityUnit, "개"),
            TargetUnitPriceKrwPerKg = settings.TargetUnitPriceKrwPerKg,
            ServiceAreaKey = serviceAreaKey ?? string.Empty,
            ServiceAreaLabel = Normalize(settings.ServiceAreaLabel, serviceAreaKey ?? string.Empty),
            RadiusMeters = settings.RadiusMeters,
            MinimumParticipantCount = settings.MinimumParticipantCount,
            MinimumTotalQuantity = settings.MinimumTotalQuantity,
            PickupPoints = pickupPoints
        };
    }

    private static CommunityVotePickupPointRecord CreatePickupPoint(
        CommunityVotePickupPointRequest request,
        int index)
    {
        var name = NormalizeOptional(request.Name)
            ?? throw new InvalidOperationException("공동수령 거점 이름이 필요합니다.");
        var addressSummary = NormalizeOptional(request.AddressSummary)
            ?? throw new InvalidOperationException("공동수령 거점의 주소 요약이 필요합니다.");
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            throw new InvalidOperationException("공동수령 거점 좌표 범위가 올바르지 않습니다.");
        }

        if (request.PickupStartsAtUtc is not null
            && request.PickupEndsAtUtc is not null
            && request.PickupStartsAtUtc >= request.PickupEndsAtUtc)
        {
            throw new InvalidOperationException("픽업 종료 시각은 시작 시각보다 이후여야 합니다.");
        }

        if (request.CapacityQuantity is < 1)
        {
            throw new InvalidOperationException("공동수령 거점 보관 가능 수량은 1개 이상이어야 합니다.");
        }

        if (request.MinimumParticipantCount is < 1 || request.MinimumTotalQuantity is < 1)
        {
            throw new InvalidOperationException("거점별 최소 참여 인원과 수량은 1 이상이어야 합니다.");
        }

        if (request.CapacityQuantity is not null
            && request.MinimumTotalQuantity is not null
            && request.MinimumTotalQuantity > request.CapacityQuantity)
        {
            throw new InvalidOperationException("거점별 최소 수량은 보관 가능 수량을 초과할 수 없습니다.");
        }

        if (request.PickupFee < 0)
        {
            throw new InvalidOperationException("픽업 수수료는 0 이상이어야 합니다.");
        }

        var storageTypeCode = Normalize(request.StorageTypeCode, CommunityVotePickupStorageTypeCodes.Ambient);
        if (storageTypeCode is not CommunityVotePickupStorageTypeCodes.Ambient
            and not CommunityVotePickupStorageTypeCodes.Refrigerated
            and not CommunityVotePickupStorageTypeCodes.Frozen)
        {
            throw new InvalidOperationException("지원하지 않는 거점 보관 유형입니다.");
        }

        return new CommunityVotePickupPointRecord
        {
            PickupPointId = Normalize(request.PickupPointId, $"pickup-{index + 1}"),
            Name = name,
            AddressSummary = addressSummary,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            StorageTypeCode = storageTypeCode,
            PickupStartsAtUtc = request.PickupStartsAtUtc,
            PickupEndsAtUtc = request.PickupEndsAtUtc,
            CapacityQuantity = request.CapacityQuantity,
            MinimumParticipantCount = request.MinimumParticipantCount,
            MinimumTotalQuantity = request.MinimumTotalQuantity,
            PickupFee = request.PickupFee
        };
    }
}
