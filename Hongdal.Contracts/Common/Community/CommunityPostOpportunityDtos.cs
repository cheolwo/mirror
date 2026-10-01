using Hongdal.Contracts.Common.AgriculturalFisheries;
using Hongdal.Contracts.Common.Localization;

namespace Hongdal.Contracts.Common.Community;

public sealed class CommunityPostOpportunityListResponse
{
    public long PostId { get; set; }
    public string DisplayLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public CommunitySharedExperiencePolicyResponse ExperiencePolicy { get; set; } = new();
    public CommunityPostParticipationEntryResponse Participation { get; set; } = new();
    public CommunityActionJourneyResponse Journey { get; set; } = new();
    public IReadOnlyList<CommunityPostOpportunityResponse> Items { get; set; } = [];
    public IReadOnlyList<CommunityDynamicTopicResponse> DynamicTopics { get; set; } = [];
    public CommunityPostContextDiscoveryResponse ContextDiscovery { get; set; } = new();
}

public sealed class CommunitySharedExperiencePolicyResponse
{
    public string ExperienceScopeCode { get; set; } = CommunityExperienceScopeCodes.SharedCommunity;
    public bool UsesSameCommunityApp { get; set; } = true;
    public bool OperatingProfileAffectsAvailability { get; set; }
    public bool DisplayLanguageAffectsContentOnly { get; set; } = true;
    public bool InfersLanguageFromCountryOrRole { get; set; }
    public IReadOnlyList<string> SupportedDisplayLanguageCodes { get; set; } = CommunityDisplayLanguageCodes.Supported;
}

public sealed class CommunityPostOpportunityResponse
{
    public string Code { get; set; } = string.Empty;
    public string StateCode { get; set; } = CommunityPostOpportunityStateCodes.Suggested;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string WhySuggested { get; set; } = string.Empty;
    public string LedgerTemplateKey { get; set; } = string.Empty;
    public bool CanStart { get; set; }
    public bool AutoStartsWorkflow { get; set; }
    public bool RequiresExplicitConsent { get; set; } = true;
    public bool InformationOnly { get; set; } = true;
    public bool IsBrokerageEnabled { get; set; }
    public string PreviewEndpoint { get; set; } = string.Empty;
    public string StartEndpoint { get; set; } = string.Empty;
    public IReadOnlyList<string> MatchedSignals { get; set; } = [];
    public IReadOnlyList<string> MissingInformationPrompts { get; set; } = [];
}

public sealed class StartCommunityMeatImportReadinessRequest
{
    public string? DisplayLanguageCode { get; set; }
    public bool ConfirmExplicitStart { get; set; }
    public bool ConfirmInformationOnly { get; set; }
    public CreateMeatImportReadinessCaseRequest Case { get; set; } = new();
}

public sealed class StartCommunityMeatImportReadinessResponse
{
    public long PostId { get; set; }
    public string DisplayLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public bool LinkedToCommunityPost { get; set; }
    public CommunityPostOpportunityResponse Opportunity { get; set; } = new();
    public MeatImportReadinessCaseResponse Case { get; set; } = new();
}

public sealed class StartCommunityPostParticipationRequest
{
    public string? DisplayLanguageCode { get; set; }
    public string? Title { get; set; }
    public DateTime? ClosesAtUtc { get; set; }
    public bool ConfirmExplicitStart { get; set; }
    public bool ConfirmNonBindingParticipation { get; set; }
}

public sealed class StartCommunityPostParticipationResponse
{
    public long PostId { get; set; }
    public string DisplayLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public bool ReusedExistingInterestVote { get; set; }
    public CommunityPostParticipationEntryResponse Participation { get; set; } = new();
    public CommunityVoteResponse InterestVote { get; set; } = new();
}

public sealed class PromoteCommunityPostParticipationRequest
{
    public Guid InterestVoteId { get; set; }
    public string? DisplayLanguageCode { get; set; }
    public string CollectiveIntentTypeCode { get; set; } = CommunityCollectiveIntentTypeCodes.GroupPurchase;
    public string TradeDirectionCode { get; set; } = string.Empty;
    public string? OriginCountryCode { get; set; }
    public string? DestinationCountryCode { get; set; }
    public IReadOnlyList<string> TransportModeCodes { get; set; } = [];
    public bool ConfirmProvisionalLedger { get; set; }
    public bool ConfirmNonBindingEvidence { get; set; }
    public bool ConfirmParticipantNotifications { get; set; }
}

