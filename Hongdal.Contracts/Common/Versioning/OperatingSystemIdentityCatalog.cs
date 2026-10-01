namespace Hongdal.Contracts.Common.Versioning;

/// <summary>
/// Persistent operating-system identifiers shared by API metadata, ledgers, and clients.
/// Existing values with the OS suffix remain canonical so stored ledgers stay compatible.
/// </summary>
public static class OperatingSystemIds
{
    public const string DomesticCargoTransport = "DomesticCargoTransportOS";
    public const string WarehouseCommerceFulfillment = "WarehouseCommerceFulfillmentOS";
    public const string GroupPurchaseImport = "GroupPurchaseImportOS";
    public const string FoodDelivery = "FoodDeliveryOS";
    public const string HongdalMartUrbanLogistics = "HongdalMartUrbanLogisticsOS";
    public const string CommunityTrust = "CommunityTrustOS";
    public const string PlatformOperations = "PlatformOperationsOS";
    public const string EducationFieldExperience = "EducationFieldExperienceOS";

    public static IReadOnlyList<string> All { get; } =
    [
        DomesticCargoTransport,
        WarehouseCommerceFulfillment,
        GroupPurchaseImport,
        FoodDelivery,
        HongdalMartUrbanLogistics,
        CommunityTrust,
        PlatformOperations,
        EducationFieldExperience
    ];

    private static readonly IReadOnlyDictionary<string, string> CanonicalByAlias =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DomesticCargoTransport] = DomesticCargoTransport,
            ["DomesticCargoTransport"] = DomesticCargoTransport,
            [WarehouseCommerceFulfillment] = WarehouseCommerceFulfillment,
            ["WarehouseCommerceFulfillment"] = WarehouseCommerceFulfillment,
            [GroupPurchaseImport] = GroupPurchaseImport,
            ["GroupPurchaseImport"] = GroupPurchaseImport,
            [FoodDelivery] = FoodDelivery,
            ["FoodDelivery"] = FoodDelivery,
            [HongdalMartUrbanLogistics] = HongdalMartUrbanLogistics,
            ["HongdalMartUrbanLogistics"] = HongdalMartUrbanLogistics,
            [CommunityTrust] = CommunityTrust,
            ["CommunityTrust"] = CommunityTrust,
            [PlatformOperations] = PlatformOperations,
            ["PlatformOperations"] = PlatformOperations,
            [EducationFieldExperience] = EducationFieldExperience,
            ["EducationFieldExperience"] = EducationFieldExperience
        };

    public static bool TryNormalize(string? value, out string operatingSystemId)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && CanonicalByAlias.TryGetValue(value.Trim(), out var canonical))
        {
            operatingSystemId = canonical;
            return true;
        }

        operatingSystemId = string.Empty;
        return false;
    }

    public static string Normalize(string value)
        => TryNormalize(value, out var operatingSystemId)
            ? operatingSystemId
            : throw new ArgumentException($"Unknown operating system identifier: {value}", nameof(value));

    public static IReadOnlyList<string> GetAliases(string operatingSystemId)
    {
        var canonical = Normalize(operatingSystemId);
        return CanonicalByAlias
            .Where(pair => string.Equals(pair.Value, canonical, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToArray();
    }
}

public static class EngineFamilyIds
{
    public const string TransportRequestDispatch = "TransportRequestDispatchEngine";
    public const string OutboundBatch = "OutboundBatchEngine";
    public const string PickingBatch = "PickingBatchEngine";
    public const string GroupPurchaseClustering = "GroupPurchaseClusteringEngine";
    public const string CommunitySignal = "CommunitySignalEngine";
    public const string WorkflowPolicy = "WorkflowPolicyEngine";
}

public static class EngineImplementationIds
{
    public const string CargoYongdalDispatch = "CargoYongdalDispatchEngine";
    public const string FoodDeliveryDispatch = "FoodDeliveryDispatchEngine";
    public const string OutboundBatch = EngineFamilyIds.OutboundBatch;
    public const string PickingBatch = EngineFamilyIds.PickingBatch;
}

public static class RuntimeCapabilityStatuses
{
    public const string Active = "Active";
    public const string Declared = "Declared";
}

public sealed record EngineImplementationBinding(
    string ImplementationId,
    string EngineFamilyId);

public static class EngineImplementationCatalog
{
    private static readonly IReadOnlyDictionary<string, string> FamilyByImplementation =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [EngineImplementationIds.CargoYongdalDispatch] = EngineFamilyIds.TransportRequestDispatch,
            [EngineImplementationIds.FoodDeliveryDispatch] = EngineFamilyIds.TransportRequestDispatch,
            [EngineImplementationIds.OutboundBatch] = EngineFamilyIds.OutboundBatch,
            [EngineImplementationIds.PickingBatch] = EngineFamilyIds.PickingBatch
        };

    public static IReadOnlyList<EngineImplementationBinding> GetAll()
        => FamilyByImplementation
            .Select(pair => new EngineImplementationBinding(pair.Key, pair.Value))
            .OrderBy(binding => binding.ImplementationId, StringComparer.Ordinal)
            .ToArray();

    public static bool TryGetFamilyId(string implementationId, out string engineFamilyId)
        => FamilyByImplementation.TryGetValue(implementationId, out engineFamilyId!);
}
