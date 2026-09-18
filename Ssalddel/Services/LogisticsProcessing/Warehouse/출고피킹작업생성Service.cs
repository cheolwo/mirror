using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.LogisticsProcessing.Warehouse;

public sealed class 출고피킹작업생성Options
{
    public const string SectionName = "WarehouseOutboundPickingPacking";

    public bool Enabled { get; set; }

    public int BatchSize { get; set; } = 50;

    public int IntervalSeconds { get; set; } = 10;
}

public sealed record 출고피킹작업생성결과(
    int 확인출고수,
    int 작업생성출고수,
    int 생성작업수,
    int 작업자배정대기수);

public interface I출고피킹작업생성Service
{
    Task<출고피킹작업생성결과> 대기출고처리Async(
        int take = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 출고예정 원장을 재처리 가능한 작업 대기열로 사용해 피킹·포장 작업을 만든다.
/// 작업자가 없거나 배치 계획이 불완전하면 가짜 작업을 저장하지 않고 다음 주기에 다시 판정한다.
/// </summary>
[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalLogisticsOs,
    SsalddelCodeLayer.Application,
    "출고예정 원장을 실제 창고 담당자의 피킹·포장 작업으로 멱등 투영한다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 25,
    StepKey = "application.warehouse-outbound-picking-packing",
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "작업자가 없거나 배치가 불완전하면 작업을 저장하지 않는다. 출고·재고·운송 상태는 변경하지 않는다.")]
public sealed class 출고피킹작업생성Service(
    SsalddelContext db,
    I피킹배치Engine engine,
    I피킹포장작업투영Service projector,
    ILogger<출고피킹작업생성Service> logger) : I출고피킹작업생성Service
{
    private const string 출고역할 = "출고";
    private const string 피킹역할 = "피킹";
    private const string 포장역할 = "포장";

    public async Task<출고피킹작업생성결과> 대기출고처리Async(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        var boundedTake = Math.Clamp(take, 1, 200);
        var 대기출고 = await (
                from outbound in db.출고예정.AsNoTracking()
                join inbound in db.입고상품.AsNoTracking()
                    on outbound.입고상품Id equals (long?)inbound.Id
                join warehouse in db.창고.AsNoTracking()
                    on outbound.출고창고Id equals warehouse.Id
                where outbound.상태 == 출고상태.예정
                      && warehouse.IsActive
                      && !db.피킹포장작업.Any(task => task.출고예정Id == outbound.Id)
                orderby outbound.CreatedAt, outbound.Id
                select new 대기출고행(
                    outbound.Id,
                    outbound.주문참조번호,
                    outbound.커뮤니티원장Id,
                    outbound.입고상품Id!.Value,
                    outbound.출고창고Id,
                    warehouse.창고명,
                    outbound.SKU,
                    outbound.상품명,
                    outbound.수량,
                    inbound.SKU,
                    inbound.보관위치))
            .Take(boundedTake)
            .ToArrayAsync(cancellationToken);

        if (대기출고.Length == 0)
        {
            return new 출고피킹작업생성결과(0, 0, 0, 0);
        }

        var 창고Ids = 대기출고.Select(row => row.창고Id).Distinct().ToList();
        var 창고사용자 = await db.창고사용자.AsNoTracking()
            .Where(user => 창고Ids.Contains(user.창고Id))
            .OrderByDescending(user => user.IsPrimary)
            .ThenBy(user => user.Id)
            .ToArrayAsync(cancellationToken);

        var 작업자Ids = 창고사용자.Select(user => user.UserId)
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var 기존부하행 = await db.피킹포장작업.AsNoTracking()
            .Where(task => 창고Ids.Contains(task.창고Id)
                           && 작업자Ids.Contains(task.작업자UserId)
                           && task.상태 != 피킹포장작업상태.완료
                           && task.상태 != 피킹포장작업상태.취소)
            .GroupBy(task => new { task.창고Id, task.작업자UserId })
            .Select(group => new
            {
                group.Key.창고Id,
                group.Key.작업자UserId,
                작업수 = group.Count(),
                수량 = group.Sum(task => task.수량)
            })
            .ToArrayAsync(cancellationToken);
        var 기존부하 = 기존부하행.ToDictionary(
            row => 작업자Key(row.창고Id, row.작업자UserId),
            row => new 작업부하(row.작업수, row.수량),
            StringComparer.OrdinalIgnoreCase);

        var 작업생성출고수 = 0;
        var 생성작업수 = 0;
        var 작업자배정대기수 = 0;

        foreach (var row in 대기출고)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidates = 작업자후보생성(row, 창고사용자, 기존부하);
            var mode = 처리방식결정(candidates);
            var plan = await engine.계획Async(new 피킹배치계획요청
            {
                출고참조번호 = $"OUTBOUND-{row.출고예정Id}",
                대상창고Id = row.창고Id,
                기본처리방식 = mode,
                창고옵션목록 =
                [
                    new 피킹배치창고옵션
                    {
                        WarehouseId = row.창고Id,
                        WarehouseName = row.창고명,
                        기본처리방식 = mode,
                        상품바코드검증필수 = true,
                        운영메모 = "출고예정 원장에서 자동 생성된 피킹·포장 작업"
                    }
                ],
                출고라인목록 =
                [
                    new 피킹배치출고라인
                    {
                        출고예정Id = row.출고예정Id,
                        출고작업Key = $"OUTBOUND-{row.출고예정Id}",
                        LineKey = $"outbound:{row.출고예정Id}",
                        InboundProductId = row.입고상품Id,
                        WarehouseId = row.창고Id,
                        WarehouseName = row.창고명,
                        Sku = row.SKU,
                        ProductName = row.상품명,
                        상품바코드 = row.SKU,
                        적재상품바코드 = row.입고SKU,
                        Quantity = row.수량,
                        적재대코드 = NullIfWhiteSpace(row.보관위치),
                        보관위치코드 = NullIfWhiteSpace(row.보관위치),
                        처리방식 = mode
                    }
                ],
                작업자후보목록 = candidates
            }, cancellationToken);

            if (!plan.IsComplete)
            {
                작업자배정대기수++;
                logger.LogDebug(
                    "출고 피킹 작업 생성을 보류했습니다. OutboundPlanId={OutboundPlanId} Message={Message}",
                    row.출고예정Id,
                    plan.Message);
                continue;
            }

            await projector.투영Async(
                row.주문참조번호,
                plan,
                row.커뮤니티원장Id,
                cancellationToken);
            작업생성출고수++;
            생성작업수 += plan.피킹작업목록.Count + plan.포장작업목록.Count;

            foreach (var load in plan.작업자부하목록)
            {
                기존부하[작업자Key(load.WarehouseId, load.WorkerUserId)] =
                    new 작업부하(load.TotalTaskCount, load.TotalQuantity);
            }
        }

        return new 출고피킹작업생성결과(
            대기출고.Length,
            작업생성출고수,
            생성작업수,
            작업자배정대기수);
    }

