namespace Ssalddel.Contracts.Common.Operations;

/// <summary>
/// 한 운송 의뢰 안에서 화주가 지정하는 업무 담당 유형입니다.
/// 화주 역할과 담당자 역할은 서로 바뀌지 않습니다.
/// </summary>
public static class 운송업무담당유형Codes
{
    public const string 주담당 = "Primary";
    public const string 보조담당 = "Assistant";

    public static bool 지원하는가(string? value)
        => value is 주담당 or 보조담당;
}

/// <summary>
/// 운송 담당자가 화주를 대신해 실행할 수 있는 업무 단위입니다.
/// 담당자 지정·철회, 화주 변경, 감사 기록 삭제는 어떤 담당자 권한에도 포함하지 않습니다.
/// </summary>
public static class 운송업무권한Codes
{
    public const string 진행조회 = "ProgressView";
    public const string 현장증빙등록 = "FieldEvidenceRegister";
    public const string 연락기록 = "ContactRecord";
    public const string 화물조건수정 = "CargoTermsModify";
    public const string 주소시간연락처수정 = "AddressScheduleContactModify";
    public const string 배차조건수정 = "DispatchConditionsModify";
    public const string 운임정산조건수정 = "FareSettlementTermsModify";
    public const string 인수확인 = "AcceptanceConfirm";
    public const string 정산확인 = "SettlementConfirm";
    public const string 비정상운송처리 = "AbnormalTransportManage";
    public const string 후속업무요청 = "FollowupRequest";

    public static IReadOnlyList<string> 보조담당기본권한 { get; } =
    [
        진행조회,
        현장증빙등록,
        연락기록
    ];

    public static IReadOnlyList<string> 주담당기본권한 { get; } =
    [
        진행조회,
        현장증빙등록,
        연락기록,
        화물조건수정,
        주소시간연락처수정,
        배차조건수정,
        운임정산조건수정,
        인수확인,
        정산확인,
        비정상운송처리,
        후속업무요청
    ];

    private static readonly HashSet<string> Supported =
        new(주담당기본권한, StringComparer.Ordinal);

    public static bool 지원하는가(string? value)
        => !string.IsNullOrWhiteSpace(value) && Supported.Contains(value.Trim());
}

public sealed class 운송업무담당자배정변경요청
{
    public Guid 클라이언트요청Id { get; set; }

    /// <summary>
    /// 배정 이력이 없는 의뢰의 현재 판본은 0입니다.
    /// </summary>
    public long 예상Revision { get; set; }

    public IReadOnlyList<운송업무담당자지정요청> 담당자목록 { get; set; } = [];
}

public sealed class 운송업무담당자지정요청
{
    public string UserId { get; set; } = string.Empty;

    public string 담당유형Code { get; set; } = 운송업무담당유형Codes.보조담당;

    /// <summary>
    /// 주 담당자는 서버가 주 담당 기본 권한으로 정규화합니다.
    /// 보조 담당자는 기본 권한에 여기에 명시한 추가 권한을 합칩니다.
    /// </summary>
    public IReadOnlyList<string> 권한Codes { get; set; } = [];
}

public sealed class 운송업무담당자배정응답
{
    public string 운송의뢰Id { get; set; } = string.Empty;

    public string 화주Id { get; set; } = string.Empty;

    public long Revision { get; set; }

    public bool 암묵적화주주담당여부 { get; set; }

    public bool 담당자관리가능여부 { get; set; }

    public IReadOnlyList<운송업무담당자응답> 담당자목록 { get; set; } = [];
}

public sealed class 운송업무담당자응답
{
    public string UserId { get; set; } = string.Empty;

    public string 담당유형Code { get; set; } = string.Empty;

    public IReadOnlyList<string> 권한Codes { get; set; } = [];

    public string 지정자UserId { get; set; } = string.Empty;

    public DateTime 지정시각Utc { get; set; }
}
