using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Contracts.Common.Dispatch;

public static class 운영배차수신의사Code
{
    public const string On = "On";
    public const string Off = "Off";
}

public static class 운영배차실효상태Code
{
    public const string 배차가능 = "Eligible";
    public const string 서버일시정지 = "PausedByServer";
    public const string 조건부적합 = "Ineligible";
    public const string 연결확인불가 = "ConnectionUnavailable";
}

public static class 운영배차주체역할Code
{
    public const string 주문자 = "Orderer";
    public const string 음식점 = "Restaurant";
    public const string 기사 = "Driver";
    public const string 플랫폼 = "Platform";
    public const string 시스템 = "System";
}

public static class 운영배차사건유형Code
{
    public const string 제안전달 = "OfferPresented";
    // 앱이 활성 제안을 표시했다는 수신 확인이며 사람의 읽음이나 수락을 의미하지 않는다.
    public const string 제안표시확인 = "OfferDisplayed";
    public const string 수락 = "Accepted";
    public const string 거절 = "Rejected";
    public const string 무응답 = "NoResponse";
    public const string 완료 = "Completed";
    public const string 중단 = "Interrupted";
    public const string 취소 = "Cancelled";
    public const string 균형건수반환 = "BalanceReleased";
    public const string 수신의사변경 = "IntentChanged";
    public const string 실효상태변경 = "EffectiveStateChanged";
    public const string 책임판정 = "ResponsibilityReviewed";
}

public static class 운영배차미응답Code
{
    public const string 앱표시확인 = "ApplicationOfferDisplayed";
    public const string 표시제안만료 = "DisplayedOfferExpired";
    public const string 기사명시재개 = "DriverExplicitNoResponseResume";
    public static string 라운드상태(int round) => $"RecommendationRound:{round}";
}

public static class 운영배차제안유효성Code
{
    public const string 유효 = "Valid";
    public const string 조건부적합 = "Ineligible";
    public const string 전달실패 = "DeliveryFailed";
    public const string 보호사유 = "ProtectedReason";
    public const string 해당없음 = "NotApplicable";
}

public static class 운영배차책임Code
{
    public const string 기사 = "Driver";
    public const string 음식점 = "Restaurant";
    public const string 주문자 = "Orderer";
    public const string 플랫폼 = "Platform";
    public const string 시스템 = "System";
    public const string 보호대상 = "Protected";
    public const string 미확정 = "Unresolved";
    public const string 해당없음 = "NotApplicable";
}

public static class 운영배차거절사유Code
{
    public const string 거리부담 = "DistanceBurden";
    public const string 시간제약 = "TimeConstraint";
    public const string 수행용량부족 = "CapacityUnavailable";
    public const string 개인사정 = "PersonalCircumstance";
    public const string 기타 = "Other";
    public const string 미입력 = "NotProvided";

    private static readonly HashSet<string> 허용목록 = new(StringComparer.Ordinal)
    {
        거리부담,
        시간제약,
        수행용량부족,
        개인사정,
        기타,
        미입력
    };

    public static string 정규화(string? code)
    {
        var clean = code?.Trim();
        if (string.IsNullOrWhiteSpace(clean))
        {
            return 미입력;
        }

        if (!허용목록.Contains(clean))
        {
            throw new ArgumentException("지원하지 않는 배차 거절 사유 코드입니다.", nameof(code));
        }

        return clean;
    }
}

public static class 운영배차음식점거절사유Code
{
    public const string 재고부족 = "InventoryUnavailable";
    public const string 영업시간외 = "OutsideBusinessHours";
    public const string 조리용량부족 = "KitchenCapacityUnavailable";
    public const string 서비스장애 = "ServiceIncident";
    public const string 음식점판단 = "RestaurantDecision";
    public const string 기타 = "Other";

    private static readonly HashSet<string> 허용목록 = new(StringComparer.Ordinal)
    {
        재고부족,
        영업시간외,
        조리용량부족,
        서비스장애,
        음식점판단,
        기타
    };

    public static string 정규화(string? code)
    {
        var clean = code?.Trim();
        if (string.IsNullOrWhiteSpace(clean))
        {
            // 기존 앱은 자유 서술 사유만 보내므로 호환 요청은 음식점의 일반 판단으로 분류한다.
            return 기타;
        }

        if (!허용목록.Contains(clean))
        {
            throw new ArgumentException("지원하지 않는 음식점 주문 거절 사유 코드입니다.", nameof(code));
        }

        return clean;
    }

    public static bool 유효제안분모제외대상(string code)
        => code is 재고부족 or 영업시간외 or 조리용량부족;
}

public static class 운영배차주문자취소사유Code
{
    public const string 단순변심 = "ChangedMind";
    public const string 중복주문 = "DuplicateOrder";
    public const string 주소문제 = "DeliveryAddressIssue";
    public const string 기타 = "Other";

    private static readonly HashSet<string> 허용목록 = new(StringComparer.Ordinal)
    {
        단순변심,
        중복주문,
        주소문제,
        기타
    };

