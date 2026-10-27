using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Application.Settlement;

/// <summary>이미 관측한 명세의 완료 주간과 합계만 구성합니다. 조회·시각 취득·지급 판단은 하지 않습니다.</summary>
internal static class WorkerWeeklyStatementProjector
{
    internal static (DateOnly Week, DateTime Start, DateTime End) CompletionWeek(DateOnly date)
    {
        var week = date.AddDays(-(((int)date.DayOfWeek - (int)DayOfWeek.Wednesday + 7) % 7));
        var start = DateTime.SpecifyKind(week.ToDateTime(TimeOnly.MinValue).AddHours(-9), DateTimeKind.Utc);
        var end = start.AddDays(7);
        return (week, start, end);
    }

    internal static WorkerWeeklyStatementDto Project(string actor, DateOnly week, DateTime start, DateTime end,
        IReadOnlyList<WorkerWeeklyStatementLineDto> lines, int unassigned, int excluded)
    {
        var unique = lines.DistinctBy(x => x.StableId, StringComparer.Ordinal).OrderBy(x => x.CompletedAtUtc).ThenBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        return new()
        {
            WorkerUserId = actor, CompletionWeekStartKst = week, PeriodStartUtc = start, PeriodEndUtc = end, Lines = unique,
            KnownAccruedAmountKrw = unique.Sum(x => x.AccruedAmountKrw ?? 0m),
            AccruedAmountKrw = unique.All(x => x.AccruedAmountKrw.HasValue) ? unique.Sum(x => x.AccruedAmountKrw!.Value) : null,
            KnownPaymentConditionReadyAmountKrw = unique.Where(x => x.PaymentConditionReady).Sum(x => x.AccruedAmountKrw ?? 0m),
            KnownHeldAmountKrw = unique.Where(x => !x.PaymentConditionReady).Sum(x => x.AccruedAmountKrw ?? 0m),
            ReportedPaymentAmountKrw = unique.Sum(x => x.ReportedPaymentAmountKrw), RecipientConfirmedAmountKrw = unique.Sum(x => x.RecipientConfirmedAmountKrw),
            UnknownAmountCount = unique.Count(x => !x.AccruedAmountKrw.HasValue), UnassignedCompletionTimeCount = unassigned,
            ExcludedDeliveryReferenceCount = excluded, ActualBankVerifiedAmountKrw = null, IsActualBankVerified = false
        };
    }
}
