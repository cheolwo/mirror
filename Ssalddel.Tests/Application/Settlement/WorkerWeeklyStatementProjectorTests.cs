using System.Globalization;
using System.Text.Json;
using Ssalddel.Application.Settlement;
using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Tests.Application.Settlement;

public sealed class WorkerWeeklyStatementProjectorTests
{
    [Theory]
    [InlineData("2026-10-20", "2026-10-14", "2026-10-13T15:00:00Z")]
    [InlineData("2026-10-21", "2026-10-21", "2026-10-20T15:00:00Z")]
    [InlineData("2026-10-25", "2026-10-21", "2026-10-20T15:00:00Z")]
    [InlineData("2027-01-01", "2026-12-30", "2026-12-29T15:00:00Z")]
    public void 완료주간은_KST수요일자정에서시작하는7일반열림범위다(string date, string week, string start)
    {
        var result = WorkerWeeklyStatementProjector.CompletionWeek(DateOnly.Parse(date, CultureInfo.InvariantCulture));

        Assert.Equal(DateOnly.Parse(week, CultureInfo.InvariantCulture), result.Week);
        Assert.Equal(DateTime.Parse(start, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal), result.Start);
        Assert.Equal(DateTimeKind.Utc, result.Start.Kind);
        Assert.Equal(result.Start.AddDays(7), result.End);
    }

    [Fact]
    public void 같은StableId는첫명세를유지하고_미확인금액과공제및대금확인은분리한다()
    {
        var at = new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc);
        var first = Line("food:same", at, 10m, true, 7m, 6m);
        var lines = new[]
        {
            first,
            Line("food:same", at.AddHours(1), 999m, false, 999m, 999m),
            Line("Food:same", at.AddMinutes(-1), 20m, false, 2m, 1m),
            Line("unknown", at, null, true, 0m, 0m),
            Line("activity:negative", at, -3m, false, -1m, -1m)
        };
        var before = JsonSerializer.Serialize(lines);
        var (week, start, end) = WorkerWeeklyStatementProjector.CompletionWeek(new(2026, 10, 20));

        var result = WorkerWeeklyStatementProjector.Project("worker", week, start, end, lines, 4, 5);

        Assert.Equal(new[] { "Food:same", "activity:negative", "food:same", "unknown" }, result.Lines.Select(line => line.StableId));
        Assert.Same(first, result.Lines[2]);
        Assert.Equal(27m, result.KnownAccruedAmountKrw);
        Assert.Null(result.AccruedAmountKrw);
        Assert.Equal(10m, result.KnownPaymentConditionReadyAmountKrw);
        Assert.Equal(17m, result.KnownHeldAmountKrw);
        Assert.Equal(8m, result.ReportedPaymentAmountKrw);
        Assert.Equal(6m, result.RecipientConfirmedAmountKrw);
        Assert.Equal(1, result.UnknownAmountCount);
        Assert.Equal(4, result.UnassignedCompletionTimeCount);
        Assert.Equal(5, result.ExcludedDeliveryReferenceCount);
        Assert.Null(result.ActualBankVerifiedAmountKrw);
        Assert.False(result.IsActualBankVerified);
        Assert.Equal("WorkCompletedAt", result.PeriodBasisCode);
        Assert.Equal(default, result.ObservedAtUtc);
        Assert.Equal(before, JsonSerializer.Serialize(lines));
    }

    [Fact]
    public void 빈명세의알려진금액과총액은0이며_관측시각은호출자가소유한다()
    {
        var (week, start, end) = WorkerWeeklyStatementProjector.CompletionWeek(new(2026, 10, 21));

        var result = WorkerWeeklyStatementProjector.Project("worker", week, start, end, [], 2, 3);

        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.KnownAccruedAmountKrw);
        Assert.Equal(0m, result.AccruedAmountKrw);
        Assert.Equal(2, result.UnassignedCompletionTimeCount);
        Assert.Equal(3, result.ExcludedDeliveryReferenceCount);
        Assert.Equal(default, result.ObservedAtUtc);
    }

    private static WorkerWeeklyStatementLineDto Line(string id, DateTime at, decimal? amount,
        bool ready, decimal reported, decimal confirmed) => new()
    {
        StableId = id, CompletedAtUtc = at, AccruedAmountKrw = amount,
        PaymentConditionReady = ready, ReportedPaymentAmountKrw = reported,
        RecipientConfirmedAmountKrw = confirmed
    };
}
