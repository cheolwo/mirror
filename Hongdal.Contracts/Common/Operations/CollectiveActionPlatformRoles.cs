namespace Hongdal.Contracts.Common.Operations;

public static class PlatformOperatingRoleCodes
{
    public const string CollectiveActionFacilitator = "CollectiveActionFacilitator";
}

public static class RegulatedExecutionResponsibilityCodes
{
    public const string ParticipatingExecutionActor =
        "ParticipatingExecutionActor";

    public const string ParticipatingTransportProvider =
        "ParticipatingTransportProvider";

    public const string ParticipatingQualifiedServiceProvider =
        "ParticipatingQualifiedServiceProvider";
}

public static class FreightWorkflowActivityCodes
{
    public const string CommunityIntentCoordination = "CommunityIntentCoordination";
    public const string QualifiedProviderParticipationRequest =
        "QualifiedProviderParticipationRequest";
    public const string RegulatedTransportationArrangement =
        "RegulatedTransportationArrangement";

    public static IReadOnlyList<string> All { get; } =
    [
        CommunityIntentCoordination,
        QualifiedProviderParticipationRequest,
        RegulatedTransportationArrangement
    ];

    public static string Normalize(
        string? activityCode,
        bool legacyRequestsTransportationArrangement = false)
    {
        if (legacyRequestsTransportationArrangement)
        {
            return RegulatedTransportationArrangement;
        }

        var candidate = activityCode?.Trim();
        return All.FirstOrDefault(value => string.Equals(
                   value,
                   candidate,
                   StringComparison.OrdinalIgnoreCase))
               ?? RegulatedTransportationArrangement;
    }

    public static bool RequiresRegulatedServiceProvider(string? activityCode)
        => string.Equals(
            Normalize(activityCode),
            RegulatedTransportationArrangement,
            StringComparison.OrdinalIgnoreCase);
}

public static class FreightServiceProviderRoleCodes
{
    public const string KoreaFreightTransportBroker = "KR.FreightTransportBroker";
    public const string UnitedStatesPropertyBroker = "US.PropertyBroker";
}