    public static string 정규화(string? code)
    {
        var clean = code?.Trim();
        if (string.IsNullOrWhiteSpace(clean) || !허용목록.Contains(clean))
        {
            throw new ArgumentException("지원하지 않는 주문자 취소 사유 코드입니다.", nameof(code));
        }

        return clean;
    }
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Contract,
    "기사의 신규 배차 수신 의사와 서버의 실제 배차 가능 상태를 분리해 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 10,
    StepKey = "contract.operational-dispatch-availability",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "기사 의사 ON은 배차 확정이 아니며 연결 오류가 기사 의사를 OFF로 바꾸지 않는다.")]
public sealed class 운영배차수신상태Dto
{
    public string 주체Id { get; set; } = string.Empty;
    public string 수신의사Code { get; set; } = 운영배차수신의사Code.Off;
    public string 실효상태Code { get; set; } = 운영배차실효상태Code.조건부적합;
    public string 실효사유Code { get; set; } = string.Empty;
    public DateTimeOffset? 수신의사변경시각Utc { get; set; }
    // 최초 운행 ON의 조건부 적용에만 사용하는 서버 원장 판본입니다. 사건이 없으면 null입니다.
    public string? 수신의사판본Token { get; set; }
    public DateTimeOffset? 실효상태변경시각Utc { get; set; }
    public DateTimeOffset 서버관측시각Utc { get; set; }
    public bool 미응답자동중지여부 { get; set; }
    public int 연속미응답횟수 { get; set; }
    public DateTimeOffset? 미응답자동중지시각Utc { get; set; }
}

public sealed class 운영배차수신의사변경요청
{
    public Guid 클라이언트요청Id { get; set; }
    public string 수신의사Code { get; set; } = 운영배차수신의사Code.Off;
    // 운행 시작의 자동 ON은 false를 유지한다. 본인의 재개 토글 ON만 true를 보낸다.
    public bool 미응답자동중지해제 { get; set; }
    // 기본 false: 기존 수동 ON/OFF 계약을 유지합니다. true는 ON + 재개 false만 허용합니다.
    public bool 최초On조건부적용 { get; set; }
    public string? 예상수신의사판본Token { get; set; }
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Contract,
    "역할별 배차 제안·수락·거절·완료·중단과 책임 귀속을 같은 사건 형식으로 전달한다.",
    Effects = SsalddelCodeEffect.None,
    FlowOrder = 20,
    StepKey = "contract.operational-dispatch-activity",
    ExecutionStage = SsalddelCodeExecutionStage.Definition,
    Boundary = "계약은 사건 의미만 전달하며 운영 원장 저장, 배차 순위 변경 또는 Simulation 상태 변경을 수행하지 않는다.")]
public sealed class 운영배차활동사건Dto
{
    public string 사건StableId { get; set; } = string.Empty;
    public DateTimeOffset 발생시각Utc { get; set; }
    // 서버 원장의 저장 순서 근거입니다. 기존 사건/메모리 구현에는 없을 수 있으며 외부 입력의 시각을 신뢰하지 않습니다.
    [System.Text.Json.Serialization.JsonIgnore]
    public DateTimeOffset? 원장기록시각Utc { get; set; }
    public string 운영시장시간대Id { get; set; } = 운영배차시장시간대Code.대한민국;
    public string? ShiftId { get; set; }
    public string? 제안Id { get; set; }
    public string? 주문Id { get; set; }
    public string? 업무시도Id { get; set; }
    public string 주체Id { get; set; } = string.Empty;
    public string 주체역할Code { get; set; } = 운영배차주체역할Code.시스템;
    public string 사건유형Code { get; set; } = string.Empty;
    public string 제안유효성Code { get; set; } = 운영배차제안유효성Code.해당없음;
    public string 책임Code { get; set; } = 운영배차책임Code.해당없음;
    public string 사유Code { get; set; } = string.Empty;
    public string 상태값Code { get; set; } = string.Empty;
    public int 지표단위수 { get; set; } = 1;
}

public static class 운영배차시장시간대Code
{
    public const string 대한민국 = "Asia/Seoul";
}

public sealed class 운영배차일별지표Dto
{
    public string 운영시장날짜 { get; set; } = string.Empty;
    public int 유효제안수 { get; set; }
    public int 수락수 { get; set; }
    public int 거절수 { get; set; }
    public int 무응답수 { get; set; }
    public int 완료수 { get; set; }
    public int 기사책임중단수 { get; set; }
    public int 외부책임취소수 { get; set; }
    public int 보호중단수 { get; set; }
    public int 균형점유수 { get; set; }
    public int 균형반환수 { get; set; }
    public int 균형순점유수 => Math.Max(0, 균형점유수 - 균형반환수);
    public decimal? 수락률 { get; set; }
    public decimal? 거절률 { get; set; }
    public decimal? 수락후완료율 { get; set; }
}

public sealed class 운영배차단기지표Dto
{
    public string 운영시장시간대Id { get; set; } = 운영배차시장시간대Code.대한민국;
    public DateTimeOffset 생성시각Utc { get; set; }
    public 운영배차일별지표Dto 오늘 { get; set; } = new();
    public 운영배차일별지표Dto 어제 { get; set; } = new();
    public 운영배차일별지표Dto 그제 { get; set; } = new();
}
