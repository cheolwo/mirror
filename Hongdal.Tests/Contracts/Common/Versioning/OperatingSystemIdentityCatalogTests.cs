using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Education;
using Hongdal.Contracts.Common.Versioning;

namespace Hongdal.Tests.Contracts.Common.Versioning;

public sealed class OperatingSystemIdentityCatalogTests
{
    [Theory]
    [InlineData("DomesticCargoTransport", OperatingSystemIds.DomesticCargoTransport)]
    [InlineData("DomesticCargoTransportOS", OperatingSystemIds.DomesticCargoTransport)]
    [InlineData("fooddelivery", OperatingSystemIds.FoodDelivery)]
    [InlineData("FoodDeliveryOS", OperatingSystemIds.FoodDelivery)]
    public void LegacyAndPersistentAliases_NormalizeToCanonicalId(string value, string expected)
    {
        Assert.Equal(expected, OperatingSystemIds.Normalize(value));
    }

    [Fact]
    public void ApiOperatingSystems_MapToUniqueCanonicalIds()
    {
        var canonicalIds = Enum.GetValues<HongdalOperatingSystem>()
            .Select(HongdalOperatingSystems.GetCanonicalId)
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
        var invalidPolicies = HongdalOperatingSystems.GetAll()
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
        Assert.Equal(EngineFamilyIds.OutboundBatch, outboundFamily);
        Assert.Equal(EngineFamilyIds.PickingBatch, pickingFamily);
    }
}
