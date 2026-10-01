using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

internal static class CommunityAuthoringAiEvidenceMapper
{
    public static CommunityAuthoringAiEvidenceDto FromCandidate(
        string toolKey,
        CommunityInformationCandidateDto item)
        => new(
            item.CandidateKey,
            toolKey,
            item.SourceKey,
            item.Provider,
            Truncate(item.Title, 240),
            Truncate(item.Summary, 500),
            item.OriginalUrl,
            item.ReferenceDate
            ?? (item.PublishedAtUtc.HasValue
                ? DateOnly.FromDateTime(item.PublishedAtUtc.Value)
                : null),
            item.MetricLabel,
            item.NumericValue,
            item.CurrencyCode,
            item.Unit,
            Truncate(item.SourceNotice, 220),
            Truncate(item.Limitations, 350));

    public static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
