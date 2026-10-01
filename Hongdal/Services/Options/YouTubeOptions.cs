namespace 홍달.Services.Options;

public sealed class YouTubeOptions
{
    public const string SectionName = "YouTube";

    public bool Enabled { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://www.googleapis.com/youtube/v3/";

    public int TimeoutSeconds { get; set; } = 20;

    public int SyncIntervalSeconds { get; set; } = 300;

    public int MaxResultsPerChannel { get; set; } = 20;

    /// <summary>
    /// 조사 카탈로그의 음식 채널을 감시 채널 DB에 추가합니다.
    /// 애플리케이션 설정에서 명시적으로 켜야 하며 테스트용 기본값은 false입니다.
    /// </summary>
    public bool SeedFoodResearchCatalog { get; set; }

    /// <summary>
    /// 지식·성찰 대표 채널 카탈로그를 감시 채널 DB에 추가합니다.
    /// 기본값은 false이며, 시드 여부와 반야 게시 승인은 서로 독립적입니다.
    /// </summary>
    public bool SeedKnowledgeReflectionCatalog { get; set; }

    /// <summary>
    /// 백그라운드 동기화를 국가별 실행 단위로 나눕니다. 비어 있으면 전체 채널을 한 번에 처리합니다.
    /// </summary>
    public List<string> CountryCollectionCodes { get; set; } = [];

    /// <summary>
    /// 권한이 확인된 자막·영상 프레임을 HIOPS AI로 분석해 재료 후보를 자동 생성합니다.
    /// 비용과 콘텐츠 권리 확인을 위해 명시적으로 켜야 합니다.
    /// </summary>
    public bool AutomaticIngredientRecognitionEnabled { get; set; }

    public int MaxIngredientRecognitionFrames { get; set; } = 4;

    public int MaxIngredientRecognitionFrameBytes { get; set; } = 4 * 1024 * 1024;

    public int MaxIngredientRecognitionTranscriptCharacters { get; set; } = 12_000;

    public decimal MinimumIngredientRecognitionConfidence { get; set; } = 0.55m;

    public List<YouTube기본감시채널Options> DefaultChannels { get; set; } = [];
}

public sealed class YouTube기본감시채널Options
{
    public string ChannelId { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string? CountryCode { get; set; }
}
