using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Admin.Finance;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Finance;
using Ssalddel.Domain.운영;
using 살뜰.Data;

namespace Ssalddel.Application.Admin.Finance;

public interface I운영재무조회UseCase
{
    운영재무메타데이터응답Dto 메타데이터조회();

    Task<운영재무사건목록응답Dto> 사건목록조회Async(
        string? profileStableId,
        string? currencyCode,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<운영관리계정잔액Dto>> 관리계정잔액조회Async(
        string? currencyCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken);

    Task<운영현금흐름요약Dto> 현금흐름요약조회Async(
        string currencyCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<운영재무대사예외Dto>> 대사예외조회Async(
        string? statusCode,
        CancellationToken cancellationToken);

    Task<플랫폼운영경제성원장평가응답Dto> 원장기반경제성평가Async(
        플랫폼운영경제성원장평가요청Dto request,
        CancellationToken cancellationToken);
}

public sealed class 운영재무조회UseCase(
    SsalddelContext db,
    I플랫폼운영경제성Calculator economicsCalculator)
    : I운영재무조회UseCase
{
    public 운영재무메타데이터응답Dto 메타데이터조회()
        => new()
        {
            관리계정목록 = 관리계정Catalog.GetAll()
                .Select(account => new 운영관리계정Dto
                {
                    StableId = account.StableId,
                    표시명 = account.DisplayName,
                    분류Code = account.CategoryCode,
                    정상잔액방향Code = account.NormalBalanceSide.ToString(),
                    운영의미 = account.Meaning,
                    실제회계계정승인여부 = account.IsActualAccountingAccountApproved
                })
                .ToArray(),
            재무영향Profile목록 = 재무영향ProfileCatalog.GetAll()
                .Select(profile => new 운영재무영향ProfileDto
                {
                    StableId = profile.StableId,
                    재무의미Code = profile.FinancialMeaningCode,
                    영향유형Code = profile.ImpactKind.ToString(),
                    인식시점Code = profile.RecognitionTiming.ToString(),
                    금액근거Code = profile.AmountBasisCode,
                    매핑Revision = profile.MappingRevision,
                    승인상태Code = profile.ApprovalStatusCode,
                    Simulation전용 = profile.IsSimulationOnly,
                    운영전표쓰기허용 = profile.OperationalPostingAllowed,
                    계정목록 = profile.Accounts.Select(account => new 운영재무영향계정Dto
                    {
                        관리계정StableId = account.ManagementAccountStableId,
                        관리계정표시명 = 관리계정Catalog.Find(account.ManagementAccountStableId)?.DisplayName ?? string.Empty,
                        역할Code = account.Role.ToString()
                    }).ToArray()
                })
                .ToArray()
        };

    public async Task<운영재무사건목록응답Dto> 사건목록조회Async(
        string? profileStableId,
        string? currencyCode,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var query = db.재무사건
            .AsNoTracking()
            .Include(item => item.관리계정전기목록)
            .AsQueryable();
        query = ApplyEventFilters(query, profileStableId, currencyCode, from, to);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.업무발생일시Utc)
            .ThenByDescending(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return new 운영재무사건목록응답Dto
        {
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            Items = items.Select(ToDto).ToArray()
        };
    }

    public async Task<IReadOnlyList<운영관리계정잔액Dto>> 관리계정잔액조회Async(
        string? currencyCode,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var eventQuery = ApplyEventFilters(
            db.재무사건.AsNoTracking(),
            null,
            currencyCode,
            from,
            to);
        var eventIds = eventQuery.Select(item => item.Id);
        var rows = await db.관리계정전기
            .AsNoTracking()
            .Where(line => eventIds.Contains(line.재무사건Id))
            .GroupBy(line => new { line.관리계정StableId, line.통화Code })
            .Select(group => new
            {
                group.Key.관리계정StableId,
                group.Key.통화Code,
                Debit = group.Where(line => line.전기방향Code == "DebitCandidate").Sum(line => line.금액),
                Credit = group.Where(line => line.전기방향Code == "CreditCandidate").Sum(line => line.금액)
            })
            .OrderBy(item => item.통화Code)
            .ThenBy(item => item.관리계정StableId)
            .ToArrayAsync(cancellationToken);

        return rows.Select(row =>
        {
            var definition = 관리계정Catalog.Find(row.관리계정StableId);
            var normalSide = definition?.NormalBalanceSide ?? 관리계정정상잔액방향.Debit;
            return new 운영관리계정잔액Dto
            {
                관리계정StableId = row.관리계정StableId,
                관리계정표시명 = definition?.DisplayName ?? row.관리계정StableId,
                통화Code = row.통화Code,
                차변합계 = row.Debit,
                대변합계 = row.Credit,
                정상잔액 = normalSide == 관리계정정상잔액방향.Debit
                    ? row.Debit - row.Credit
                    : row.Credit - row.Debit,
                정상잔액방향Code = normalSide.ToString()
            };
        }).ToArray();
    }

    public async Task<IReadOnlyList<운영재무대사예외Dto>> 대사예외조회Async(
        string? statusCode,
        CancellationToken cancellationToken)
    {
        var query = db.재무대사예외.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            var normalized = statusCode.Trim();
            query = query.Where(item => item.상태Code == normalized);
        }

        return await query
            .OrderByDescending(item => item.감지일시Utc)
            .Take(300)
            .Select(item => new 운영재무대사예외Dto
            {
                StableId = item.StableId,
                재무사건StableId = item.재무사건StableId,
                예외Code = item.예외Code,
                상태Code = item.상태Code,
                요약Code = item.요약Code,
                감지일시Utc = item.감지일시Utc,
                해결일시Utc = item.해결일시Utc
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<운영현금흐름요약Dto> 현금흐름요약조회Async(
        string currencyCode,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var currency = currencyCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (currency.Length != 3)
        {
            throw new ArgumentException("통화 코드는 ISO 4217 세 글자 형식이어야 합니다.", nameof(currencyCode));
        }

        if (from > to)
        {
            throw new ArgumentException("현금 흐름 조회 시작일은 종료일보다 늦을 수 없습니다.", nameof(from));
        }

        var startUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var events = await db.재무사건
            .AsNoTracking()
            .Include(item => item.관리계정전기목록)
            .Where(item => item.통화Code == currency
                && ((item.업무발생일시Utc >= startUtc && item.업무발생일시Utc < endExclusiveUtc)
                    || (item.현금이동일시Utc.HasValue
                        && item.현금이동일시Utc.Value >= startUtc
                        && item.현금이동일시Utc.Value < endExclusiveUtc)))
            .OrderBy(item => item.StableId)
            .ToArrayAsync(cancellationToken);

        var cashMovementEvents = events
            .Where(item => item.현금이동일시Utc.HasValue
                && item.현금이동일시Utc.Value >= startUtc
                && item.현금이동일시Utc.Value < endExclusiveUtc)
            .Where(item => item.관리계정전기목록.Any(line =>
                line.관리계정StableId == 관리계정StableIds.가용현금))
            .ToArray();
        var cashLines = cashMovementEvents
            .SelectMany(item => item.관리계정전기목록)
            .Where(line => line.관리계정StableId == 관리계정StableIds.가용현금)
            .ToArray();
        var occurredEvents = events
            .Where(item => item.업무발생일시Utc >= startUtc && item.업무발생일시Utc < endExclusiveUtc)
            .ToArray();
        var occurredLines = occurredEvents
            .SelectMany(item => item.관리계정전기목록)
            .ToArray();

        var cashIn = cashLines
            .Where(line => line.전기방향Code == "DebitCandidate")
            .Sum(line => line.금액);
        var cashOut = cashLines
            .Where(line => line.전기방향Code == "CreditCandidate")
            .Sum(line => line.금액);
        var receivableDelta = SumNormalBalance(
            occurredLines,
            definition => definition.CategoryCode == "AssetCandidate"
                && definition.StableId != 관리계정StableIds.가용현금);
        var payableDelta = SumNormalBalance(
            occurredLines,
            definition => definition.CategoryCode == "LiabilityCandidate");
        var refundDelta = SumNormalBalance(
            occurredLines,
            definition => definition.StableId == 관리계정StableIds.고객환불의무);
        var snapshotHash = BuildSnapshotHash(events.SelectMany(item =>
            item.관리계정전기목록
                .OrderBy(line => line.LineNumber)
                .Select(line => string.Join("|",
                    item.StableId,
                    item.원본Revision.ToString(CultureInfo.InvariantCulture),
                    item.업무발생일시Utc.ToString("O", CultureInfo.InvariantCulture),
                    item.현금이동일시Utc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                    line.LineNumber.ToString(CultureInfo.InvariantCulture),
                    line.관리계정StableId,
                    line.전기방향Code,
                    line.금액.ToString(CultureInfo.InvariantCulture),
                    line.매핑Revision))));

        return new 운영현금흐름요약Dto
        {
            통화Code = currency,
            기간시작일 = from,
            기간종료일 = to,
            현금유입합계 = cashIn,
            현금유출합계 = cashOut,
            순현금변동 = cashIn - cashOut,
            기간미수순변동후보 = receivableDelta,
            기간지급의무순변동후보 = payableDelta,
            기간고객환불의무순변동후보 = refundDelta,
            현금이동사건수 = cashMovementEvents.Length,
            기간재무사건수 = occurredEvents.Length,
            가용현금잔액확정가능여부 = false,
            가용현금잔액제한Code = "OpeningBalanceNotIncluded",
            원장SnapshotHash = snapshotHash,
            운영전표쓰기허용 = false
        };
    }

    public async Task<플랫폼운영경제성원장평가응답Dto> 원장기반경제성평가Async(
        플랫폼운영경제성원장평가요청Dto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.시나리오항목.Any(item => string.Equals(
                item.분류Code,
                플랫폼운영경제성금액분류Codes.총거래액,
                StringComparison.Ordinal)))
        {
            throw new ArgumentException("원장 기반 평가에서 총거래액은 서버 재무 사건에서만 결정합니다.", nameof(request));
        }

        var currency = request.통화Code?.Trim().ToUpperInvariant() ?? string.Empty;
        var startUtc = request.기간시작일.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = request.기간종료일.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var events = await db.재무사건
            .AsNoTracking()
            .Where(item => item.통화Code == currency
                && item.업무발생일시Utc >= startUtc
                && item.업무발생일시Utc < endUtc)
            .OrderBy(item => item.StableId)
            .Select(item => new { item.StableId, item.재무의미Code, item.금액, item.증빙Hash })
            .ToArrayAsync(cancellationToken);
        var snapshotHash = BuildSnapshotHash(events.Select(item =>
            $"{item.StableId}|{item.재무의미Code}|{item.금액.ToString(CultureInfo.InvariantCulture)}|{item.증빙Hash}"));
        var gross = events
            .Where(item => item.재무의미Code == 재무의미Codes.고객결제승인)
            .Sum(item => item.금액);
        var items = request.시나리오항목.ToList();
        items.Add(new 플랫폼운영경제성금액항목Dto
        {
            StableId = $"ledger:gross-transaction:{snapshotHash}",
            분류Code = 플랫폼운영경제성금액분류Codes.총거래액,
            금액 = gross,
            근거Revision = snapshotHash
        });

        var evaluation = economicsCalculator.평가(new 플랫폼운영경제성평가요청Dto
        {
            시나리오StableId = request.시나리오StableId,
            국가Code = request.국가Code,
            관할Code = request.관할Code,
            통화Code = currency,
            기간시작일 = request.기간시작일,
            기간종료일 = request.기간종료일,
            완료주문수 = request.완료주문수,
            기초가용현금 = request.기초가용현금,
            본인대리인가정Code = request.본인대리인가정Code,
            입력Revision = $"financial-ledger:{snapshotHash}",
            항목 = items
        });
        return new 플랫폼운영경제성원장평가응답Dto
        {
            원장SnapshotHash = snapshotHash,
            평가 = evaluation
        };
    }

    private static IQueryable<살뜰.도메인.정산.재무사건> ApplyEventFilters(
        IQueryable<살뜰.도메인.정산.재무사건> query,
        string? profileStableId,
        string? currencyCode,
        DateOnly? from,
        DateOnly? to)
    {
        if (!string.IsNullOrWhiteSpace(profileStableId))
        {
            var normalized = profileStableId.Trim();
            query = query.Where(item => item.재무영향ProfileStableId == normalized);
        }

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            var normalized = currencyCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.통화Code == normalized);
        }

        if (from.HasValue)
        {
            var fromUtc = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.업무발생일시Utc >= fromUtc);
        }

        if (to.HasValue)
        {
            var toExclusive = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.업무발생일시Utc < toExclusive);
        }

