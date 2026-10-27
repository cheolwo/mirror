using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Domain.Notifications;

namespace Ssalddel.Services.OperationsAlerts;

/// <summary>저장된 알림에서 읽기 안내만 분류합니다. 담당 지정·고객 응답·처리 기한이나 업무 상태를 만들지 않습니다.</summary>
public static class OperationsAlertHandlingPolicy
{
    public static readonly TimeSpan SourceFreshnessWindow = TimeSpan.FromMinutes(2);

    public static bool IsSourceCurrent(OperationsAlert alert, DateTime nowUtc)
    {
        var verified = Utc(alert.LastVerifiedAtUtc);
        var now = Utc(nowUtc);
        return verified != default && verified <= now && now - verified <= SourceFreshnessWindow;
    }

    public static OperationsAlertHandlingDto Describe(OperationsAlert alert, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(alert);
        var resolved = alert.Status == "Resolved";
        var knownState = alert.Status is "Open" or "Acknowledged" or "Resolved";
        var current = IsSourceCurrent(alert, nowUtc);
        var state = resolved ? "Resolved" : !knownState || !current ? "SourceRevalidationRequired"
            : alert.Status == "Acknowledged" ? "AcknowledgedUnresolved" : "OpenUnresolved";
        var verified = Utc(alert.LastVerifiedAtUtc);
        DateTime? revalidation = !resolved && verified != default && verified <= Utc(nowUtc)
            && verified <= DateTime.MaxValue - SourceFreshnessWindow
                ? verified + SourceFreshnessWindow : null;
        var dispute = alert.ProblemCode == "TransactionDispute";

        return new(state, "운영 담당자",
            resolved ? "원 업무 결과 확인" : !knownState || !current ? "원장 다시 확인" : NextAction(alert.ProblemCode),
            NextOperatorReviewAtUtc: null, NextRevalidationAtUtc: revalidation,
            ServiceClockCode: dispute ? "WorkingHoursNotConfigured" : "NotApplicable",
            ServiceTargetSummary: dispute ? "응답 6시간 · 처리 24시간 · 추가 처리 누적 72시간" : null);
    }

    private static string NextAction(string code) => code switch
    {
        "TransactionDispute" => "분쟁·지급 보류 확인",
        "PrivacyIncident" => "노출 범위·중단 조치 확인",
        "AfterPickupRecoveryRequired" => "안전·물품 확인",
        "CargoIncidentReviewRequired" => "현장 상황 확인",
        "DispatchLedgerMissing" or "DispatchCorrelationMismatch" => "원장 연결 확인",
        "SharedLedgerOutboxReviewRequired" => "후속 처리 복구 확인",
        "DriverNotificationOutboxReviewRequired" or "NotificationExhausted" => "알림 수신·발송 확인",
        "RestaurantPreparationDelay" => "음식 준비 상태 확인",
        "DeliveryProgressDelay" => "기사 안전·진행 확인",
        "DriverAssignmentDelay" or "NoCandidate" => "시간창·기사 여력 확인",
        "CollectionEvidenceReviewRequired" => "수납 근거 확인",
        "MartReceiptHold" => "수령 문제·합의 확인",
        "StockReleasePending" => "반출·인계 근거 확인",
        "PhotoUploadStalled" => "기존 업로드 결과 확인",
        "SourceReadFailed" => "원장 연결 상태 확인",
        "UsageQuotaReached" => "사용량·예산 확인",
        _ => "업무 상태 확인"
    };

    private static DateTime Utc(DateTime value)
        => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
