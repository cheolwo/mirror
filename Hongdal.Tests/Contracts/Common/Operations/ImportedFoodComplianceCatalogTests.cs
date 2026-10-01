using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Tests.Contracts.Common.Operations;

public sealed class ImportedFoodComplianceCatalogTests
{
    [Fact]
    public void Profiles_AreInformationalAndCannotPerformRegulatedActions()
    {
        Assert.Equal(2, ImportedFoodComplianceCatalog.AllProfiles.Count);

        foreach (var profile in ImportedFoodComplianceCatalog.AllProfiles)
        {
            Assert.True(profile.IsInformationOnly);
            Assert.False(profile.IsOperationallyEnabled);
            Assert.False(profile.CanAutoFileDeclaration);
            Assert.False(profile.CanAutoClearOrRelease);
            Assert.False(profile.CanAutoSelectImporterOrBroker);
            Assert.True(profile.RequiresProductSpecificOfficialCheck);
            Assert.True(profile.RequiresQualifiedProfessionalReview);
        }
    }

    [Fact]
    public void Australia_IsAResearchDestination_NotAnEnabledOperatingMarket()
    {
        Assert.True(
            ImportedFoodDestinationCodes.TryNormalize("Australia", out var destinationCode));
        Assert.Equal(ImportedFoodDestinationCodes.Australia, destinationCode);
        Assert.False(OperatingMarketCodes.IsSupported("AU"));
    }

    [Fact]
    public void UnsupportedDestination_DoesNotFallBackToAnotherCountry()
    {
        Assert.False(
            ImportedFoodComplianceCatalog.TryGetProfile("CA", out var profile));
        Assert.Null(profile);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ImportedFoodComplianceCatalog.GetProfile("CA"));
    }

    [Fact]
    public void UnitedStatesBaseChecklist_RequiresJurisdictionEntryAndAgencyRelease()
    {
        var requirements = ImportedFoodComplianceCatalog.ResolveRequirements("US");
        var codes = requirements.Select(item => item.Code).ToHashSet();

        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .UnitedStatesDetermineAgencyJurisdiction,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.UnitedStatesCustomsEntry,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.UnitedStatesAgencyHoldAndRelease,
            codes);
        Assert.DoesNotContain(
            ImportedFoodComplianceRequirementCodes.UnitedStatesPriorNotice,
            codes);
        Assert.DoesNotContain(
            ImportedFoodComplianceRequirementCodes
                .UnitedStatesFsisEligibilityCertificationAndReinspection,
            codes);
    }

    [Fact]
    public void UnitedStatesSeafoodChecklist_AddsFdaAndSeafoodRequirements()
    {
        var requirements = ImportedFoodComplianceCatalog.ResolveRequirements(
            "USA",
            [
                ImportedFoodProductScopeCodes.Seafood,
                ImportedFoodProductScopeCodes.RetailPackagedFood
            ]);
        var codes = requirements.Select(item => item.Code).ToHashSet();

        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .UnitedStatesFoodFacilityRegistration,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.UnitedStatesPriorNotice,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.UnitedStatesFsvp,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.UnitedStatesSeafoodHaccp,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.UnitedStatesLabelAndComposition,
            codes);
        Assert.DoesNotContain(
            ImportedFoodComplianceRequirementCodes
                .UnitedStatesFsisEligibilityCertificationAndReinspection,
            codes);
    }

    [Fact]
    public void UnitedStatesFsisChecklist_AddsAnimalHealthAndFsisBranch()
    {
        var requirements = ImportedFoodComplianceCatalog.ResolveRequirements(
            ImportedFoodDestinationCodes.UnitedStates,
            [
                ImportedFoodProductScopeCodes
                    .FsisRegulatedMeatPoultryEggAndSiluriformes
            ]);
        var codes = requirements.Select(item => item.Code).ToHashSet();

        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .UnitedStatesAnimalProductAdmissibility,
            codes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .UnitedStatesFsisEligibilityCertificationAndReinspection,
            codes);
        Assert.DoesNotContain(
            ImportedFoodComplianceRequirementCodes.UnitedStatesPriorNotice,
            codes);
    }

    [Fact]
    public void AustraliaChecklist_SeparatesGeneralAndConditionalRequirements()
    {
        var baseRequirements = ImportedFoodComplianceCatalog.ResolveRequirements("AU");
        var baseCodes = baseRequirements.Select(item => item.Code).ToHashSet();

        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.AustraliaBiconProductAssessment,
            baseCodes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.AustraliaFullImportDeclaration,
            baseCodes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .AustraliaIfisReferralInspectionAndTesting,
            baseCodes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .AustraliaFoodStandardsCodeCompliance,
            baseCodes);
        Assert.DoesNotContain(
            ImportedFoodComplianceRequirementCodes.AustraliaRiskFoodCertification,
            baseCodes);
        Assert.DoesNotContain(
            ImportedFoodComplianceRequirementCodes
                .AustraliaCountryOfOriginFoodLabeling,
            baseCodes);

        var retailRiskRequirements = ImportedFoodComplianceCatalog.ResolveRequirements(
            "Australia",
            [
                ImportedFoodProductScopeCodes.AustraliaRiskFood,
                ImportedFoodProductScopeCodes.RetailPackagedFood
            ]);
        var retailRiskCodes = retailRiskRequirements
            .Select(item => item.Code)
            .ToHashSet();

        Assert.Contains(
            ImportedFoodComplianceRequirementCodes.AustraliaRiskFoodCertification,
            retailRiskCodes);
        Assert.Contains(
            ImportedFoodComplianceRequirementCodes
                .AustraliaCountryOfOriginFoodLabeling,
            retailRiskCodes);
    }

    [Fact]
    public void Catalog_UsesCurrentOfficialSourcesAndResolvableReferences()
    {
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "www.govinfo.gov",
            "www.cbp.gov",
            "www.fda.gov",
            "www.ecfr.gov",
            "www.aphis.usda.gov",
            "www.fsis.usda.gov",
            "www.legislation.gov.au",
            "www.abf.gov.au",
            "www.agriculture.gov.au",
            "bicon.agriculture.gov.au",
            "www.foodstandards.gov.au"
        };

        Assert.All(
            ImportedFoodComplianceCatalog.AllOfficialReferences,
            reference =>
            {
                Assert.True(Uri.TryCreate(reference.SourceUrl, UriKind.Absolute, out var uri));
                Assert.Equal(Uri.UriSchemeHttps, uri!.Scheme);
                Assert.Contains(uri.Host, allowedHosts);
                Assert.Equal(new DateOnly(2026, 7, 19), reference.ReviewedOn);
                Assert.DoesNotContain(
                    "Conditionally Non-prohibited Goods) Determination 2016",
                    reference.Citation,
                    StringComparison.OrdinalIgnoreCase);
            });

        var referencedCodes = ImportedFoodComplianceCatalog.AllProfiles
            .SelectMany(profile => profile.RequirementCodes)
            .Select(ImportedFoodComplianceCatalog.GetRequirement)
            .SelectMany(requirement => requirement.OfficialReferenceCodes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.All(
            referencedCodes,
            code => Assert.NotNull(
                ImportedFoodComplianceCatalog.GetOfficialReference(code)));
    }
}
