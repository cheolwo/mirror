namespace Ssalddel.Contracts.Driver.Profile;

/// <summary>법정 적격 확정이나 공식기관 조회가 아닌 수단별 비공개 자료 검토 준비 계약.</summary>
public static class 운송자격Codes
{
    public const string PolicyVersion = "transport-readiness-r1";
    public const string NotSubmitted = "NotSubmitted", Pending = "Pending", Valid = "Valid",
        Rejected = "Rejected", Expired = "Expired", Suspended = "Suspended";
    public const string Motorcycle = "Motorcycle", FreightVehicle = "FreightVehicle";
    public const string DocumentReview = "AdministratorDocumentReview";
    public const string OfficialLookup = "OfficialAuthorityLookup";
    public const string DriverLicense = "DriverLicense", FreightDriver = "FreightDriverQualification",
        OperatorPermit = "TransportOperatorPermit", OperatorRelationship = "OperatorRelationship",
        VehicleRegistration = "VehicleRegistration", VehicleUse = "VehicleUseRelationship",
        CompulsoryInsurance = "CompulsoryInsurance", DeliveryCoverage = "PaidDeliveryCoverage";
    public static IReadOnlyList<string> RequiredFor(string vehicleKind) => vehicleKind switch
    {
        Motorcycle => [DriverLicense, VehicleRegistration, VehicleUse, CompulsoryInsurance, DeliveryCoverage],
        FreightVehicle => [DriverLicense, FreightDriver, OperatorPermit, OperatorRelationship,
            VehicleRegistration, VehicleUse, CompulsoryInsurance, DeliveryCoverage],
        _ => []
    };
}

public sealed record 운송자격근거Dto
{
    public string RequirementCode { get; init; } = "";
    public string PrivateEvidenceReference { get; init; } = "";
    public string Summary { get; init; } = "";
    public DateTime? ValidFromUtc { get; init; }
    public DateTime? ValidUntilUtc { get; init; }
    public bool NoStatedExpiry { get; init; }
    public string VerificationLevel { get; init; } = "Submitted";
    public string ReviewReason { get; init; } = "";
}

public sealed record 운송자격제출요청
{
    public int ExpectedRevision { get; init; }
    public string PolicyVersion { get; init; } = 운송자격Codes.PolicyVersion;
    public string VehicleKind { get; init; } = "";
    public string VehicleProfileValue { get; init; } = "";
    public string ActualVehicleReference { get; init; } = "";
    public string TransportOperatorReference { get; init; } = "";
    public IReadOnlyList<운송자격근거Dto> Evidence { get; init; } = [];
}

public sealed record 운송자격검토요청
{
    public int ExpectedRevision { get; init; }
    public string Decision { get; init; } = "";
    public string ReviewReason { get; init; } = "";
    public string ActualVehicleReference { get; init; } = "";
    public DateTime? RecheckAtUtc { get; init; }
    public IReadOnlyList<운송자격근거Dto> Evidence { get; init; } = [];
}

public sealed record 운송자격평가결과(
    bool CanReceiveNewWork, string ReasonCode, string Message, string Status,
    int Revision = 0, bool IsSimulationBypass = false);

public sealed record 운송자격상세Dto
{
    public string DriverId { get; init; } = "";
    public string Status { get; init; } = 운송자격Codes.NotSubmitted;
    public int Revision { get; init; }
    public string PolicyVersion { get; init; } = 운송자격Codes.PolicyVersion;
    public string VehicleKind { get; init; } = "";
    public string VehicleProfileValue { get; init; } = "";
    public string ActualVehicleReference { get; init; } = "";
    public string TransportOperatorReference { get; init; } = "";
    public DateTime? SubmittedAtUtc { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public DateTime? RecheckAtUtc { get; init; }
    public string? ReviewerId { get; init; }
    public string ReviewReason { get; init; } = "";
    public IReadOnlyList<string> RequiredEvidenceCodes { get; init; } = [];
    public IReadOnlyList<운송자격근거Dto> Evidence { get; init; } = [];
    public bool OfficialAuthorityVerified { get; init; }
    public 운송자격평가결과 Eligibility { get; init; } = new(false,
        "Qualification.NotSubmitted", "운송 자격 검토 자료를 제출해 주세요.", 운송자격Codes.NotSubmitted);
}
