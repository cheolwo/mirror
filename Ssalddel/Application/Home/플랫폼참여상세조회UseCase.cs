using System.Globalization;
using Ssalddel.Contracts.Common.Home;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Home;

public interface I플랫폼참여상세조회UseCase
{
    Task<HomeParticipationDetailsResponse> 상세조회Async(int page, CancellationToken cancellationToken);
}

/// <summary>공개 집계의 기준과 기록이 있는 월의 숫자만 읽습니다. 개인 원장·자금·업무 상태를 변경하지 않습니다.</summary>
public sealed class 플랫폼참여상세조회UseCase(SsalddelContext db, ISsalddelExecutionModePolicy execution,
    TimeProvider clock) : I플랫폼참여상세조회UseCase
{
    private const int PageSize = 12;
    public async Task<HomeParticipationDetailsResponse> 상세조회Async(int page, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 10000) throw new ArgumentOutOfRangeException(nameof(page));
        cancellationToken.ThrowIfCancellationRequested();
        // 호출자가 소유한 transaction은 종료하지 않습니다. 새 읽기 transaction은 실행 전략 안에서만 소유합니다.
        if (db.Database.CurrentTransaction is not null) return await ReadAsync(page, cancellationToken);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            var response = await ReadAsync(page, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return response;
        });
    }

    private async Task<HomeParticipationDetailsResponse> ReadAsync(int page, CancellationToken cancellationToken)
    {
        var summary = await HomeParticipationAggregateQuery.ReadSummaryAsync(db, execution, clock, cancellationToken);
        var months = await HomeParticipationAggregateQuery.EligibleRecords(db)
            .GroupBy(item => new { Year = item.ArrivedAtUtc.AddHours(9).Year, Month = item.ArrivedAtUtc.AddHours(9).Month })
            .OrderByDescending(group => group.Key.Year).ThenByDescending(group => group.Key.Month)
            .Select(group => new
            {
                group.Key.Year, group.Key.Month,
                CompletedCount = group.Count(),
                ShipperCount = group.Where(item => item.ShipperBound).Select(item => item.ShipperId).Distinct().Count(),
                DriverCount = group.Where(item => item.DriverBound).Select(item => item.DriverId).Distinct().Count(),
                Incomplete = group.Any(item => !item.ShipperBound || !item.DriverBound)
            }).Skip((page - 1) * PageSize).Take(PageSize + 1).ToArrayAsync(cancellationToken);
        // 현재 응답을 읽은 시각이며, 다른 페이지까지 고정하는 영속 snapshot이 아닙니다.
        summary.AsOfUtc = clock.GetUtcNow();
        return new()
        {
            Summary = summary, Page = page, PageSize = PageSize, HasMore = months.Length > PageSize,
            MonthlyItems = months.Take(PageSize).Select(month => new HomeParticipationMonthSummary
            {
                MonthCode = month.Year.ToString("D4", CultureInfo.InvariantCulture) + "-" + month.Month.ToString("D2", CultureInfo.InvariantCulture),
                CompletedFreightTransportCount = month.CompletedCount,
                FreightShipperAccountCount = month.ShipperCount, FreightDriverAccountCount = month.DriverCount,
                HasIncompleteParticipantBindings = month.Incomplete
            }).ToArray()
        };
    }
}
