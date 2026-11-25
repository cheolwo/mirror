namespace Ssalddel.Contracts.Common.Dispatch;

/// <summary>저장된 한 사건과 정확한 공통 설명 판본의 연결입니다. 수량은 품질·확률·선택 기여도 점수가 아닙니다.</summary>
public sealed class CargoDispatchBasisEvidence
{
    public string Status { get; set; } = "legacy-unlinked";
    public string? CatalogVersion { get; set; }
    public string? CatalogContentHash { get; set; }
    public int RecordedFactCount { get; set; }
    public int IntakeFactCount { get; set; }
    public int DecisionFactCount { get; set; }
    public int MissingFactCount { get; set; }
    public IReadOnlyList<CargoDispatchBasisEntry> Entries { get; set; } = [];
}

/// <summary>State는 recorded/confirmed/not-confirmed/missing이며 누락 값은 null입니다. 누락을 false나 확인됨으로 바꾸지 않습니다.</summary>
public sealed record CargoDispatchBasisEntry(string Code, string Label, string Kind, string Explanation, string State, string? Value);

/// <summary>본인 권한으로 조회한 미래 개인 안내의 입력입니다. 조회·준비는 동의·발송·배차 변경을 뜻하지 않습니다.</summary>
public sealed class CargoDispatchNoticeContext
{
    public string EvidenceId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public int RecommendationRound { get; set; }
    public string MethodLabel { get; set; } = string.Empty;
    public string TemplateVersion { get; set; } = "cargo-dispatch-notice.r1";
    public string Purpose { get; set; } = "preparation-only";
    public string Subject { get; set; } = string.Empty;
    public IReadOnlyList<string> Lines { get; set; } = [];
    public CargoDispatchBasisEvidence Basis { get; set; } = new();
}