public sealed class PromoteCommunityPostParticipationResponse
{
    public long PostId { get; set; }
    public string DisplayLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public bool ReusedExistingProvisionalLedger { get; set; }
    public string CollectiveIntentTypeCode { get; set; } = CommunityCollectiveIntentTypeCodes.GroupPurchase;
    public string TradeDirectionCode { get; set; } = CommunityTradeDirectionCodes.Domestic;
    public string OriginCountryCode { get; set; } = string.Empty;
    public string DestinationCountryCode { get; set; } = string.Empty;
    public IReadOnlyList<string> TransportModeCodes { get; set; } = [];
    public CommunityPostProvisionalLedgerResponse ProvisionalLedger { get; set; } = new();
    public CommunityPostParticipationEntryResponse Participation { get; set; } = new();
}

public sealed class JoinCommunityPostProfessionalRequest
{
    public string ProvisionalLedgerId { get; set; } = string.Empty;
    public string ProfessionalRoleCode { get; set; } = string.Empty;
    public string? DisplayLanguageCode { get; set; }
    public bool ConfirmProfessionalCapacity { get; set; }
    public bool ConfirmVoluntaryNonBindingParticipation { get; set; }
    public bool ConfirmParticipantNotification { get; set; }
}

public sealed class JoinCommunityPostProfessionalResponse
{
    public long PostId { get; set; }
    public string DisplayLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public bool ReusedExistingParticipation { get; set; }
    public string JoinedProfessionalRoleCode { get; set; } = string.Empty;
    public CommunityPostProvisionalLedgerResponse ProvisionalLedger { get; set; } = new();
    public CommunityPostParticipationEntryResponse Participation { get; set; } = new();
}

public sealed class JoinCommunityPostPartyRoleRequest
{
    public string ProvisionalLedgerId { get; set; } = string.Empty;
    public string PartyRoleCode { get; set; } = string.Empty;
    public string? DisplayLanguageCode { get; set; }
    public bool ConfirmRoleCapacity { get; set; }
    public bool ConfirmVoluntaryNonBindingParticipation { get; set; }
    public bool ConfirmParticipantNotification { get; set; }
}

public sealed class JoinCommunityPostPartyRoleResponse
{
    public long PostId { get; set; }
    public string DisplayLanguageCode { get; set; } = CommunityDisplayLanguageCodes.Korean;
    public bool ReusedExistingParticipation { get; set; }
    public string JoinedPartyRoleCode { get; set; } = string.Empty;
    public CommunityPostProvisionalLedgerResponse ProvisionalLedger { get; set; } = new();
    public CommunityPostParticipationEntryResponse Participation { get; set; } = new();
}

public sealed class CommunityPostProvisionalLedgerResponse
{
    public string LedgerId { get; set; } = string.Empty;
    public long Revision { get; set; }
    public string LedgerTemplateKey { get; set; } = CommunityLedgerTemplateKeys.GroupPurchase;
    public string State { get; set; } = string.Empty;
    public string CurrentStageCode { get; set; } = string.Empty;
    public int ParticipantCount { get; set; }
    public string EvidenceSnapshotHash { get; set; } = string.Empty;
    public bool NonBinding { get; set; } = true;
    public bool ParticipantNotificationsRequested { get; set; }
    public string TradeDirectionCode { get; set; } = CommunityTradeDirectionCodes.Domestic;
    public string OriginCountryCode { get; set; } = string.Empty;
    public string DestinationCountryCode { get; set; } = string.Empty;
    public IReadOnlyList<string> TransportModeCodes { get; set; } = [];
}

