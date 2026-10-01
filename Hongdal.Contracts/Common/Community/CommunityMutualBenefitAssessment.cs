namespace Hongdal.Contracts.Common.Community;

public static class CommunityMutualBenefitAssessmentStatusCodes
{
    public const string NeedsInformation = "needs-information";
    public const string NeedsAdjustment = "needs-adjustment";
    public const string ReadyForConversation = "ready-for-conversation";
    public const string MutualBenefitCandidate = "mutual-benefit-candidate";
}

public static class CommunityMutualBenefitRoleStatusCodes
{
    public const string NeedsInformation = "needs-information";
    public const string NeedsAdjustment = "needs-adjustment";
    public const string AwaitingParticipantReview = "awaiting-participant-review";
    public const string Candidate = "candidate";
}

public sealed class CommunityMutualBenefitRoleInput
{
    public string RoleKey { get; set; } = string.Empty;
    public string RoleLabel { get; set; } = string.Empty;
    public string ParticipantLabel { get; set; } = string.Empty;
    public string ExpectedBenefit { get; set; } = string.Empty;
    public string ContributionOrBurden { get; set; } = string.Empty;
    public string RiskOrCondition { get; set; } = string.Empty;
    public decimal? ExpectedBenefitAmount { get; set; }
    public decimal? ExpectedBurdenAmount { get; set; }
    public bool ParticipantReviewed { get; set; }
}

public sealed class CommunityMutualBenefitAssessmentRequest
{
    public string SharedPurpose { get; set; } = string.Empty;
    public string AllocationRule { get; set; } = string.Empty;
    public string ExitRule { get; set; } = string.Empty;
    public string EvidenceNote { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "KRW";
    public IReadOnlyList<CommunityMutualBenefitRoleInput> Roles { get; set; } = [];
}

public sealed record CommunityMutualBenefitRoleAssessment(
    string RoleKey,
    string RoleLabel,
    string ParticipantLabel,
    string StatusCode,
    decimal? NetBenefitAmount,
    IReadOnlyList<string> Issues);

public sealed class CommunityMutualBenefitAssessmentResult
{
    public string StatusCode { get; init; } = CommunityMutualBenefitAssessmentStatusCodes.NeedsInformation;
    public string CurrencyCode { get; init; } = string.Empty;
    public int RoleCount { get; init; }
    public int QuantifiedRoleCount { get; init; }
    public int ReviewedRoleCount { get; init; }
    public bool AllRolesReviewed { get; init; }
    public bool HasKnownImbalance { get; init; }
    public IReadOnlyList<CommunityMutualBenefitRoleAssessment> Roles { get; init; } = [];
    public IReadOnlyList<string> Issues { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public bool IsMutualBenefitCandidate
        => StatusCode == CommunityMutualBenefitAssessmentStatusCodes.MutualBenefitCandidate;

    public const string BoundaryNotice
        = "이 결과는 작성자가 입력한 추정과 당사자 확인 기록을 정리한 사전 검토이며, 계약·가격 합의·전문 자문·거래 실행을 대신하지 않습니다.";

    public const string EconomicValidationNotice
        = "물량·단가·비용과 참여자별 최소 편익은 공동조달 경제성 계획에서 계산 리비전과 참여자별 동의로 다시 확인해야 합니다.";
}

public static class CommunityMutualBenefitAssessmentEvaluator
{
    private const int MaximumRoles = 30;

