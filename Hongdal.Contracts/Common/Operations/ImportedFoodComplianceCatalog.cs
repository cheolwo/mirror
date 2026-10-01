namespace Hongdal.Contracts.Common.Operations;

public static class ImportedFoodDestinationCodes
{
    public const string UnitedStates = "US";
    public const string Australia = "AU";

    public static readonly IReadOnlyList<string> All = [UnitedStates, Australia];

    public static bool TryNormalize(string? destinationCode, out string normalizedCode)
    {
        normalizedCode = string.Empty;
        if (string.IsNullOrWhiteSpace(destinationCode))
        {
            return false;
        }

        normalizedCode = destinationCode.Trim().ToUpperInvariant() switch
        {
            "US" or "USA" or "UNITEDSTATES" or "UNITEDSTATESOFAMERICA" => UnitedStates,
            "AU" or "AUS" or "AUSTRALIA" => Australia,
            _ => string.Empty
        };

        return normalizedCode.Length > 0;
    }
}

public static class ImportedFoodComplianceStageCodes
{
    public const string ProductClassification = "ProductClassification";
    public const string CustomsEntry = "CustomsEntry";
    public const string BiosecurityAndAgriculture = "BiosecurityAndAgriculture";
    public const string FacilityAndSupplierControls = "FacilityAndSupplierControls";
    public const string PreArrivalNotification = "PreArrivalNotification";
    public const string ProductSpecificControls = "ProductSpecificControls";
    public const string CertificatesAndPermits = "CertificatesAndPermits";
    public const string BorderInspectionAndRelease = "BorderInspectionAndRelease";
    public const string LabelingAndSale = "LabelingAndSale";
}

public static class ImportedFoodRequirementApplicabilityCodes
{
    public const string AlwaysAssess = "AlwaysAssess";
    public const string AppliesUnlessExempt = "AppliesUnlessExempt";
    public const string ConditionalByProductOriginProcessingAndUse =
        "ConditionalByProductOriginProcessingAndUse";
    public const string ConditionalOnAgencyReferral = "ConditionalOnAgencyReferral";
    public const string ConditionalForRetailSale = "ConditionalForRetailSale";
}

public static class ImportedFoodProductScopeCodes
{
    public const string AllHumanFood = "AllHumanFood";
    public const string FdaRegulatedHumanFood = "FdaRegulatedHumanFood";
    public const string FsisRegulatedMeatPoultryEggAndSiluriformes =
        "FsisRegulatedMeatPoultryEggAndSiluriformes";
    public const string PlantOrPlantProduct = "PlantOrPlantProduct";
    public const string AnimalOriginProduct = "AnimalOriginProduct";
    public const string Seafood = "Seafood";
    public const string Juice = "Juice";
    public const string AcidifiedOrLowAcidCannedFood =
        "AcidifiedOrLowAcidCannedFood";
    public const string RetailPackagedFood = "RetailPackagedFood";
    public const string AustraliaRiskFood = "AustraliaRiskFood";
}

public static class ImportedFoodRegulatoryAuthorityCodes
{
    public const string UnitedStatesCustomsAndBorderProtection = "US-CBP";
    public const string UnitedStatesFoodAndDrugAdministration = "US-FDA";
    public const string UnitedStatesAnimalAndPlantHealthInspectionService =
        "US-USDA-APHIS";
    public const string UnitedStatesFoodSafetyAndInspectionService =
        "US-USDA-FSIS";
    public const string AustraliaBorderForce = "AU-ABF";
    public const string AustraliaDepartmentOfAgricultureFisheriesAndForestry =
        "AU-DAFF";
    public const string FoodStandardsAustraliaNewZealand = "AU-FSANZ";
    public const string AustraliaCompetitionAndConsumerCommission = "AU-ACCC";
}

public static class ImportedFoodResponsiblePartyCodes
{
    public const string ImporterOfRecord = "ImporterOfRecord";
    public const string LicensedCustomsBrokerOrSelfFiler =
        "LicensedCustomsBrokerOrSelfFiler";
    public const string FsvpImporter = "FsvpImporter";
    public const string ForeignFoodFacility = "ForeignFoodFacility";
    public const string UnitedStatesAgentForForeignFacility =
        "UnitedStatesAgentForForeignFacility";
    public const string ForeignSupplier = "ForeignSupplier";
    public const string AustraliaOwnerImporter = "AustraliaOwnerImporter";
    public const string AustraliaLicensedCustomsBroker =
        "AustraliaLicensedCustomsBroker";
    public const string ForeignManufacturerOrExporter =
        "ForeignManufacturerOrExporter";
    public const string QualifiedFoodAndCustomsProfessional =
        "QualifiedFoodAndCustomsProfessional";
}

public static class ImportedFoodEvidenceDocumentCodes
{
    public const string ProductSpecification = "ProductSpecification";
    public const string IngredientAndProcessInformation =
        "IngredientAndProcessInformation";
    public const string TariffClassificationAndCustomsValue =
        "TariffClassificationAndCustomsValue";
    public const string CommercialInvoice = "CommercialInvoice";
    public const string PackingList = "PackingList";
    public const string BillOfLadingOrAirWaybill = "BillOfLadingOrAirWaybill";
    public const string CountryOfOriginEvidence = "CountryOfOriginEvidence";
    public const string ImportPermitOrTreatmentEvidence =
        "ImportPermitOrTreatmentEvidence";
    public const string OfficialHealthOrGovernmentCertificate =
        "OfficialHealthOrGovernmentCertificate";
    public const string FoodFacilityRegistrationEvidence =
        "FoodFacilityRegistrationEvidence";
    public const string PriorNoticeConfirmation = "PriorNoticeConfirmation";
    public const string ForeignSupplierVerificationRecords =
        "ForeignSupplierVerificationRecords";
    public const string HaccpOrScheduledProcessRecords =
        "HaccpOrScheduledProcessRecords";
    public const string LabelArtworkAndAllergenDeclaration =
        "LabelArtworkAndAllergenDeclaration";
    public const string CustomsEntryAndAgencyData = "CustomsEntryAndAgencyData";
    public const string FoodControlCertificate = "FoodControlCertificate";
    public const string InspectionOrLaboratoryResult =
        "InspectionOrLaboratoryResult";
    public const string ReleaseEvidence = "ReleaseEvidence";
}

