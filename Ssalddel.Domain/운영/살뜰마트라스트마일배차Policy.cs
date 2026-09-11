namespace 살뜰.도메인.운영;

/// <summary>
/// 살뜰마트 출고 준비 상태와 현재 운영 부하를 같은 입력으로 받으면 같은 전략을 반환하는 순수 정책입니다.
/// 정책은 후보 등록과 기사 제안 가능성만 판단하며 배차·기사 배정을 확정하지 않습니다.
/// </summary>
public sealed class 살뜰마트라스트마일배차Policy
{
    public const string PolicyRevision = "ssalddel-mart-last-mile-dispatch-policy.v1";
    public const int 내부목표분 = 40;
    public const int 고객약속상한분 = 50;
    public const int 조기기사제안허용분 = 8;

    public 살뜰마트라스트마일배차판정 Evaluate(살뜰마트라스트마일배차입력 input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var targetAt = input.주문확정시각Utc.AddMinutes(내부목표분);
        var promiseAt = input.주문확정시각Utc.AddMinutes(고객약속상한분);
        var remainingTargetMinutes = RemainingMinutes(targetAt, input.판단시각Utc);
        var remainingPromiseMinutes = RemainingMinutes(promiseAt, input.판단시각Utc);

        if (input.수락된기사배정있음)
        {
            return new 살뜰마트라스트마일배차판정(
                살뜰마트라스트마일배차전략Codes.수락배정유지,
                후보등록: false,
                지금기사제안: false,
                전략변경허용: false,
                최대묶음주문수: 1,
                targetAt,
                promiseAt,
                remainingTargetMinutes,
                remainingPromiseMinutes,
                [살뜰마트라스트마일배차사유Codes.수락배정불변],
                PolicyRevision);
        }

        var candidateEligible = 살뜰마트라스트마일준비단계Codes.후보등록가능(input.준비단계Code);
        var readyForPickup = input.준비단계Code == 살뜰마트라스트마일준비단계Codes.픽업준비완료;
        var expectedReadyDelay = input.픽업준비예정시각Utc.HasValue
            ? input.픽업준비예정시각Utc.Value - input.판단시각Utc
            : (TimeSpan?)null;

        var reasons = new List<string>();
        string strategy;
        int maxBundleOrderCount;

        if (remainingPromiseMinutes <= 10)
        {
            strategy = 살뜰마트라스트마일배차전략Codes.오래된주문회복;
            maxBundleOrderCount = 1;
            reasons.Add(살뜰마트라스트마일배차사유Codes.고객약속임박);
        }
        else if (!readyForPickup && (!input.픽업준비예정시각Utc.HasValue || input.픽업준비예정시각Utc > targetAt))
        {
            strategy = 살뜰마트라스트마일배차전략Codes.준비지연보호;
            maxBundleOrderCount = 1;
            reasons.Add(input.픽업준비예정시각Utc.HasValue
                ? 살뜰마트라스트마일배차사유Codes.내부목표이후준비예정
                : 살뜰마트라스트마일배차사유Codes.준비예정시각불명);
        }
        else if (input.배차가능기사수 is 0
                 || input.배차가능기사수.HasValue
                 && input.배차대기주문수 > Math.Max(1, input.배차가능기사수.Value * 3))
        {
            strategy = 살뜰마트라스트마일배차전략Codes.기사부족;
            maxBundleOrderCount = 1;
            reasons.Add(살뜰마트라스트마일배차사유Codes.기사공급부족);
        }
        else if (input.배차대기주문수 >= 6)
        {
            strategy = 살뜰마트라스트마일배차전략Codes.피크즉시;
            maxBundleOrderCount = 1;
            reasons.Add(살뜰마트라스트마일배차사유Codes.주문적체);
        }
        else if (input.함께묶을준비주문수 >= 2 && remainingTargetMinutes >= 15)
        {
            strategy = 살뜰마트라스트마일배차전략Codes.묶음효율;
            maxBundleOrderCount = Math.Min(3, input.함께묶을준비주문수);
            reasons.Add(살뜰마트라스트마일배차사유Codes.묶음여유);
        }
        else
        {
            strategy = 살뜰마트라스트마일배차전략Codes.균형;
            maxBundleOrderCount = input.함께묶을준비주문수 >= 2 ? 2 : 1;
            reasons.Add(살뜰마트라스트마일배차사유Codes.기본균형);
        }

        var urgentStrategy = strategy is
            살뜰마트라스트마일배차전략Codes.피크즉시 or
            살뜰마트라스트마일배차전략Codes.오래된주문회복 or
            살뜰마트라스트마일배차전략Codes.기사부족 or
            살뜰마트라스트마일배차전략Codes.준비지연보호;
        var offerEarly = candidateEligible
                         && urgentStrategy
                         && expectedReadyDelay.HasValue
                         && expectedReadyDelay.Value >= TimeSpan.Zero
                         && expectedReadyDelay.Value <= TimeSpan.FromMinutes(조기기사제안허용분);

        return new 살뜰마트라스트마일배차판정(
            strategy,
            candidateEligible,
            candidateEligible && (readyForPickup || offerEarly),
            전략변경허용: true,
            maxBundleOrderCount,
            targetAt,
            promiseAt,
            remainingTargetMinutes,
            remainingPromiseMinutes,
            reasons.ToArray(),
            PolicyRevision);
    }

