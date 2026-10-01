namespace Hongdal.Contracts.Common.Customs;

public sealed class GroupImportHsCodeSearchResponse
{
    public IReadOnlyList<GroupImportHsCodeItemResponse> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 30;
}

public sealed class GroupImportHsCodeItemResponse
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NormalizedCode { get; set; } = string.Empty;

    public string KoreanName { get; set; } = string.Empty;

    public string EnglishName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int Level { get; set; }

    public string LevelLabel { get; set; } = string.Empty;

    public int BusinessCategory { get; set; }

    public string BusinessCategoryLabel { get; set; } = string.Empty;

    public bool BrokerReviewRecommended { get; set; }

    public IReadOnlyList<GroupImportHsCodeRiskTagResponse> RiskTags { get; set; } = [];
}

public sealed class GroupImportHsCodeRiskTagResponse
{
    public int TagType { get; set; }

    public string Label { get; set; } = string.Empty;
}
