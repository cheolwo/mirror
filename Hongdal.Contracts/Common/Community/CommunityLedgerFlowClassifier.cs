namespace Hongdal.Contracts.Common.Community;

public static class CommunityLedgerFlowClassifier
{
    private const int StrongMatchScore = 70;
    private const int PartialMatchScore = 35;
    private const int HumanReviewScoreGap = 8;

    private static readonly IReadOnlySet<string> GenericSignals = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "원장",
        "생활",
        "참여자",
        "진행",
        "진행상태",
        "상태",
        "확인",
        "완료",
        "증빙",
        "메모",
        "사진첨부",
        "타임라인"
    };

    public static CommunityLedgerFlowAnalysisResponse Analyze(CommunityLedgerFlowAnalysisRequest? request)
    {
        request ??= new CommunityLedgerFlowAnalysisRequest();

        var candidates = CommunityLedgerTemplateCatalog.All
            .Where(template => !template.IsCommunityOpportunityTemplate && !template.IsInternalAggregationTemplate)
            .Select(template => BuildCandidate(template, request))
            .OrderByDescending(candidate => candidate.MatchScore)
            .ThenBy(candidate => candidate.MissingRequiredSignals.Count)
            .ThenBy(candidate => candidate.TemplateKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var primary = candidates.Count > 0 ? candidates[0] : new CommunityLedgerFlowCandidateResponse();
        var runnerUp = candidates.Skip(1).FirstOrDefault();
        var closeRunnerUp = runnerUp is not null && primary.MatchScore - runnerUp.MatchScore < HumanReviewScoreGap;
        var weakPrimary = primary.MatchScore < PartialMatchScore;

        return new()
        {
            PrimaryCandidate = primary,
            Candidates = candidates,
            RequiresHumanReview = weakPrimary || closeRunnerUp,
            ReviewReason = BuildReviewReason(primary, runnerUp, weakPrimary, closeRunnerUp)
        };
    }

    private static CommunityLedgerFlowCandidateResponse BuildCandidate(
        CommunityLedgerTemplateResponse template,
        CommunityLedgerFlowAnalysisRequest request)
    {
        var requestSignals = BuildRequestSignals(request).ToList();
        var matchedSignals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedBlockCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var score = 0;

        score += AddMatchScore(template.DisplayName, requestSignals, matchedSignals, 22);
        score += AddMatchScore(template.WorkflowTag, requestSignals, matchedSignals, 18);
        score += AddMatchScore(template.TargetOperatingSystemName, requestSignals, matchedSignals, 12);
        score += AddMatchScore(template.Summary, requestSignals, matchedSignals, 8);

        foreach (var section in template.UiSectionHints)
        {
            score += AddMatchScore(section, requestSignals, matchedSignals, 10);
        }

        foreach (var action in template.ActionHints)
        {
            score += AddMatchScore(action, requestSignals, matchedSignals, 10);
        }

        foreach (var engine in template.EngineHints)
        {
            score += AddMatchScore(engine, requestSignals, matchedSignals, 5);
        }

        foreach (var role in template.Roles)
        {
            score += AddMatchScore(role.RoleName, requestSignals, matchedSignals, 5);
            score += AddMatchScore(role.Description, requestSignals, matchedSignals, 3);
        }

        foreach (var block in template.LedgerBlocks)
        {
            var blockScore = 0;
            blockScore += AddMatchScore(block.DisplayName, requestSignals, matchedSignals, 8);
            blockScore += AddMatchScore(block.UiSectionHint, requestSignals, matchedSignals, 8);
            blockScore += AddMatchScore(block.BlockType, requestSignals, matchedSignals, 4);

            foreach (var dataHint in block.DataHints)
            {
                blockScore += AddMatchScore(dataHint, requestSignals, matchedSignals, 3);
            }

            foreach (var actionHint in block.ActionHints)
            {
                blockScore += AddMatchScore(actionHint, requestSignals, matchedSignals, 5);
            }

            if (blockScore > 0)
            {
                matchedBlockCodes.Add(block.Code);
            }

            score += blockScore;
        }

        foreach (var rule in template.CompositionRules)
        {
            score += AddMatchScore(rule.Title, requestSignals, matchedSignals, 5);
            score += AddMatchScore(rule.Description, requestSignals, matchedSignals, 5);

            foreach (var signal in rule.RequiredUiSectionHints.Concat(rule.GatedActionHints))
            {
                score += AddMatchScore(signal, requestSignals, matchedSignals, 7);
            }
        }

        var missingRequiredSignals = template.CompositionRules
            .SelectMany(rule => rule.RequiredUiSectionHints)
            .Where(signal => !Matches(signal, requestSignals, allowGeneric: true))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        score = Math.Max(0, score - missingRequiredSignals.Count * 4);

        var relatedRuleCodes = template.CompositionRules
            .Where(rule => RuleHasRelatedSignal(rule, requestSignals))
            .Select(rule => rule.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var relationCode = score >= StrongMatchScore && missingRequiredSignals.Count <= 2
            ? CommunityLedgerFlowRelationCodes.StrongFlowMatch
            : score >= PartialMatchScore
                ? CommunityLedgerFlowRelationCodes.PartialFlowMatch
                : CommunityLedgerFlowRelationCodes.LooseCommunityRequest;

        return new()
        {
            TemplateKey = template.Key,
            DisplayName = template.DisplayName,
            TargetOperatingSystemCode = template.TargetOperatingSystemCode,
            TargetOperatingSystemName = template.TargetOperatingSystemName,
            RelationCode = relationCode,
            MatchScore = score,
            EngineHints = template.EngineHints,
            MatchedSignals = matchedSignals.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            MissingRequiredSignals = missingRequiredSignals,
            RelatedCompositionRuleCodes = relatedRuleCodes,
            RelatedLedgerBlockCodes = matchedBlockCodes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
            RelatedProcessingSurfaceHints = template.ProcessingSurfaces
                .Take(4)
                .Select(CommunityLedgerTemplateCatalog.BuildProcessingSurfaceHint)
                .ToList(),
            Reason = $"{template.DisplayName} 신호 {matchedSignals.Count}개와 누락 신호 {missingRequiredSignals.Count}개를 기준으로 {relationCode} 판정했습니다."
        };
    }

    private static int AddMatchScore(
        string? templateSignal,
        IReadOnlyList<string> requestSignals,
        ISet<string> matchedSignals,
        int weight)
    {
        if (!Matches(templateSignal, requestSignals))
        {
            return 0;
        }

        var normalized = NormalizeSignal(templateSignal);
        if (string.IsNullOrWhiteSpace(normalized) || GenericSignals.Contains(normalized))
        {
            return 0;
        }

        return matchedSignals.Add(templateSignal!.Trim()) ? weight : 0;
    }

    private static bool RuleHasRelatedSignal(
        CommunityLedgerCompositionRuleResponse rule,
        IReadOnlyList<string> requestSignals)
        => Matches(rule.Title, requestSignals)
           || Matches(rule.Description, requestSignals)
           || rule.RequiredUiSectionHints.Concat(rule.GatedActionHints).Any(signal => Matches(signal, requestSignals));

    private static IReadOnlyList<string> BuildRequestSignals(CommunityLedgerFlowAnalysisRequest request)
    {
        var values = new List<string>
        {
            request.Title,
            request.Body
        };

        values.AddRange(request.UiSectionHints);
        values.AddRange(request.ActionHints);
        values.AddRange(request.StateHints);

        foreach (var attribute in request.Attributes)
        {
            values.Add(attribute.Key);
            values.Add(attribute.Value);
        }

        values.AddRange(values.SelectMany(SplitTerms).ToList());

        return values
            .Select(NormalizeSignal)
            .Where(signal => !string.IsNullOrWhiteSpace(signal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool Matches(string? templateSignal, IReadOnlyList<string> requestSignals, bool allowGeneric = false)
    {
        var normalized = NormalizeSignal(templateSignal);
        if (string.IsNullOrWhiteSpace(normalized) || (!allowGeneric && GenericSignals.Contains(normalized)))
        {
            return false;
        }

        return requestSignals.Any(signal =>
            signal.Length >= 2
            && (signal.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(signal, StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<string> SplitTerms(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(
                [' ', '\t', '\r', '\n', ',', '.', '/', '|', '-', '_', ':', ';', '·', '(', ')', '[', ']'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeSignal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Trim().Where(ch => !char.IsWhiteSpace(ch)).ToArray());
    }

    private static string BuildReviewReason(
        CommunityLedgerFlowCandidateResponse primary,
        CommunityLedgerFlowCandidateResponse? runnerUp,
        bool weakPrimary,
        bool closeRunnerUp)
    {
        if (weakPrimary)
        {
            return "원장 형태 신호가 부족해 사람이 템플릿과 플로우를 확인해야 합니다.";
        }

        if (closeRunnerUp && runnerUp is not null)
        {
            return $"{primary.DisplayName}와 {runnerUp.DisplayName} 후보 점수 차이가 작아 사람이 최종 플로우를 확인해야 합니다.";
        }

        return "상위 후보가 충분히 분리되어 자동 제안할 수 있습니다.";
    }
}
