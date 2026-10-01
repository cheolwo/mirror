namespace Hongdal.Services.Community;

public sealed class CommunityPostOpportunityAnalyzer : ICommunityPostOpportunityAnalyzer
{
    private static readonly string[] MeatSignals =
    [
        "소고기", "쇠고기", "돼지고기", "육류", "축산물", "beef", "pork", "meat"
    ];

    private static readonly string[] CrossBorderSignals =
    [
        "수입", "수출", "해외 작업장", "해외작업장", "검역", "통관",
        "import", "export", "foreign establishment", "quarantine", "customs"
    ];

    public CommunityPostOpportunityAnalysis Analyze(string? title, string? body)
    {
        var text = $"{title}\n{body}";
        var meatMatches = MeatSignals.Where(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
        var crossBorderMatches = CrossBorderSignals.Where(signal => text.Contains(signal, StringComparison.OrdinalIgnoreCase));
        var matched = meatMatches
            .Concat(crossBorderMatches)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new(
            meatMatches.Any() && crossBorderMatches.Any(),
            matched);
    }
}
