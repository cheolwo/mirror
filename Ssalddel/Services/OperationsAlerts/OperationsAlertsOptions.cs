using 살뜰.Services.Options;

namespace Ssalddel.Services.OperationsAlerts;

public sealed class OperationsAlertsOptions
{
    public const string SectionName = "OperationsAlerts";
    public bool Enabled { get; set; }
    public bool ExternalSendEnabled { get; set; }
    public string OperatorUserId { get; set; } = "";
    public string RecipientEmail { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";
    public int PollSeconds { get; set; } = 30;
    public int PageSize { get; set; } = 50;
    public int PagesPerCycle { get; set; } = 4;
    public CommunityPostEmailGmailOptions Gmail { get; set; } = new();
}

public sealed record OperationsObservation(string SourceKind, string SourceId, string ProblemCode,
    string Severity, bool Active, DateTime ChangedAtUtc);
public interface IOperationsAlertSource
{
    string Key { get; }
    bool RepeatActiveSweep => false;
    Task<IReadOnlyList<OperationsObservation>> ReadChangesAsync(DateTime afterUtc, string afterId, int take, CancellationToken ct);
    Task<IReadOnlyList<OperationsObservation>> ReadCurrentAsync(string sourceId, CancellationToken ct);
}

public static class OperationsAlertPresentation
{
    // 서버에 등록한 code만 표현한다. 원문 예외/사용자 입력은 알림·로그에 복사하지 않는다.
    public static string Title(string code) => code switch
    {
        "TransactionDispute" => "거래 문제 접수", "PrivacyIncident" => "개인정보 사고 접수",
        "AfterPickupRecoveryRequired" => "픽업 후 배송 중단",
        "CargoIncidentReviewRequired" => "화물 현장 문제 확인 필요",
        "DispatchLedgerMissing" => "배차 원장 확인 필요", "DispatchCorrelationMismatch" => "배차 연결 확인 필요",
        "SharedLedgerOutboxReviewRequired" => "주문 원장 동기화 복구 소진",
        "DriverNotificationOutboxReviewRequired" => "기사 배차 알림 복구 소진",
        "RestaurantPreparationDelay" => "음식 준비 지연 확인 필요",
        "DeliveryProgressDelay" => "배송 진행 지연 확인 필요",
        "DriverAssignmentDelay" => "기사 확보 지연 확인 필요",
        "CollectionEvidenceReviewRequired" => "수납 근거 확인 필요",
        "MartReceiptHold" => "물품 수령 문제 해결 대기",
        "StockReleasePending" => "공간 종료 물품 반출 대기",
        "PhotoUploadStalled" => "사진 업로드 확인 필요", "NotificationExhausted" => "알림 발송 확인 필요",
        "NoCandidate" => "배송 제안 시간 경과", "SourceReadFailed" => "업무 정보 조회 확인 필요",
        "UsageQuotaReached" => "일일 사용량 한도 확인 필요",
        _ => "업무 진행 확인 필요"
    };
    public static string Action(string code) => code switch
    {
        "TransactionDispute" => "지원 사건에서 당사자의 확인 근거와 다음 담당자를 확인하세요. 사건 종결과 실제 환불·입금 완료는 따로 기록하세요.",
        "PrivacyIncident" => "지원 사건에서 노출 범위와 중단 조치를 확인하고 개인정보 사고 대응 절차를 진행하세요.",
        "AfterPickupRecoveryRequired" => "기사의 안전과 물품 상태를 먼저 확인하고 재전달·재조리 등 복구 담당자를 정하세요. 원장 상태를 직접 덮어쓰지 마세요.",
        "DispatchLedgerMissing" => "원주문의 배차 요청과 실행 원장 연결을 확인하고 같은 요청의 멱등 복구 경로를 사용하세요.",
        "DispatchCorrelationMismatch" => "원주문과 배차 원장의 식별값을 대조하고 연결 Event를 재처리하세요. 원장을 직접 수정하지 마세요.",
        "SharedLedgerOutboxReviewRequired" => "반복 실패 원인을 확인하고 해소한 뒤 관리자 사유를 남겨 공동 원장 후속 처리를 재시도하세요.",
        "DriverNotificationOutboxReviewRequired" => "기사 앱의 설치·알림 등록·수신 상태와 발송 설정을 확인하세요. 서버 재조회로 현재 추천·재배차 진행 상태를 확인하고 만료된 추천은 다시 보내지 마세요.",
        "RestaurantPreparationDelay" => "음식점의 현재 준비 상태와 변경된 예정시각을 확인하고 필요한 경우 주문자에게 지연 사실을 안내하세요.",
        "DeliveryProgressDelay" => "기사의 마지막 진행 상태와 안전 문제를 확인하고 필요한 경우 기사 또는 주문자에게 연락하세요.",
        "DriverAssignmentDelay" => "주문의 대기 시간과 참여 기사 여력을 확인하고 주문자·음식점에 다음 확인 시각과 가능한 대안을 안내하세요.",
        "CollectionEvidenceReviewRequired" => "문제 사건은 종료됐지만 수납 근거가 미확인입니다. 결제·입금 원장을 대조하세요. 사건 종료를 입금 완료로 바꾸지 마세요.",
        "MartReceiptHold" => "주문자와 판매자의 수령 문제 해결 근거를 확인하세요. 부분 합의·반품·추가비용은 별도 검토하며 보류를 임의로 해제하지 마세요.",
        "StockReleasePending" => "공간 제공자와 물품 소유자가 반출 담당·실수량·인계 근거를 확인하도록 안내하세요. 잔여 재고와 예약이 없어진 뒤 공간을 종료하세요.",
        "NotificationExhausted" => "알림 채널의 설정과 수신 가능 여부를 확인하세요. 원래 업무가 해결됐는지는 해당 원장을 별도로 확인하세요.",
        "SourceReadFailed" => "업무 정보 조회 연결을 확인하세요. 조회 실패를 업무 완료나 문제 해결로 처리하지 마세요.",
        "NoCandidate" => "배송 시간창과 참여 기사 여력을 확인하고 당사자에게 변경 가능한 일정을 안내하세요.",
        "PhotoUploadStalled" => "사진 업로드 상태와 연결을 확인하고 기존 업로드 결과를 먼저 재조회하세요. 증빙 없이 완료 처리하지 마세요.",
        "UsageQuotaReached" => "남은 사용량과 예산을 확인하세요. 진행 중 업무의 완료와 증빙 저장은 계속 허용됩니다.",
        _ => "해당 업무를 열어 진행 상태와 다음 담당자, 처리 근거를 확인하세요."
    };
    public static string Href(string kind, string id) => kind switch
    {
        "support" => "/privacy-support/" + Uri.EscapeDataString(id),
        "food" => "/food-operations/orders/" + Uri.EscapeDataString(id),
        "operator-outbox" or "budget" or "monitor" => "/operations-alerts",
        _ => "/operations"
    };
}
