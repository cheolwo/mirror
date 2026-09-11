using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Ssalddel.Contracts.Common.Metadata
{

public static class SsalddelCodeFeatureKeys
{
    public const string FoodWorkflowLineage = "food-workflow-lineage";
    public const string ApifyActorIntegration = "apify-actor-integration";
    public const string AppContextImageBatch = "app-context-image-batch";
    public const string AppContextImageAsset = "app-context-image-asset";
    public const string CommunityAuthoringImage = "community-authoring-image";
    public const string CommunityActivityPaidDetail = "community-activity-paid-detail";
    public const string CommunityWorldMapObservation = "community-world-map-observation";
    public const string GroupPurchaseDemandProcessManager = "group-purchase-demand-process-manager";
    public const string GroupImportTradeReadiness = "group-import-trade-readiness";
    public const string ImportedFoodKoreanLabelIntegration = "imported-food-korean-label-integration";
    public const string IntegratedSeedbedExhibition = "integrated-seedbed-exhibition";
    public const string PlatformDeliveryZoneLedger = "platform-delivery-zone-ledger";
    public const string OperationalDispatchCore = "operational-dispatch-core";
    public const string OperationalLogisticsOs = "operational-logistics-os";
    public const string PlatformSupplyBrokerage = "platform-supply-brokerage";
    public const string PotatoProductionDistributionWorld = "potato-production-distribution-world";
    public const string RegionalCultureImagePrompt = "regional-culture-image-prompt";
    public const string RegionalCulturePublicInstitution = "regional-culture-public-institution";
    public const string RegionalAgriculturalMap = "regional-agricultural-map";
    public const string CropReferenceInformation = "crop-reference-information";
    public const string CommonFoodProductIdentity = "common-food-product-identity";
    public const string HongikAcademyContentMap = "hongik-academy-content-map";
    public const string TransportExecutionProfile = "transport-execution-profile";
    public const string WarehouseInboundVertical = "warehouse-inbound-vertical";
    public const string WorldRolePerspective = "world-role-perspective";
    public const string TradeLedgerExtensions = "trade-ledger-extensions";

    public const string SimulationSessionLifecycle = "simulation-session-lifecycle";
    public const string SimulationParallelBattle = "simulation-parallel-battle";
    public const string SimulationFarmCombatInput = "simulation-farm-combat-input";
    public const string SimulationSaveReplay = "simulation-save-replay";
    public const string SimulationWorldDerivation = "simulation-world-derivation";
    public const string SimulationSyntyLandscape = "simulation-synty-landscape";
    public const string SimulationWorldStreaming = "simulation-world-streaming";
    public const string SimulationFarmRealityEvidence = "simulation-farm-reality-evidence";
    public const string UnityResilientWorldLoad = "unity-resilient-world-load";
}

public enum SsalddelCodeLayer
{
    Contract,
    Api,
    Application,
    Domain,
    Infrastructure,
    ExternalAdapter,
    ClientAdapter,
    ViewModel,
    View
}

[Flags]
public enum SsalddelCodeEffect
{
    None = 0,
    NetworkCall = 1 << 0,
    ThirdPartyApiCall = 1 << 1,
    PersistentRead = 1 << 2,
    PersistentWrite = 1 << 3,
    ObjectStorageRead = 1 << 4,
    ObjectStorageWrite = 1 << 5,
    UiStateMutation = 1 << 6,
    MayIncurExternalCost = 1 << 7,
    StateMutation = 1 << 8
}

[Flags]
public enum SsalddelCodeDataScope
{
    None = 0,
    OperationalState = 1 << 0,
    SharedPublicData = 1 << 1,
    SimulationState = 1 << 2,
    DerivedWorld = 1 << 3,
    ClientPresentation = 1 << 4
}

public enum SsalddelCodeExecutionStage
{
    Unspecified,
    Definition,
    Query,
    Preview,
    Confirm,
    Tick,
    Projection,
    Persistence,
    Presentation
}

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct,
    AllowMultiple = true,
    Inherited = false)]
