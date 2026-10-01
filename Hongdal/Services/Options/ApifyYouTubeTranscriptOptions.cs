namespace 홍달.Services.Options;

public sealed class ApifyYouTubeTranscriptOptions
{
    public const string SectionName = "ApifyYouTubeTranscript";

    /// <summary>
    /// 비용이 발생할 수 있는 YouTube 자막 Actor 실행을 명시적으로 허용합니다.
    /// </summary>
    public bool Enabled { get; set; }

    public string ActorId { get; set; } = "pintostudio~youtube-transcript-scraper";

    public int ActorTimeoutSeconds { get; set; } = 120;

    public int MemoryMegabytes { get; set; } = 512;

    public int MaxDatasetItems { get; set; } = 10;

    public decimal MaxTotalChargeUsd { get; set; } = 0.25m;

    public string DefaultTargetLanguage { get; set; } = "en";

    public int MaxSegments { get; set; } = 2_000;

    public int MaxSegmentTextCharacters { get; set; } = 1_000;

    public int MaxTranscriptCharacters { get; set; } = 12_000;
}
