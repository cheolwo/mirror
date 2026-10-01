using System.Net.Mail;
using System.Text;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Services.Operations;

public interface IThirdPartyLogisticsProviderOutreachPreparationService
{
    ThirdPartyLogisticsProviderOutreachPreparationResponse Prepare(
        PrepareThirdPartyLogisticsProviderOutreachRequest request);
}

public sealed class UnitedStatesThirdPartyLogisticsProviderOutreachPreparationService
    : IThirdPartyLogisticsProviderOutreachPreparationService
{
    private const string ComplianceGuidanceUrl =
        "https://www.ftc.gov/business-guidance/resources/can-spam-act-compliance-guide-business";

    public ThirdPartyLogisticsProviderOutreachPreparationResponse Prepare(
        PrepareThirdPartyLogisticsProviderOutreachRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scopeCode = request.ScopeCode?.Trim() ?? string.Empty;
        if (!string.Equals(
                scopeCode,
                ThirdPartyLogisticsProviderOutreachScopeCodes.BondedToDoorPilot,
                StringComparison.OrdinalIgnoreCase))
        {
            return new ThirdPartyLogisticsProviderOutreachPreparationResponse
            {
                Success = false,
                MarketCode = OperatingMarketCodes.UnitedStates,
                ScopeCode = scopeCode,
                ErrorCode = ThirdPartyLogisticsProviderOutreachErrorCodes
                    .UnsupportedScope,
                ErrorMessage = "Only the bonded-to-door pilot outreach scope is available."
            };
        }

        var requestedProviderKeys = (request.ProviderKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var contacts = UnitedStatesThirdPartyLogisticsOutreachContactCatalog
            .Contacts
            .Where(contact => requestedProviderKeys.Length == 0
                              || requestedProviderKeys.Contains(
                                  contact.ProviderKey,
                                  StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var knownContactKeys = UnitedStatesThirdPartyLogisticsOutreachContactCatalog
            .Contacts
            .Select(contact => contact.ProviderKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownProviderKeys = requestedProviderKeys
            .Where(key => !knownContactKeys.Contains(key))
            .ToArray();
        var providers = UnitedStatesThirdPartyLogisticsProviderCatalog.Providers
            .ToDictionary(provider => provider.ProviderKey, StringComparer.OrdinalIgnoreCase);
        var profiles = UnitedStatesBondedToDoorLogisticsCatalog.Profiles
            .ToDictionary(profile => profile.ProviderKey, StringComparer.OrdinalIgnoreCase);
        var missingRequirements = ResolveMissingRequirements(request);
        var ready = missingRequirements.Count == 0;
        var drafts = contacts
            .Where(contact => providers.ContainsKey(contact.ProviderKey)
                              && profiles.ContainsKey(contact.ProviderKey))
            .Select(contact => BuildDraft(
                contact,
                providers[contact.ProviderKey],
                profiles[contact.ProviderKey],
                request,
                missingRequirements,
                ready))
            .ToArray();

        return new ThirdPartyLogisticsProviderOutreachPreparationResponse
        {
            Success = true,
            MarketCode = OperatingMarketCodes.UnitedStates,
            ScopeCode = ThirdPartyLogisticsProviderOutreachScopeCodes
                .BondedToDoorPilot,
            CatalogVersion = UnitedStatesThirdPartyLogisticsOutreachContactCatalog
                .CatalogVersion,
            ContactSnapshotReviewedOn =
                UnitedStatesThirdPartyLogisticsOutreachContactCatalog
                    .SnapshotReviewedOn,
            AutomaticDispatchEnabled = false,
            RequiresPerRecipientApproval = true,
            ComplianceGuidanceUrl = ComplianceGuidanceUrl,
            TotalDraftCount = drafts.Length,
            ReadyForManualApprovalCount = drafts.Count(draft =>
                draft.ReadinessCode ==
                ThirdPartyLogisticsProviderOutreachReadinessCodes
                    .ReadyForManualApproval),
            DirectEmailDraftCount = drafts.Count(draft =>
                draft.ContactChannelCode ==
                ThirdPartyLogisticsProviderOutreachContactChannelCodes
                    .VerifiedPublicBusinessEmail),
            OfficialInquiryFormDraftCount = drafts.Count(draft =>
                draft.ContactChannelCode ==
                ThirdPartyLogisticsProviderOutreachContactChannelCodes
                    .OfficialInquiryForm),
            BlockedDraftCount = drafts.Count(draft =>
                draft.ReadinessCode ==
                ThirdPartyLogisticsProviderOutreachReadinessCodes
                    .MissingSenderRequirements),
            UnknownProviderKeys = unknownProviderKeys,
            MissingSenderRequirementCodes = missingRequirements,
            Items = drafts
        };
    }

    private static ThirdPartyLogisticsProviderOutreachDraft BuildDraft(
        ThirdPartyLogisticsOutreachContact contact,
        ThirdPartyLogisticsProviderDirectoryItem provider,
        BondedToDoorLogisticsProfile profile,
        PrepareThirdPartyLogisticsProviderOutreachRequest request,
        IReadOnlyList<string> missingRequirements,
        bool ready)
    {
        var directEmail = contact.ContactChannelCode ==
                          ThirdPartyLogisticsProviderOutreachContactChannelCodes
                              .VerifiedPublicBusinessEmail;
        return new ThirdPartyLogisticsProviderOutreachDraft
        {
            ProviderKey = provider.ProviderKey,
            ProviderDisplayName = provider.DisplayName,
            ContactChannelCode = contact.ContactChannelCode,
            OfficialInquiryUrl = contact.OfficialInquiryUrl,
            RecipientEmailAddress = contact.PublicBusinessEmail,
            RecipientEmailVerifiedFromOfficialSource = directEmail,
            ContactSourceTitle = contact.SourceTitle,
            ContactSourceUrl = contact.SourceUrl,
            ContactReviewedOn = contact.ReviewedOn,
            SupportedStageCodes = profile.StageCodes,
            Subject = "Exploratory capability inquiry - community-led U.S. import logistics",
            PlainTextBody = BuildBody(provider, profile, request),
            ReadinessCode = ready
                ? ThirdPartyLogisticsProviderOutreachReadinessCodes
                    .ReadyForManualApproval
                : ThirdPartyLogisticsProviderOutreachReadinessCodes
                    .MissingSenderRequirements,
            MissingSenderRequirementCodes = missingRequirements,
            ComplianceRequirementCodes =
                ThirdPartyLogisticsProviderOutreachComplianceRequirementCodes.All,
            CanCreateManualEmailDraft = ready && directEmail,
            CanUseOfficialInquiryForm = ready && !directEmail,
            RequiresRecipientAddressReverification = true,
            RequiresPerRecipientApproval = true,
            AutomaticDispatchEnabled = false
        };
    }

    private static string BuildBody(
        ThirdPartyLogisticsProviderDirectoryItem provider,
        BondedToDoorLogisticsProfile profile,
        PrepareThirdPartyLogisticsProviderOutreachRequest request)
    {
        var senderName = ValueOrPlaceholder(request.SenderName, "Sender name required");
        var organization = ValueOrPlaceholder(
            request.SenderOrganizationName,
            "Organization name required");
        var senderRole = ValueOrPlaceholder(
            request.SenderRole,
            "Platform facilitator");
        var senderEmail = ValueOrPlaceholder(
            request.SenderEmail,
            "Sender email required");
        var replyToEmail = ValueOrPlaceholder(
            request.ReplyToEmail,
            "Reply-to email required");
        var website = ValueOrPlaceholder(
            request.SenderOrganizationWebsiteUrl,
            "Organization website required");
        var postalAddress = ValueOrPlaceholder(
            request.PhysicalPostalAddress,
            "Physical postal address required");
        var cargo = ValueOrPlaceholder(
            request.PlannedCargoDescription,
            "Consumer goods for future community-led collective import pilots");
        var origin = ValueOrPlaceholder(
            request.OriginDescription,
            "Overseas origins to be confirmed per opportunity");
        var destination = ValueOrPlaceholder(
            request.DestinationDescription,
            "United States");
        var volume = ValueOrPlaceholder(
            request.EstimatedVolumeDescription,
            "Pilot volume is not yet fixed");
        var timing = ValueOrPlaceholder(
            request.TargetTimingDescription,
            "Exploratory stage; no active shipment or booking");
        var builder = new StringBuilder();

        builder.AppendLine($"Hello {provider.DisplayName} team,");
        builder.AppendLine();
        builder.AppendLine(
            $"My name is {senderName}, {senderRole} at {organization}. " +
            "I am preparing Hongdal, an early-stage community coordination platform " +
            "where people can express non-binding collective purchase or import " +
            "interest and then invite qualified logistics professionals to review " +
            "whether an opportunity is feasible.");
        builder.AppendLine();
        builder.AppendLine(
            "Hongdal is not presenting itself as a freight broker and does not " +
            "assign carriers, accept cargo, book freight, collect transportation " +
            "charges, or bind a provider through a platform role slot. Any future " +
            "service would require direct authority and capability verification, " +
            "a quote, and a separate contract with the appropriate commercial party.");
        builder.AppendLine();
        builder.AppendLine("Current exploratory scenario:");
        builder.AppendLine($"- Cargo: {cargo}");
        builder.AppendLine($"- Origin: {origin}");
        builder.AppendLine($"- Destination: {destination}");
        builder.AppendLine($"- Estimated volume: {volume}");
        builder.AppendLine($"- Timing: {timing}");
        builder.AppendLine();
        builder.AppendLine(
            "Our public-source review suggests that your organization may support " +
            "some of the following stages. These capabilities and all regulatory " +
            "authorizations remain unverified by Hongdal:");
        foreach (var stage in profile.StageCodes)
        {
            builder.AppendLine($"- {StageLabel(stage)}");
        }

        builder.AppendLine();
        builder.AppendLine("Could your team please confirm:");
        var questionNumber = 1;
        AppendQuestion(
            "The correct legal contracting entity and sales or solutions contact for this inquiry.");
        AppendQuestion(
            "The U.S. facilities, service areas, minimum volumes, onboarding lead time, and pricing process that may fit a pilot.");
        if (profile.StageCodes.Contains(
                BondedToDoorLogisticsStageCodes.CustomsControlledStorage,
                StringComparer.OrdinalIgnoreCase))
        {
            AppendQuestion(
                "Whether any proposed site is currently CBP bonded or FTZ activated, who operates it, and the exact facility or FIRMS details to verify separately.");
        }

        if (profile.StageCodes.Contains(
                BondedToDoorLogisticsStageCodes.InBondTransportation,
                StringComparer.OrdinalIgnoreCase))
        {
            AppendQuestion(
                "Whether in-bond transportation is performed directly or through another authorized carrier, subject to separate authority and bond checks.");
        }

        AppendQuestion(
            "Available receiving, break-pack, kitting, labeling, pick-pack, parcel tender, final-mile, and returns capabilities, as applicable.");
        AppendQuestion(
            "Available API, EDI, portal, inventory, shipment-status, and proof-of-delivery integrations.");
        AppendQuestion(
            "Whether a verified representative may be willing to join a future non-binding Hongdal ledger role slot to review suitable opportunities before any contract exists.");
        builder.AppendLine();
        builder.AppendLine(
            "This message is an exploratory commercial business inquiry, not a " +
            "tender, dispatch, load offer, booking, or representation of a confirmed shipment.");
        builder.AppendLine();
        builder.AppendLine("Regards,");
        builder.AppendLine(senderName);
        builder.AppendLine($"{senderRole}, {organization}");
        builder.AppendLine(senderEmail);
        builder.AppendLine(website);
        builder.AppendLine(postalAddress);
        builder.AppendLine();
        builder.AppendLine(
            $"To stop future outreach from {organization}, reply 'unsubscribe' to {replyToEmail}.");

        return builder.ToString().TrimEnd();

        void AppendQuestion(string question)
            => builder.AppendLine($"{questionNumber++}. {question}");
    }

    private static IReadOnlyList<string> ResolveMissingRequirements(
        PrepareThirdPartyLogisticsProviderOutreachRequest request)
    {
        var missing = new List<string>();
        AddIfWhiteSpace(
            request.SenderName,
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes.SenderName,
            missing);
        AddIfWhiteSpace(
            request.SenderOrganizationName,
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                .SenderOrganizationName,
            missing);
        AddIfInvalidEmail(
            request.SenderEmail,
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes.SenderEmail,
            missing);
        AddIfInvalidEmail(
            request.ReplyToEmail,
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes.ReplyToEmail,
            missing);
        if (!IsHttpUrl(request.SenderOrganizationWebsiteUrl))
        {
            missing.Add(
                ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                    .SenderOrganizationWebsiteUrl);
        }

        AddIfWhiteSpace(
            request.PhysicalPostalAddress,
            ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                .PhysicalPostalAddress,
            missing);
        if (!request.ConfirmSenderIdentityAccuracy)
        {
            missing.Add(
                ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                    .SenderIdentityAccuracyConfirmation);
        }

        if (!request.ConfirmPhysicalAddressValidity)
        {
            missing.Add(
                ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                    .PhysicalAddressValidityConfirmation);
        }

        if (!request.ConfirmSuppressionListChecked)
        {
            missing.Add(
                ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                    .SuppressionListCheckConfirmation);
        }

        if (!request.ConfirmPerRecipientReview)
        {
            missing.Add(
                ThirdPartyLogisticsProviderOutreachRequiredFieldCodes
                    .PerRecipientReviewConfirmation);
        }

        return missing;
    }

    private static void AddIfWhiteSpace(
        string? value,
        string fieldCode,
        ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add(fieldCode);
        }
    }

    private static void AddIfInvalidEmail(
        string? value,
        string fieldCode,
        ICollection<string> missing)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !MailAddress.TryCreate(value.Trim(), out _))
        {
            missing.Add(fieldCode);
        }
    }

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
           && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private static string ValueOrPlaceholder(string? value, string placeholder)
        => string.IsNullOrWhiteSpace(value) ? $"[{placeholder}]" : value.Trim();

    private static string StageLabel(string stageCode)
        => stageCode switch
        {
            BondedToDoorLogisticsStageCodes.CustomsControlledStorage =>
                "Customs-controlled storage or FTZ handoff, where currently authorized",
            BondedToDoorLogisticsStageCodes.CustomsWithdrawalAndRelease =>
                "Customs withdrawal and release coordination",
            BondedToDoorLogisticsStageCodes.InBondTransportation =>
                "Pre-release in-bond transportation, where separately authorized",
            BondedToDoorLogisticsStageCodes.ReleasedDomesticTransfer =>
                "Post-release domestic transfer",
            BondedToDoorLogisticsStageCodes.FulfillmentWarehouseInbound =>
                "Fulfillment warehouse receiving",
            BondedToDoorLogisticsStageCodes.BreakPackKittingAndRelabeling =>
                "Break-pack, kitting, and relabeling",
            BondedToDoorLogisticsStageCodes.ParticipantOrderPickPackAndParcelTender =>
                "Participant-order pick-pack and parcel tender",
            BondedToDoorLogisticsStageCodes.ParticipantAddressFinalMileDelivery =>
                "Delivery to participant addresses",
            BondedToDoorLogisticsStageCodes.ReturnsProcessing =>
                "Returns processing",
            _ => stageCode
        };
}

public sealed class UnavailableThirdPartyLogisticsProviderOutreachPreparationService
    : IThirdPartyLogisticsProviderOutreachPreparationService
{
    private readonly IOperatingMarketDeployment _deployment;

    public UnavailableThirdPartyLogisticsProviderOutreachPreparationService(
        IOperatingMarketDeployment deployment)
    {
        _deployment = deployment;
    }

    public ThirdPartyLogisticsProviderOutreachPreparationResponse Prepare(
        PrepareThirdPartyLogisticsProviderOutreachRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ThirdPartyLogisticsProviderOutreachPreparationResponse
        {
            Success = false,
            MarketCode = _deployment.MarketCode,
            ScopeCode = request.ScopeCode,
            AutomaticDispatchEnabled = false,
            ErrorCode = ThirdPartyLogisticsProviderOutreachErrorCodes
                .MarketNotAvailableInDeployment,
            ErrorMessage =
                "United States logistics outreach preparation is not available in this deployment."
        };
    }
}
