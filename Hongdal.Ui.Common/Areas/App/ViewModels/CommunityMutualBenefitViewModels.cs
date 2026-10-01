using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Ui.Common.Areas.App.ViewModels;

public sealed record CommunityMutualBenefitRoleSeed(
    string RoleLabel,
    string ParticipantLabel);

public sealed class CommunityMutualBenefitRoleViewModel : ObservableObject
{
    private string _roleLabel;
    private string _participantLabel;
    private string _expectedBenefit;
    private string _contributionOrBurden;
    private string _riskOrCondition;
    private decimal? _expectedBenefitAmount;
    private decimal? _expectedBurdenAmount;
    private bool _participantReviewed;

    public CommunityMutualBenefitRoleViewModel(
        string roleKey,
        string roleLabel,
        string participantLabel,
        string expectedBenefit,
        string contributionOrBurden,
        string riskOrCondition)
    {
        RoleKey = roleKey;
        _roleLabel = roleLabel;
        _participantLabel = participantLabel;
        _expectedBenefit = expectedBenefit;
        _contributionOrBurden = contributionOrBurden;
        _riskOrCondition = riskOrCondition;
    }

    public string RoleKey { get; }

    public string RoleLabel
    {
        get => _roleLabel;
        set => SetProperty(ref _roleLabel, value ?? string.Empty);
    }

    public string ParticipantLabel
    {
        get => _participantLabel;
        set => SetProperty(ref _participantLabel, value ?? string.Empty);
    }

    public string ExpectedBenefit
    {
        get => _expectedBenefit;
        set => SetProperty(ref _expectedBenefit, value ?? string.Empty);
    }

    public string ContributionOrBurden
    {
        get => _contributionOrBurden;
        set => SetProperty(ref _contributionOrBurden, value ?? string.Empty);
    }

    public string RiskOrCondition
    {
        get => _riskOrCondition;
        set => SetProperty(ref _riskOrCondition, value ?? string.Empty);
    }

    public decimal? ExpectedBenefitAmount
    {
        get => _expectedBenefitAmount;
        set => SetProperty(ref _expectedBenefitAmount, value);
    }

    public decimal? ExpectedBurdenAmount
    {
        get => _expectedBurdenAmount;
        set => SetProperty(ref _expectedBurdenAmount, value);
    }

    public bool ParticipantReviewed
    {
        get => _participantReviewed;
        set => SetProperty(ref _participantReviewed, value);
    }

    public CommunityMutualBenefitRoleInput ToInput()
        => new()
        {
            RoleKey = RoleKey,
            RoleLabel = RoleLabel.Trim(),
            ParticipantLabel = ParticipantLabel.Trim(),
            ExpectedBenefit = ExpectedBenefit.Trim(),
            ContributionOrBurden = ContributionOrBurden.Trim(),
            RiskOrCondition = RiskOrCondition.Trim(),
            ExpectedBenefitAmount = ExpectedBenefitAmount,
            ExpectedBurdenAmount = ExpectedBurdenAmount,
            ParticipantReviewed = ParticipantReviewed
        };
}

public sealed class CommunityAuthoringMutualBenefitViewModel : ObservableObject
{
    private string _sharedPurpose = string.Empty;
    private string _allocationRule = string.Empty;
    private string _exitRule = string.Empty;
    private string _evidenceNote = string.Empty;
    private string _currencyCode = "KRW";
    private bool _includeAmountsInDraft;
    private CommunityMutualBenefitAssessmentResult? _assessment;
    private string? _statusMessage;

    public CommunityAuthoringMutualBenefitViewModel()
    {
        Reset();
    }

    public ObservableCollection<CommunityMutualBenefitRoleViewModel> Roles { get; } = [];

    public string SharedPurpose
    {
        get => _sharedPurpose;
        set
        {
            if (SetProperty(ref _sharedPurpose, value ?? string.Empty))
            {
                InvalidateAssessment();
            }
        }
    }

