namespace Hongdal.Contracts.Common.Content;

public sealed record YouTubeTranscriptRequest(
    string VideoId,
    string? TargetLanguage = null);

public sealed record YouTubeTranscriptSegmentDto(
    decimal? StartSeconds,
    decimal? DurationSeconds,
    string Text);

public sealed record YouTubeTranscriptResponse(
    string VideoId,
    string VideoUrl,
    string LanguageCode,
    string Provider,
    DateTime CollectedAtUtc,
    IReadOnlyList<YouTubeTranscriptSegmentDto> Segments,
    string Transcript);
