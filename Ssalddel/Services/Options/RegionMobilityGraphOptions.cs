namespace 살뜰.Services.Options;

/// <summary>
/// 게시 전 사가정 이동망 후보를 인증된 개발 환경에서만 읽기 위한 로컬 경로입니다.
/// 빈 경로는 자료 미구성으로 처리하며 운영 자료나 sample fallback을 만들지 않습니다.
/// </summary>
public sealed class RegionMobilityGraphOptions
{
    public const string SectionName = "RegionMobilityGraph";

    public string LocalPrivatePreviewRoot { get; set; } = string.Empty;
}
