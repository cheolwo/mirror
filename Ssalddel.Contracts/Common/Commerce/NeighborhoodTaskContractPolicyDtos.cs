namespace Ssalddel.Contracts.Common.Commerce;

public static class NeighborhoodTaskContractModels
{
    public const string PerTaskFreelance = "PerTaskFreelance";
}

/// <summary>
/// 새 건별 의뢰의 준비 정책을 설명하는 정보입니다.
/// 계약 성립, 근로자성 판정 또는 미성년자의 실제 참여 허가를 의미하지 않습니다.
/// </summary>
public sealed class NeighborhoodTaskContractPolicyDto
{
    public string Version { get; set; } = string.Empty;
    public string DefaultContractModel { get; set; } = NeighborhoodTaskContractModels.PerTaskFreelance;
    public string ScopeCode { get; set; } = "NewPerTaskProposalPreparation";
    public int OperationalMinimumAgeYears { get; set; } = 19;
    public bool CanCreateContracts { get; set; }
    public bool IsLegalClassificationDetermined { get; set; }
    public bool IsMinorParticipationEnabled { get; set; }
    public string MinorParticipationStatusCode { get; set; } = "ReservedDisabled";
    public bool RequiresVerifiedLegalRepresentative { get; set; } = true;
    public IReadOnlyList<string> FutureActivationRequirements { get; set; } = [];
}