public sealed class CommunityPostParticipationEntryResponse
{
    public string Code { get; set; } = CommunityPostParticipationCodes.CollectiveActionInterest;
    public string StateCode { get; set; } = CommunityPostParticipationStateCodes.Available;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool CanStart { get; set; } = true;
    public bool CanJoin { get; set; }
    public bool AutoStartsWorkflow { get; set; }
    public bool NonBinding { get; set; } = true;
    public bool RequiresExplicitStart { get; set; } = true;
    public bool RequiresExplicitPromotionToPlanning { get; set; } = true;
    public bool CanPromoteToProvisionalLedger { get; set; }
    public int MinimumPromotionParticipantCount { get; set; } = CommunityPostProvisionalLedgerPolicy.MinimumParticipantCount;
    public Guid? InterestVoteId { get; set; }
    public string? ProvisionalLedgerId { get; set; }
    public int ParticipantCount { get; set; }
    public string StartEndpoint { get; set; } = string.Empty;
    public string JoinEndpoint { get; set; } = string.Empty;
    public string PlanningEndpoint { get; set; } = "/api/v1/collective-procurement/plans";
    public string PlanningSourceTypeCode { get; set; } = "community-interest-vote";
    public string PlanningSourceReferenceId { get; set; } = string.Empty;
    public string ProvisionalLedgerEndpoint { get; set; } = string.Empty;
    public IReadOnlyList<CommunityPostParticipationRoleOptionResponse> RoleOptions { get; set; } = [];
    public CommunityPostPartyFormationResponse PartyFormation { get; set; } = new();
    public CommunityPostProfessionalParticipationResponse ProfessionalParticipation { get; set; } = new();
}

public sealed class CommunityPostParticipationRoleOptionResponse
{
    public string RoleCode { get; set; } = string.Empty;
    public string OptionId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int InterestCount { get; set; }
}

public sealed class CommunityPostProfessionalParticipationResponse
{
    public bool IsAvailable { get; set; }
    public bool RequiresVerifiedAccountRole { get; set; } = true;
    public bool PlatformRoleDoesNotProveExternalLicense { get; set; } = true;
    public bool RequiresExplicitAcceptance { get; set; } = true;
    public bool DoesNotAssignWork { get; set; } = true;
    public bool PlatformPromotionActive { get; set; }
    public string MomentumCode { get; set; } = CommunityPostMomentumCodes.None;
    public string MomentumMessage { get; set; } = string.Empty;
    public int PlatformConfirmedRoleParticipantCount { get; set; }
    public string JoinEndpoint { get; set; } = string.Empty;
    public IReadOnlyList<CommunityPostProfessionalRoleOpeningResponse> RoleOpenings { get; set; } = [];
}

public sealed class CommunityPostProfessionalRoleOpeningResponse
{
    public string RoleCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool VerificationRequired { get; set; } = true;
    public string VerificationRequirementCode { get; set; } =
        CommunityPartyRoleVerificationRequirementCodes.PlatformProfile;
    public bool ExternalCredentialVerificationRequired { get; set; }
    public bool ExternalCredentialVerified { get; set; }
    public int PlatformConfirmedParticipantCount { get; set; }
    public bool HasPlatformConfirmedParticipant => PlatformConfirmedParticipantCount > 0;
    public string CandidateDirectoryEndpoint { get; set; } = string.Empty;
    public bool CandidateDirectoryIsResearchOnly { get; set; }
    public bool RequiresSeparateAuthorityAndContractVerification { get; set; }
}

public sealed class CommunityPostPartyFormationResponse
{
    public bool IsAvailable { get; set; }
    public bool NonBinding { get; set; } = true;
    public bool RequiresExplicitRoleAcceptance { get; set; } = true;
    public bool PlatformDoesNotAssignWork { get; set; } = true;
    public bool PlatformDoesNotCreateContracts { get; set; } = true;
    public string TradeDirectionCode { get; set; } = CommunityTradeDirectionCodes.Domestic;
    public string OriginCountryCode { get; set; } = string.Empty;
    public string DestinationCountryCode { get; set; } = string.Empty;
    public IReadOnlyList<string> TransportModeCodes { get; set; } = [];
    public bool TradeRouteNeedsConfirmation { get; set; }
    public int RequiredRoleSlotCount { get; set; }
    public int RepresentedRequiredRoleSlotCount { get; set; }
    public bool IsReadyForRealLedgerReview { get; set; }
    public string ReadinessMessage { get; set; } = string.Empty;
    public IReadOnlyList<CommunityPostPartyRoleSlotResponse> RoleSlots { get; set; } = [];
}

