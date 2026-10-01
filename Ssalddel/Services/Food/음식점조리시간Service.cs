using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.도메인.음식;

namespace Ssalddel.Services.Food;

public sealed record 음식조리시간결정(
    int 참고조리분,
    int? 음식점선택조리분,
    int 적용조리분,
    string 결정출처Code,
    int 관측표본수);

public interface I음식점조리시간Service
{
    Task<음식점조리시간설정응답> 설정조회Async(long 음식점Id, CancellationToken cancellationToken = default);
    Task<음식점조리시간설정응답> 설정교체Async(long 음식점Id, string 변경UserId, 음식점조리시간설정변경요청 request, CancellationToken cancellationToken = default);
    Task<음식점조리참고응답?> 주문참고조회Async(string 주문번호, long 음식점Id, DateTime 기준시각Utc, CancellationToken cancellationToken = default);
    Task<음식조리시간결정?> 주문수락값결정Async(string 주문번호, long 음식점Id, int? 명시선택분, bool 즉시픽업, DateTime 기준시각Utc, CancellationToken cancellationToken = default);
    Task 주문결정기록Async(string 주문번호, 음식조리시간결정 decision, CancellationToken cancellationToken = default);
}

public sealed class 음식점조리시간Service(SsalddelContext db) : I음식점조리시간Service
{
    private const int 관측기간일수 = 28;
    private const int 최소관측표본수 = 3;
    private const int 기본조리분 = 20;

    public async Task<음식점조리시간설정응답> 설정조회Async(
        long 음식점Id,
        CancellationToken cancellationToken = default)
    {
        var revision = await db.음식점조리시간설정
            .Where(x => x.음식점Id == 음식점Id)
            .Select(x => (long?)x.설정Revision)
            .MaxAsync(cancellationToken) ?? 0;
        var items = revision == 0
            ? []
            : await db.음식점조리시간설정
                .AsNoTracking()
                .Where(x => x.음식점Id == 음식점Id && x.설정Revision == revision)
                .OrderBy(x => x.메뉴Id)
                .ThenBy(x => x.시작분)
                .Select(x => new 음식점조리시간설정항목Dto
                {
                    메뉴Id = x.메뉴Id,
                    시작분 = x.시작분,
                    종료분 = x.종료분,
                    조리예상분 = x.조리예상분
                })
                .ToArrayAsync(cancellationToken);
        var changedAt = revision == 0
            ? null
            : await db.음식점조리시간설정
                .Where(x => x.음식점Id == 음식점Id && x.설정Revision == revision)
                .Select(x => (DateTime?)x.UpdatedAtUtc)
                .MaxAsync(cancellationToken);

        return new 음식점조리시간설정응답
        {
            음식점Id = 음식점Id,
            Revision = revision,
            항목 = items,
            최근변경시각Utc = changedAt
        };
    }

