namespace Hongdal.Contracts.Common.Operations;

public static class DispatchConfirmationDecisionSourceCodes
{
    public const string PlatformCandidateInformation = "PlatformCandidateInformation";
    public const string ParticipatingDriverSelfAcceptance =
        "ParticipatingDriverSelfAcceptance";
    public const string QualifiedServiceProviderConfirmation =
        "QualifiedServiceProviderConfirmation";

    public static IReadOnlyList<string> All { get; } =
    [
        PlatformCandidateInformation,
        ParticipatingDriverSelfAcceptance,
        QualifiedServiceProviderConfirmation
    ];

    public static IReadOnlyList<string> ConfirmationCapable { get; } =
    [
        ParticipatingDriverSelfAcceptance,
        QualifiedServiceProviderConfirmation
    ];

    public static string Normalize(string? decisionSourceCode)
    {
        var candidate = decisionSourceCode?.Trim();
        return All.FirstOrDefault(value => string.Equals(
                   value,
                   candidate,
                   StringComparison.OrdinalIgnoreCase))
               ?? PlatformCandidateInformation;
    }
}

public static class DispatchConfirmationBoundaryDecisionCodes
{
    public const string CandidateInformationOnly = "CandidateInformationOnly";
    public const string ParticipatingDriverDecisionVerified =
        "ParticipatingDriverDecisionVerified";
    public const string QualifiedServiceProviderDecisionVerified =
        "QualifiedServiceProviderDecisionVerified";
    public const string ParticipantIdentityMismatch = "ParticipantIdentityMismatch";
    public const string VerifiedQualifiedServiceProviderRequired =
        "VerifiedQualifiedServiceProviderRequired";
}

public sealed record DispatchConfirmationBoundaryRequest(
    string DecisionSourceCode,
    string? ActorParticipantId,
    string? SelectedDriverParticipantId,
    string? VerifiedQualifiedServiceProviderParticipantId = null)
{
    public static DispatchConfirmationBoundaryRequest ForPlatformCandidateInformation(
        string? candidateDriverParticipantId = null)
        => new(
            DispatchConfirmationDecisionSourceCodes.PlatformCandidateInformation,
            ActorParticipantId: null,
            SelectedDriverParticipantId: candidateDriverParticipantId);

    public static DispatchConfirmationBoundaryRequest ForDriverSelfAcceptance(
        string? actorDriverParticipantId,
        string? selectedDriverParticipantId)
        => new(
            DispatchConfirmationDecisionSourceCodes.ParticipatingDriverSelfAcceptance,
            actorDriverParticipantId,
            selectedDriverParticipantId);
}

public sealed record DispatchConfirmationBoundaryDecision(
    string DecisionSourceCode,
    bool CanProvideCandidateInformation,
    bool CanConfirmDispatch,
    string DecisionCode,
    string ExecutionResponsibilityCode);

public static class CollectiveActionDispatchBoundaryPolicy
{
    public static DispatchConfirmationBoundaryDecision Evaluate(
        DispatchConfirmationBoundaryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decisionSourceCode = DispatchConfirmationDecisionSourceCodes.Normalize(
            request.DecisionSourceCode);
        var actorParticipantId = NormalizeParticipantId(request.ActorParticipantId);
        var selectedDriverParticipantId = NormalizeParticipantId(
            request.SelectedDriverParticipantId);

        if (decisionSourceCode ==
            DispatchConfirmationDecisionSourceCodes.ParticipatingDriverSelfAcceptance)
        {
            var canConfirm = actorParticipantId is not null
                             && selectedDriverParticipantId is not null
                             && string.Equals(
                                 actorParticipantId,
                                 selectedDriverParticipantId,
                                 StringComparison.Ordinal);

            return new DispatchConfirmationBoundaryDecision(
                decisionSourceCode,
                CanProvideCandidateInformation: true,
                CanConfirmDispatch: canConfirm,
                canConfirm
                    ? DispatchConfirmationBoundaryDecisionCodes
                        .ParticipatingDriverDecisionVerified
                    : DispatchConfirmationBoundaryDecisionCodes.ParticipantIdentityMismatch,
                RegulatedExecutionResponsibilityCodes.ParticipatingTransportProvider);
        }

        if (decisionSourceCode ==
            DispatchConfirmationDecisionSourceCodes.QualifiedServiceProviderConfirmation)
        {
            var verifiedParticipantId = NormalizeParticipantId(
                request.VerifiedQualifiedServiceProviderParticipantId);
            var canConfirm = actorParticipantId is not null
                             && selectedDriverParticipantId is not null
                             && verifiedParticipantId is not null
                             && string.Equals(
                                 actorParticipantId,
                                 verifiedParticipantId,
                                 StringComparison.Ordinal);

            return new DispatchConfirmationBoundaryDecision(
                decisionSourceCode,
                CanProvideCandidateInformation: true,
                CanConfirmDispatch: canConfirm,
                canConfirm
                    ? DispatchConfirmationBoundaryDecisionCodes
                        .QualifiedServiceProviderDecisionVerified
                    : DispatchConfirmationBoundaryDecisionCodes
                        .VerifiedQualifiedServiceProviderRequired,
                RegulatedExecutionResponsibilityCodes.ParticipatingQualifiedServiceProvider);
        }

        return new DispatchConfirmationBoundaryDecision(
            DispatchConfirmationDecisionSourceCodes.PlatformCandidateInformation,
            CanProvideCandidateInformation: true,
            CanConfirmDispatch: false,
            DispatchConfirmationBoundaryDecisionCodes.CandidateInformationOnly,
            RegulatedExecutionResponsibilityCodes.ParticipatingExecutionActor);
    }

    private static string? NormalizeParticipantId(string? participantId)
        => string.IsNullOrWhiteSpace(participantId) ? null : participantId.Trim();
}