    private static void Validate(살뜰마트라스트마일배차입력 input)
    {
        if (input.주문확정시각Utc.Kind != DateTimeKind.Utc || input.판단시각Utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("주문 확정·판단 시각은 UTC여야 합니다.", nameof(input));
        }

        if (input.픽업준비예정시각Utc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException("픽업 준비 예정 시각은 UTC여야 합니다.", nameof(input));
        }

        if (!살뜰마트라스트마일준비단계Codes.IsKnown(input.준비단계Code))
        {
            throw new ArgumentException("알 수 없는 마트 라스트마일 준비 단계입니다.", nameof(input));
        }

        if (input.배차대기주문수 < 0 || input.배차가능기사수 < 0 || input.함께묶을준비주문수 < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input), "운영 부하 수치는 음수일 수 없습니다.");
        }
    }

    private static int RemainingMinutes(DateTime deadlineUtc, DateTime asOfUtc)
        => (int)Math.Floor((deadlineUtc - asOfUtc).TotalMinutes);
}

public sealed record 살뜰마트라스트마일배차입력(
    DateTime 주문확정시각Utc,
    DateTime 판단시각Utc,
    string 준비단계Code,
    DateTime? 픽업준비예정시각Utc,
    int 배차대기주문수,
    int? 배차가능기사수,
    int 함께묶을준비주문수,
    bool 수락된기사배정있음);

public sealed record 살뜰마트라스트마일배차판정(
    string 전략Code,
    bool 후보등록,
    bool 지금기사제안,
    bool 전략변경허용,
    int 최대묶음주문수,
    DateTime 내부목표시각Utc,
    DateTime 고객약속상한시각Utc,
    int 내부목표잔여분,
    int 고객약속잔여분,
    IReadOnlyList<string> 사유Codes,
    string 정책Revision);

public static class 살뜰마트라스트마일준비단계Codes
{
    public const string 피킹대기 = "PickingPending";
    public const string 피킹시작 = "PickingStarted";
    public const string 피킹완료 = "PickingCompleted";
    public const string 포장중 = "PackingInProgress";
    public const string 픽업준비완료 = "ReadyForPickup";

    public static bool IsKnown(string value)
        => value is 피킹대기 or 피킹시작 or 피킹완료 or 포장중 or 픽업준비완료;

    public static bool 후보등록가능(string value)
        => value is 피킹시작 or 피킹완료 or 포장중 or 픽업준비완료;
}

public static class 살뜰마트라스트마일배차전략Codes
{
    public const string 피크즉시 = "PeakImmediate";
    public const string 균형 = "Balanced";
    public const string 묶음효율 = "Consolidation";
    public const string 오래된주문회복 = "OldestFirstRecovery";
    public const string 기사부족 = "CourierScarcity";
    public const string 준비지연보호 = "PreparationDelayProtection";
    public const string 수락배정유지 = "AcceptedAssignmentPreserved";
}

public static class 살뜰마트라스트마일배차사유Codes
{
    public const string 고객약속임박 = "PromiseDeadlineAtRisk";
    public const string 내부목표이후준비예정 = "ExpectedReadyAfterInternalTarget";
    public const string 준비예정시각불명 = "ExpectedReadyUnknown";
    public const string 기사공급부족 = "CourierSupplyScarce";
    public const string 주문적체 = "PendingOrderBacklog";
    public const string 묶음여유 = "ConsolidationSlackAvailable";
    public const string 기본균형 = "BalancedDefault";
    public const string 수락배정불변 = "AcceptedAssignmentMustNotChange";
}