    public static CommunityMutualBenefitAssessmentResult Evaluate(
        CommunityMutualBenefitAssessmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<string>();
        var warnings = new List<string>();
        var roles = request.Roles ?? [];
        var currencyCode = NormalizeCurrencyCode(request.CurrencyCode, issues);

        if (string.IsNullOrWhiteSpace(request.SharedPurpose))
        {
            issues.Add("함께 이루려는 목적을 적어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.AllocationRule))
        {
            issues.Add("비용·편익·업무를 나누는 기준을 적어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.ExitRule))
        {
            issues.Add("조건이 맞지 않을 때 중단하거나 다시 협의하는 기준을 적어야 합니다.");
        }

        if (roles.Count is < 2 or > MaximumRoles)
        {
            issues.Add($"영향을 받는 역할은 2개 이상 {MaximumRoles}개 이하로 구성해야 합니다.");
        }

        var duplicateRoleLabels = roles
            .Where(role => !string.IsNullOrWhiteSpace(role.RoleLabel))
            .GroupBy(role => role.RoleLabel.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateRoleLabels.Length > 0)
        {
            issues.Add($"같은 역할이 중복되었습니다: {string.Join(", ", duplicateRoleLabels)}");
        }

        var roleAssessments = roles
            .Take(MaximumRoles)
            .Select(EvaluateRole)
            .ToArray();
        var hasKnownImbalance = roleAssessments.Any(role =>
            role.StatusCode == CommunityMutualBenefitRoleStatusCodes.NeedsAdjustment);
        var hasIncompleteRole = roleAssessments.Any(role =>
            role.StatusCode == CommunityMutualBenefitRoleStatusCodes.NeedsInformation);
        var allRolesReviewed = roleAssessments.Length >= 2
                               && roleAssessments.All(role =>
                                   role.StatusCode == CommunityMutualBenefitRoleStatusCodes.Candidate);
        var quantifiedRoleCount = roleAssessments.Count(role => role.NetBenefitAmount.HasValue);
        var reviewedRoleCount = roles.Take(MaximumRoles).Count(role => role.ParticipantReviewed);

        if (string.IsNullOrWhiteSpace(request.EvidenceNote))
        {
            warnings.Add("가격·수량·역할 가정의 출처나 확인 시점을 남기면 다음 검토가 쉬워집니다.");
        }

        if (quantifiedRoleCount < roleAssessments.Length)
        {
            warnings.Add("금액이 모두 입력되지 않아 일부 역할은 정성적인 편익·부담만 검토했습니다.");
        }

        if (reviewedRoleCount < roleAssessments.Length)
        {
            warnings.Add("당사자가 직접 확인하지 않은 역할은 작성자의 가정으로 남습니다.");
        }

        var statusCode = hasKnownImbalance
            ? CommunityMutualBenefitAssessmentStatusCodes.NeedsAdjustment
            : issues.Count > 0 || hasIncompleteRole
                ? CommunityMutualBenefitAssessmentStatusCodes.NeedsInformation
                : allRolesReviewed
                    ? CommunityMutualBenefitAssessmentStatusCodes.MutualBenefitCandidate
                    : CommunityMutualBenefitAssessmentStatusCodes.ReadyForConversation;

        return new CommunityMutualBenefitAssessmentResult
        {
            StatusCode = statusCode,
            CurrencyCode = currencyCode,
            RoleCount = roleAssessments.Length,
            QuantifiedRoleCount = quantifiedRoleCount,
            ReviewedRoleCount = reviewedRoleCount,
            AllRolesReviewed = allRolesReviewed,
            HasKnownImbalance = hasKnownImbalance,
            Roles = roleAssessments,
            Issues = issues,
            Warnings = warnings
        };
    }

    private static CommunityMutualBenefitRoleAssessment EvaluateRole(
        CommunityMutualBenefitRoleInput role)
    {
        ArgumentNullException.ThrowIfNull(role);
        var issues = new List<string>();
        var roleLabel = role.RoleLabel?.Trim() ?? string.Empty;
        var participantLabel = role.ParticipantLabel?.Trim() ?? string.Empty;

        if (roleLabel.Length == 0)
        {
            issues.Add("역할 이름이 비어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(role.ExpectedBenefit))
        {
            issues.Add("이 역할이 얻을 기대 편익을 적어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(role.ContributionOrBurden))
        {
            issues.Add("이 역할이 맡을 기여나 부담을 적어야 합니다.");
        }

        if (string.IsNullOrWhiteSpace(role.RiskOrCondition))
        {
            issues.Add("이 역할의 위험 또는 미확정 조건을 적어야 합니다.");
        }

        decimal? netBenefit = null;
        if (role.ExpectedBenefitAmount.HasValue != role.ExpectedBurdenAmount.HasValue)
        {
            issues.Add("금액을 비교하려면 기대 편익과 예상 부담을 함께 입력해야 합니다.");
        }
        else if (role.ExpectedBenefitAmount is < 0m || role.ExpectedBurdenAmount is < 0m)
        {
            issues.Add("기대 편익과 예상 부담 금액은 0 이상이어야 합니다.");
        }
        else if (role.ExpectedBenefitAmount.HasValue && role.ExpectedBurdenAmount.HasValue)
        {
            netBenefit = Math.Round(
                role.ExpectedBenefitAmount.Value - role.ExpectedBurdenAmount.Value,
                2,
                MidpointRounding.AwayFromZero);
        }

        var statusCode = issues.Count > 0
            ? CommunityMutualBenefitRoleStatusCodes.NeedsInformation
            : netBenefit is <= 0m
                ? CommunityMutualBenefitRoleStatusCodes.NeedsAdjustment
                : role.ParticipantReviewed
                    ? CommunityMutualBenefitRoleStatusCodes.Candidate
                    : CommunityMutualBenefitRoleStatusCodes.AwaitingParticipantReview;
        if (statusCode == CommunityMutualBenefitRoleStatusCodes.NeedsAdjustment)
        {
            issues.Add("입력한 금액 기준으로 기대 편익이 부담보다 크지 않습니다.");
        }

        return new CommunityMutualBenefitRoleAssessment(
            role.RoleKey?.Trim() ?? string.Empty,
            roleLabel,
            participantLabel,
            statusCode,
            netBenefit,
            issues);
    }

    private static string NormalizeCurrencyCode(
        string? value,
        ICollection<string> issues)
    {
        var normalized = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || normalized.Any(character => !char.IsLetter(character)))
        {
            issues.Add("통화 코드는 ISO 4217 세 글자로 입력해야 합니다.");
        }

        return normalized;
    }
}