public sealed class CommunityPostPartyRoleSlotResponse
{
    public string RoleCode { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsRecommended { get; set; }
    public string? TransportModeCode { get; set; }
    public string VerificationRequirementCode { get; set; } = string.Empty;
    public bool ExternalCredentialVerificationRequired { get; set; }
    public bool ExternalCredentialVerified { get; set; }
    public int InterestCount { get; set; }
    public int ConfirmedParticipantCount { get; set; }
    public string StateCode { get; set; } = CommunityPartyRoleSlotStateCodes.Open;
    public bool IsRepresented => ConfirmedParticipantCount > 0;
    public string CandidateDirectoryEndpoint { get; set; } = string.Empty;
    public bool CandidateDirectoryIsResearchOnly { get; set; }
    public bool RequiresSeparateAuthorityAndContractVerification { get; set; }
}

public static class CommunityPostOpportunityCodes
{
    public const string MeatImportReadiness = "MeatImportReadiness";
}

public static class CommunityPostParticipationCodes
{
    public const string CollectiveActionInterest = "CollectiveActionInterest";
}

public static class CommunityPostParticipationStateCodes
{
    public const string Available = "Available";
    public const string Gathering = "Gathering";
    public const string ProvisionalLedgerCreated = "ProvisionalLedgerCreated";
    public const string Closed = "Closed";
}

public static class CommunityPostProvisionalLedgerPolicy
{
    public const int MinimumParticipantCount = 2;
    public const string LedgerMaturityAttributeKey = "LedgerMaturityCode";
    public const string LedgerMaturityCode = "Provisional";
    public const string BindingEffectAttributeKey = "BindingEffectCode";
    public const string NonBindingEffectCode = "NonBinding";
    public const string CollectiveIntentTypeAttributeKey = "CollectiveIntentTypeCode";
    public const string TradeDirectionAttributeKey = "TradeDirectionCode";
    public const string OriginCountryAttributeKey = "OriginCountryCode";
    public const string DestinationCountryAttributeKey = "DestinationCountryCode";
    public const string TransportModesAttributeKey = "TransportModeCodesJson";
    public const string EvidenceSnapshotHashAttributeKey = "InterestEvidenceSnapshotHash";
    public const string ParticipantNotificationsAttributeKey = "ParticipantNotificationsRequested";
    public const string ProfessionalParticipationBlockId = "professional-participation";
    public const string RequiredProfessionalRolesAttributeKey = "RequiredProfessionalRolesJson";
    public const string ConfirmedPartyRoleAssignmentsAttributeKey = "ConfirmedPartyRoleAssignmentsJson";
    public const string ConfirmedPartyRoleParticipantCountAttributeKey = "ConfirmedPartyRoleParticipantCount";
    public const string AuthorProfessionalRolesAttributeKey = "AuthorVerifiedProfessionalRolesJson";
    public const string CommunityMomentumCodeAttributeKey = "CommunityMomentumCode";
    public const string CommunityPromotionRequestedAttributeKey = "CommunityPromotionRequested";
    public const string LastPartyRoleJoinedUserIdAttributeKey = "LastPartyRoleJoinedUserId";
    public const string LastPartyRoleJoinedDisplayNameAttributeKey = "LastPartyRoleJoinedDisplayName";
    public const string LastPartyRoleJoinedRoleCodeAttributeKey = "LastPartyRoleJoinedRoleCode";
    public const string LastPartyRoleJoinRevisionAttributeKey = "LastPartyRoleJoinRevision";
}

public static class CommunityCollectiveIntentTypeCodes
{
    public const string GroupPurchase = "GroupPurchase";
    public const string GroupImportCandidate = "GroupImportCandidate";
    public const string GroupExportCandidate = "GroupExportCandidate";

    public static IReadOnlyList<string> All { get; } = [GroupPurchase, GroupImportCandidate, GroupExportCandidate];

    public static bool IsSupported(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
}

public static class CommunityPostParticipationRoleCodes
{
    public const string Buyer = "Buyer";
    public const string Supplier = "Supplier";
    public const string FreightBroker = "FreightBroker";
    public const string Carrier = "Carrier";
    public const string CustomsBroker = "CustomsBroker";
    public const string WarehouseOperator = "WarehouseOperator";
    public const string Facilitator = "Facilitator";
    public const string FollowOnly = "FollowOnly";

