using Ssalddel.Contracts.Admin.DispatchTraining;

namespace SsalddelAdminApp.Services;

public static class DispatchTrainingPresentation
{
    public static string Phase(string code) => code switch
    {
        DispatchTrainingPhaseCodes.Idle => "시작 전", DispatchTrainingPhaseCodes.Running => "새 주문 발생 중",
        DispatchTrainingPhaseCodes.Paused => "새 주문 발생 일시정지", DispatchTrainingPhaseCodes.Draining => "새 주문 발생 종료 · 진행 업무 처리 중",
        DispatchTrainingPhaseCodes.Completed => "훈련 완료", DispatchTrainingPhaseCodes.Failed => "상태 확인 필요", _ => "상태 미확인"
    };
    public static string Time(DateTime? value) => value is null || value == default(DateTime) ? "미확인" : value.Value.ToLocalTime().ToString("HH:mm:ss");
    public static string Remaining(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";
    public static string DriverState(string code) => code switch
    { "Active" => "새 주문 수신 중", "ReceivingOff" => "새 주문 수신 꺼짐", "Standby" => "지원 대기", _ => "상태 미확인" };
    public static string Score(double? value) => value is { } score && double.IsFinite(score) ? score.ToString("0.##") : "미확인";
    public static string Eligibility(string code) => code switch
    {
        "CandidateAllowed" or "Eligible" or "Allowed" => "적격", "Ineligible" or "Rejected" => "조건 불충족",
        "DriverAppMismatch" => "업무 앱 불일치", "ActiveVehicleMissing" => "활동 차량 미확인",
        "DriverExcluded" => "이번 배차 제외", "DriverRejected" => "기사 거절", "DriverNotDriving" => "새 주문 수신 중지",
        "DriverLocationUnavailableOrStale" => "현재 위치 미확인", "DistanceUnavailable" => "거리 미확인",
        "OutsideApproachAllowance" => "픽업 접근 범위 밖", "VehicleIncompatible" => "차량 조건 불일치",
        "Unknown" or "Unconfirmed" => "미확인", _ => "조건 확인 필요"
    };
    public static string Ai(string? status) => status switch
    {
        "Applied" or "Success" or "Succeeded" or "Completed" => "AI 비교 완료", "Pending" or "Running" => "AI 비교 중",
        "Timeout" => "AI 응답 시간 초과", "Unavailable" or "NotConfigured" => "AI 연결 미확인",
        "SingleCandidate" => "후보 1명 · AI 비교 생략", "CallBudgetExhausted" or "BudgetExhausted" => "이번 회차 AI 비교 횟수 소진",
        "HttpFailure" or "NetworkFailure" => "AI 연결 실패", "InvalidSelection" => "허용 후보 밖 AI 선택",
        "InvalidInput" => "AI 비교 입력 확인 필요", "ResponseTooLarge" => "AI 응답 크기 초과",
        "Failed" or "Error" or "Rejected" or "InvalidResponse" => "AI 비교 실패", "Skipped" or "Disabled" => "AI 비교 보류", _ => "AI 결과 미확인"
    };
}