    public async Task<음식점조리시간설정응답> 설정교체Async(
        long 음식점Id,
        string 변경UserId,
        음식점조리시간설정변경요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (음식점Id <= 0 || string.IsNullOrWhiteSpace(변경UserId) || request.클라이언트요청Id == Guid.Empty)
        {
            throw new ArgumentException("음식점, 변경 주체와 클라이언트 요청 ID가 필요합니다.");
        }

        var duplicate = await db.음식점조리시간설정
            .AnyAsync(x => x.음식점Id == 음식점Id && x.변경요청Id == request.클라이언트요청Id, cancellationToken);
        if (duplicate)
        {
            return await 설정조회Async(음식점Id, cancellationToken);
        }

        var currentRevision = await db.음식점조리시간설정
            .Where(x => x.음식점Id == 음식점Id)
            .Select(x => (long?)x.설정Revision)
            .MaxAsync(cancellationToken) ?? 0;
        if (request.예상Revision.HasValue && request.예상Revision.Value != currentRevision)
        {
            throw new DbUpdateConcurrencyException("음식점 조리시간 설정이 다른 요청에서 먼저 변경됐습니다.");
        }

        var items = (request.항목 ?? []).ToArray();
        검증(items);
        var menuIds = items.Where(x => x.메뉴Id.HasValue).Select(x => x.메뉴Id!.Value).Distinct().ToArray();
        if (menuIds.Length > 0)
        {
            var ownedCount = await db.음식점메뉴.CountAsync(
                x => x.음식점공개프로필Id == 음식점Id && menuIds.Contains(x.Id),
                cancellationToken);
            if (ownedCount != menuIds.Length)
            {
                throw new ArgumentException("해당 음식점의 메뉴만 조리시간을 설정할 수 있습니다.");
            }
        }

        var now = DateTime.UtcNow;
        var nextRevision = currentRevision + 1;
        db.음식점조리시간설정.AddRange(items.Select(x => new 음식점조리시간설정
        {
            음식점Id = 음식점Id,
            메뉴Id = x.메뉴Id,
            시작분 = x.시작분,
            종료분 = x.종료분,
            조리예상분 = x.조리예상분,
            설정Revision = nextRevision,
            변경요청Id = request.클라이언트요청Id,
            변경UserId = 변경UserId.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await 설정조회Async(음식점Id, cancellationToken);
    }

    public async Task<음식점조리참고응답?> 주문참고조회Async(
        string 주문번호,
        long 음식점Id,
        DateTime 기준시각Utc,
        CancellationToken cancellationToken = default)
    {
        var decision = await 주문수락값결정Async(주문번호, 음식점Id, null, false, 기준시각Utc, cancellationToken);
        return decision is null ? null : new 음식점조리참고응답
        {
            주문번호 = 주문번호,
            참고조리분 = decision.참고조리분,
            참고결정출처Code = decision.결정출처Code,
            관측표본수 = decision.관측표본수
        };
    }

    public async Task<음식조리시간결정?> 주문수락값결정Async(
        string 주문번호,
        long 음식점Id,
        int? 명시선택분,
        bool 즉시픽업,
        DateTime 기준시각Utc,
        CancellationToken cancellationToken = default)
    {
        var order = await db.음식주문
            .AsNoTracking()
            .Include(x => x.상품목록)
            .SingleOrDefaultAsync(x => x.주문번호 == 주문번호 && x.음식점Id == 음식점Id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        if (즉시픽업)
        {
            return new 음식조리시간결정(0, 0, 0, 음식조리시간결정출처Code.즉시픽업, 0);
        }

        var reference = await 참고값계산Async(order, 기준시각Utc, cancellationToken);
        if (명시선택분.HasValue)
        {
            var selected = 음식점조리시간정책.Clamp(명시선택분.Value);
            return new 음식조리시간결정(reference.Minutes, selected, selected, 음식조리시간결정출처Code.음식점명시선택, reference.SampleCount);
        }

        return new 음식조리시간결정(reference.Minutes, null, reference.Minutes, reference.SourceCode, reference.SampleCount);
    }

    public async Task 주문결정기록Async(
        string 주문번호,
        음식조리시간결정 decision,
        CancellationToken cancellationToken = default)
    {
        var order = await db.음식주문.SingleOrDefaultAsync(x => x.주문번호 == 주문번호, cancellationToken);
        if (order is null) return;
        order.플랫폼참고조리분 = decision.참고조리분;
        order.음식점선택조리분 = decision.음식점선택조리분;
        order.적용조리분 = decision.적용조리분;
        order.조리시간결정출처Code = decision.결정출처Code;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<(int Minutes, string SourceCode, int SampleCount)> 참고값계산Async(
        음식주문 order,
        DateTime 기준시각Utc,
        CancellationToken cancellationToken)
    {
        var utc = DateTime.SpecifyKind(기준시각Utc, DateTimeKind.Utc);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, 대한민국시간대());
        var minute = local.Hour * 60 + local.Minute;
        var currentSettings = await 현재설정Async(order.음식점Id, cancellationToken);
        var activeRanges = currentSettings.Where(x => 포함(x, minute)).ToArray();
        var menuIds = order.상품목록.Where(x => x.메뉴Id.HasValue).Select(x => x.메뉴Id!.Value).Distinct().ToArray();
        var productRanges = activeRanges
            .Where(x => x.메뉴Id.HasValue && menuIds.Contains(x.메뉴Id.Value))
            .ToArray();
        var referenceRanges = productRanges.Length > 0
            ? productRanges
            : activeRanges.Where(x => !x.메뉴Id.HasValue).ToArray();
        var rangeStart = referenceRanges.Select(x => (int?)x.시작분).Max() ?? local.Hour * 60;
        var rangeEnd = referenceRanges.Select(x => (int?)x.종료분).Min() ?? Math.Min(1440, rangeStart + 60);
        var cutoff = utc.AddDays(-관측기간일수);
        var samples = await db.음식주문
            .AsNoTracking()
            .Include(x => x.상태이력)
            .Where(x => x.음식점Id == order.음식점Id
                        && x.음식점수락시각Utc >= cutoff
                        && x.음식점수락시각Utc < utc)
            .OrderByDescending(x => x.음식점수락시각Utc)
            .Take(500)
            .ToArrayAsync(cancellationToken);
        var durations = samples.Select(sample =>
            {
                var accepted = sample.상태이력
                    .Where(x => x.사유 == "음식점 조리 시작")
                    .Select(x => (DateTime?)x.전이시각Utc).Min()
                    ?? (sample.상태이력.Any(x => x.다음상태 == 음식주문상태코드.주문확인)
                        ? (DateTime?)null : sample.음식점수락시각Utc);
                if (!accepted.HasValue) return (double?)null;
                var acceptedLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(accepted.Value, DateTimeKind.Utc), 대한민국시간대());
                var acceptedMinute = acceptedLocal.Hour * 60 + acceptedLocal.Minute;
                if (acceptedMinute < rangeStart || acceptedMinute >= rangeEnd) return (double?)null;
                var ready = sample.상태이력
                    .Where(x => x.사유.StartsWith("음식점 픽업 준비 완료", StringComparison.Ordinal)
                                || x.다음상태 == 음식주문상태코드.픽업대기)
                    .OrderBy(x => x.전이시각Utc)
                    .Select(x => (DateTime?)x.전이시각Utc)
                    .FirstOrDefault();
                if (!ready.HasValue) return null;
                var elapsed = (ready.Value - accepted.Value).TotalMinutes;
                return elapsed is >= 1 and <= 180 ? elapsed : null;
            })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToArray();
        if (durations.Length >= 최소관측표본수)
        {
            return (음식점조리시간정책.Clamp((int)Math.Round(durations.Average(), MidpointRounding.AwayFromZero)), 음식조리시간결정출처Code.플랫폼관측평균, durations.Length);
        }

        var productValues = activeRanges.Where(x => x.메뉴Id.HasValue && menuIds.Contains(x.메뉴Id.Value)).Select(x => x.조리예상분).ToArray();
        var configured = productValues.Length > 0
            ? productValues.Max()
            : activeRanges.Where(x => !x.메뉴Id.HasValue).Select(x => (int?)x.조리예상분).Max();
        return configured.HasValue
            ? (configured.Value, 음식조리시간결정출처Code.음식점설정, durations.Length)
            : (기본조리분, 음식조리시간결정출처Code.시스템기본, durations.Length);
    }

    private async Task<IReadOnlyList<음식점조리시간설정>> 현재설정Async(long 음식점Id, CancellationToken cancellationToken)
    {
        var revision = await db.음식점조리시간설정.Where(x => x.음식점Id == 음식점Id).Select(x => (long?)x.설정Revision).MaxAsync(cancellationToken) ?? 0;
        return revision == 0 ? [] : await db.음식점조리시간설정.AsNoTracking().Where(x => x.음식점Id == 음식점Id && x.설정Revision == revision).ToArrayAsync(cancellationToken);
    }

    private static bool 포함(음식점조리시간설정 item, int minute)
        => item.시작분 <= minute && minute < item.종료분;

    private static void 검증(IReadOnlyList<음식점조리시간설정항목Dto> items)
    {
        if (items.Count == 0)
            throw new ArgumentException("조리시간 설정 항목이 하나 이상 필요합니다.");

        foreach (var item in items)
        {
            if (item.시작분 is < 0 or >= 1440 || item.종료분 is <= 0 or > 1440 || item.시작분 >= item.종료분)
                throw new ArgumentException("조리시간 구간은 같은 날의 0~1440분 사이에서 시작이 종료보다 빨라야 합니다.");
            if (item.조리예상분 is < 1 or > 180)
                throw new ArgumentException("조리 예상 시간은 1~180분이어야 합니다.");
        }

        foreach (var group in items.GroupBy(x => x.메뉴Id))
        {
            var ordered = group.OrderBy(x => x.시작분).ThenBy(x => x.종료분).ToArray();
            for (var index = 1; index < ordered.Length; index++)
            {
                if (ordered[index].시작분 < ordered[index - 1].종료분)
                    throw new ArgumentException("같은 메뉴 범위의 조리시간 구간은 겹칠 수 없습니다.");
            }
        }
    }

    private static TimeZoneInfo 대한민국시간대()
    {
        foreach (var id in new[] { "Asia/Seoul", "Korea Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        throw new TimeZoneNotFoundException("대한민국 운영 시간대를 찾지 못했습니다.");
    }
}