    public static IReadOnlyList<string> All { get; } =
    [
        Buyer,
        Supplier,
        FreightBroker,
        Carrier,
        CustomsBroker,
        WarehouseOperator,
        Facilitator,
        FollowOnly
    ];
}

public static class CommunityPostPartyRoleCodes
{
    public const string Buyer = "Buyer";
    public const string Seller = "Seller";
    public const string Importer = "Importer";
    public const string Exporter = "Exporter";
    public const string ImportCustomsBroker = "ImportCustomsBroker";
    public const string ExportCustomsBroker = "ExportCustomsBroker";
    public const string OceanFreightForwarder = "OceanFreightForwarder";
    public const string AirFreightForwarder = "AirFreightForwarder";
    public const string RoadFreightBroker = "RoadFreightBroker";
    public const string MultimodalCoordinator = "MultimodalCoordinator";
    public const string OceanCarrier = "OceanCarrier";
    public const string AirCarrier = "AirCarrier";
    public const string RoadCarrier = "RoadCarrier";
    public const string RailCarrier = "RailCarrier";
    public const string WarehouseOperator = "WarehouseOperator";
    public const string CustomsControlledFacilityOperator =
        "CustomsControlledFacilityOperator";
    public const string InBondCarrier = "InBondCarrier";
    public const string DomesticFulfillmentOperator =
        "DomesticFulfillmentOperator";
    public const string ParticipantAddressDeliveryProvider =
        "ParticipantAddressDeliveryProvider";

    public static IReadOnlyList<string> All { get; } =
    [
        Buyer,
        Seller,
        Importer,
        Exporter,
        ImportCustomsBroker,
        ExportCustomsBroker,
        OceanFreightForwarder,
        AirFreightForwarder,
        RoadFreightBroker,
        MultimodalCoordinator,
        OceanCarrier,
        AirCarrier,
        RoadCarrier,
        RailCarrier,
        WarehouseOperator,
        CustomsControlledFacilityOperator,
        InBondCarrier,
        DomesticFulfillmentOperator,
        ParticipantAddressDeliveryProvider
    ];

    public static IReadOnlyList<string> SpecialistRoles { get; } =
    [
        ImportCustomsBroker,
        ExportCustomsBroker,
        OceanFreightForwarder,
        AirFreightForwarder,
        RoadFreightBroker,
        MultimodalCoordinator,
        OceanCarrier,
        AirCarrier,
        RoadCarrier,
        RailCarrier,
        WarehouseOperator,
        CustomsControlledFacilityOperator,
        InBondCarrier,
        DomesticFulfillmentOperator,
        ParticipantAddressDeliveryProvider
    ];

    public static IReadOnlyList<string> CommercialPartyRoles { get; } =
    [
        Buyer,
        Seller,
        Importer,
        Exporter
    ];