public sealed class SsalddelCodeMetadataAttribute : Attribute
{
    public SsalddelCodeMetadataAttribute(
        string featureKey,
        SsalddelCodeLayer layer,
        string responsibility)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            throw new ArgumentException("기능 키는 비어 있을 수 없습니다.", nameof(featureKey));
        }

        if (string.IsNullOrWhiteSpace(responsibility))
        {
            throw new ArgumentException("책임 설명은 비어 있을 수 없습니다.", nameof(responsibility));
        }

        FeatureKey = featureKey.Trim();
        Layer = layer;
        Responsibility = responsibility.Trim();
    }

    public string FeatureKey { get; }
    public SsalddelCodeLayer Layer { get; }
    public string Responsibility { get; }
    public SsalddelCodeEffect Effects { get; set; }
    public Type? ContractType { get; set; }
    public int FlowOrder { get; set; }
    public string Boundary { get; set; } = string.Empty;
    public string StepKey { get; set; } = string.Empty;
    public string[] DependsOnStepKeys { get; set; } = Array.Empty<string>();
    public SsalddelCodeExecutionStage ExecutionStage { get; set; }
    public SsalddelCodeDataScope ReadsFrom { get; set; }
    public SsalddelCodeDataScope WritesTo { get; set; }
    // 코드 출처이며 런타임 호출·데이터 읽기 권한을 뜻하지 않는다.
    public string[] SourceCodeRefs { get; set; } = Array.Empty<string>();
    public string ReuseKind { get; set; } = string.Empty;
    public string[] SharedRuleRefs { get; set; } = Array.Empty<string>();
    public string Adaptation { get; set; } = string.Empty;
}

public sealed record SsalddelCodeMetadataDescriptor(
    Type ComponentType,
    string FeatureKey,
    SsalddelCodeLayer Layer,
    string Responsibility,
    SsalddelCodeEffect Effects,
    Type? ContractType,
    int FlowOrder,
    string Boundary)
{
    public string StepKey { get; init; } = string.Empty;
    public IReadOnlyList<string> DependsOnStepKeys { get; init; } = Array.Empty<string>();
    public SsalddelCodeExecutionStage ExecutionStage { get; init; }
    public SsalddelCodeDataScope ReadsFrom { get; init; }
    public SsalddelCodeDataScope WritesTo { get; init; }
    public IReadOnlyList<string> SourceCodeRefs { get; init; } = Array.Empty<string>();
    public string ReuseKind { get; init; } = string.Empty;
    public IReadOnlyList<string> SharedRuleRefs { get; init; } = Array.Empty<string>();
    public string Adaptation { get; init; } = string.Empty;
}

public static class SsalddelCodeMetadataReader
{
    public static IReadOnlyList<SsalddelCodeMetadataDescriptor> Read(params Assembly[] assemblies)
    {
        if (assemblies is null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }

        return assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .SelectMany(GetLoadableTypes)
            .SelectMany(ReadType)
            .OrderBy(metadata => metadata.FeatureKey, StringComparer.Ordinal)
            .ThenBy(metadata => metadata.FlowOrder)
            .ThenBy(metadata => metadata.ComponentType.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<SsalddelCodeMetadataDescriptor> ReadFeature(
        string featureKey,
        params Assembly[] assemblies)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            throw new ArgumentException("기능 키는 비어 있을 수 없습니다.", nameof(featureKey));
        }

        return Read(assemblies)
            .Where(metadata => string.Equals(metadata.FeatureKey, featureKey.Trim(), StringComparison.Ordinal))
            .ToArray();
    }

    public static IReadOnlyList<SsalddelCodeMetadataDescriptor> Read(Type componentType)
    {
        if (componentType is null)
        {
            throw new ArgumentNullException(nameof(componentType));
        }

        return ReadType(componentType).ToArray();
    }

    private static IEnumerable<SsalddelCodeMetadataDescriptor> ReadType(Type componentType)
        => componentType
            .GetCustomAttributes<SsalddelCodeMetadataAttribute>(inherit: false)
            .Select(attribute => new SsalddelCodeMetadataDescriptor(
                componentType,
                attribute.FeatureKey,
                attribute.Layer,
                attribute.Responsibility,
                attribute.Effects,
                attribute.ContractType,
                attribute.FlowOrder,
                attribute.Boundary)
            {
                StepKey = attribute.StepKey.Trim(),
                DependsOnStepKeys = NormalizeStepKeys(attribute.DependsOnStepKeys),
                ExecutionStage = attribute.ExecutionStage,
                ReadsFrom = attribute.ReadsFrom,
                WritesTo = attribute.WritesTo,
                SourceCodeRefs = NormalizeStepKeys(attribute.SourceCodeRefs),
                ReuseKind = (attribute.ReuseKind ?? string.Empty).Trim(),
                SharedRuleRefs = NormalizeStepKeys(attribute.SharedRuleRefs),
                Adaptation = (attribute.Adaptation ?? string.Empty).Trim()
            });

    private static IReadOnlyList<string> NormalizeStepKeys(IEnumerable<string>? stepKeys)
        => (stepKeys ?? Array.Empty<string>())
            .Where(stepKey => !string.IsNullOrWhiteSpace(stepKey))
            .Select(stepKey => stepKey.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(stepKey => stepKey, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }
}
}