    public string AllocationRule
    {
        get => _allocationRule;
        set
        {
            if (SetProperty(ref _allocationRule, value ?? string.Empty))
            {
                InvalidateAssessment();
            }
        }
    }

    public string ExitRule
    {
        get => _exitRule;
        set
        {
            if (SetProperty(ref _exitRule, value ?? string.Empty))
            {
                InvalidateAssessment();
            }
        }
    }

    public string EvidenceNote
    {
        get => _evidenceNote;
        set
        {
            if (SetProperty(ref _evidenceNote, value ?? string.Empty))
            {
                InvalidateAssessment();
            }
        }
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        set
        {
            if (SetProperty(ref _currencyCode, value ?? string.Empty))
            {
                InvalidateAssessment();
            }
        }
    }

    public bool IncludeAmountsInDraft
    {
        get => _includeAmountsInDraft;
        set => SetProperty(ref _includeAmountsInDraft, value);
    }

    public CommunityMutualBenefitAssessmentResult? Assessment
    {
        get => _assessment;
        private set
        {
            if (SetProperty(ref _assessment, value))
            {
                OnPropertyChanged(nameof(HasAssessment));
                OnPropertyChanged(nameof(AssessmentStatusLabel));
            }
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasAssessment => Assessment is not null;

    public string AssessmentStatusLabel
        => ResolveAssessmentStatusLabel(Assessment?.StatusCode);

    public void PrepareFromDraft(string? title)
    {
        if (string.IsNullOrWhiteSpace(SharedPurpose)
            && !string.IsNullOrWhiteSpace(title))
        {
            SharedPurpose = title.Trim();
        }
    }

    public CommunityMutualBenefitRoleViewModel AddRole()
    {
        var role = AddRoleCore(
            $"role-{Guid.NewGuid():N}",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty);
        StatusMessage = "새 역할을 추가했습니다.";
        return role;
    }

    public bool RemoveRole(CommunityMutualBenefitRoleViewModel role)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (!Roles.Remove(role))
        {
            return false;
        }

        role.PropertyChanged -= HandleRoleChanged;
        InvalidateAssessment();
        StatusMessage = $"'{role.RoleLabel}' 역할을 제거했습니다.";
        return true;
    }

    public int ImportRoleSeeds(IEnumerable<CommunityMutualBenefitRoleSeed> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);
        var importedCount = 0;
        foreach (var group in seeds
                     .Where(seed => !string.IsNullOrWhiteSpace(seed.RoleLabel))
                     .GroupBy(seed => seed.RoleLabel.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var participants = group
                .Select(seed => seed.ParticipantLabel?.Trim() ?? string.Empty)
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var existing = Roles.FirstOrDefault(role => string.Equals(
                role.RoleLabel.Trim(),
                group.Key,
                StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                var mergedParticipants = existing.ParticipantLabel
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Concat(participants)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                existing.ParticipantLabel = string.Join(", ", mergedParticipants);
                importedCount++;
                continue;
            }

            AddRoleCore(
                $"diagram-role-{Guid.NewGuid():N}",
                group.Key,
                string.Join(", ", participants),
                "참여 조건과 대가가 투명하게 확인됩니다.",
                "해당 단계의 제공 범위, 가능량과 조건을 직접 제안합니다.",
                "자격·등록·계약 권한과 실제 이행 가능성을 별도로 확인해야 합니다.");
            importedCount++;
        }

        InvalidateAssessment();
        StatusMessage = importedCount > 0
            ? $"다이어그램의 업체 역할 {importedCount:N0}개를 반영했습니다. 실제 당사자 확인 전에는 가정으로 남습니다."
            : "다이어그램에 새로 가져올 업체 역할이 없습니다.";
        return importedCount;
    }

    public CommunityMutualBenefitAssessmentResult Evaluate()
    {
        Assessment = CommunityMutualBenefitAssessmentEvaluator.Evaluate(new()
        {
            SharedPurpose = SharedPurpose,
            AllocationRule = AllocationRule,
            ExitRule = ExitRule,
            EvidenceNote = EvidenceNote,
            CurrencyCode = CurrencyCode,
            Roles = Roles.Select(role => role.ToInput()).ToArray()
        });
        StatusMessage = Assessment.StatusCode switch
        {
            CommunityMutualBenefitAssessmentStatusCodes.NeedsAdjustment
                => "알려진 조건에서 한 역할의 편익이 부담보다 크지 않습니다. 조건을 다시 나눠야 합니다.",
            CommunityMutualBenefitAssessmentStatusCodes.NeedsInformation
                => "판정에 필요한 목적, 역할별 편익·부담 또는 공통 규칙을 보완해 주세요.",
            CommunityMutualBenefitAssessmentStatusCodes.MutualBenefitCandidate
                => "입력된 조건과 당사자 확인 기록 기준으로 상호 이익 후보입니다. 실제 경제성은 별도 리비전에서 검증합니다.",
            _ => "작성자 사전 검토가 끝났습니다. 각 당사자가 자신의 편익·부담과 조건을 직접 확인해야 합니다."
        };
        return Assessment;
    }

    public string BuildDraftSection()
    {
        var assessment = Evaluate();
        var lines = new List<string>
        {
            $"판정: {ResolveAssessmentStatusLabel(assessment.StatusCode)}",
            $"공동 목적: {SharedPurpose.Trim()}",
            $"검토 역할: {assessment.RoleCount:N0}개 · 당사자 확인 {assessment.ReviewedRoleCount:N0}/{assessment.RoleCount:N0}",
            string.Empty,
            "역할별 기대와 부담"
        };

        foreach (var role in Roles)
        {
            var roleAssessment = assessment.Roles.FirstOrDefault(item =>
                string.Equals(item.RoleKey, role.RoleKey, StringComparison.Ordinal));
            var participant = string.IsNullOrWhiteSpace(role.ParticipantLabel)
                ? string.Empty
                : $" · 후보/참여자: {role.ParticipantLabel.Trim()}";
            lines.Add($"- {role.RoleLabel.Trim()} [{ResolveRoleStatusLabel(roleAssessment?.StatusCode)}]{participant}");
            lines.Add($"  기대 편익: {role.ExpectedBenefit.Trim()}");
            lines.Add($"  기여·부담: {role.ContributionOrBurden.Trim()}");
            lines.Add($"  위험·조건: {role.RiskOrCondition.Trim()}");
            if (IncludeAmountsInDraft && roleAssessment?.NetBenefitAmount is decimal netBenefit)
            {
                lines.Add($"  공개용 순편익 추정: {netBenefit:N2} {assessment.CurrencyCode}");
            }
        }

        lines.Add(string.Empty);
        lines.Add($"배분 기준: {AllocationRule.Trim()}");
        lines.Add($"중단·재협의 기준: {ExitRule.Trim()}");
        if (!string.IsNullOrWhiteSpace(EvidenceNote))
        {
            lines.Add($"근거·확인 시점: {EvidenceNote.Trim()}");
        }

        var findings = assessment.Issues
            .Concat(assessment.Roles.SelectMany(role => role.Issues.Select(issue => $"{role.RoleLabel}: {issue}")))
            .Concat(assessment.Warnings)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (findings.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add("더 확인할 점");
            lines.AddRange(findings.Select(finding => $"- {finding}"));
        }

        lines.Add(string.Empty);
        lines.Add($"※ {CommunityMutualBenefitAssessmentResult.BoundaryNotice}");
        lines.Add($"※ {CommunityMutualBenefitAssessmentResult.EconomicValidationNotice}");
        return string.Join(Environment.NewLine, lines);
    }

    public void Reset()
    {
        foreach (var role in Roles)
        {
            role.PropertyChanged -= HandleRoleChanged;
        }

        Roles.Clear();
        _sharedPurpose = string.Empty;
        _allocationRule = "실제 수량, 확인된 비용과 맡은 업무 범위에 따라 나누고 변경 시 새 리비전으로 다시 확인합니다.";
        _exitRule = "목표 단가·품질·법적 요건이 맞지 않으면 계약·결제 전에 중단하고 다시 협의합니다.";
        _evidenceNote = string.Empty;
        _currencyCode = "KRW";
        _includeAmountsInDraft = false;
        AddRoleCore(
            "buyer",
            "구매자·참여자",
            string.Empty,
            "개별 구매보다 나은 조건과 필요한 수량을 확보합니다.",
            "참여 수량과 결제·수령 조건을 직접 확인합니다.",
            "최종 단가, 품질, 납기와 취소 조건을 확인해야 합니다.");
        AddRoleCore(
            "supplier",
            "판매자·공급자",
            string.Empty,
            "예측 가능한 묶음 수요와 출하 계획을 확보합니다.",
            "공급 가능량, 가격·품질·납기 근거를 제안합니다.",
            "최소 주문량, 생산 여력, 반품과 대금 조건을 확인해야 합니다.");
        AddRoleCore(
            "facilitator",
            "제안자·조율자",
            string.Empty,
            "정보와 조건이 투명하게 모여 실제 협의 가능성을 확인합니다.",
            "자료 출처, 변경 사항과 역할 공백을 공개하고 확인을 요청합니다.",
            "계약·가격 결정·운송 주선을 대신하지 않으며 당사자의 독립 판단이 필요합니다.");
        Assessment = null;
        StatusMessage = null;
        OnPropertyChanged(nameof(SharedPurpose));
        OnPropertyChanged(nameof(AllocationRule));
        OnPropertyChanged(nameof(ExitRule));
        OnPropertyChanged(nameof(EvidenceNote));
        OnPropertyChanged(nameof(CurrencyCode));
        OnPropertyChanged(nameof(IncludeAmountsInDraft));
        OnPropertyChanged(nameof(Roles));
    }

    public static string ResolveAssessmentStatusLabel(string? statusCode)
        => statusCode switch
        {
            CommunityMutualBenefitAssessmentStatusCodes.NeedsAdjustment => "조건 조정 필요",
            CommunityMutualBenefitAssessmentStatusCodes.ReadyForConversation => "당사자 협의 준비",
            CommunityMutualBenefitAssessmentStatusCodes.MutualBenefitCandidate => "상호 이익 후보",
            _ => "정보 보완 필요"
        };

    public static string ResolveRoleStatusLabel(string? statusCode)
        => statusCode switch
        {
            CommunityMutualBenefitRoleStatusCodes.NeedsAdjustment => "조건 조정",
            CommunityMutualBenefitRoleStatusCodes.AwaitingParticipantReview => "당사자 확인 전",
            CommunityMutualBenefitRoleStatusCodes.Candidate => "확인 기록 있음",
            _ => "정보 보완"
        };

    private CommunityMutualBenefitRoleViewModel AddRoleCore(
        string roleKey,
        string roleLabel,
        string participantLabel,
        string expectedBenefit,
        string contributionOrBurden,
        string riskOrCondition)
    {
        var role = new CommunityMutualBenefitRoleViewModel(
            roleKey,
            roleLabel,
            participantLabel,
            expectedBenefit,
            contributionOrBurden,
            riskOrCondition);
        role.PropertyChanged += HandleRoleChanged;
        Roles.Add(role);
        return role;
    }

    private void HandleRoleChanged(object? sender, PropertyChangedEventArgs e)
        => InvalidateAssessment();

    private void InvalidateAssessment()
    {
        if (Assessment is null)
        {
            return;
        }

        Assessment = null;
        StatusMessage = "조건이 바뀌었습니다. 다시 판정해 주세요.";
    }
}
