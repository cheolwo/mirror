using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>저장된 회차를 변경하지 않고 조회자에게 허용된 전문 원천의 현재 상태만 모읍니다.</summary>
internal static class 공간공동운영변화Query
{
    internal static async Task<공간공동운영변화Dto> 조회Async(공간공동운영Record record, string actor,
        string currentExecutionMode, IReadOnlyList<공간공동운영원천상태Dto> latestCycleSources,
        I공간공동운영전문업무Bridge bridge, TimeProvider clock, CancellationToken cancellationToken)
    {
        var accepted = record.Participants.Where(item => item.StateCode == "accepted").ToArray();
        var acceptedAccounts = accepted.Select(item => item.UserId).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);
        var acceptedRoles = accepted.GroupBy(item => item.RoleCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        var matchesMode = record.ExecutionMode == currentExecutionMode;
        var cycles = new List<공간공동운영회차변화Dto>();
        for (var index = 0; index < record.Cycles.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cycle = record.Cycles[index];
            // 마지막 회차는 같은 상세 응답의 readiness 조회 결과를 재사용합니다. 다른 회차로 전파하지 않습니다.
            var rawSources = index == record.Cycles.Count - 1 ? latestCycleSources
                : !matchesMode || cycle.References.Count == 0 ? []
                : await bridge.ValidateAsync(actor, cycle.References, cancellationToken);
            var referenceGroups = cycle.References.GroupBy(reference => (reference.KindCode, reference.ReferenceId)).ToArray();
            var sources = referenceGroups.Select(group =>
            {
                var reference = group.First();
                if (!matchesMode) return 미확인(reference, "execution-mode-mismatch", "현재 실행 모드와 다른 회차의 원천은 확인하지 않았습니다.");
                if (group.Count() != 1) return 미확인(reference, "duplicate-reference", "같은 전문 원장 연결이 중복되어 다시 확인해야 합니다.");
                var matching = rawSources.Where(source => source.KindCode == reference.KindCode && source.ReferenceId == reference.ReferenceId).ToArray();
                if (matching.Length != 1) return 미확인(reference, matching.Length == 0 ? "source-result-missing" : "source-result-duplicate",
                    "이 연결의 유일한 현재 조회 결과를 확인하지 못했습니다.");
                var source = matching[0];
                return new 공간공동운영원천상태Dto
                {
                    KindCode = reference.KindCode, ReferenceId = reference.ReferenceId, StatusCode = source.StatusCode,
                    Label = source.Label, Verified = source.Verified, Completed = source.Verified && source.Completed,
                    VerifiedRevision = source.Verified ? source.VerifiedRevision : null,
                    RequiresAttention = !source.Verified || source.RequiresAttention,
                    NextActionLabel = source.NextActionLabel, Href = source.Href
                };
            }).ToArray();
            cycles.Add(new()
            {
                CycleId = cycle.CycleId, Number = cycle.Number, TermsRevision = cycle.TermsRevision,
                StatusCode = cycle.StatusCode, StartedAtUtc = cycle.StartedAtUtc, CompletedAtUtc = cycle.CompletedAtUtc,
                SourceCompletionRecorded = cycle.SourceCompletionVerified,
                LinkedSourceCount = referenceGroups.Length,
                DuplicateReferenceCount = referenceGroups.Sum(group => group.Count() - 1),
                UnverifiedSourceCount = sources.Count(source => !source.Verified),
                AttentionSourceCount = sources.Count(source => source.RequiresAttention), Sources = sources
            });
        }
        return new()
        {
            AsOfUtc = clock.GetUtcNow(), ExecutionMode = record.ExecutionMode, MatchesCurrentExecutionMode = matchesMode,
            TermsRevision = record.TermsRevision, AcceptedParticipantCount = acceptedAccounts.Count,
            InterestedParticipantCount = record.Participants.Where(item => item.StateCode == "interested" && !acceptedAccounts.Contains(item.UserId))
                .Select(item => item.UserId).Distinct(StringComparer.Ordinal).Count(),
            AcceptedRoleCodes = acceptedRoles.Select(group => group.Key).ToArray(),
            CurrentTermsAgreedRoleCodes = acceptedRoles.Where(group => group.All(item => item.AgreedTermsRevision == record.TermsRevision))
                .Select(group => group.Key).ToArray(),
            PendingTermsAgreementRoleCodes = acceptedRoles.Where(group => group.Any(item => item.AgreedTermsRevision != record.TermsRevision))
                .Select(group => group.Key).ToArray(),
            MissingRoleCodes = record.Terms.RequiredRoleCodes.Where(role => !acceptedRoles.Any(group => group.Key == role))
                .Distinct(StringComparer.Ordinal).ToArray(), Cycles = cycles
        };
    }

    private static 공간공동운영원천상태Dto 미확인(공간공동운영전문참조Dto reference, string code, string label)
        => new() { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId,
            StatusCode = code, Label = label, RequiresAttention = true };
}