public static class ImportedFoodOfficialReferenceTypeCodes
{
    public const string Statute = "Statute";
    public const string Regulation = "Regulation";
    public const string LegislativeInstrument = "LegislativeInstrument";
    public const string OfficialGuidance = "OfficialGuidance";
    public const string OfficialDecisionSystem = "OfficialDecisionSystem";
}

public static class ImportedFoodOfficialReferenceCodes
{
    public const string UnitedStatesCustomsEntryStatute =
        "US.CBP.19USC1484";
    public const string UnitedStatesCbpBasicImporting =
        "US.CBP.BasicImporting";
    public const string UnitedStatesFdcaImportStatute =
        "US.FDA.21USC381";
    public const string UnitedStatesFoodFacilityRegistrationStatute =
        "US.FDA.21USC350d";
    public const string UnitedStatesFoodFacilityRegistrationRegulation =
        "US.FDA.21CFR1.SubpartH";
    public const string UnitedStatesFdaImportingHumanFoods =
        "US.FDA.ImportingHumanFoods";
    public const string UnitedStatesPriorNoticeRegulation =
        "US.FDA.21CFR1.SubpartI";
    public const string UnitedStatesPriorNoticeGuidance =
        "US.FDA.PriorNotice";
    public const string UnitedStatesFsvpStatute = "US.FDA.21USC384a";
    public const string UnitedStatesFsvpRegulation =
        "US.FDA.21CFR1.SubpartL";
    public const string UnitedStatesFsvpRuleGuidance =
        "US.FDA.FSVP.FinalRule";
    public const string UnitedStatesFoodLabelingRegulation =
        "US.FDA.21CFR101";
    public const string UnitedStatesSeafoodHaccpRegulation =
        "US.FDA.21CFR123";
    public const string UnitedStatesJuiceHaccpRegulation =
        "US.FDA.21CFR120";
    public const string UnitedStatesAcidifiedLowAcidCannedFoodRules =
        "US.FDA.21CFR108.113.114";
    public const string UnitedStatesAphisPlantImportRequirements =
        "US.USDA.APHIS.PlantImports";
    public const string UnitedStatesAphisAnimalProductImportRequirements =
        "US.USDA.APHIS.AnimalProducts";
    public const string UnitedStatesFsisImportRequirements =
        "US.USDA.FSIS.ImportRequirements";

    public const string AustraliaCustomsAct = "AU.ABF.CustomsAct1901";
    public const string AustraliaAbfImportDeclaration =
        "AU.ABF.ImportDeclaration";
    public const string AustraliaBiosecurityAct = "AU.DAFF.BiosecurityAct2015";
    public const string AustraliaConditionalGoodsDetermination =
        "AU.DAFF.ConditionalGoodsDetermination2021";
    public const string AustraliaImportedFoodControlAct =
        "AU.DAFF.ImportedFoodControlAct1992";
    public const string AustraliaImportedFoodControlRegulations =
        "AU.DAFF.ImportedFoodControlRegulations2019";
    public const string AustraliaImportedFoodControlOrder =
        "AU.DAFF.ImportedFoodControlOrder2019";
    public const string AustraliaImportedFoodLegislationGuide =
        "AU.DAFF.ImportedFoodLegislation";
    public const string AustraliaBicon = "AU.DAFF.BICON";
    public const string AustraliaFoodImporterGuide =
        "AU.DAFF.FoodImporterGuide";
    public const string AustraliaFoodStandardsCode =
        "AU.FSANZ.FoodStandardsCode";
    public const string AustraliaImportedFoodsGuide =
        "AU.FSANZ.ImportedFoods";
    public const string AustraliaCountryOfOriginFoodStandard =
        "AU.ACCC.CountryOfOriginFoodStandard2016";
}

public static class ImportedFoodComplianceRequirementCodes
{
    public const string UnitedStatesDetermineAgencyJurisdiction =
        "US.Product.DetermineAgencyJurisdiction";
    public const string UnitedStatesCustomsEntry = "US.CBP.CustomsEntry";
    public const string UnitedStatesPlantAdmissibility =
        "US.APHIS.PlantAdmissibility";
    public const string UnitedStatesAnimalProductAdmissibility =
        "US.APHIS.AnimalProductAdmissibility";
    public const string UnitedStatesFoodFacilityRegistration =
        "US.FDA.FoodFacilityRegistration";
    public const string UnitedStatesPriorNotice = "US.FDA.PriorNotice";
    public const string UnitedStatesFsvp = "US.FDA.FSVP";
    public const string UnitedStatesLabelAndComposition =
        "US.FDA.LabelAndComposition";
    public const string UnitedStatesSeafoodHaccp = "US.FDA.SeafoodHACCP";
    public const string UnitedStatesJuiceHaccp = "US.FDA.JuiceHACCP";
    public const string UnitedStatesAcidifiedLowAcidCannedFood =
        "US.FDA.AcidifiedLowAcidCannedFood";
    public const string UnitedStatesFsisEligibilityCertificationAndReinspection =
        "US.FSIS.EligibilityCertificationAndReinspection";
    public const string UnitedStatesAgencyHoldAndRelease =
        "US.Agency.HoldAndRelease";

