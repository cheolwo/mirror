namespace Ssalddel.Contracts.Common.Dispatch;

public sealed record CargoDispatchExplanationFact(string Label, string Value);

public sealed class CargoDispatchExplanationItem
{
    public string EvidenceId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public int RecommendationRound { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string DecisionKind { get; set; } = "recommendation";
    public string MethodLabel { get; set; } = string.Empty;
    public IReadOnlyList<CargoDispatchExplanationFact> Facts { get; set; } = [];
    public IReadOnlyList<string> Limitations { get; set; } = [];
    public bool CanSubmitFeedback { get; set; }
    public IReadOnlyList<CargoDispatchFeedbackReceipt> MyFeedback { get; set; } = [];
    public CargoDispatchBasisEvidence? Basis { get; set; }
}

public sealed class CargoDispatchExplanationResponse
{
    public string Status { get; set; } = "unrecorded";
    public string Notice { get; set; } = "당시 배차 설명이 기록되지 않았습니다.";
    public IReadOnlyList<CargoDispatchExplanationItem> Items { get; set; } = [];
}

public sealed class CargoDispatchFeedbackRequest
{
    public Guid ClientRequestId { get; set; }
    public string EvidenceId { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public sealed class CargoDispatchFeedbackReceipt
{
    public string FeedbackId { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    public string EvidenceId { get; set; } = string.Empty;
    public string Status { get; set; } = "received";
    public string CategoryCode { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public string SubmissionFingerprint { get; set; } = string.Empty;
}

public static class CargoDispatchFeedbackFingerprint
{
    // EvidenceId/ClientRequestId are checked separately. Match the exact submitted text;
    // null and empty comment are the same server input, other whitespace is preserved.
    public static string Compute(string categoryCode, string? comment)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(
                new[] { categoryCode, comment ?? string.Empty }))));
}

public static class CargoDispatchFeedbackCategories
{
    public static IReadOnlyList<string> All { get; } =
        ["helpful", "incorrect-information", "time-or-route", "explanation-unclear", "other"];

    public static string Label(string code) => code switch
    {
        "helpful" => "도움이 됨",
        "incorrect-information" => "정보가 다름",
        "time-or-route" => "시간·경로가 맞지 않음",
        "explanation-unclear" => "설명이 부족함",
        "other" => "다른 의견",
        _ => "의견"
    };
}
