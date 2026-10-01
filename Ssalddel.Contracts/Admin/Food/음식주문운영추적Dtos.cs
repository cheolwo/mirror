namespace Ssalddel.Contracts.Admin.Food;

public static class 음식주문운영추적상태코드
{
    public const string 정상 = "정상";
    public const string 진행중 = "진행중";
    public const string 주의 = "주의";
    public const string 복구필요 = "복구필요";
    public const string 완료 = "완료";
    public const string 종료 = "종료";
    public const string 미시작 = "미시작";
    public const string 해당없음 = "해당없음";
}

public static class 음식배달운영생명주기단계Codes
{
    public const string 기사확보대기 = "AwaitingDriverAssignment";
    public const string 음식점응답대기 = "RestaurantDecision";
    public const string 조리배차병행 = "CookingAndDispatch";
    public const string 픽업인계 = "PickupHandoff";
    public const string 배송 = "Delivery";
    public const string 수령확인대기 = "ReceiptConfirmation";
    public const string 종료 = "Closed";
}

public static class 음식배달운영책임주체Codes
{
    public const string 주문자 = "Orderer";
    public const string 음식점 = "RestaurantOperator";
    public const string 배차Engine = "FoodDeliveryDispatchEngine";
    public const string 음식배달기사 = "FoodDeliveryDriver";
    public const string 플랫폼운영자 = "PlatformOperator";
}

public static class 음식배달운영주의상태Codes
{
    public const string 정상 = "Normal";
    public const string 주의 = "Attention";
    public const string 자동회복중 = "AutomaticRecovery";
    public const string 운영자확인필요 = "OperatorReviewRequired";
}

public static class 음식배달운영지연상태Codes
{
    public const string 없음 = "None";
    public const string 주의 = "Attention";
    public const string 운영자확인필요 = "OperatorReviewRequired";
}

public static class 음식배달운영업무지연Codes
{
    public const string 음식점준비지연 = "RestaurantPreparationDelay";
    public const string 배달진행지연 = "DeliveryProgressDelay";
}

public static class 음식배달운영자동회복Codes
{
    public const string 기사추천만료 = "DriverRecommendationExpired";
    public const string 최근배달시도중단 = "LatestDeliveryAttemptInterrupted";
}

public static class 음식배달운영기술이상Codes
{
    public const string 배차원장누락 = "DispatchLedgerMissing";
    public const string 배차연결불일치 = "DispatchCorrelationMismatch";
    public const string 공동원장동기화확인필요 = "SharedLedgerOutboxReviewRequired";
    public const string 기사알림확인필요 = "DriverNotificationOutboxReviewRequired";
}

// 기존 소비자의 코드값 호환을 위한 별칭입니다. 자동 회복 신호는 예외 목록이 아니라
// 음식배달운영생명주기조화응답.자동회복Codes에서 읽습니다.
public static class 음식배달운영예외Codes
{
    public const string 음식점준비지연 = 음식배달운영업무지연Codes.음식점준비지연;
    public const string 배달진행지연 = 음식배달운영업무지연Codes.배달진행지연;
    public const string 배차원장누락 = 음식배달운영기술이상Codes.배차원장누락;
    public const string 배차연결불일치 = 음식배달운영기술이상Codes.배차연결불일치;
    public const string 기사추천만료 = 음식배달운영자동회복Codes.기사추천만료;
    public const string 최근배달시도중단 = 음식배달운영자동회복Codes.최근배달시도중단;
    public const string 공동원장동기화확인필요 = 음식배달운영기술이상Codes.공동원장동기화확인필요;
    public const string 기사알림확인필요 = 음식배달운영기술이상Codes.기사알림확인필요;
}

/// <summary>
/// 주문자·음식점·배차·기사 OS가 같은 음식 주문 원장의 어느 단계에서
/// 다음 책임을 가지는지 보여 주는 읽기 전용 판정입니다. 상태 변경 권위는 없습니다.
/// </summary>
public sealed class 음식배달운영생명주기조화응답
{
    public string 현재단계Code { get; set; } = 음식배달운영생명주기단계Codes.음식점응답대기;

    public string 주의상태Code { get; set; } = 음식배달운영주의상태Codes.정상;

    public IReadOnlyList<string> 현재책임주체Codes { get; set; } = [];

    /// <summary>조리·포장 또는 픽업 이후 전달 진행이 늦어진 업무 의미입니다.</summary>
    public IReadOnlyList<string> 업무지연Codes { get; set; } = [];

    /// <summary>배차 엔진이 운영자 개입 없이 다시 처리하는 일시 상태입니다.</summary>
    public IReadOnlyList<string> 자동회복Codes { get; set; } = [];

    /// <summary>원장 연결·Outbox처럼 플랫폼 운영자가 확인해야 하는 기술 이상입니다.</summary>
    public IReadOnlyList<string> 기술이상Codes { get; set; } = [];