    public const string AustraliaBiconProductAssessment =
        "AU.DAFF.BICON.ProductAssessment";
    public const string AustraliaFullImportDeclaration =
        "AU.ABF.FullImportDeclaration";
    public const string AustraliaBiosecurityPermitTreatmentAndEvidence =
        "AU.DAFF.Biosecurity.PermitTreatmentAndEvidence";
    public const string AustraliaIfisReferralInspectionAndTesting =
        "AU.DAFF.IFIS.ReferralInspectionAndTesting";
    public const string AustraliaRiskFoodCertification =
        "AU.DAFF.RiskFood.Certification";
    public const string AustraliaFoodStandardsCodeCompliance =
        "AU.FSANZ.CodeCompliance";
    public const string AustraliaCountryOfOriginFoodLabeling =
        "AU.CountryOfOriginFoodLabeling";
    public const string AustraliaFoodControlHoldAndRelease =
        "AU.DAFF.FoodControlHoldAndRelease";
}

public sealed record ImportedFoodOfficialReference(
    string Code,
    string Citation,
    string Title,
    string AuthorityCode,
    string ReferenceTypeCode,
    string SourceUrl,
    DateOnly ReviewedOn);

public sealed record ImportedFoodComplianceRequirement(
    string Code,
    string StageCode,
    string DisplayName,
    string Summary,
    string ApplicabilityCode,
    IReadOnlyList<string> ProductScopeCodes,
    IReadOnlyList<string> ResponsiblePartyCodes,
    IReadOnlyList<string> EvidenceDocumentCodes,
    IReadOnlyList<string> OfficialReferenceCodes,
    bool BlocksProgressWhenApplicable,
    bool RequiresCurrentOfficialCheck);

public sealed record ImportedFoodComplianceProfile(
    string DestinationCode,
    string DisplayName,
    string CatalogVersion,
    DateOnly ReviewedOn,
    IReadOnlyList<string> RequirementCodes,
    bool IsInformationOnly,
    bool IsOperationallyEnabled,
    bool CanAutoFileDeclaration,
    bool CanAutoClearOrRelease,
    bool CanAutoSelectImporterOrBroker,
    bool RequiresProductSpecificOfficialCheck,
    bool RequiresQualifiedProfessionalReview);

public static class ImportedFoodComplianceCatalog
{
    public const string CatalogVersion = "2026-07-19";

    private static readonly DateOnly ReviewDate = new(2026, 7, 19);

