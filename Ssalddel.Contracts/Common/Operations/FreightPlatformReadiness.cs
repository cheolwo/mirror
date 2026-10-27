namespace Ssalddel.Contracts.Common.Operations;

public static class FreightPlatformReadinessRoutes
{
    public const string Readiness = "api/v1/admin/operations/freight-platform-readiness";
}

public static class FreightPlatformReadinessStatusCodes
{
    public const string Unknown = "Unknown";
    public const string Pending = "Pending";
    public const string Prepared = "Prepared";
    public const string Submitted = "Submitted";
    public const string Confirmed = "Confirmed";
    public const string Expired = "Expired";
    public const string Rejected = "Rejected";
}

/// <summary>관리자 설정 근거를 읽은 준비 현황이며 기관 등록 승인이나 영업 허용을 뜻하지 않습니다.</summary>
public sealed class FreightPlatformReadinessDto
{
    public string PolicyVersion { get; init; } = "freight-platform-readiness-2026-10-19-r1";
    public DateOnly RegistrationEffectiveDate { get; init; } = new(2027, 9, 23);
    public DateTimeOffset EvaluatedAtUtc { get; init; }
    public bool IsPreparationComplete { get; init; }
    public bool ActualRegistrationConfirmed { get; init; }
    public bool RequiredAuthorityProceduresConfirmed { get; init; }
    public string PlatformRegistrationStatusCode { get; init; } = FreightPlatformReadinessStatusCodes.Unknown;
    public string BrokeragePermitStatusCode { get; init; } = FreightPlatformReadinessStatusCodes.Unknown;
    public bool FreeConnectionConfigured { get; init; }
    public bool CanActivateOperations { get; init; }
    public FreightInformationSubscriptionPlanDto? PlannedInformationSubscription { get; init; }
    public string Notice { get; init; } = "준비 자료 확인과 실제 기관 등록·허가, 영업 활성화는 별개입니다. 이 조회는 운영을 활성화하지 않습니다.";
    public IReadOnlyList<FreightPlatformReadinessRequirementDto> Requirements { get; init; } = [];
    public IReadOnlyList<string> MissingRequirementCodes { get; init; } = [];
}

/// <summary>향후 사업 모델의 검토 후보이며 현재 이용료·청구 명령·운송 계약 권한이 아닙니다.</summary>
public sealed class FreightInformationSubscriptionPlanDto
{
    public string ModelCode { get; init; } = string.Empty;
    public IReadOnlyList<decimal> CandidateMonthlyAmountsKrw { get; init; } = [];
    public decimal? ConfirmedMonthlyAmountKrw { get; init; }
    public bool PerShipmentCommissionPlanned { get; init; }
    public bool RealtimeRecommendationsPlanned { get; init; }
    public bool PartyFinalDecisionRequired { get; init; }
    public bool BillingImplemented { get; init; }
    public string LegalClassificationStatusCode { get; init; } = FreightPlatformReadinessStatusCodes.Unknown;
    public bool LegalClassificationConfirmed { get; init; }
    public string Notice { get; init; } = string.Empty;
}

public sealed class FreightPlatformReadinessRequirementDto
{
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string StatusCode { get; init; } = FreightPlatformReadinessStatusCodes.Unknown;
    public bool IsSatisfied { get; init; }
    public bool HasEvidenceReference { get; init; }
    public bool HasAuthorityReceiptReference { get; init; }
    public DateTimeOffset? ReviewedAtUtc { get; init; }
    public DateTimeOffset? RecheckAtUtc { get; init; }
    public string NextAction { get; init; } = string.Empty;
}

public sealed record FreightCargoRegistrationInput(
    string? CargoType, decimal? GrossWeightKg, int? LengthMm, int? WidthMm, int? HeightMm,
    string? PickupAddress, string? DropoffAddress);

/// <summary>운영 배차용 등록의 최소값 검사. 초안·Simulation에는 호출자가 적용하지 않습니다.</summary>
public static class FreightCargoRegistrationPolicy
{
    public static IReadOnlyList<string> MissingFields(FreightCargoRegistrationInput cargo)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(cargo.CargoType)) missing.Add("CargoType");
        if (cargo.GrossWeightKg is not > 0m) missing.Add("GrossWeightIncludingPackagingKg");
        if (cargo.LengthMm is not > 0) missing.Add("LengthMm");
        if (cargo.WidthMm is not > 0) missing.Add("WidthMm");
        if (cargo.HeightMm is not > 0) missing.Add("HeightMm");
        if (string.IsNullOrWhiteSpace(cargo.PickupAddress)) missing.Add("PickupAddress");
        if (string.IsNullOrWhiteSpace(cargo.DropoffAddress)) missing.Add("DropoffAddress");
        return missing;
    }
}
