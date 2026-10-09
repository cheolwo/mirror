using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Commerce;

namespace Ssalddel.Services.Commerce;

/// <summary>준비 정책의 정보만 제공한다. 자격 승인·계약 성립·배차·입금 기능이 아니다.</summary>
public sealed class NeighborhoodTaskContractPolicyService(IOptions<NeighborhoodTaskContractOptions> options)
{
    public const string Version = "neighborhood-task-contract-policy-2026-10-08-r39";

    public NeighborhoodTaskContractPolicyDto GetPolicy()
    {
        var configured = options.Value;
        // DI 외부에서 직접 생성해도 잘못된 활성화 설정이 조용히 허용되지 않는다.
        var validation = new NeighborhoodTaskContractOptionsValidator().Validate(Options.DefaultName, configured);
        if (validation.Failed)
            throw new OptionsValidationException(Options.DefaultName, typeof(NeighborhoodTaskContractOptions), validation.Failures);

        return new NeighborhoodTaskContractPolicyDto
        {
            Version = Version,
            DefaultContractModel = configured.DefaultContractModel,
            ScopeCode = "NewPerTaskProposalPreparation",
            OperationalMinimumAgeYears = 19,
            CanCreateContracts = false,
            IsLegalClassificationDetermined = false,
            IsMinorParticipationEnabled = false,
            MinorParticipationStatusCode = "ReservedDisabled",
            RequiresVerifiedLegalRepresentative = true,
            FutureActivationRequirements =
            [
                "EffectiveLawAndReviewedPolicyRevision",
                "VerifiedParticipantAgeAndIdentity",
                "VerifiedLegalRepresentativeAuthority",
                "TaskScopedConsentAndExpiry",
                "PermittedWorkTimeAndTransport",
                "InsuranceTaxAndIncidentReadiness",
                "ServerEnforcementAcrossAcceptanceAssistanceAndWork",
                "ExplicitOperationalRolloutAfterValidation"
            ]
        };
    }
}
