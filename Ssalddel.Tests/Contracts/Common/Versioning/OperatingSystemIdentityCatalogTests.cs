using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Education;
using Ssalddel.Contracts.Common.Versioning;

namespace Ssalddel.Tests.Contracts.Common.Versioning;

public sealed class OperatingSystemIdentityCatalogTests
{
    [Theory]
    [InlineData("DomesticCargoTransport", OperatingSystemIds.DomesticCargoTransport)]
    [InlineData("DomesticCargoTransportOS", OperatingSystemIds.DomesticCargoTransport)]
    [InlineData("GroupPurchaseDemand", OperatingSystemIds.GroupPurchaseDemand)]
    [InlineData("GroupPurchaseDemandOS", OperatingSystemIds.GroupPurchaseDemand)]
    [InlineData("fooddelivery", OperatingSystemIds.FoodDelivery)]
    [InlineData("FoodDeliveryOS", OperatingSystemIds.FoodDelivery)]
    public void LegacyAndPersistentAliases_NormalizeToCanonicalId(string value, string expected)
    {
        Assert.Equal(expected, OperatingSystemIds.Normalize(value));
    }

    [Fact]
    public void ApiOperatingSystems_MapToUniqueCanonicalIds()
    {
        var canonicalIds = Enum.GetValues<SsalddelOperatingSystem>()
            .Select(SsalddelOperatingSystems.GetCanonicalId)
            .ToArray();

        Assert.Equal(canonicalIds.Length, canonicalIds.Distinct(StringComparer.Ordinal).Count());
        Assert.All(canonicalIds, id => Assert.Contains(id, OperatingSystemIds.All));
    }

    [Fact]
    public void ExistingLedgerAndEducationCodes_UseCanonicalCatalog()
    {
        Assert.Equal(OperatingSystemIds.DomesticCargoTransport, CommunityLedgerOperatingSystemCodes.DomesticCargoTransport);
        Assert.Equal(OperatingSystemIds.GroupPurchaseImport, CommunityLedgerOperatingSystemCodes.GroupPurchaseImport);
        Assert.Equal(OperatingSystemIds.EducationFieldExperience, 현장체험활동원장상수.대상OsCode);
    }

    [Fact]
    public void EveryCommunityLedgerTemplate_UsesKnownCanonicalOperatingSystemId()
    {
        var unknownTemplates = CommunityLedgerTemplateCatalog.All
            .Where(template => !OperatingSystemIds.TryNormalize(template.TargetOperatingSystemCode, out _))
            .Select(template => template.Key)
            .ToArray();

        Assert.Empty(unknownTemplates);
    }

    [Fact]
    public void EverySchedulingPolicy_ReferencesAnEngineDeclaredByItsOperatingSystem()
    {
        var invalidPolicies = SsalddelOperatingSystems.GetAll()
            .SelectMany(operatingSystem => operatingSystem.SchedulingPolicies
                .Where(policy => operatingSystem.Engines.All(engine =>
                    !string.Equals(engine.EngineCode, policy.AppliedEngineCode, StringComparison.Ordinal)))
                .Select(policy => $"{operatingSystem.OperatingSystem}:{policy.PolicyCode}"))
            .ToArray();

        Assert.Empty(invalidPolicies);
    }

    [Fact]
    public void DispatchImplementations_MapToLogicalEngineFamily()
    {
        Assert.True(EngineImplementationCatalog.TryGetFamilyId(
            EngineImplementationIds.CargoYongdalDispatch,
            out var cargoFamily));
        Assert.True(EngineImplementationCatalog.TryGetFamilyId(
            EngineImplementationIds.FoodDeliveryDispatch,
            out var foodFamily));

        Assert.Equal(EngineFamilyIds.TransportRequestDispatch, cargoFamily);
        Assert.Equal(EngineFamilyIds.TransportRequestDispatch, foodFamily);
        Assert.True(EngineImplementationCatalog.TryGetFamilyId(
            EngineImplementationIds.OutboundBatch,
            out var outboundFamily));
        Assert.True(EngineImplementationCatalog.TryGetFamilyId(
            EngineImplementationIds.PickingBatch,
            out var pickingFamily));
        Assert.True(EngineImplementationCatalog.TryGetFamilyId(
            EngineImplementationIds.GroupPurchaseClustering,
            out var groupPurchaseFamily));
        Assert.Equal(EngineFamilyIds.OutboundBatch, outboundFamily);
        Assert.Equal(EngineFamilyIds.PickingBatch, pickingFamily);
        Assert.Equal(EngineFamilyIds.GroupPurchaseClustering, groupPurchaseFamily);
    }

    [Fact]
    public void CargoAndFoodOperatingSystems_OwnSeparateOrderedLifecycles()
    {
        var cargo = OperatingSystemLifecycleCatalog.Get(OperatingSystemIds.DomesticCargoTransport);
        var food = OperatingSystemLifecycleCatalog.Get(OperatingSystemIds.FoodDelivery);

        Assert.Equal(8, cargo.Stages.Count);
        Assert.Equal(8, food.Stages.Count);
        Assert.Equal(cargo.Stages.OrderBy(stage => stage.Sequence), cargo.Stages);
        Assert.Equal(food.Stages.OrderBy(stage => stage.Sequence), food.Stages);
        Assert.Contains(cargo.Stages, stage => stage.StageId == OperatingSystemLifecycleStageIds.CargoTermsAgreement);
        Assert.Contains(food.Stages, stage => stage.StageId == OperatingSystemLifecycleStageIds.FoodCancellationCompensation);
        Assert.Empty(cargo.Stages.Select(stage => stage.StageId).Intersect(food.Stages.Select(stage => stage.StageId)));
    }

    [Fact]
    public void DispatchEngineCatalog_IsolatesImplementationsByOwningOperatingSystem()
    {
        var cargo = OperatingSystemEngineCatalog.GetByOperatingSystem(OperatingSystemIds.DomesticCargoTransport);
        var food = OperatingSystemEngineCatalog.GetByOperatingSystem(OperatingSystemIds.FoodDelivery);

        var cargoPrimary = Assert.Single(cargo, entry =>
            entry.Role == OperatingSystemEngineRoles.Primary &&
            entry.ActivationStatus == OperatingSystemEngineActivationStatuses.Active);
        var foodPrimary = Assert.Single(food, entry =>
            entry.Role == OperatingSystemEngineRoles.Primary &&
            entry.ActivationStatus == OperatingSystemEngineActivationStatuses.Active);

        Assert.Equal(EngineImplementationIds.CargoYongdalDispatch, cargoPrimary.ImplementationId);
        Assert.Equal(EngineImplementationIds.FoodDeliveryDispatch, foodPrimary.ImplementationId);
        Assert.DoesNotContain(cargo, entry => entry.ImplementationId == EngineImplementationIds.FoodDeliveryDispatch);
        Assert.DoesNotContain(food, entry => entry.ImplementationId == EngineImplementationIds.CargoYongdalDispatch);
        Assert.DoesNotContain(OperatingSystemEngineCatalog.GetAll(), entry =>
            entry.Role is OperatingSystemEngineRoles.ApprovedSafeFallback or OperatingSystemEngineRoles.Shadow);
    }
}
