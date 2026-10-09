using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Contracts.Common.Pricing;

namespace Ssalddel.Domain.Pricing;

/// <summary>저장 판본의 무결성 확인. 현재 요율을 가져와 과거 금액을 재계산하지 않습니다.</summary>
public static class AgreedRateTableEvidencePolicy
{
    private sealed record Terms(FoodDeliveryPayoutPricingPolicy? FoodPricing, AgreedCargoPricingDto? CargoPricing);
    public static string TermsDigest(FoodDeliveryPayoutPricingPolicy? food, AgreedCargoPricingDto? cargo)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Terms(food, cargo),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))))).ToLowerInvariant();

    public static bool IsValid(AgreedRateTableSnapshotDto? s)
    {
        if (s is null || string.IsNullOrWhiteSpace(s.AgreementId) || s.Revision < 1 || s.Proposer is null || s.Counterparty is null
            || string.IsNullOrWhiteSpace(s.Proposer.PartyId) || string.IsNullOrWhiteSpace(s.Counterparty.PartyId)
            || s.AcceptedAtUtc.Kind != DateTimeKind.Utc || s.EffectiveFromUtc.Kind != DateTimeKind.Utc || s.EffectiveUntilUtc.Kind != DateTimeKind.Utc
            || s.EffectiveFromUtc >= s.EffectiveUntilUtc || s.AcceptedAtUtc >= s.EffectiveUntilUtc) return false;
        var pair = (s.Proposer.PartyKind, s.Counterparty.PartyKind);
        var shape = s.BusinessKind switch
        {
            AgreedRateBusinessKinds.FoodDelivery => s.VehicleCode is "Car" or "Motorcycle" && s.FoodPricing is not null && s.CargoPricing is null
                && pair is (AgreedRatePartyKinds.Restaurant, AgreedRatePartyKinds.FoodDriver) or (AgreedRatePartyKinds.FoodDriver, AgreedRatePartyKinds.Restaurant),
            AgreedRateBusinessKinds.CargoTransport => !string.IsNullOrWhiteSpace(s.VehicleCode) && s.CargoPricing is not null && s.FoodPricing is null
                && pair is (AgreedRatePartyKinds.Shipper, AgreedRatePartyKinds.CargoDriver) or (AgreedRatePartyKinds.CargoDriver, AgreedRatePartyKinds.Shipper),
            _ => false
        };
        return shape && string.Equals(s.TermsDigest, TermsDigest(s.FoodPricing, s.CargoPricing), StringComparison.Ordinal);
    }

    public static bool MatchesStoredAgreement(AgreedRateTableSnapshotDto? s, AgreedRateTableRecord agreement, long frozenRevision)
        => IsValid(s) && s!.AgreementId == agreement.AgreementId && s.Revision == frozenRevision
           && s.BusinessKind == agreement.BusinessKind && s.VehicleCode == agreement.VehicleCode
           && s.Proposer == new AgreedRatePartyDto(agreement.ProposerKind, agreement.ProposerId)
           && s.Counterparty == new AgreedRatePartyDto(agreement.CounterpartyKind, agreement.CounterpartyId)
           && s.EffectiveFromUtc == agreement.EffectiveFromUtc && s.EffectiveUntilUtc == agreement.EffectiveUntilUtc
           && s.AcceptedAtUtc == agreement.AcceptedAtUtc && s.TermsDigest == agreement.TermsDigest;
}
