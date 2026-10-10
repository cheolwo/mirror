using System.Text.Json;
using System.Text.RegularExpressions;

namespace RoleWorkspacePreview;

/// <summary>명시적으로 가져온 개인 메뉴 자료. 운영 판매 메뉴나 주문 명령 계약으로 변환하지 않습니다.</summary>
public sealed record RecordedMenuCatalogDataset
{
    public int SchemaVersion { get; init; }
    public bool LocalVerificationOnly { get; init; }
    public string ExecutionModeCode { get; init; } = "";
    public bool OperationalRegistration { get; init; }
    public bool PublicDisplayApproved { get; init; }
    public bool PublicUseApproved { get; init; }
    public string SourceDatasetSha256 { get; init; } = "";
    public RecordedMenuCatalogSource[] Sources { get; init; } = [];
    public RecordedMenuCatalogBranch[] Branches { get; init; } = [];
    public RecordedMenuCatalogOrder[] Orders { get; init; } = [];
    public RecordedMenuCatalogMenu[] Menus { get; init; } = [];
    public RecordedMenuCatalogAsset[] Assets { get; init; } = [];

    public static RecordedMenuCatalogDataset Read(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("개인 메뉴 자료 크기를 확인해 주세요.");
        var dataset = JsonSerializer.Deserialize<RecordedMenuCatalogDataset>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("개인 메뉴 자료가 없습니다.");
        dataset.Validate();
        return dataset;
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || !LocalVerificationOnly || ExecutionModeCode != "Simulation" || OperationalRegistration || PublicDisplayApproved || PublicUseApproved)
            throw new InvalidDataException("개인 검토용 Simulation 자료만 읽을 수 있습니다.");
        if (!IsSha256(SourceDatasetSha256) || Sources is null || Branches is null || Orders is null || Menus is null || Assets is null)
            throw new InvalidDataException("자료 출처 또는 목록을 확인해 주세요.");
        if (Sources.Any(x => x is null || string.IsNullOrWhiteSpace(x.Kind) || !IsSha256(x.Sha256)))
            throw new InvalidDataException("자료 출처를 확인해 주세요.");
        RequireUnique(Branches.Select(x => x?.BranchId));
        RequireUnique(Orders.Select(x => x?.RecordId));
        RequireUnique(Menus.Select(x => x?.MenuId));
        RequireUnique(Assets.Select(x => x?.AssetId));
        var branchIds = Branches.Select(x => x.BranchId).ToHashSet(StringComparer.Ordinal);
        if (Branches.Any(x => string.IsNullOrWhiteSpace(x.DisplayName))) throw new InvalidDataException("지점 표시명을 확인해 주세요.");
        var orders = Orders.ToDictionary(x => x.RecordId, StringComparer.Ordinal);
        foreach (var order in Orders)
            if (!branchIds.Contains(order.BranchId) || order.ServiceDate == default || order.Sequence < 1 || order.FoodOrderAmountKrw < 0)
                throw new InvalidDataException("주문 자료의 지점·날짜·금액을 확인해 주세요.");
        var assetIds = Assets.Select(x => x.AssetId).ToHashSet(StringComparer.Ordinal);
        foreach (var asset in Assets)
            if (!Regex.IsMatch(asset.AssetId, "^[A-Za-z0-9_-]{1,128}$", RegexOptions.CultureInvariant) || !IsSha256(asset.Sha256)
                || string.IsNullOrWhiteSpace(asset.RelativePath) || asset.Bytes is < 1 or > 20 * 1024 * 1024
                || !SupportedMimeTypes.Contains(asset.MimeType) || asset.Width < 1 || asset.Height < 1)
                throw new InvalidDataException("메뉴 사진의 식별값·형식·크기를 확인해 주세요.");
        foreach (var menu in Menus)
        {
            if (!branchIds.Contains(menu.BranchId) || string.IsNullOrWhiteSpace(menu.RawMenu) || menu.SourceKind is not ("structured" or "per_order_summary")
                || menu.Observations is null || menu.ImageCandidates is null || menu.Observations.Length == 0)
                throw new InvalidDataException("메뉴 자료 연결을 확인해 주세요.");
            foreach (var observation in menu.Observations)
                if (observation is null || !orders.TryGetValue(observation.RecordId, out var order) || order.BranchId != menu.BranchId
                    || observation.Quantity < 1 || observation.UnitPriceKrw < 0 || observation.DisplayedItemAmountKrw < 0
                    || observation.Options is null || observation.Options.Any(x => x is null))
                    throw new InvalidDataException("메뉴의 관측 주문·수량·가격을 확인해 주세요.");
            foreach (var candidate in menu.ImageCandidates)
                if (candidate is null || !assetIds.Contains(candidate.AssetId) || string.IsNullOrWhiteSpace(candidate.CandidateId)
                    || string.IsNullOrWhiteSpace(candidate.OfficialMenuName) || candidate.OfficialSourceStatus != "verified"
                    || candidate.MatchLevel is not ("official_same_menu" or "same_menu_name_candidate")
                    || candidate.OptionsMatch || candidate.RightsStatus != "unconfirmed"
                    || candidate.PublicDisplayApproved || candidate.PublicUseApproved || candidate.ActualBranchPhotoVerified || candidate.HistoricalProductVersionVerified)
                    throw new InvalidDataException("메뉴 사진 후보의 출처·일치 범위·검토 권한을 확인해 주세요.");
        }
    }

    private static void RequireUnique(IEnumerable<string?> ids)
    {
        var values = ids.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidDataException("자료 식별값이 비어 있거나 중복입니다.");
    }

    private static bool IsSha256(string? value) => value is not null && Regex.IsMatch(value, "^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant);
    internal static readonly HashSet<string> SupportedMimeTypes = new(StringComparer.Ordinal) { "image/jpeg", "image/png", "image/webp", "image/gif" };
}

