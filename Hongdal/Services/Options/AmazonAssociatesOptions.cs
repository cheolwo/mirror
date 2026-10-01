namespace 홍달.Services.Options;

public sealed class AmazonAssociatesOptions
{
    public const string SectionName = "AmazonAssociates";

    /// <summary>
    /// 운영자가 선택한 Amazon 상품 URL을 제휴 링크로 변환하는 기능을 허용합니다.
    /// </summary>
    public bool Enabled { get; set; }

    public Dictionary<string, string> TrackingIdsByMarketplaceHost { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string LinkDisclosure { get; set; } =
        "(광고·제휴 링크) 이 링크를 통한 적격 구매로 홍달이 수수료를 받을 수 있습니다.";

    public string AssociateIdentification { get; set; } =
        "As an Amazon Associate I earn from qualifying purchases.";
}