    private static readonly IReadOnlyDictionary<string, ImportedFoodOfficialReference>
        References = BuildReferences().ToDictionary(
            item => item.Code,
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, ImportedFoodComplianceRequirement>
        Requirements = BuildRequirements().ToDictionary(
            item => item.Code,
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, ImportedFoodComplianceProfile>
        Profiles = BuildProfiles().ToDictionary(
            item => item.DestinationCode,
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ImportedFoodComplianceProfile> AllProfiles { get; } =
        ImportedFoodDestinationCodes.All.Select(code => Profiles[code]).ToArray();

    public static IReadOnlyList<ImportedFoodOfficialReference> AllOfficialReferences { get; } =
        References.Values.OrderBy(item => item.Code, StringComparer.Ordinal).ToArray();

    public static ImportedFoodComplianceProfile GetProfile(string? destinationCode)
    {
        if (!TryGetProfile(destinationCode, out var profile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationCode),
                destinationCode,
                "The imported-food destination is not supported.");
        }

        return profile;
    }

    public static bool TryGetProfile(
        string? destinationCode,
        out ImportedFoodComplianceProfile profile)
    {
        if (!ImportedFoodDestinationCodes.TryNormalize(destinationCode, out var normalizedCode))
        {
            profile = null!;
            return false;
        }

        profile = Profiles[normalizedCode];
        return true;
    }

    public static ImportedFoodComplianceRequirement GetRequirement(string requirementCode)
        => Requirements.TryGetValue(requirementCode, out var requirement)
            ? requirement
            : throw new KeyNotFoundException(
                $"Imported-food requirement '{requirementCode}' was not found.");

    public static ImportedFoodOfficialReference GetOfficialReference(string referenceCode)
        => References.TryGetValue(referenceCode, out var reference)
            ? reference
            : throw new KeyNotFoundException(
                $"Imported-food official reference '{referenceCode}' was not found.");

    public static IReadOnlyList<ImportedFoodComplianceRequirement> ResolveRequirements(
        string? destinationCode,
        IEnumerable<string>? productScopeCodes = null)
    {
        var profile = GetProfile(destinationCode);
        var selectedScopes = ExpandProductScopes(productScopeCodes);

        return profile.RequirementCodes
            .Select(GetRequirement)
            .Where(requirement =>
                requirement.ProductScopeCodes.Contains(
                    ImportedFoodProductScopeCodes.AllHumanFood,
                    StringComparer.OrdinalIgnoreCase) ||
                requirement.ProductScopeCodes.Any(selectedScopes.Contains))
            .ToArray();
    }

    private static HashSet<string> ExpandProductScopes(IEnumerable<string>? productScopeCodes)
    {
        var scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (productScopeCodes is not null)
        {
            foreach (var scope in productScopeCodes.Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                scopes.Add(scope.Trim());
            }
        }

        if (scopes.Contains(ImportedFoodProductScopeCodes.Seafood) ||
            scopes.Contains(ImportedFoodProductScopeCodes.Juice) ||
            scopes.Contains(ImportedFoodProductScopeCodes.AcidifiedOrLowAcidCannedFood))
        {
            scopes.Add(ImportedFoodProductScopeCodes.FdaRegulatedHumanFood);
        }

        if (scopes.Contains(
                ImportedFoodProductScopeCodes.FsisRegulatedMeatPoultryEggAndSiluriformes))
        {
            scopes.Add(ImportedFoodProductScopeCodes.AnimalOriginProduct);
        }

        return scopes;
    }

    private static IReadOnlyList<ImportedFoodComplianceProfile> BuildProfiles()
        =>
        [
            new(
                ImportedFoodDestinationCodes.UnitedStates,
                "United States imported human food",
                CatalogVersion,
                ReviewDate,
                [
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesDetermineAgencyJurisdiction,
                    ImportedFoodComplianceRequirementCodes.UnitedStatesCustomsEntry,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesPlantAdmissibility,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesAnimalProductAdmissibility,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesFoodFacilityRegistration,
                    ImportedFoodComplianceRequirementCodes.UnitedStatesPriorNotice,
                    ImportedFoodComplianceRequirementCodes.UnitedStatesFsvp,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesLabelAndComposition,
                    ImportedFoodComplianceRequirementCodes.UnitedStatesSeafoodHaccp,
                    ImportedFoodComplianceRequirementCodes.UnitedStatesJuiceHaccp,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesAcidifiedLowAcidCannedFood,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesFsisEligibilityCertificationAndReinspection,
                    ImportedFoodComplianceRequirementCodes
                        .UnitedStatesAgencyHoldAndRelease
                ],
                IsInformationOnly: true,
                IsOperationallyEnabled: false,
                CanAutoFileDeclaration: false,
                CanAutoClearOrRelease: false,
                CanAutoSelectImporterOrBroker: false,
                RequiresProductSpecificOfficialCheck: true,
                RequiresQualifiedProfessionalReview: true),
            new(
                ImportedFoodDestinationCodes.Australia,
                "Australia imported food for sale",
                CatalogVersion,
                ReviewDate,
                [
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaBiconProductAssessment,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaFullImportDeclaration,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaBiosecurityPermitTreatmentAndEvidence,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaIfisReferralInspectionAndTesting,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaRiskFoodCertification,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaFoodStandardsCodeCompliance,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaCountryOfOriginFoodLabeling,
                    ImportedFoodComplianceRequirementCodes
                        .AustraliaFoodControlHoldAndRelease
                ],
                IsInformationOnly: true,
                IsOperationallyEnabled: false,
                CanAutoFileDeclaration: false,
                CanAutoClearOrRelease: false,
                CanAutoSelectImporterOrBroker: false,
                RequiresProductSpecificOfficialCheck: true,
                RequiresQualifiedProfessionalReview: true)
        ];

    private static IReadOnlyList<ImportedFoodComplianceRequirement> BuildRequirements()
        =>
        [
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesDetermineAgencyJurisdiction,
                ImportedFoodComplianceStageCodes.ProductClassification,
                "Determine the competent U.S. agencies",
                "Classify the product, ingredients, processing, origin and intended use before deciding whether FDA, FSIS and APHIS requirements apply.",
                ImportedFoodRequirementApplicabilityCodes.AlwaysAssess,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.QualifiedFoodAndCustomsProfessional
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ProductSpecification,
                    ImportedFoodEvidenceDocumentCodes.IngredientAndProcessInformation,
                    ImportedFoodEvidenceDocumentCodes.CountryOfOriginEvidence
                ],
                [
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFdaImportingHumanFoods,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFsisImportRequirements,
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesAphisPlantImportRequirements,
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesAphisAnimalProductImportRequirements
                ],
                blocksProgress: false),
            Requirement(
                ImportedFoodComplianceRequirementCodes.UnitedStatesCustomsEntry,
                ImportedFoodComplianceStageCodes.CustomsEntry,
                "File the CBP entry with reasonable care",
                "The importer of record remains responsible for entry data, classification, customs value, origin and admissibility even when a licensed broker files the entry.",
                ImportedFoodRequirementApplicabilityCodes.AlwaysAssess,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.LicensedCustomsBrokerOrSelfFiler
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.TariffClassificationAndCustomsValue,
                    ImportedFoodEvidenceDocumentCodes.CommercialInvoice,
                    ImportedFoodEvidenceDocumentCodes.PackingList,
                    ImportedFoodEvidenceDocumentCodes.BillOfLadingOrAirWaybill,
                    ImportedFoodEvidenceDocumentCodes.CustomsEntryAndAgencyData
                ],
                [
                    ImportedFoodOfficialReferenceCodes.UnitedStatesCustomsEntryStatute,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesCbpBasicImporting
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes.UnitedStatesPlantAdmissibility,
                ImportedFoodComplianceStageCodes.BiosecurityAndAgriculture,
                "Confirm APHIS plant-product admissibility",
                "Use the current APHIS commodity and country-of-origin requirements to determine eligibility, permit, phytosanitary certificate, treatment and inspection conditions.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.PlantOrPlantProduct],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ProductSpecification,
                    ImportedFoodEvidenceDocumentCodes.CountryOfOriginEvidence,
                    ImportedFoodEvidenceDocumentCodes.ImportPermitOrTreatmentEvidence,
                    ImportedFoodEvidenceDocumentCodes.OfficialHealthOrGovernmentCertificate
                ],
                [
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesAphisPlantImportRequirements
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesAnimalProductAdmissibility,
                ImportedFoodComplianceStageCodes.BiosecurityAndAgriculture,
                "Confirm APHIS animal-product admissibility",
                "Check current animal-health status, permit, certificate and ACE data requirements in addition to any FDA or FSIS food-safety path.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.AnimalOriginProduct],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ProductSpecification,
                    ImportedFoodEvidenceDocumentCodes.CountryOfOriginEvidence,
                    ImportedFoodEvidenceDocumentCodes.ImportPermitOrTreatmentEvidence,
                    ImportedFoodEvidenceDocumentCodes.OfficialHealthOrGovernmentCertificate
                ],
                [
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesAphisAnimalProductImportRequirements
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesFoodFacilityRegistration,
                ImportedFoodComplianceStageCodes.FacilityAndSupplierControls,
                "Verify food-facility registration",
                "Most facilities that manufacture, process, pack or hold FDA-regulated food must be registered; a foreign facility must designate a U.S. agent when the rule applies.",
                ImportedFoodRequirementApplicabilityCodes.AppliesUnlessExempt,
                [ImportedFoodProductScopeCodes.FdaRegulatedHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.ForeignFoodFacility,
                    ImportedFoodResponsiblePartyCodes.UnitedStatesAgentForForeignFacility,
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord
                ],
                [ImportedFoodEvidenceDocumentCodes.FoodFacilityRegistrationEvidence],
                [
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesFoodFacilityRegistrationStatute,
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesFoodFacilityRegistrationRegulation,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFdaImportingHumanFoods
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes.UnitedStatesPriorNotice,
                ImportedFoodComplianceStageCodes.PreArrivalNotification,
                "Submit FDA prior notice",
                "Prior notice is generally required before FDA-regulated food is imported or offered for import unless a specific exclusion or exemption applies.",
                ImportedFoodRequirementApplicabilityCodes.AppliesUnlessExempt,
                [ImportedFoodProductScopeCodes.FdaRegulatedHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.LicensedCustomsBrokerOrSelfFiler
                ],
                [ImportedFoodEvidenceDocumentCodes.PriorNoticeConfirmation],
                [
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFdcaImportStatute,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesPriorNoticeRegulation,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesPriorNoticeGuidance
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes.UnitedStatesFsvp,
                ImportedFoodComplianceStageCodes.FacilityAndSupplierControls,
                "Establish the applicable FSVP",
                "The covered FSVP importer must maintain a food-and-supplier-specific verification program; this role is not necessarily the CBP importer of record.",
                ImportedFoodRequirementApplicabilityCodes.AppliesUnlessExempt,
                [ImportedFoodProductScopeCodes.FdaRegulatedHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.FsvpImporter,
                    ImportedFoodResponsiblePartyCodes.ForeignSupplier
                ],
                [ImportedFoodEvidenceDocumentCodes.ForeignSupplierVerificationRecords],
                [
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFsvpStatute,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFsvpRegulation,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFsvpRuleGuidance
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesLabelAndComposition,
                ImportedFoodComplianceStageCodes.LabelingAndSale,
                "Verify U.S. composition and labeling",
                "Check permitted ingredients and additives, identity, net quantity, nutrition, allergen, business identity and other product-specific labeling before offering the food for sale.",
                ImportedFoodRequirementApplicabilityCodes.AppliesUnlessExempt,
                [
                    ImportedFoodProductScopeCodes.FdaRegulatedHumanFood,
                    ImportedFoodProductScopeCodes.RetailPackagedFood
                ],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ProductSpecification,
                    ImportedFoodEvidenceDocumentCodes.LabelArtworkAndAllergenDeclaration
                ],
                [
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFdaImportingHumanFoods,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFoodLabelingRegulation
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes.UnitedStatesSeafoodHaccp,
                ImportedFoodComplianceStageCodes.ProductSpecificControls,
                "Verify seafood HACCP controls",
                "When seafood is FDA-regulated, confirm the processor and importer controls required by 21 CFR Part 123.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.Seafood],
                [
                    ImportedFoodResponsiblePartyCodes.ForeignFoodFacility,
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord
                ],
                [ImportedFoodEvidenceDocumentCodes.HaccpOrScheduledProcessRecords],
                [ImportedFoodOfficialReferenceCodes.UnitedStatesSeafoodHaccpRegulation]),
            Requirement(
                ImportedFoodComplianceRequirementCodes.UnitedStatesJuiceHaccp,
                ImportedFoodComplianceStageCodes.ProductSpecificControls,
                "Verify juice HACCP controls",
                "Juice processors and importers must satisfy the applicable 21 CFR Part 120 hazard-analysis, HACCP and importer-verification requirements.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.Juice],
                [
                    ImportedFoodResponsiblePartyCodes.ForeignFoodFacility,
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord
                ],
                [ImportedFoodEvidenceDocumentCodes.HaccpOrScheduledProcessRecords],
                [ImportedFoodOfficialReferenceCodes.UnitedStatesJuiceHaccpRegulation]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesAcidifiedLowAcidCannedFood,
                ImportedFoodComplianceStageCodes.ProductSpecificControls,
                "Verify acidified and low-acid canned food filings",
                "For covered shelf-stable acidified or low-acid canned foods, confirm processing-establishment registration and the scheduled process filing for the exact product and container.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.AcidifiedOrLowAcidCannedFood],
                [
                    ImportedFoodResponsiblePartyCodes.ForeignFoodFacility,
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord
                ],
                [ImportedFoodEvidenceDocumentCodes.HaccpOrScheduledProcessRecords],
                [
                    ImportedFoodOfficialReferenceCodes
                        .UnitedStatesAcidifiedLowAcidCannedFoodRules
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesFsisEligibilityCertificationAndReinspection,
                ImportedFoodComplianceStageCodes.CertificatesAndPermits,
                "Satisfy the FSIS import path",
                "Covered meat, poultry, egg products and Siluriformes products must follow the applicable eligible-country and establishment, official certification and FSIS reinspection requirements.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [
                    ImportedFoodProductScopeCodes
                        .FsisRegulatedMeatPoultryEggAndSiluriformes
                ],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter,
                    ImportedFoodResponsiblePartyCodes.LicensedCustomsBrokerOrSelfFiler
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.OfficialHealthOrGovernmentCertificate,
                    ImportedFoodEvidenceDocumentCodes.CustomsEntryAndAgencyData,
                    ImportedFoodEvidenceDocumentCodes.InspectionOrLaboratoryResult
                ],
                [ImportedFoodOfficialReferenceCodes.UnitedStatesFsisImportRequirements]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .UnitedStatesAgencyHoldAndRelease,
                ImportedFoodComplianceStageCodes.BorderInspectionAndRelease,
                "Confirm every applicable agency release",
                "Do not treat CBP cargo release as proof that FDA, FSIS or APHIS admissibility has also been resolved; preserve hold, examination, reinspection, refusal and final release evidence.",
                ImportedFoodRequirementApplicabilityCodes.AlwaysAssess,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.ImporterOfRecord,
                    ImportedFoodResponsiblePartyCodes.LicensedCustomsBrokerOrSelfFiler
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.CustomsEntryAndAgencyData,
                    ImportedFoodEvidenceDocumentCodes.InspectionOrLaboratoryResult,
                    ImportedFoodEvidenceDocumentCodes.ReleaseEvidence
                ],
                [
                    ImportedFoodOfficialReferenceCodes.UnitedStatesCbpBasicImporting,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFdaImportingHumanFoods,
                    ImportedFoodOfficialReferenceCodes.UnitedStatesFsisImportRequirements
                ]),

            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaBiconProductAssessment,
                ImportedFoodComplianceStageCodes.ProductClassification,
                "Run the current BICON assessment",
                "Search by the exact food, ingredients, processing, origin and end use to determine whether import is allowed and which biosecurity and food-safety case applies.",
                ImportedFoodRequirementApplicabilityCodes.AlwaysAssess,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ProductSpecification,
                    ImportedFoodEvidenceDocumentCodes.IngredientAndProcessInformation,
                    ImportedFoodEvidenceDocumentCodes.CountryOfOriginEvidence
                ],
                [
                    ImportedFoodOfficialReferenceCodes.AustraliaBiosecurityAct,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaConditionalGoodsDetermination,
                    ImportedFoodOfficialReferenceCodes.AustraliaBicon,
                    ImportedFoodOfficialReferenceCodes.AustraliaFoodImporterGuide
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaFullImportDeclaration,
                ImportedFoodComplianceStageCodes.CustomsEntry,
                "Lodge the Australian import declaration",
                "The owner/importer or licensed customs broker must lodge accurate goods, importer, transport, tariff and customs-value data and the food details required for border referral.",
                ImportedFoodRequirementApplicabilityCodes.AlwaysAssess,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.AustraliaLicensedCustomsBroker
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.TariffClassificationAndCustomsValue,
                    ImportedFoodEvidenceDocumentCodes.CommercialInvoice,
                    ImportedFoodEvidenceDocumentCodes.PackingList,
                    ImportedFoodEvidenceDocumentCodes.BillOfLadingOrAirWaybill,
                    ImportedFoodEvidenceDocumentCodes.CustomsEntryAndAgencyData
                ],
                [
                    ImportedFoodOfficialReferenceCodes.AustraliaCustomsAct,
                    ImportedFoodOfficialReferenceCodes.AustraliaAbfImportDeclaration,
                    ImportedFoodOfficialReferenceCodes.AustraliaFoodImporterGuide
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaBiosecurityPermitTreatmentAndEvidence,
                ImportedFoodComplianceStageCodes.BiosecurityAndAgriculture,
                "Meet BICON biosecurity conditions",
                "Obtain any permit before shipment and provide the treatment, processing, packaging and official evidence required by the current BICON case.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ImportPermitOrTreatmentEvidence,
                    ImportedFoodEvidenceDocumentCodes.OfficialHealthOrGovernmentCertificate
                ],
                [
                    ImportedFoodOfficialReferenceCodes.AustraliaBiosecurityAct,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaConditionalGoodsDetermination,
                    ImportedFoodOfficialReferenceCodes.AustraliaBicon
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaIfisReferralInspectionAndTesting,
                ImportedFoodComplianceStageCodes.BorderInspectionAndRelease,
                "Respond to IFIS referral, inspection and testing",
                "Food referred through the Imported Food Inspection Scheme may require document review, label inspection, sampling and laboratory testing under a Food Control Certificate.",
                ImportedFoodRequirementApplicabilityCodes.ConditionalOnAgencyReferral,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.AustraliaLicensedCustomsBroker
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.FoodControlCertificate,
                    ImportedFoodEvidenceDocumentCodes.LabelArtworkAndAllergenDeclaration,
                    ImportedFoodEvidenceDocumentCodes.InspectionOrLaboratoryResult
                ],
                [
                    ImportedFoodOfficialReferenceCodes.AustraliaImportedFoodControlAct,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodControlRegulations,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodControlOrder,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodLegislationGuide,
                    ImportedFoodOfficialReferenceCodes.AustraliaFoodImporterGuide
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaRiskFoodCertification,
                ImportedFoodComplianceStageCodes.CertificatesAndPermits,
                "Obtain risk-food certification when required",
                "Risk foods listed in the current Imported Food Control Order may require a recognized foreign-government certificate or food-safety-management certificate.",
                ImportedFoodRequirementApplicabilityCodes
                    .ConditionalByProductOriginProcessingAndUse,
                [ImportedFoodProductScopeCodes.AustraliaRiskFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [ImportedFoodEvidenceDocumentCodes.OfficialHealthOrGovernmentCertificate],
                [
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodControlOrder,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodLegislationGuide,
                    ImportedFoodOfficialReferenceCodes.AustraliaBicon
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaFoodStandardsCodeCompliance,
                ImportedFoodComplianceStageCodes.LabelingAndSale,
                "Verify Australia New Zealand Food Standards Code compliance",
                "Before sale, verify composition, additives, contaminants, microbiological limits, packaging, identity, nutrition and allergen labeling against the current Code.",
                ImportedFoodRequirementApplicabilityCodes.AlwaysAssess,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.ProductSpecification,
                    ImportedFoodEvidenceDocumentCodes.IngredientAndProcessInformation,
                    ImportedFoodEvidenceDocumentCodes.LabelArtworkAndAllergenDeclaration
                ],
                [
                    ImportedFoodOfficialReferenceCodes.AustraliaImportedFoodControlAct,
                    ImportedFoodOfficialReferenceCodes.AustraliaFoodStandardsCode,
                    ImportedFoodOfficialReferenceCodes.AustraliaImportedFoodsGuide
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaCountryOfOriginFoodLabeling,
                ImportedFoodComplianceStageCodes.LabelingAndSale,
                "Verify country-of-origin food labeling",
                "Determine whether the retail food must carry a country-of-origin statement or standard mark and substantiate any origin claim.",
                ImportedFoodRequirementApplicabilityCodes.ConditionalForRetailSale,
                [ImportedFoodProductScopeCodes.RetailPackagedFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.ForeignManufacturerOrExporter
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.CountryOfOriginEvidence,
                    ImportedFoodEvidenceDocumentCodes.LabelArtworkAndAllergenDeclaration
                ],
                [
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaCountryOfOriginFoodStandard,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodLegislationGuide
                ]),
            Requirement(
                ImportedFoodComplianceRequirementCodes
                    .AustraliaFoodControlHoldAndRelease,
                ImportedFoodComplianceStageCodes.BorderInspectionAndRelease,
                "Keep referred food on hold until released",
                "A Food Control Certificate identifies food that must remain on hold; do not distribute it until all biosecurity and imported-food directions are completed and release evidence is received.",
                ImportedFoodRequirementApplicabilityCodes.ConditionalOnAgencyReferral,
                [ImportedFoodProductScopeCodes.AllHumanFood],
                [
                    ImportedFoodResponsiblePartyCodes.AustraliaOwnerImporter,
                    ImportedFoodResponsiblePartyCodes.AustraliaLicensedCustomsBroker
                ],
                [
                    ImportedFoodEvidenceDocumentCodes.FoodControlCertificate,
                    ImportedFoodEvidenceDocumentCodes.InspectionOrLaboratoryResult,
                    ImportedFoodEvidenceDocumentCodes.ReleaseEvidence
                ],
                [
                    ImportedFoodOfficialReferenceCodes.AustraliaFoodImporterGuide,
                    ImportedFoodOfficialReferenceCodes
                        .AustraliaImportedFoodControlRegulations
                ])
        ];

    private static ImportedFoodComplianceRequirement Requirement(
        string code,
        string stageCode,
        string displayName,
        string summary,
        string applicabilityCode,
        IReadOnlyList<string> productScopeCodes,
        IReadOnlyList<string> responsiblePartyCodes,
        IReadOnlyList<string> evidenceDocumentCodes,
        IReadOnlyList<string> officialReferenceCodes,
        bool blocksProgress = true)
        => new(
            code,
            stageCode,
            displayName,
            summary,
            applicabilityCode,
            productScopeCodes,
            responsiblePartyCodes,
            evidenceDocumentCodes,
            officialReferenceCodes,
            blocksProgress,
            RequiresCurrentOfficialCheck: true);

    private static IReadOnlyList<ImportedFoodOfficialReference> BuildReferences()
        =>
        [
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesCustomsEntryStatute,
                "19 U.S.C. 1484",
                "Entry of merchandise and importer reasonable care",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesCustomsAndBorderProtection,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.govinfo.gov/link/uscode/19/1484"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesCbpBasicImporting,
                "CBP basic importing guidance",
                "Basic Importing and Exporting",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesCustomsAndBorderProtection,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.cbp.gov/trade/basic-import-export"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFdcaImportStatute,
                "21 U.S.C. 381",
                "Imports and exports under the Federal Food, Drug, and Cosmetic Act",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.govinfo.gov/link/uscode/21/381"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .UnitedStatesFoodFacilityRegistrationStatute,
                "21 U.S.C. 350d",
                "Registration of food facilities",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.govinfo.gov/link/uscode/21/350d"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .UnitedStatesFoodFacilityRegistrationRegulation,
                "21 CFR Part 1 Subpart H",
                "Registration of food facilities",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.ecfr.gov/current/title-21/chapter-I/subchapter-A/part-1/subpart-H"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFdaImportingHumanFoods,
                "FDA import requirements",
                "Importing Human Foods",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.fda.gov/industry/importing-fda-regulated-products/importing-human-foods"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesPriorNoticeRegulation,
                "21 CFR Part 1 Subpart I",
                "Prior notice of imported food",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.ecfr.gov/current/title-21/chapter-I/subchapter-A/part-1/subpart-I"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesPriorNoticeGuidance,
                "FDA prior notice guidance",
                "Prior Notice of Imported Foods",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.fda.gov/industry/fda-import-process/prior-notice-imported-foods"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFsvpStatute,
                "21 U.S.C. 384a",
                "Foreign supplier verification program",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.govinfo.gov/link/uscode/21/384a"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFsvpRegulation,
                "21 CFR Part 1 Subpart L",
                "Foreign Supplier Verification Programs",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.ecfr.gov/current/title-21/chapter-I/subchapter-A/part-1/subpart-L"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFsvpRuleGuidance,
                "FSMA FSVP final rule",
                "FSVP for importers of food for humans and animals",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.fda.gov/food/food-safety-modernization-act-fsma/fsma-final-rule-foreign-supplier-verification-programs-fsvp-importers-food-humans-and-animals"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFoodLabelingRegulation,
                "21 CFR Part 101",
                "Food labeling",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.ecfr.gov/current/title-21/chapter-I/subchapter-B/part-101"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesSeafoodHaccpRegulation,
                "21 CFR Part 123",
                "Fish and fishery products",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.ecfr.gov/current/title-21/chapter-I/subchapter-B/part-123"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesJuiceHaccpRegulation,
                "21 CFR Part 120",
                "Juice HACCP systems",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.ecfr.gov/current/title-21/chapter-I/subchapter-B/part-120"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .UnitedStatesAcidifiedLowAcidCannedFoodRules,
                "21 CFR Parts 108, 113 and 114",
                "Acidified and low-acid canned foods",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodAndDrugAdministration,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.fda.gov/food/guidance-documents-regulatory-information-topic-food-and-dietary-supplements/acidified-low-acid-canned-foods-guidance-documents-regulatory-information"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .UnitedStatesAphisPlantImportRequirements,
                "APHIS ACIR and permit requirements",
                "How to Import Plants and Plant Products into the United States",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesAnimalAndPlantHealthInspectionService,
                ImportedFoodOfficialReferenceTypeCodes.OfficialDecisionSystem,
                "https://www.aphis.usda.gov/plant-imports/how-to-import"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .UnitedStatesAphisAnimalProductImportRequirements,
                "APHIS Veterinary Services import requirements",
                "Animal Product Imports",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesAnimalAndPlantHealthInspectionService,
                ImportedFoodOfficialReferenceTypeCodes.OfficialDecisionSystem,
                "https://www.aphis.usda.gov/animal-product-import"),
            Reference(
                ImportedFoodOfficialReferenceCodes.UnitedStatesFsisImportRequirements,
                "9 CFR Parts 327, 381 Subpart T, 590.900-970 and 530.557",
                "FSIS guideline for importing meat, poultry and egg products",
                ImportedFoodRegulatoryAuthorityCodes
                    .UnitedStatesFoodSafetyAndInspectionService,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.fsis.usda.gov/guidelines/2022-0001"),

            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaCustomsAct,
                "Customs Act 1901",
                "Australian customs legislation",
                ImportedFoodRegulatoryAuthorityCodes.AustraliaBorderForce,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.legislation.gov.au/C1901A00006/latest"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaAbfImportDeclaration,
                "ABF import declaration guidance",
                "Import declarations",
                ImportedFoodRegulatoryAuthorityCodes.AustraliaBorderForce,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.abf.gov.au/importing-exporting-and-manufacturing/importing/how-to-import/import-declaration"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaBiosecurityAct,
                "Biosecurity Act 2015",
                "Biosecurity Act 2015",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.legislation.gov.au/C2015A00061/latest"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .AustraliaConditionalGoodsDetermination,
                "Biosecurity (Conditionally Non-prohibited Goods) Determination 2021",
                "Current conditions for conditionally non-prohibited goods",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.LegislativeInstrument,
                "https://www.legislation.gov.au/F2021L00258/latest"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaImportedFoodControlAct,
                "Imported Food Control Act 1992",
                "Imported Food Control Act 1992",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.Statute,
                "https://www.legislation.gov.au/C2004A04512/latest"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .AustraliaImportedFoodControlRegulations,
                "Imported Food Control Regulations 2019",
                "Imported Food Control Regulations 2019",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.Regulation,
                "https://www.legislation.gov.au/F2019L01006/latest"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaImportedFoodControlOrder,
                "Imported Food Control Order 2019",
                "Current risk-food and certification classifications",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.LegislativeInstrument,
                "https://www.legislation.gov.au/F2019L01233/latest"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .AustraliaImportedFoodLegislationGuide,
                "DAFF imported-food legislation guidance",
                "Imported food legislation",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.agriculture.gov.au/import/goods/food/legislation"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaBicon,
                "BICON",
                "Biosecurity Import Conditions system",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.OfficialDecisionSystem,
                "https://bicon.agriculture.gov.au/"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaFoodImporterGuide,
                "DAFF step-by-step guidance",
                "How to import food into Australia",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaDepartmentOfAgricultureFisheriesAndForestry,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.agriculture.gov.au/import/goods/food/info-for-food-importers"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaFoodStandardsCode,
                "Australia New Zealand Food Standards Code",
                "Current Food Standards Code legislation",
                ImportedFoodRegulatoryAuthorityCodes.FoodStandardsAustraliaNewZealand,
                ImportedFoodOfficialReferenceTypeCodes.LegislativeInstrument,
                "https://www.foodstandards.gov.au/food-standards-code/legislation"),
            Reference(
                ImportedFoodOfficialReferenceCodes.AustraliaImportedFoodsGuide,
                "FSANZ imported-food guidance",
                "Imported foods",
                ImportedFoodRegulatoryAuthorityCodes.FoodStandardsAustraliaNewZealand,
                ImportedFoodOfficialReferenceTypeCodes.OfficialGuidance,
                "https://www.foodstandards.gov.au/consumer/imported-foods"),
            Reference(
                ImportedFoodOfficialReferenceCodes
                    .AustraliaCountryOfOriginFoodStandard,
                "Country of Origin Food Labelling Information Standard 2016",
                "Country of Origin Food Labelling Information Standard 2016",
                ImportedFoodRegulatoryAuthorityCodes
                    .AustraliaCompetitionAndConsumerCommission,
                ImportedFoodOfficialReferenceTypeCodes.LegislativeInstrument,
                "https://www.legislation.gov.au/F2016L00528/latest")
        ];

    private static ImportedFoodOfficialReference Reference(
        string code,
        string citation,
        string title,
        string authorityCode,
        string referenceTypeCode,
        string sourceUrl)
        => new(
            code,
            citation,
            title,
            authorityCode,
            referenceTypeCode,
            sourceUrl,
            ReviewDate);
}
