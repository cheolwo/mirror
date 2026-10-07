namespace Ssalddel.Contracts.Driver.Transport;

using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Privacy;

public class 기사운송요약응답
{
    public bool 개인정보제공보류 { get; set; }
    public long Id { get; set; }
    public string 운송번호 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public string 출발지 { get; set; } = string.Empty;
    public string 도착지 { get; set; } = string.Empty;
    public string 기사_운송자 { get; set; } = string.Empty;
    public DateTime? 출발_픽업 { get; set; }
    public DateTime? 도착 { get; set; }
    public decimal? 운임 { get; set; }
    public decimal? 예상거리Km { get; set; }
    public string? 거리계산방식 { get; set; }
    public string 결제방식 { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.DisplayName,
        "배정된 화물 운송의 상차 담당자 식별",
        ProtectionNote = "배정 기사 업무 조회로 제한하며 공개 화물 요약에는 포함하지 않음")]
    public string 상차담당자명 { get; set; } = string.Empty;
    [IsmsPProtectedData(PersonalDataFieldKey.PhoneNumber,
        "배정된 화물 운송의 상차 담당자 연락",
        ProtectionNote = "배정 기사 업무 조회로 제한하며 공개 화물 요약에는 포함하지 않음")]
    public string 상차연락처 { get; set; } = string.Empty;
    public DateTime? 상차시간창시작일시 { get; set; }
    public DateTime? 상차시간창종료일시 { get; set; }
    public DateTime? 하차시간창시작일시 { get; set; }
    public DateTime? 하차시간창종료일시 { get; set; }
    public string 수령자명 { get; set; } = string.Empty;
    public string 수령자연락처 { get; set; } = string.Empty;
    public string 전달요청 { get; set; } = string.Empty;
    public bool 인수증필요 { get; set; }
    public bool 인수증서명필수 { get; set; }
    public bool 예외신고됨 { get; set; }
    public string 최근예외단계 { get; set; } = string.Empty;
    public string 최근예외코드 { get; set; } = string.Empty;
    public string 최근예외메시지 { get; set; } = string.Empty;
    public string 다음행동안내 { get; set; } = string.Empty;
    public bool 관리자확인필요 { get; set; }
    /// <summary>관리자 검토나 정산 보류와 별개인 현재 전체 운송 보류입니다.</summary>
    public bool 운송진행보류 { get; set; }
    public bool 정산보류 { get; set; }
    public IReadOnlyList<기사운송예외검토응답> 예외검토목록 { get; set; } = [];
    /// <summary>null은 구판 응답입니다. 빈 목록은 서버가 현재 수행 행동을 허용하지 않는다는 뜻입니다.</summary>
    public IReadOnlyList<string>? 가능한행동 { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed record 기사운송예외검토응답(
    string 사건StableId, string 단계Code, string 예외Code, string 상태Code,
    string 현재담당Code, string 업무통제상태Code, string 보류범위Code, bool 정산보류,
    long Revision, DateTime UpdatedAt);

public sealed class 기사화물운송작업공간응답
{
    public const string CurrentRuleRevision = "freight-driver-workspace.r2";

    public string RuleRevision { get; set; } = CurrentRuleRevision;
    public IReadOnlyList<기사운송요약응답> 활성운송목록 { get; set; } = [];
    public 기사운송요약응답? 다음행동운송 { get; set; }
    public string 다음행동 { get; set; } = "대기";
    public IReadOnlyList<기사화물경로정차응답> 권장경로정차목록 { get; set; } = [];
    public string 일정검증상태 { get; set; } = "수락시재검증";
    public string 적재검증상태 { get; set; } = "수락시재검증";
    public IReadOnlyList<string> WarningCodes { get; set; } = [];
    public IReadOnlyList<string> BlockCodes { get; set; } = [];
    public 화물연속배차상태Dto 연속배차 { get; set; } = new();
    public IReadOnlyList<화물운송시간약속Dto> 시간약속목록 { get; set; } = [];
    public 화물경로위험Dto 현재경로위험 { get; set; } = new();
}

public sealed class 기사화물경로정차응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public string 단계 { get; set; } = string.Empty;
    public string 주소 { get; set; } = string.Empty;
    public int 순서 { get; set; }
    public DateTime? 시간창종료일시 { get; set; }
    public bool 좌표근거있음 { get; set; }
    public bool 시간창근거있음 { get; set; }
}

public sealed class 기사운송상세응답 : 기사운송요약응답
{
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate,
        "배정된 화물 운송의 상차 장소 확인",
        ProtectionNote = "기사 소유 운송과 명시적으로 연결된 의뢰만 제공하며 생활배송 동의 보류 시 비공개")]
    public decimal? 픽업위도 { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate,
        "배정된 화물 운송의 상차 장소 확인",
        ProtectionNote = "기사 소유 운송과 명시적으로 연결된 의뢰만 제공하며 생활배송 동의 보류 시 비공개")]
    public decimal? 픽업경도 { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate,
        "배정된 화물 운송의 하차 장소 확인",
        ProtectionNote = "기사 소유 운송과 명시적으로 연결된 의뢰만 제공하며 생활배송 동의 보류 시 비공개")]
    public decimal? 하차위도 { get; set; }
    [IsmsPProtectedData(PersonalDataFieldKey.LocationCoordinate,
        "배정된 화물 운송의 하차 장소 확인",
        ProtectionNote = "기사 소유 운송과 명시적으로 연결된 의뢰만 제공하며 생활배송 동의 보류 시 비공개")]
    public decimal? 하차경도 { get; set; }
    public string 첨부Json { get; set; } = string.Empty;
    public string 메모 { get; set; } = string.Empty;
}

public sealed class 기사운송상태변경응답
{
    public long Id { get; set; }
    public string 운송번호 { get; set; } = string.Empty;
    public string 상태 { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class 기사운송상차완료요청
{
    public string? 상차사진ObjectName { get; set; }
    public string? 상차사진Url { get; set; }
    public string? 인수증증빙방식 { get; set; }
    public string? 인수자명 { get; set; }
    public string? 인수자소속 { get; set; }
    public string? 인수자서명 { get; set; }
    public string? 기사서명 { get; set; }
    public bool 인수증확인완료 { get; set; }
    public bool 인수증서명생략확인 { get; set; }
    public string? 인수증서명생략사유 { get; set; }
}

public sealed class 기사운송하차완료요청
{
    public string? 하차사진ObjectName { get; set; }
    public string? 하차사진Url { get; set; }
}

public sealed class 기사운송문제신고요청
{
    public string? 단계 { get; set; }
    public string? 예외코드 { get; set; }
    public string 사유 { get; set; } = string.Empty;
    public string? 메모 { get; set; }
    public string? 증빙ObjectName { get; set; }
    public string? 증빙Url { get; set; }
    public bool 관리자확인요청 { get; set; }
    public int? 정상확인수량 { get; set; }
    public int? 영향수량 { get; set; }
    public bool 현장진행불가 { get; set; }
}
