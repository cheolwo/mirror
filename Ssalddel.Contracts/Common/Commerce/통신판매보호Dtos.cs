using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Common.Commerce;

public static class 판매자유형Codes
{
    public const string 미확인 = "Unknown";
    public const string 사업자 = "Business";
    public const string 개인 = "Individual";
    public static bool 유효(string? value) => value is 사업자 or 개인;
}

public sealed class 운영주체정보
{
    public string 상호 { get; set; } = string.Empty;
    public string 대표자 { get; set; } = string.Empty;
    public string 주소 { get; set; } = string.Empty;
    public string 전화번호 { get; set; } = string.Empty;
    public string 이메일 { get; set; } = string.Empty;
    public string 사업자등록번호 { get; set; } = string.Empty;
    public string 개인정보담당연락처 { get; set; } = string.Empty;
}

public sealed record 법적안내Section(string Title, string Text);
public sealed record 법적안내문서(string Code, string Title, IReadOnlyList<법적안내Section> Sections);
public sealed class 통신판매안내Response
{
    public string Version { get; set; } = string.Empty;
    public string IntermediaryNotice { get; set; } = string.Empty;
    public 운영주체정보 OperatorInfo { get; set; } = new();
    public IReadOnlyList<법적안내문서> Documents { get; set; } = [];
    public bool IsOperationalReady { get; set; }
    public IReadOnlyList<string> MissingRequirements { get; set; } = [];
}

public sealed class 판매자등록Request
{
    public Guid ClientRequestId { get; set; }
    public long ExpectedRevision { get; set; }
    public string SellerKind { get; set; } = 판매자유형Codes.미확인;
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName, "판매자 등록 표시명")]
    public string DisplayName { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName, "사업자 대표 확인")]
    public string RepresentativeName { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DetailedAddress, "사업자 신원 확인 주소")]
    public string BusinessAddress { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "개인 판매자 비공개 연락처 확인")]
    public string PhoneNumber { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.Email, "개인 판매자 비공개 이메일 확인")]
    public string Email { get; set; } = string.Empty;
    public string BusinessRegistrationNumber { get; set; } = string.Empty;
    public string MailOrderRegistrationNumber { get; set; } = string.Empty;
    public string MailOrderRegistrationExemptionReason { get; set; } = string.Empty;
    public string NoticeVersion { get; set; } = string.Empty;
    public bool CollectionConsentAccepted { get; set; }
}

public sealed class 판매자확인Request
{
    public long ExpectedRevision { get; set; }
    public string VerificationReference { get; set; } = string.Empty;
}

public sealed class 판매자확인Response
{
    public long Revision { get; set; }
    public string SellerKind { get; set; } = 판매자유형Codes.미확인;
    public string DisplayName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "Unverified";
    public bool PhoneVerified { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsAdult { get; set; }
    public DateTime? VerifiedAtUtc { get; set; }
    public DateTime? VerificationExpiresAtUtc { get; set; }
    public IReadOnlyList<string> MissingRequirements { get; set; } = [];
}

/// <summary>공개 사업자 정보와 개인의 비공개 신원은 서로 다른 계약으로 다룹니다.</summary>
public sealed class 판매자공개정보Response
{
    public long Revision { get; set; }
    public string SellerKind { get; set; } = 판매자유형Codes.미확인;
    public string DisplayName { get; set; } = string.Empty;
    public string StatusCode { get; set; } = "Unverified";
    public string? RepresentativeName { get; set; }
    public string? BusinessAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? BusinessRegistrationNumber { get; set; }
    public string? MailOrderRegistrationNumber { get; set; }
    public string? MailOrderRegistrationExemptionReason { get; set; }
}

public sealed class 거래보호확인Request
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? SellerRevision { get; set; }
    public string NoticeVersion { get; set; } = string.Empty;
    public bool NoticeAccepted { get; set; }
}

public sealed class 거래보호상태Response
{
    public string StatusCode { get; set; } = "Unverified";
    public bool CanTransact { get; set; }
    public string NoticeVersion { get; set; } = string.Empty;
    public IReadOnlyList<string> MissingRequirements { get; set; } = [];
}
