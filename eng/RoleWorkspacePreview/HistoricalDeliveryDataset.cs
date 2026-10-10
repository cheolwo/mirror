using System.Text.Json;

namespace RoleWorkspacePreview;

// Development-only input contract. Personal rows are never embedded in the binary.
public sealed record HistoricalDeliveryDataset
{
    public int SchemaVersion { get; init; } = 1;
    public bool LocalVerificationOnly { get; init; }
    public string DriverId { get; init; } = "replay-driver";
    public string SourceLedgerSha256 { get; init; } = "";
    public DateOnly CoverageStart { get; init; }
    public DateOnly CoverageEnd { get; init; }
    public HistoricalDeliveryBranch[] Branches { get; init; } = [];
    public HistoricalDeliveryRecord[] Records { get; init; } = [];

    public static HistoricalDeliveryDataset Read(string path)
    {
        var data = JsonSerializer.Deserialize<HistoricalDeliveryDataset>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("로컬 배달 자료를 읽을 수 없습니다.");
        data.Validate();
        return data;
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || !LocalVerificationOnly || DriverId != "replay-driver" || Branches is null || Records is null)
            throw new InvalidDataException("로컬 검증용 자료만 허용합니다.");
        if (Branches.Any(x => x is null || string.IsNullOrWhiteSpace(x.BranchId) || string.IsNullOrWhiteSpace(x.DisplayName))
            || Records.Any(x => x is null || x.MenuItems is null || x.SourceRefs is null || x.FeeComponents is null || x.MenuSummary is null)
            || CoverageStart > CoverageEnd || Branches.Select(x => x.BranchId).Distinct().Count() != Branches.Length
            || Records.Select(x => x.RecordId).Distinct().Count() != Records.Length)
            throw new InvalidDataException("날짜 또는 식별값이 중복됩니다.");
        var branches = Branches.Select(x => x.BranchId).ToHashSet(StringComparer.Ordinal);
        foreach (var row in Records)
        {
            if (!row.RecordId.StartsWith("replay-", StringComparison.Ordinal) || !branches.Contains(row.BranchId)
                || row.ServiceDate < CoverageStart || row.ServiceDate > CoverageEnd || row.Sequence < 1
                || row.DeliveryFeeKrw < 0 || row.DistanceKm < 0 || row.FoodOrderAmountKrw < 0
                || row.MenuItems.Any(x => x.Quantity <= 0 || x.UnitPriceKrw < 0 || x.DisplayedItemAmountKrw < 0))
                throw new InvalidDataException("배달 자료 관계 또는 금액이 올바르지 않습니다.");
        }
    }
}

public sealed record HistoricalDeliveryBranch
{
    public string BranchId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string? PublicPickupAddress { get; init; }
}

public sealed record HistoricalDeliveryRecord
{
    public string RecordId { get; init; } = "";
    public DateOnly ServiceDate { get; init; }
    public int Sequence { get; init; }
    public string BranchId { get; init; } = "";
    public string PickupVisitId { get; init; } = "";
    public decimal DeliveryFeeKrw { get; init; }
    public decimal DistanceKm { get; init; }
    public string DistanceBasis { get; init; } = "AppDisplayed";
    public decimal? FoodOrderAmountKrw { get; init; }
    public DateTime? ActualCompletedAtUtc { get; init; }
    public HistoricalDeliveryMenuItem[] MenuItems { get; init; } = [];
    public string[] MenuSummary { get; init; } = [];
    public HistoricalDeliveryFeeComponent[] FeeComponents { get; init; } = [];
    public HistoricalDeliverySourceRef[] SourceRefs { get; init; } = [];
}

public sealed record HistoricalDeliveryMenuItem
{
    public string Name { get; init; } = "";
    public int? Quantity { get; init; }
    public decimal? UnitPriceKrw { get; init; }
    public decimal? DisplayedItemAmountKrw { get; init; }
    public string AmountBasis { get; init; } = "Unknown";
    public string[] Options { get; init; } = [];
}

public sealed record HistoricalDeliveryFeeComponent
{
    public string Code { get; init; } = "";
    public string Label { get; init; } = "";
    public decimal? AmountKrw { get; init; }
    public HistoricalDeliveryFeeComponent[] Children { get; init; } = [];
}

public sealed record HistoricalDeliverySourceRef
{
    public string RelativePath { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string Field { get; init; } = "";
    public string Basis { get; init; } = "";
}
