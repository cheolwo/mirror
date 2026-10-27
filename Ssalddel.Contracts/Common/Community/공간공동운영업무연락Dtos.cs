using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Contracts.Common.Community;

/// <summary>현재 원천과 전문 조회 권한으로 확인한 등록 업무 연락처만 반환합니다. 전화 발신·새 등록은 수행하지 않습니다.</summary>
public sealed class 공간공동운영업무연락Response
{
    public string LedgerId { get; set; } = "";
    public string CycleId { get; set; } = "";
    public string StageKey { get; set; } = "";
    public long Revision { get; set; }
    public DateTimeOffset AsOfUtc { get; set; }
    public string StatusCode { get; set; } = "not-linked";
    public IReadOnlyList<공간공동운영업무연락Dto> Contacts { get; set; } = [];
    public IReadOnlyList<공간공동운영업무연락원천Dto> Sources { get; set; } = [];
}

public sealed class 공간공동운영업무연락Dto
{
    public string ContactKey { get; set; } = "";
    public string SourceNodeKey { get; set; } = "";
    public string PurposeCode { get; set; } = "";
    public string Label { get; set; } = "";
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName, "권한이 확인된 등록 업무 연락처의 담당 표시명 확인")]
    public string DisplayName { get; set; } = "";
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber, "현재 업무 원천과 전문 조회 권한이 확인된 등록 현장·창고 연락처 확인",
        ProtectionNote = "명시적 비공개 조회로만 제공하며 주소·계정 연락처를 추가 조회하거나 발신하지 않음")]
    public string PhoneNumber { get; set; } = "";
    public long SourceRevision { get; set; }
    public string Href { get; set; } = "";
}

public sealed class 공간공동운영업무연락원천Dto
{
    public string SourceNodeKey { get; set; } = "";
    public string KindCode { get; set; } = "";
    public string StatusCode { get; set; } = "unsupported";
}