    private static IReadOnlyList<피킹포장작업자후보> 작업자후보생성(
        대기출고행 row,
        IReadOnlyCollection<창고사용자> warehouseUsers,
        IReadOnlyDictionary<string, 작업부하> existingLoads)
        => warehouseUsers
            .Where(user => user.창고Id == row.창고Id && !string.IsNullOrWhiteSpace(user.UserId))
            .GroupBy(user => user.UserId.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var workTypes = group
                    .SelectMany(user => 역할작업유형(user.역할명))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                existingLoads.TryGetValue(작업자Key(row.창고Id, group.Key), out var load);
                return new 피킹포장작업자후보
                {
                    UserId = group.Key,
                    DisplayName = group.Key,
                    WarehouseId = row.창고Id,
                    WarehouseName = row.창고명,
                    가능작업유형코드목록 = workTypes,
                    IsAvailable = workTypes.Length > 0,
                    진행중작업수 = load?.작업수 ?? 0,
                    기배정수량 = load?.수량 ?? 0
                };
            })
            .Where(candidate => candidate.IsAvailable)
            .ToArray();

    private static 피킹포장처리방식 처리방식결정(IReadOnlyList<피킹포장작업자후보> candidates)
    {
        var pickers = candidates
            .Where(candidate => candidate.가능작업유형코드목록.Contains("Picking", StringComparer.OrdinalIgnoreCase))
            .Select(candidate => candidate.UserId)
            .ToArray();
        var packers = candidates
            .Where(candidate => candidate.가능작업유형코드목록.Contains("Packing", StringComparer.OrdinalIgnoreCase))
            .Select(candidate => candidate.UserId)
            .ToArray();
        var hasSeparateWorkers = pickers.Any(picker =>
            packers.Any(packer => !string.Equals(picker, packer, StringComparison.OrdinalIgnoreCase)));

        return hasSeparateWorkers
            ? 피킹포장처리방식.피킹포장분리
            : 피킹포장처리방식.피킹포장통합;
    }

    private static IReadOnlyList<string> 역할작업유형(string roleName)
        => roleName.Trim() switch
        {
            출고역할 => ["Picking", "Packing"],
            피킹역할 => ["Picking"],
            포장역할 => ["Packing"],
            _ => []
        };

    private static string 작업자Key(long warehouseId, string userId)
        => $"{warehouseId}:{userId.Trim()}";

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record 대기출고행(
        long 출고예정Id,
        string 주문참조번호,
        string? 커뮤니티원장Id,
        long 입고상품Id,
        long 창고Id,
        string 창고명,
        string SKU,
        string 상품명,
        int 수량,
        string 입고SKU,
        string 보관위치);

    private sealed record 작업부하(int 작업수, int 수량);
}

public sealed class 출고피킹작업생성Worker(
    IServiceScopeFactory scopeFactory,
    ISsalddelExecutionModePolicy executionMode,
    Microsoft.Extensions.Options.IOptions<출고피킹작업생성Options> options,
    ILogger<출고피킹작업생성Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            if (executionMode.IsOperational && settings.Enabled)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var service = scope.ServiceProvider.GetRequiredService<I출고피킹작업생성Service>();
                    await service.대기출고처리Async(settings.BatchSize, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "출고예정 피킹·포장 작업 생성 주기가 실패했습니다.");
                }
            }

            var intervalSeconds = Math.Clamp(settings.IntervalSeconds, 5, 3600);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