    /// <summary>운영자 확인이 필요한 업무 지연과 기술 이상만 포함하는 호환 목록입니다.</summary>
    public IReadOnlyList<string> 예외Codes { get; set; } = [];

    public string 음식점준비지연상태Code { get; set; } = 음식배달운영지연상태Codes.없음;

    public int 음식점준비초과분 { get; set; }

    public string 배달진행지연상태Code { get; set; } = 음식배달운영지연상태Codes.없음;

    public int 배달진행초과분 { get; set; }

    public bool 정상경로조화여부 { get; set; } = true;

    public bool 자동회복대상여부 { get; set; }

    public bool 운영자확인필요여부 { get; set; }
}

public sealed class 음식주문운영추적응답
{
    public string 주문번호 { get; set; } = string.Empty;

    public string 음식점명 { get; set; } = string.Empty;

    public string 주문상태 { get; set; } = string.Empty;

    public string 배차상태 { get; set; } = string.Empty;

    public string 전체상태 { get; set; } = 음식주문운영추적상태코드.진행중;

    public long? 배차대기Id { get; set; }

    public string 운송번호 { get; set; } = string.Empty;

    public string 운송상태 { get; set; } = string.Empty;

    public string 원본의뢰유형 { get; set; } = string.Empty;

    public string 원본의뢰Id { get; set; } = string.Empty;

    public string 커뮤니티원장Id { get; set; } = string.Empty;

    public string 커뮤니티원장상태 { get; set; } = string.Empty;

    public string 추천상태 { get; set; } = 음식주문운영추적상태코드.미시작;

    public int 추천라운드 { get; set; }

    public DateTime? 추천만료시각Utc { get; set; }

    public bool 추천만료됨 { get; set; }

    public 음식배달운영생명주기조화응답 생명주기조화 { get; set; } = new();

    public DateTime 생성시각Utc { get; set; }

    public DateTime 최근변경시각Utc { get; set; }

    public DateTime 조회시각Utc { get; set; }

    public IReadOnlyList<음식주문운영체크포인트응답> 체크포인트 { get; set; } = [];

    public IReadOnlyList<음식주문운영Outbox응답> Outbox목록 { get; set; } = [];

    public IReadOnlyList<음식주문운영이벤트응답> 운송이벤트목록 { get; set; } = [];

    public IReadOnlyList<음식배달시도운영응답> 배달시도목록 { get; set; } = [];

    public IReadOnlyList<string> 경고목록 { get; set; } = [];

    public IReadOnlyList<string> 복구안내목록 { get; set; } = [];
}

public sealed class 음식주문운영체크포인트응답
{
    public string 단계Key { get; set; } = string.Empty;

    public string 단계명 { get; set; } = string.Empty;

    public string 상태 { get; set; } = 음식주문운영추적상태코드.미시작;

    public string 설명 { get; set; } = string.Empty;

    public DateTime? 변경시각Utc { get; set; }
}

public sealed class 음식주문운영Outbox응답
{
    public string 종류 { get; set; } = string.Empty;

    public long OutboxId { get; set; }

    public string 상태 { get; set; } = string.Empty;

    public int 시도횟수 { get; set; }

    public DateTime? 마지막시도시각Utc { get; set; }

    public DateTime 갱신시각Utc { get; set; }

    public bool 재시도예정 { get; set; }

    public bool 운영자확인필요 { get; set; }

    public string 실패요약 { get; set; } = string.Empty;
}

public sealed class 음식주문운영이벤트응답
{
    public long 이벤트Id { get; set; }

    public string 이벤트유형 { get; set; } = string.Empty;

    public DateTime 이벤트시각Utc { get; set; }
}

public sealed class 음식배달시도운영응답
{
    public string 시도StableId { get; set; } = string.Empty;
    public string 제안Id { get; set; } = string.Empty;
    public string 기사Id { get; set; } = string.Empty;
    public int 시도순번 { get; set; }
    public long Revision { get; set; }
    public string 상태Code { get; set; } = string.Empty;
    public DateTime 수락시각Utc { get; set; }
    public DateTime? 표시준비예정시각Utc { get; set; }
    public DateTime? 가게도착시각Utc { get; set; }
    public DateTime? 픽업완료시각Utc { get; set; }
    public DateTime? 중단시각Utc { get; set; }
    public DateTime? 전달완료시각Utc { get; set; }
    public int? 현장대기초 { get; set; }
    public string 중단사유Code { get; set; } = string.Empty;
    public string 책임Code { get; set; } = string.Empty;
    public bool 조리지연재배차여부 { get; set; }
    public string 재조리요청StableId { get; set; } = string.Empty;
    public DateTime? 재조리요청시각Utc { get; set; }
    public bool 유산추정여부 { get; set; }
    public bool 악용확정여부 { get; set; }
    public string 검토사유 { get; set; } = string.Empty;
}
