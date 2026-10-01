namespace Hongdal.Contracts.Common.Orderer;

public static class DomesticGroupPurchaseQuantitySourceCodes
{
    public const string AllDemand = "all-demand";
    public const string ReservedOrConfirmed = "reserved-or-confirmed";
    public const string ExplicitOrders = "explicit-orders";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AllDemand,
        ReservedOrConfirmed
    };
}

public sealed class DomesticGroupPurchaseVehicleRecommendationRequest
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string AutoGroupId { get; set; } = string.Empty;
    public string QuantitySourceCode { get; set; } = DomesticGroupPurchaseQuantitySourceCodes.AllDemand;
    public IReadOnlyList<DomesticGroupPurchaseVehicleOrderItem> Orders { get; set; } = [];
    public IReadOnlyList<DomesticGroupPurchaseProductPackageSpecification> ProductPackages { get; set; } = [];
    public bool KeepParticipantPackagesSeparate { get; set; }
    public bool AllowSplitTransport { get; set; } = true;
    public decimal LoadingEfficiencyRate { get; set; } = 0.85m;
    public decimal SafetyMarginRate { get; set; } = 0.05m;
    public int? ExplicitPalletCount { get; set; }
    public bool RequiresRainProtection { get; set; }
    public bool RequiresLift { get; set; }
    public bool RequiresSideLoading { get; set; }
}

public sealed class DomesticGroupPurchaseVehicleOrderItem
{
    public string OrderKey { get; set; } = string.Empty;
    public string ParticipantKey { get; set; } = string.Empty;
    public string ProductKey { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseProductPackageSpecification
{
    public string ProductKey { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string QuantityUnit { get; set; } = string.Empty;
    public decimal UnitsPerPackage { get; set; }
    public int PackageLengthMm { get; set; }
    public int PackageWidthMm { get; set; }
    public int PackageHeightMm { get; set; }
    public decimal PackageGrossWeightKg { get; set; }
    public bool CanRotateOnFloor { get; set; } = true;
    public bool Stackable { get; set; } = true;
    public int? PackagesPerPallet { get; set; }
    public string TemperatureCode { get; set; } = "상온";
}

public sealed class DomesticGroupPurchaseVehicleRecommendationResponse
{
    public Guid GroupPurchaseCampaignId { get; set; }
    public string AutoGroupId { get; set; } = string.Empty;
    public string QuantitySourceCode { get; set; } = string.Empty;
    public bool ContainsUnconfirmedDemand { get; set; }
    public int ParticipantCount { get; set; }
    public int OrderCount { get; set; }
    public int TotalPackageCount { get; set; }
    public decimal ActualGrossWeightKg { get; set; }
    public decimal PlannedWeightWithMarginKg { get; set; }
    public decimal RawPackageVolumeCbm { get; set; }
    public decimal PlannedLoadingVolumeCbm { get; set; }
    public decimal NonStackableFloorAreaM2 { get; set; }
    public int? PalletCount { get; set; }
    public decimal LoadingEfficiencyRate { get; set; }
    public decimal SafetyMarginRate { get; set; }
    public string TemperatureCode { get; set; } = string.Empty;
    public string RecommendedVehicleType { get; set; } = string.Empty;
    public bool CanTransportInSingleTrip { get; set; }
    public int RecommendedTripCount { get; set; }
    public IReadOnlyList<DomesticGroupPurchaseProductLoadSummary> ProductSummaries { get; set; } = [];
    public IReadOnlyList<DomesticGroupPurchaseVehicleCandidateResponse> Candidates { get; set; } = [];
    public IReadOnlyList<DomesticGroupPurchaseRejectedVehicleResponse> RejectedVehicles { get; set; } = [];
    public IReadOnlyList<string> CalculationBasis { get; set; } = [];
    public IReadOnlyList<string> Warnings { get; set; } = [];
}

public sealed class DomesticGroupPurchaseProductLoadSummary
{
    public string ProductKey { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalOrderedQuantity { get; set; }
    public string QuantityUnit { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public int ParticipantCount { get; set; }
    public int PackageCount { get; set; }
    public decimal PackageGrossWeightKg { get; set; }
    public decimal TotalGrossWeightKg { get; set; }
    public decimal TotalPackageVolumeCbm { get; set; }
    public int? PalletCount { get; set; }
}

public sealed class DomesticGroupPurchaseVehicleCandidateResponse
{
    public int Rank { get; set; }
    public string VehicleCode { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string BodyType { get; set; } = string.Empty;
    public int LoadBedLengthMm { get; set; }
    public int LoadBedWidthMm { get; set; }
    public int? LoadBedHeightMm { get; set; }
    public decimal AllowedWeightKg { get; set; }
    public decimal? AllowedVolumeCbm { get; set; }
    public int? AllowedPalletCount { get; set; }
    public bool CanTransportInSingleTrip { get; set; }
    public int RecommendedTripCount { get; set; } = 1;
    public decimal? WeightUtilizationPercent { get; set; }
    public decimal? VolumeUtilizationPercent { get; set; }
    public decimal? PalletUtilizationPercent { get; set; }
    public decimal? FloorAreaUtilizationPercent { get; set; }
    public IReadOnlyList<string> SingleTripLimitReasons { get; set; } = [];
    public IReadOnlyList<string> VerificationWarnings { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
}

public sealed class DomesticGroupPurchaseRejectedVehicleResponse
{
    public string VehicleCode { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public IReadOnlyList<string> Reasons { get; set; } = [];
}
