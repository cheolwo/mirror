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
        "PhotoUploadStalled" => "사진 업로드 확인 필요", "NotificationExhausted" => "알림 발송 확인 필요",
        "NoCandidate" => "배송 제안 시간 경과", "SourceReadFailed" => "업무 정보 조회 확인 필요",
        "UsageQuotaReached" => "일일 사용량 한도 확인 필요",
        _ => "업무 진행 확인 필요"
    };
    public static string Href(string kind, string id) => kind switch
    {
        "support" => "/privacy-support/" + Uri.EscapeDataString(id),
        "food" => "/food-operations/orders/" + Uri.EscapeDataString(id),
        "operator-outbox" or "budget" or "monitor" => "/operations-alerts",
        _ => "/operations"
    };
}
