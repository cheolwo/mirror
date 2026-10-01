namespace 홍달.Services.Options;

public sealed class 수입식품한글표시사항조회Options
{
    public const string SectionName = "수입식품한글표시사항조회";

    public string BaseUrl { get; set; } = "https://apis.data.go.kr/1471000/IprtFoodPrdtKoreanLabelingItem";

    public string Path { get; set; } = "/getIprtFoodPrdtKoreanLabelingItem";

    public string ServiceKey { get; set; } = string.Empty;

    public string DefaultType { get; set; } = "xml";

    public int TimeoutSeconds { get; set; } = 20;
}
