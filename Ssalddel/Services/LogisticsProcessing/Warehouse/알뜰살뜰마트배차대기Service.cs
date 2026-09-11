using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;
using 살뜰.도메인.운영;
using 살뜰.Services.Dispatch.Engine;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public interface I알뜰살뜰마트배차대기Service
{
    Task<IReadOnlyList<알뜰살뜰마트배차대기생성결과>> 입고상품포장완료반영Async(
        long 입고상품Id,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task<알뜰살뜰마트배차대기생성결과> 주문포장완료후배차대기생성Async(
        string 주문참조번호,
        string updatedBy,
        CancellationToken cancellationToken = default);
}

public sealed class 알뜰살뜰마트배차대기Service : I알뜰살뜰마트배차대기Service
{
    private readonly SsalddelContext _db;
    private readonly I살뜰마트라스트마일배차인계Service _lastMileHandoffService;
    private readonly I운송원장Mongo동기화Service _transportLedgerSync;
    private readonly I음식마트원장동기화OutboxService _foodMartLedgerOutbox;
    private readonly ILogger<알뜰살뜰마트배차대기Service> _logger;
    private readonly TimeProvider _timeProvider;

    public 알뜰살뜰마트배차대기Service(
        SsalddelContext db,
        I살뜰마트라스트마일배차인계Service lastMileHandoffService,
        I운송원장Mongo동기화Service transportLedgerSync,
        I음식마트원장동기화OutboxService foodMartLedgerOutbox,
        ILogger<알뜰살뜰마트배차대기Service> logger,
        TimeProvider timeProvider)
    {
        _db = db;
        _lastMileHandoffService = lastMileHandoffService;
        _transportLedgerSync = transportLedgerSync;
        _foodMartLedgerOutbox = foodMartLedgerOutbox;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<알뜰살뜰마트배차대기생성결과>> 입고상품포장완료반영Async(
        long 입고상품Id,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var 포장작업목록 = await _db.피킹포장작업
            .Where(x => x.입고상품Id == 입고상품Id && x.작업유형 == 피킹포장작업유형.포장)
            .ToListAsync(cancellationToken);

        foreach (var 작업 in 포장작업목록)
        {
            작업.상태 = 피킹포장작업상태.완료;
            작업.시작일시Utc ??= now;
            작업.완료일시Utc = now;
            작업.UpdatedAt = now;
        }

        var 주문참조번호목록 = 포장작업목록
            .Select(x => x.주문참조번호)
            .Concat(await _db.출고예정
                .AsNoTracking()
                .Where(x => x.입고상품Id == 입고상품Id)
                .Select(x => x.주문참조번호)
                .ToListAsync(cancellationToken))
            .Select(Clean)
            .Where(x => x is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (포장작업목록.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var results = new List<알뜰살뜰마트배차대기생성결과>();
        foreach (var 주문참조번호 in 주문참조번호목록)
        {
            results.Add(await 주문포장완료후배차대기생성Async(주문참조번호, updatedBy, cancellationToken));
        }

        return results;
    }

    public async Task<알뜰살뜰마트배차대기생성결과> 주문포장완료후배차대기생성Async(
        string 주문참조번호,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var orderRef = Clean(주문참조번호);
        if (orderRef is null)
        {
            return 알뜰살뜰마트배차대기생성결과.보류(
                string.Empty,
                알뜰살뜰마트배차대기결과코드.주문참조번호없음,
                "주문참조번호가 없어 마트 배차대기를 만들지 않았습니다.");
        }

        var 출고목록 = await _db.출고예정
            .Where(x => x.주문참조번호 == orderRef)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (출고목록.Count == 0)
        {
            return 알뜰살뜰마트배차대기생성결과.보류(
                orderRef,
                알뜰살뜰마트배차대기결과코드.출고예정없음,
                "마트 주문에 연결된 출고예정이 없어 배차대기를 만들지 않았습니다.");
        }

        var 포장준비상태 = await 포장준비상태조회Async(orderRef, 출고목록, cancellationToken);
        if (!포장준비상태.완료)
        {
            return 알뜰살뜰마트배차대기생성결과.보류(
                orderRef,
                알뜰살뜰마트배차대기결과코드.포장대기,
                "포장 완료 전이라 음식 배달 배차대기를 만들지 않았습니다.");
        }

        var targetResult = await BuildDispatchTargetAsync(orderRef, 출고목록, cancellationToken);
        if (targetResult.Target is null)
        {
            return 알뜰살뜰마트배차대기생성결과.보류(
                orderRef,
                targetResult.결과코드,
                targetResult.메시지,
                포장완료: true);
        }

        var target = targetResult.Target;
        var existingQueue = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                     && x.원본의뢰Id == orderRef,
                cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingQueue?.확정기사Id))
        {
            return 알뜰살뜰마트배차대기생성결과.생성됨(
                orderRef,
                existingQueue.Id,
                existingQueue.의뢰Id,
                "이미 기사가 수락한 마트 라스트마일 배차를 유지했습니다.",
                전략Code: 살뜰마트라스트마일배차전략Codes.수락배정유지);
        }

        var pendingOrderCount = await _db.운송원장
            .AsNoTracking()
            .CountAsync(
                x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                     && x.확정기사Id == null,
                cancellationToken);
        var samePickupReadyCount = await _db.운송원장
            .AsNoTracking()
            .CountAsync(
                x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                     && x.확정기사Id == null
                     && x.픽업_도로명주소 == target.상차주소,
                cancellationToken);
        var orderConfirmedAtUtc = 출고목록.Min(x => AsUtc(x.CreatedAt));
        var readyAtUtc = 포장준비상태.완료시각Utc ?? orderConfirmedAtUtc;
        var evaluatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var handoffResult = await _lastMileHandoffService.인계Async(
            new 살뜰마트라스트마일배차인계요청(
                orderRef,
                orderConfirmedAtUtc.Ticks,
                출고목록.Count,
                출고목록.Sum(x => x.수량),
                target,
                new 살뜰마트라스트마일배차입력(
                    orderConfirmedAtUtc,
                    evaluatedAtUtc,
                    살뜰마트라스트마일준비단계Codes.픽업준비완료,
                    readyAtUtc,
                    pendingOrderCount,
                    배차가능기사수: null,
                    함께묶을준비주문수: samePickupReadyCount + 1,
                    수락된기사배정있음: false)),
            cancellationToken);
        var queue = handoffResult.배차대기;
        if (queue is null)
        {
            return 알뜰살뜰마트배차대기생성결과.보류(
                orderRef,
                알뜰살뜰마트배차대기결과코드.기사제안대기,
                "라스트마일 인계 후보는 등록했지만 현재 전략은 기사 제안을 기다립니다.",
                포장완료: true,
                전략Code: handoffResult.정책판정.전략Code,
                인계StableId: handoffResult.인계.인계StableId);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        foreach (var 출고 in 출고목록)
        {
            출고.운송의뢰Id = queue.의뢰Id;
            if (출고.상태 != 출고상태.출고완료)
            {
                출고.상태 = 출고상태.준비중;
            }

            출고.UpdatedAt = now;
        }

        var 마트주문 = await _db.마트주문
            .Include(x => x.상품목록)
            .FirstOrDefaultAsync(x => x.주문참조번호 == orderRef, cancellationToken);

        if (마트주문 is not null)
        {
            마트주문.상태 = "포장 완료";
            마트주문.현재단계 = "배차대기";
            마트주문.UpdatedAt = now;

            foreach (var 상품 in 마트주문.상품목록)
            {
                상품.상태 = "포장 완료";
                상품.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _transportLedgerSync.운송실행투영동기화Async(queue, updatedBy, cancellationToken);
        await 출고원장동기화Async(출고목록, updatedBy, cancellationToken);

        return 알뜰살뜰마트배차대기생성결과.생성됨(
            orderRef,
            queue.Id,
            queue.의뢰Id,
            "포장 완료 뒤 마트 라스트마일 자식 업무를 음식배달 OS에 인계하고 배차대기를 생성하거나 조회했습니다.",
            handoffResult.정책판정.전략Code,
            handoffResult.인계.인계StableId);
    }

    private async Task<마트포장준비상태> 포장준비상태조회Async(
        string 주문참조번호,
        IReadOnlyList<출고예정> 출고목록,
        CancellationToken cancellationToken)
    {
        var 포장작업목록 = await _db.피킹포장작업
            .AsNoTracking()
            .Where(x => x.주문참조번호 == 주문참조번호 && x.작업유형 == 피킹포장작업유형.포장)
            .ToListAsync(cancellationToken);

        if (포장작업목록.Count > 0)
        {
            var completed = 포장작업목록.All(x => x.상태 == 피킹포장작업상태.완료);
            return new 마트포장준비상태(
                completed,
                completed
                    ? 포장작업목록.Max(x => AsUtc(x.완료일시Utc ?? x.UpdatedAt))
                    : null);
        }

        var 출고묶음목록 = await _db.출고묶음
            .AsNoTracking()
            .Where(x => x.주문참조번호 == 주문참조번호)
            .ToListAsync(cancellationToken);

        if (출고묶음목록.Count > 0)
        {
            var completed = 출고묶음목록.All(x => x.포장완료일시.HasValue || x.상태 == 출고상태.출고완료);
            return new 마트포장준비상태(
                completed,
                completed
                    ? 출고묶음목록.Max(x => AsUtc(x.포장완료일시 ?? x.UpdatedAt))
                    : null);
        }

        var 입고상품Ids = 출고목록
            .Select(x => x.입고상품Id)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        if (입고상품Ids.Length > 0)
        {
            var 포장된입고상품수 = await _db.입고상품
                .AsNoTracking()
                .Where(x => 입고상품Ids.Contains(x.Id) && x.상태.StartsWith("포장완료"))
                .CountAsync(cancellationToken);

            var completed = 포장된입고상품수 == 입고상품Ids.Length;
            return new 마트포장준비상태(
                completed,
                completed ? 출고목록.Max(x => AsUtc(x.CreatedAt)) : null);
        }

        var outboundsCompleted = 출고목록.All(x => x.상태 == 출고상태.출고완료);
        return new 마트포장준비상태(
            outboundsCompleted,
            outboundsCompleted ? 출고목록.Max(x => AsUtc(x.CreatedAt)) : null);
    }

    private async Task<마트배차대상생성결과> BuildDispatchTargetAsync(
        string 주문참조번호,
        IReadOnlyList<출고예정> 출고목록,
        CancellationToken cancellationToken)
    {
        var first = 출고목록[0];
        var warehouse = await _db.창고
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == first.출고창고Id, cancellationToken);
        var pickupAddress = Clean(warehouse?.주소);
        if (pickupAddress is null)
        {
            return 마트배차대상생성결과.보류(
                알뜰살뜰마트배차대기결과코드.상차주소없음,
                "포장은 완료됐지만 출고 창고의 실제 상차 주소가 없어 배차대기를 만들지 않았습니다.");
        }

        var ordererProfile = await _db.주문자프로필
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == first.주문자UserId, cancellationToken);
        var deliveryAddress = Clean(ordererProfile?.기본주소);
        if (deliveryAddress is null)
        {
            return 마트배차대상생성결과.보류(
                알뜰살뜰마트배차대기결과코드.배송목적지없음,
                "포장은 완료됐지만 주문자의 확인된 배송 목적지가 없어 배차대기를 만들지 않았습니다.");
        }

        return 마트배차대상생성결과.생성(
            new 출고예정운송대상
            {
                원천유형 = 출고예정운송대상원천유형.살뜰마트주문,
                원천참조번호 = 주문참조번호,
                운송의뢰Id = 주문참조번호,
                표시명 = string.Join(", ", 출고목록.Select(x => $"{x.상품명} {x.수량}")),
                출고예정Id = 출고목록.Count == 1 ? first.Id : null,
                판매자UserId = first.판매자UserId,
                주문자UserId = first.주문자UserId,
                상차주소 = pickupAddress,
                상차위도 = warehouse?.위도,
                상차경도 = warehouse?.경도,
                하차주소 = deliveryAddress,
                하차위도 = null,
                하차경도 = null,
                온도조건 = ResolveTemperatureBand(출고목록),
                파손주의 = false,
                Lines = 출고목록.Select(x => new 출고예정운송대상라인
                {
                    LineKey = x.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    InboundProductId = x.입고상품Id,
                    SalesProductId = x.판매상품Id,
                    Sku = x.SKU,
                    ProductName = x.상품명,
                    Quantity = x.수량
                }).ToArray()
            });
    }

    private async Task 출고원장동기화Async(
        IReadOnlyList<출고예정> 출고목록,
        string updatedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            var 입고요청Ids = 출고목록
                .Select(x => x.입고요청Id)
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToArray();
            IReadOnlyList<입고요청> 입고목록 = 입고요청Ids.Length == 0
                ? []
                : await _db.입고요청
                    .AsNoTracking()
                    .Where(x => 입고요청Ids.Contains(x.Id))
                    .ToListAsync(cancellationToken);

            var orderRef = 출고목록
                .Select(x => x.주문참조번호)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                ?? 출고목록[0].Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await _foodMartLedgerOutbox.출고원장예약후즉시처리Async(
                출고목록,
                입고목록,
                updatedBy,
                $"mart-dispatch:{orderRef}:{출고목록.Max(x => x.UpdatedAt).Ticks}",
                currentStageKey: "배차대기",
                ledgerTemplateKey: CommunityLedgerTemplateKeys.SsalddelMart,
                cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            var orderRef = 출고목록.Select(x => x.주문참조번호).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            _logger.LogWarning(ex, "알뜰살뜰 마트 포장 완료 후 출고 원장 동기화에 실패했습니다. 주문참조번호={주문참조번호}", orderRef);
        }
    }

    private static string ResolveTemperatureBand(IReadOnlyList<출고예정> 출고목록)
    {
        var joined = string.Join(" ", 출고목록.Select(x => $"{x.상품명} {x.SKU}"));
        if (ContainsAny(joined, "냉동", "frozen"))
        {
            return "냉동";
        }

        if (ContainsAny(joined, "냉장", "chilled", "fresh"))
        {
            return "냉장";
        }

        return "상온";
    }

    private static bool ContainsAny(string? source, params string[] terms)
        => !string.IsNullOrWhiteSpace(source)
           && terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

public sealed record 알뜰살뜰마트배차대기생성결과(
    bool 생성또는조회됨,
    bool 포장완료,
    string 결과코드,
    string 메시지,
    string 주문참조번호,
    long? 배차대기Id = null,
    string? 의뢰Id = null,
    string 전략Code = "",
    string 인계StableId = "")
{
    public static 알뜰살뜰마트배차대기생성결과 생성됨(
        string 주문참조번호,
        long 배차대기Id,
        string 의뢰Id,
        string 메시지,
        string 전략Code = "",
        string 인계StableId = "")
        => new(
            true,
            true,
            알뜰살뜰마트배차대기결과코드.생성또는조회됨,
            메시지,
            주문참조번호,
            배차대기Id,
            의뢰Id,
            전략Code,
            인계StableId);

    public static 알뜰살뜰마트배차대기생성결과 보류(
        string 주문참조번호,
        string 결과코드,
        string 메시지,
        bool 포장완료 = false,
        string 전략Code = "",
        string 인계StableId = "")
        => new(false, 포장완료, 결과코드, 메시지, 주문참조번호, 전략Code: 전략Code, 인계StableId: 인계StableId);
}

public static class 알뜰살뜰마트배차대기결과코드
{
    public const string 생성또는조회됨 = "생성또는조회됨";
    public const string 주문참조번호없음 = "주문참조번호없음";
    public const string 출고예정없음 = "출고예정없음";
    public const string 포장대기 = "포장대기";
    public const string 기사제안대기 = "기사제안대기";
    public const string 상차주소없음 = "상차주소없음";
    public const string 배송목적지없음 = "배송목적지없음";
}

internal sealed record 마트포장준비상태(bool 완료, DateTime? 완료시각Utc);

internal sealed record 마트배차대상생성결과(
    출고예정운송대상? Target,
    string 결과코드,
    string 메시지)
{
    public static 마트배차대상생성결과 생성(출고예정운송대상 target)
        => new(target, 알뜰살뜰마트배차대기결과코드.생성또는조회됨, string.Empty);

    public static 마트배차대상생성결과 보류(string 결과코드, string 메시지)
        => new(null, 결과코드, 메시지);
}
