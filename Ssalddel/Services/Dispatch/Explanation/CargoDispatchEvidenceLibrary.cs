using System.Globalization;
using Ssalddel.Contracts.Common.Dispatch;

namespace 살뜰.Services.Dispatch.Explanation;

/// <summary>공통 설명은 독립 불변 판본, 건별 값은 기존 보호 사건에 둔다. 현재 정책으로 과거를 보충하지 않는다.</summary>
internal static class CargoDispatchEvidenceLibrary
{
    internal static CargoDispatchBasisReference Capture(CargoDispatchExplanationSnapshot saved)
    {
        var catalog = CargoDispatchBasisCatalog.Current;
        var values = Values(saved);
        return new()
        {
            Version = catalog.Version, ContentHash = catalog.ContentHash,
            RecordedCodes = catalog.Definitions.Where(x => values[x.Code] is not null).Select(x => x.Code).ToArray(),
            MissingCodes = catalog.Definitions.Where(x => values[x.Code] is null).Select(x => x.Code).ToArray()
        };
    }

    internal static CargoDispatchBasisEvidence Project(CargoDispatchExplanationSnapshot saved)
    {
        if (saved.Basis is not { } reference) return new();
        CargoDispatchBasisEvidence Held(string status) => new()
        {
            Status = status,
            CatalogVersion = reference.Version is { Length: <= 80 } version
                && System.Text.RegularExpressions.Regex.IsMatch(version, "^cargo-dispatch-basis\\.r[0-9]+$") ? version : null,
            CatalogContentHash = reference.ContentHash is { Length: 64 } hash && hash.All(Uri.IsHexDigit) ? hash : null
        };
        if (!CargoDispatchBasisCatalog.TryGet(reference.Version, out var catalog)) return Held("catalog-unavailable");
        if (!string.Equals(reference.ContentHash, catalog.ContentHash, StringComparison.Ordinal)) return Held("mismatch");
        var values = Values(saved);
        if (catalog.Definitions.Any(x => !values.ContainsKey(x.Code))) return Held("catalog-unavailable");
        var recorded = catalog.Definitions.Where(x => values[x.Code] is not null).Select(x => x.Code).ToArray();
        var missing = catalog.Definitions.Where(x => values[x.Code] is null).Select(x => x.Code).ToArray();
        if (reference.RecordedCodes is null || reference.MissingCodes is null
            || !reference.RecordedCodes.SequenceEqual(recorded, StringComparer.Ordinal)
            || !reference.MissingCodes.SequenceEqual(missing, StringComparer.Ordinal)) return Held("mismatch");
        var entries = catalog.Definitions.Select(x => new CargoDispatchBasisEntry(x.Code, x.Label, x.Kind, x.Explanation,
            State(x.Code, values[x.Code], saved), values[x.Code])).ToArray();
        return new()
        {
            Status = missing.Length == 0 ? "recorded" : "partial",
            CatalogVersion = catalog.Version, CatalogContentHash = catalog.ContentHash,
            RecordedFactCount = recorded.Length, MissingFactCount = missing.Length,
            IntakeFactCount = entries.Count(x => x.Kind == "intake" && x.State != "missing"),
            DecisionFactCount = entries.Count(x => x.Kind == "decision" && x.State != "missing"), Entries = entries
        };
    }

    internal static string CoverageNotice(CargoDispatchBasisEvidence basis) => basis.Status switch
    {
        "recorded" or "partial" => $"공통 안내 항목 기준으로 당시 기록 {basis.RecordedFactCount}항목(접수 정보 {basis.IntakeFactCount}, 판정 자료 {basis.DecisionFactCount}), 미기록 {basis.MissingFactCount}항목입니다. 항목 수는 AI의 신뢰도나 선택 기여도 점수가 아닙니다.",
        "legacy-unlinked" => "개별 기록은 있으나 당시 공통 근거 판본은 연결되지 않았습니다. 현재 기준으로 소급 보충하지 않습니다.",
        "catalog-unavailable" => "당시 공통 근거 판본을 조회할 수 없습니다. 개별 기록과 의견 연결은 유지합니다.",
        _ => "저장한 공통 근거와 개별 기록의 일치를 확인하지 못했습니다. 개별 기록과 의견 연결은 유지합니다."
    };

    private static string State(string code, string? value, CargoDispatchExplanationSnapshot saved)
    {
        if (value is null) return "missing";
        bool? confirmed = code switch
        {
            "decision.plan" => saved.DecisionFacts?.FullPlanFeasible,
            "decision.loading" => saved.DecisionFacts?.SegmentLoadingVerified,
            _ => null
        };
        return confirmed is { } known ? (known ? "confirmed" : "not-confirmed") : "recorded";
    }

    // These are the values of the immutable event, not a fresh request/route/model query.
    private static Dictionary<string, string?> Values(CargoDispatchExplanationSnapshot saved) => new(StringComparer.Ordinal)
    {
        ["intake.weight"] = saved.CargoWeightKg?.ToString("0.###", CultureInfo.InvariantCulture) is { } weight ? weight + "kg" : null,
        ["intake.mode"] = saved.TransportMode,
        ["intake.temperature"] = saved.Temperature,
        ["intake.pickup-deadline"] = Time(saved.PickupDeadlineUtc),
        ["intake.dropoff-deadline"] = Time(saved.DropoffDeadlineUtc),
        ["decision.plan"] = Boolean(saved.DecisionFacts?.FullPlanFeasible),
        ["decision.loading"] = Boolean(saved.DecisionFacts?.SegmentLoadingVerified),
        ["decision.pickup-order"] = Order(saved.DecisionFacts?.OwnPickupOrder),
        ["decision.dropoff-order"] = Order(saved.DecisionFacts?.OwnDropoffOrder)
    };
    private static string? Boolean(bool? value) => value is { } flag ? (flag ? "가능 확인" : "가능 미확인") : null;
    private static string? Order(int? value) => value is { } order ? order.ToString(CultureInfo.InvariantCulture) + "번째 정차" : null;
    private static string? Time(DateTime? value) => value is { } date
        ? new DateTimeOffset(CargoDispatchExplanationCodec.Utc(date)).ToOffset(TimeSpan.FromHours(9))
            .ToString("yyyy.MM.dd HH:mm '(한국시간)'", CultureInfo.InvariantCulture) : null;
}

internal sealed class CargoDispatchBasisReference
{
    public string Version { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public IReadOnlyList<string> RecordedCodes { get; set; } = [];
    public IReadOnlyList<string> MissingCodes { get; set; } = [];
}