        return query;
    }

    private static 운영재무사건Dto ToDto(살뜰.도메인.정산.재무사건 item)
        => new()
        {
            StableId = item.StableId,
            원본Event유형 = item.원본Event유형,
            원본StableId = item.원본StableId,
            원본Revision = item.원본Revision,
            재무영향ProfileStableId = item.재무영향ProfileStableId,
            재무의미Code = item.재무의미Code,
            금액 = item.금액,
            통화Code = item.통화Code,
            업무발생일시Utc = item.업무발생일시Utc,
            현금이동일시Utc = item.현금이동일시Utc,
            지급기일Utc = item.지급기일Utc,
            투영상태Code = item.투영상태Code,
            증빙Hash = item.증빙Hash,
            전기목록 = item.관리계정전기목록
                .OrderBy(line => line.LineNumber)
                .Select(line => new 운영관리계정전기Dto
                {
                    LineNumber = line.LineNumber,
                    관리계정StableId = line.관리계정StableId,
                    관리계정표시명 = 관리계정Catalog.Find(line.관리계정StableId)?.DisplayName ?? line.관리계정StableId,
                    전기방향Code = line.전기방향Code,
                    금액 = line.금액,
                    통화Code = line.통화Code,
                    매핑Revision = line.매핑Revision
                }).ToArray()
        };

    private static decimal SumNormalBalance(
        IEnumerable<살뜰.도메인.정산.관리계정전기> lines,
        Func<관리계정Definition, bool> include)
    {
        decimal total = 0;
        foreach (var group in lines.GroupBy(line => line.관리계정StableId, StringComparer.Ordinal))
        {
            var definition = 관리계정Catalog.Find(group.Key);
            if (definition is null || !include(definition))
            {
                continue;
            }

            var debit = group
                .Where(line => line.전기방향Code == "DebitCandidate")
                .Sum(line => line.금액);
            var credit = group
                .Where(line => line.전기방향Code == "CreditCandidate")
                .Sum(line => line.금액);
            total += definition.NormalBalanceSide == 관리계정정상잔액방향.Debit
                ? debit - credit
                : credit - debit;
        }

        return total;
    }

    private static string BuildSnapshotHash(IEnumerable<string> rows)
    {
        var canonical = string.Join('\n', rows.OrderBy(row => row, StringComparer.Ordinal));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