    public static bool IsSupported(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsSpecialist(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && SpecialistRoles.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsCommercialParty(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && CommercialPartyRoles.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ForPlan(
        string? tradeDirectionCode,
        IEnumerable<string>? transportModeCodes,
        string? destinationCountryCode = null)
    {
        var roles = new List<string>
        {
            Buyer,
            Seller
        };
        var isImport = string.Equals(
            tradeDirectionCode,
            CommunityTradeDirectionCodes.Import,
            StringComparison.OrdinalIgnoreCase);
        var isUnitedStatesDestination = string.Equals(
            destinationCountryCode?.Trim(),
            "US",
            StringComparison.OrdinalIgnoreCase);
        if (isImport)
        {
            roles.Add(Importer);
            roles.Add(Exporter);
            roles.Add(ImportCustomsBroker);
            if (isUnitedStatesDestination)
            {
                roles.Add(CustomsControlledFacilityOperator);
                roles.Add(InBondCarrier);
                roles.Add(DomesticFulfillmentOperator);
                roles.Add(ParticipantAddressDeliveryProvider);
            }
            else
            {
                roles.Add(WarehouseOperator);
            }
        }
        else if (string.Equals(tradeDirectionCode, CommunityTradeDirectionCodes.Export, StringComparison.OrdinalIgnoreCase))
        {
            roles.Add(Importer);
            roles.Add(Exporter);
            roles.Add(ExportCustomsBroker);
            roles.Add(WarehouseOperator);
        }
        else
        {
            roles.Add(WarehouseOperator);
        }

        foreach (var mode in CommunityTransportModeCodes.NormalizeMany(transportModeCodes))
        {
            switch (mode)
            {
                case CommunityTransportModeCodes.Ocean:
                    roles.Add(OceanFreightForwarder);
                    roles.Add(OceanCarrier);
                    break;
                case CommunityTransportModeCodes.Air:
                    roles.Add(AirFreightForwarder);
                    roles.Add(AirCarrier);
                    break;
                case CommunityTransportModeCodes.Road:
                    roles.Add(RoadFreightBroker);
                    roles.Add(RoadCarrier);
                    break;
                case CommunityTransportModeCodes.Rail:
                    roles.Add(RailCarrier);
                    break;
                case CommunityTransportModeCodes.Multimodal:
                    roles.Add(MultimodalCoordinator);
                    break;
            }
        }

        return roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}

public static class CommunityPartyRoleCategoryCodes
{
    public const string CommercialParty = "CommercialParty";
    public const string CustomsAndDocumentation = "CustomsAndDocumentation";
    public const string TransportationIntermediary = "TransportationIntermediary";
    public const string Carrier = "Carrier";
    public const string Fulfillment = "Fulfillment";
}

public static class CommunityPartyRoleVerificationRequirementCodes
{
    public const string ExplicitPartyAcceptance = "ExplicitPartyAcceptance";
    public const string PlatformProfile = "PlatformProfile";
    public const string JurisdictionLicenseOrRegistration = "JurisdictionLicenseOrRegistration";
    public const string CarrierOperatingAuthority = "CarrierOperatingAuthority";
    public const string CustomsFacilityAuthorization =
        "CustomsFacilityAuthorization";
    public const string BondedCarrierOperatingAuthority =
        "BondedCarrierOperatingAuthority";
    public const string FacilityCapabilityAndContract =
        "FacilityCapabilityAndContract";
}

public static class CommunityPartyRoleSlotStateCodes
{
    public const string Open = "Open";
    public const string InterestExpressed = "InterestExpressed";
    public const string RoleAccepted = "RoleAccepted";
}

public static class CommunityTradeDirectionCodes
{
    public const string Domestic = "Domestic";
    public const string Import = "Import";
    public const string Export = "Export";

    public static IReadOnlyList<string> All { get; } = [Domestic, Import, Export];

    public static bool IsSupported(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string ExpectedForIntent(string intentTypeCode)
        => intentTypeCode switch
        {
            CommunityCollectiveIntentTypeCodes.GroupImportCandidate => Import,
            CommunityCollectiveIntentTypeCodes.GroupExportCandidate => Export,
            _ => Domestic
        };
}

public static class CommunityTransportModeCodes
{
    public const string Ocean = "Ocean";
    public const string Air = "Air";
    public const string Road = "Road";
    public const string Rail = "Rail";
    public const string Multimodal = "Multimodal";

    public static IReadOnlyList<string> All { get; } = [Ocean, Air, Road, Rail, Multimodal];

    public static IReadOnlyList<string> NormalizeMany(IEnumerable<string>? values)
        => (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => All.FirstOrDefault(candidate => string.Equals(
                candidate,
                value.Trim(),
                StringComparison.OrdinalIgnoreCase)))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}

public static class CommunityPostMomentumCodes
{
    public const string None = "None";
    public const string SeekingParty = "SeekingParty";
    public const string PartyForming = "PartyForming";
    public const string ReadyForRealLedgerReview = "ReadyForRealLedgerReview";
}

public static class CommunityPostOpportunityStateCodes
{
    public const string Suggested = "Suggested";
    public const string Active = "Active";
    public const string BlockedByAnotherLedger = "BlockedByAnotherLedger";
}

public static class CommunityExperienceScopeCodes
{
    public const string SharedCommunity = "SharedCommunity";
}

public static class CommunityDisplayLanguageCodes
{
    public const string Korean = DisplayLanguageCodes.Korean;
    public const string English = DisplayLanguageCodes.English;

    public static IReadOnlyList<string> Supported => DisplayLanguageCodes.Supported;

    public static string Normalize(string? value)
        => DisplayLanguageCodes.Normalize(value);
}