public sealed record RecordedMenuCatalogSource
{
    public string Kind { get; init; } = "";
    public string Sha256 { get; init; } = "";
}
public sealed record RecordedMenuCatalogBranch
{
    public string BranchId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? PublicPickupAddress { get; init; }
}
public sealed record RecordedMenuCatalogOrder
{
    public string RecordId { get; init; } = "";
    public DateOnly ServiceDate { get; init; }
    public int Sequence { get; init; }
    public string BranchId { get; init; } = "";
    public string PickupVisitId { get; init; } = "";
    public decimal? FoodOrderAmountKrw { get; init; }
}
public sealed record RecordedMenuCatalogMenu
{
    public string MenuId { get; init; } = "";
    public string BranchId { get; init; } = "";
    public string RawMenu { get; init; } = "";
    public string SourceKind { get; init; } = "";
    public RecordedMenuCatalogObservation[] Observations { get; init; } = [];
    public RecordedMenuCatalogImageCandidate[] ImageCandidates { get; init; } = [];
}
public sealed record RecordedMenuCatalogObservation
{
    public string RecordId { get; init; } = "";
    public int? MenuItemIndex { get; init; }
    public int? SummaryIndex { get; init; }
    public int? Quantity { get; init; }
    public decimal? UnitPriceKrw { get; init; }
    public decimal? DisplayedItemAmountKrw { get; init; }
    public string AmountBasis { get; init; } = "Unknown";
    public string[] Options { get; init; } = [];
}
public sealed record RecordedMenuCatalogImageCandidate
{
    public string CandidateId { get; init; } = "";
    public string AssetId { get; init; } = "";
    public string OfficialMenuName { get; init; } = "";
    public string MatchLevel { get; init; } = "";
    public string OfficialSourceStatus { get; init; } = "";
    public bool OptionsMatch { get; init; }
    public string RightsStatus { get; init; } = "unconfirmed";
    public bool ActualBranchPhotoVerified { get; init; }
    public bool HistoricalProductVersionVerified { get; init; }
    public bool PublicDisplayApproved { get; init; }
    public bool PublicUseApproved { get; init; }
    public string? SourcePageUrl { get; init; }
}
public sealed record RecordedMenuCatalogAsset
{
    public string AssetId { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string RelativePath { get; init; } = "";
    public string MimeType { get; init; } = "";
    public long Bytes { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}
