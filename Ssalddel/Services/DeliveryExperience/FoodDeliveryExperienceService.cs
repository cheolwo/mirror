using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.도메인.음식;
using 살뜰.도메인.공통;

namespace Ssalddel.Services.DeliveryExperience;

public sealed class FoodDeliveryExperienceOptions
{
    public const string SectionName = "FoodDeliveryExperience";
    public bool Enabled { get; set; }
}

/// <summary>완료 사실의 수집과 현재 관계자에게 제공하는 제한된 집계입니다. 배차/평가에는 사용하지 않습니다.</summary>
public sealed class 음식배달경험Service(SsalddelContext db, IOptions<FoodDeliveryExperienceOptions> options,
    ISsalddelExecutionModePolicy execution, TimeProvider clock, ILogger<음식배달경험Service> logger,
    Ssalddel.Services.PrivacyRetention.I개인정보보존Store? privacy = null)
{
    public bool Enabled => options.Value.Enabled;
    private string Mode => execution.Mode.ToString();

    // 호출자가 완료 확정과 같은 트랜잭션에서 SaveChanges 합니다.
    public async Task 완료근거추가Async(음식주문 order, 음식배달시도 attempt, string metric, CancellationToken ct)
    {
        if (!Enabled || attempt.유산추정여부 || attempt.경험실행모드Code != Mode
            || order.모의처리처수행 || attempt.주문번호 != order.주문번호
            || string.IsNullOrWhiteSpace(attempt.기사Id) || string.IsNullOrWhiteSpace(attempt.시도StableId)) return;
        var completed = metric == "PickupCompleted" ? attempt.픽업완료시각Utc : metric == "DeliveryCompleted" ? attempt.전달완료시각Utc : null;
        if (!completed.HasValue) return;
        var id = Key(Mode, attempt.기사Id, order.주문번호, metric);
        if (db.Set<음식배달경험완료Outbox>().Local.Any(x => x.Id == id)
            || await db.Set<음식배달경험완료Outbox>().AnyAsync(x => x.Id == id, ct)) return;
        var region = metric == "DeliveryCompleted"
            ? await db.Set<음식배달전달지역Snapshot>().AsNoTracking().SingleOrDefaultAsync(x => x.주문번호 == order.주문번호, ct)
            : null;
        db.Set<음식배달경험완료Outbox>().Add(new()
        {
            Id = id, 기사Id = attempt.기사Id, 주문번호 = order.주문번호, 원천시도Id = attempt.시도StableId,
            완료종류Code = metric, 실행모드Code = Mode, CompletedAtUtc = completed.Value,
            지점Id = metric == "PickupCompleted" && order.음식점Id > 0 ? order.음식점Id.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            지점표시명 = metric == "PickupCompleted" ? order.음식점명 : null,
            도로Id = region?.도로Id, 도로표시명 = region?.도로표시명,
            단지Id = region?.단지Id, 단지표시명 = region?.단지표시명,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime
        });
    }

    /// <summary>bounded page, 재실행 시 같은 fact ID. 복수 worker 충돌은 rollback 후 다음 pass에서 재조회합니다.</summary>
    public async Task<int> 투영Async(CancellationToken ct)
    {
        if (!Enabled) return 0;
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("ExperienceProjectionRequiresOwnTransaction");
        var strategy = db.Database.CreateExecutionStrategy();
        var executionAttempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (executionAttempt++ > 0)
                foreach (var entry in db.ChangeTracker.Entries().Where(x => x.Entity is 음식배달경험Fact or 음식배달경험완료Outbox).ToArray())
                    entry.State = EntityState.Detached;
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            var pending = await db.Set<음식배달경험완료Outbox>().Where(x => x.ProjectedAtUtc == null)
                .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Take(100).ToListAsync(ct);
            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var item in pending)
            {
                var places = item.완료종류Code == "PickupCompleted"
                    ? new[] { ("Restaurant", item.지점Id, item.지점표시명) }
                    : new[] { ("Road", item.도로Id, item.도로표시명), ("ApartmentComplex", item.단지Id, item.단지표시명) };
                foreach (var (kind, placeId, name) in places)
                {
                    if (string.IsNullOrWhiteSpace(placeId) || (kind != "Restaurant" && string.IsNullOrWhiteSpace(name))) continue;
                    var id = Key(item.Id, kind);
                    if (!await db.Set<음식배달경험Fact>().AnyAsync(x => x.Id == id, ct))
                        db.Set<음식배달경험Fact>().Add(new()
                        {
                            Id = id, 완료OutboxId = item.Id, 기사Id = item.기사Id, 완료종류Code = item.완료종류Code,
                            장소종류Code = kind, 장소Id = placeId, 장소표시명 = name ?? string.Empty,
                            실행모드Code = item.실행모드Code, ProjectedAtUtc = now
                        });
                }
                item.ProjectedAtUtc = now;
            }
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return pending.Count;
        });
    }

    /// <summary>기존 앱 기록만 점검합니다. 모드/시도/완료 상태이력/파기 장벽을 대조하며 기본은 dry run입니다.</summary>
    public async Task<음식배달경험과거점검결과> 과거근거점검Async(long afterAttemptId = 0, bool apply = false, CancellationToken ct = default)
    {
        if (!Enabled) return new(0, 0, 0, 0, 0, afterAttemptId, false);
        if (!apply) return await 과거근거점검CoreAsync(afterAttemptId, false, ct);
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("ExperienceBackfillRequiresOwnTransaction");
        var strategy = db.Database.CreateExecutionStrategy();
        var executionAttempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (executionAttempt++ > 0)
                foreach (var entry in db.ChangeTracker.Entries<음식배달경험완료Outbox>().ToArray())
                    entry.State = EntityState.Detached;
            return await 과거근거점검CoreAsync(afterAttemptId, true, ct);
        });
    }

    private async Task<음식배달경험과거점검결과> 과거근거점검CoreAsync(long afterAttemptId, bool apply, CancellationToken ct)
    {
        await using var tx = apply && db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        var attempts = await db.음식배달시도.AsNoTracking().Where(x => x.Id > afterAttemptId)
            .OrderBy(x => x.Id).Take(101).ToListAsync(ct);
        var selected = attempts.Take(100).ToArray();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        int candidate = 0, legacy = 0, unknownMode = 0, unclear = 0;
        foreach (var attempt in selected)
        {
            if (attempt.유산추정여부) { legacy++; continue; }
            if (attempt.경험실행모드Code != Mode) { unknownMode++; continue; }
            var order = await db.음식주문.AsNoTracking().Include(x => x.상태이력).SingleOrDefaultAsync(x => x.주문번호 == attempt.주문번호, ct);
            if (order is null || order.모의처리처수행 || string.IsNullOrWhiteSpace(attempt.기사Id)
                || (execution.IsOperational && privacy is null)
                || (privacy is not null && await privacy.HasDeletionAsync(Ssalddel.Contracts.Common.PrivacyRetention.개인정보파기원천Codes.FoodOrder, order.주문번호, ct)))
            { unclear++; continue; }
            foreach (var (metric, state, completed) in new[]
            {
                ("PickupCompleted", 음식주문상태코드.픽업완료, attempt.픽업완료시각Utc),
                ("DeliveryCompleted", 음식주문상태코드.전달완료, attempt.전달완료시각Utc)
            })
            {
                if (!completed.HasValue) continue;
                if (!order.상태이력.Any(x => x.다음상태 == state && x.전이시각Utc == completed.Value)) { unclear++; continue; }
                var key = Key(Mode, attempt.기사Id, order.주문번호, metric);
                if (!seenKeys.Add(key) || db.Set<음식배달경험완료Outbox>().Local.Any(x => x.Id == key)
                    || await db.Set<음식배달경험완료Outbox>().AnyAsync(x => x.Id == key, ct)) continue;
                if (metric == "DeliveryCompleted" && !await db.Set<음식배달전달지역Snapshot>().AnyAsync(x => x.주문번호 == order.주문번호
                    && (x.도로Id != null || x.단지Id != null), ct)) { unclear++; continue; }
                candidate++;
                if (apply) await 완료근거추가Async(order, attempt, metric, ct);
            }
        }
        if (apply)
        {
            await db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        return new(selected.Length, candidate, legacy, unknownMode, unclear,
            selected.LastOrDefault()?.Id ?? afterAttemptId, attempts.Count > 100);
    }

    public IReadOnlyList<DeliveryExperienceSummaryDto> 기사현재업무요약(string driverId, string orderNo)
        => 안전조회(() => 요약(driverId, orderNo, null, null));
    public IReadOnlyList<DeliveryExperienceSummaryDto> 음식점요약(string orderNo, long restaurantId)
        => 안전조회(() => 요약(null, orderNo, restaurantId, null));
    public IReadOnlyList<DeliveryExperienceSummaryDto> 주문자요약(string orderNo, string userId)
        => 안전조회(() => 요약(null, orderNo, null, userId));

    /// <summary>기존 음식 개인정보 파기 트랜잭션에 참여합니다. 주문별 지역 사본을 없애고 고객 상세 없는 비공개 집계 근거만 보존합니다.</summary>
    public async Task 원천지역최소화Async(string orderNo, CancellationToken ct)
    {
        if (!Enabled) return;
        var region = await db.Set<음식배달전달지역Snapshot>().SingleOrDefaultAsync(x => x.주문번호 == orderNo, ct);
        if (region is not null) db.Remove(region);
        foreach (var item in await db.Set<음식배달경험완료Outbox>().Where(x => x.주문번호 == orderNo).ToListAsync(ct))
        {
            item.도로Id = item.도로표시명 = item.단지Id = item.단지표시명 = null;
            // 미처리된 과거 지역은 개인정보 파기 뒤에 새 집계로 복구하지 않습니다.
            item.ProjectedAtUtc ??= clock.GetUtcNow().UtcDateTime;
        }
    }

    private IReadOnlyList<DeliveryExperienceSummaryDto> 안전조회(Func<IReadOnlyList<DeliveryExperienceSummaryDto>> query)
    {
        if (!Enabled) return [];
        try { return query(); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 완료 건수를 0이나 sample로 바꾸지 않습니다. 부가 정보 실패로 수행 버튼을 막지 않습니다.
            logger.LogWarning("배달 경험 부가 조회 실패. ErrorType={ErrorType}", ex.GetType().Name);
            return [];
        }
    }

    private IReadOnlyList<DeliveryExperienceSummaryDto> 요약(string? selfDriverId, string orderNo, long? restaurantId, string? ordererId)
    {
        var order = db.음식주문.AsNoTracking().SingleOrDefault(x => x.주문번호 == orderNo);
        if (order is null || order.모의처리처수행 || 닫힘(order.상태)
            || (restaurantId.HasValue && order.음식점Id != restaurantId)
            || (ordererId is not null && order.주문자UserId != ordererId)) return [];
        var attempt = db.음식배달시도.AsNoTracking().Where(x => x.주문번호 == orderNo)
            .OrderByDescending(x => x.시도순번).FirstOrDefault();
        if (attempt is null || attempt.유산추정여부 || attempt.경험실행모드Code != Mode
            || attempt.중단시각Utc.HasValue || attempt.전달완료시각Utc.HasValue
            || (selfDriverId is not null && attempt.기사Id != selfDriverId)) return [];
        // 원 시도뿐 아니라 현재 배차 확정도 다시 확인합니다.
        if (!db.운송원장.AsNoTracking().Any(x => x.의뢰Id == attempt.제안Id
            && x.배차업무유형 == 상태값.배차업무유형.음식배달 && x.확정기사Id == attempt.기사Id
            && x.배차큐단계 == 상태값.배차큐단계.확정)) return [];
        var consent = selfDriverId is null ? db.Set<음식배달경험공유설정>().AsNoTracking().SingleOrDefault(x => x.기사Id == attempt.기사Id) : null;
        if (selfDriverId is null && consent?.현재관계자공유 != true) return [];
        if (ordererId is not null && !attempt.픽업완료시각Utc.HasValue) return [];
        var summaries = new List<DeliveryExperienceSummaryDto>();
        if (restaurantId.HasValue || (selfDriverId is not null && !attempt.픽업완료시각Utc.HasValue))
        {
            if (order.음식점Id > 0) summaries.Add(지역집계(attempt.기사Id, "PickupCompleted", "Restaurant",
                order.음식점Id.ToString(System.Globalization.CultureInfo.InvariantCulture), order.음식점명));
        }
        else
        {
            var region = db.Set<음식배달전달지역Snapshot>().AsNoTracking().SingleOrDefault(x => x.주문번호 == orderNo);
            if (region?.도로Id is not null && region.도로표시명 is not null)
                summaries.Add(지역집계(attempt.기사Id, "DeliveryCompleted", "Road", region.도로Id, region.도로표시명));
            if (region?.단지Id is not null && region.단지표시명 is not null)
                summaries.Add(지역집계(attempt.기사Id, "DeliveryCompleted", "ApartmentComplex", region.단지Id, region.단지표시명));
        }
        // 철회/재배정/종료가 조회 도중 발생한 경우 늦게 계산된 문구를 보내지 않습니다.
        if (selfDriverId is null && !db.Set<음식배달경험공유설정>().AsNoTracking()
            .Any(x => x.기사Id == attempt.기사Id && x.현재관계자공유 && x.Revision == consent!.Revision)) return [];
        var latest = db.음식배달시도.AsNoTracking().Where(x => x.주문번호 == orderNo).OrderByDescending(x => x.시도순번).FirstOrDefault();
        if (latest?.시도StableId != attempt.시도StableId || latest.Revision != attempt.Revision
            || latest.중단시각Utc.HasValue || latest.전달완료시각Utc.HasValue
            || !db.운송원장.AsNoTracking().Any(x => x.의뢰Id == attempt.제안Id
                && x.배차업무유형 == 상태값.배차업무유형.음식배달 && x.확정기사Id == attempt.기사Id
                && x.배차큐단계 == 상태값.배차큐단계.확정)
            || !db.음식주문.AsNoTracking().Any(x => x.주문번호 == orderNo && x.상태 == order.상태)) return [];
        return summaries;
    }

    private DeliveryExperienceSummaryDto 지역집계(string driverId, string metric, string kind, string placeId, string name)
    {
        var count = db.Set<음식배달경험Fact>().AsNoTracking().LongCount(x => x.기사Id == driverId
            && x.완료종류Code == metric && x.장소종류Code == kind && x.장소Id == placeId && x.실행모드Code == Mode);
        var pending = db.Set<음식배달경험완료Outbox>().AsNoTracking().Any(x => x.기사Id == driverId
            && x.완료종류Code == metric && x.실행모드Code == Mode && x.ProjectedAtUtc == null
            && ((kind == "Restaurant" && x.지점Id == placeId) || (kind == "Road" && x.도로Id == placeId) || (kind == "ApartmentComplex" && x.단지Id == placeId)));
        var partial = db.음식배달시도.AsNoTracking().Any(x => x.기사Id == driverId
            && (x.경험실행모드Code == null || x.유산추정여부
                || (x.경험실행모드Code == Mode && !db.Set<음식배달경험완료Outbox>()
                    .Any(y => y.기사Id == x.기사Id && y.주문번호 == x.주문번호 && y.완료종류Code == metric && y.실행모드Code == Mode)))
            && (metric == "PickupCompleted" ? x.픽업완료시각Utc != null : x.전달완료시각Utc != null));
        return new() { MetricCode = metric, PlaceKind = kind, PlaceDisplayName = kind == "Restaurant" && string.IsNullOrWhiteSpace(name) ? "음식점" : name,
            CompletedCount = pending ? null : count, EvidenceStatusCode = pending ? "Pending" : partial ? "Partial" : "Confirmed",
            AsOfUtc = clock.GetUtcNow().UtcDateTime, ExecutionModeCode = Mode };
    }

    public static string Key(params string[] values)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\u001f", values)))).ToLowerInvariant();
    private static bool 닫힘(string state) => state is 음식주문상태코드.취소 or 음식주문상태코드.거절 or 음식주문상태코드.전달완료 or 음식주문상태코드.수령확인;
}

public sealed record 음식배달경험과거점검결과(int Scanned, int WouldAppend, int SkippedLegacy,
    int SkippedUnknownMode, int SkippedUnclearEvidence, long NextAttemptId, bool HasMore);

public sealed class 음식배달경험Worker(IServiceScopeFactory scopes, ILogger<음식배달경험Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<음식배달경험Service>().투영Async(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("배달 경험 투영 재처리 대기. ErrorType={ErrorType}", ex.GetType().Name); }
        }
    }
}
