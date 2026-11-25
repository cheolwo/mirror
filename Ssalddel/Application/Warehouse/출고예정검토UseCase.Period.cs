using System.Globalization;
using System.Data;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Inventory;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 출고예정검토UseCase
{
    private static Error? PeriodRequestError(출고예정검토목록조회요청 request)
    {
        if (PeriodWorkflowRequestError(request) is { } workflowError) return workflowError;
        if (!request.PeriodStartUtc.HasValue && !request.PeriodEndUtc.HasValue) return null;
        if (request.PeriodStartUtc is not { Kind: DateTimeKind.Utc } start
            || request.PeriodEndUtc is not { Kind: DateTimeKind.Utc } end
            || request.WarehouseId is not > 0 || end <= start || end - start > TimeSpan.FromHours(24)
            || start < DateTime.MinValue.AddDays(8) || end > DateTime.MaxValue.AddHours(-9))
            return new Error("창고와 UTC 시작·종료 시각을 지정하고 24시간 이내의 조회 구간을 확인해 주세요.")
                .WithMetadata("StatusCode", StatusCodes.Status400BadRequest);
        return null;
    }

    private async Task<Result<OutboundPeriodResponse>> ReadPeriodAsync(
        출고예정검토목록조회요청 request, string userId, CancellationToken cancellationToken)
    {
        // 여러 집계와 페이지를 같은 원장 관측으로 읽고, 호출자의 기존 transaction이 있으면 재사용한다.
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
            return await ReadPeriodSnapshotAsync(request, userId, cancellationToken);
        var isolation = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? IsolationLevel.Serializable : IsolationLevel.RepeatableRead;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var snapshot = await db.Database.BeginTransactionAsync(isolation, cancellationToken);
            var result = await ReadPeriodSnapshotAsync(request, userId, cancellationToken);
            await snapshot.CommitAsync(cancellationToken);
            return result;
        });
    }

    private async Task<Result<OutboundPeriodResponse>> ReadPeriodSnapshotAsync(
        출고예정검토목록조회요청 request, string userId, CancellationToken cancellationToken)
    {
        var warehouseId = request.WarehouseId!.Value;
        var warehouseQuery = db.창고.AsNoTracking().Where(warehouse => warehouse.Id == warehouseId);
        if (!string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
            warehouseQuery = warehouseQuery.Where(warehouse => warehouse.소유자UserId == userId
                || db.창고사용자.Any(member => member.창고Id == warehouse.Id && member.UserId == userId));
        var warehouseName = await warehouseQuery.Select(warehouse => warehouse.창고명).SingleOrDefaultAsync(cancellationToken);
        if (warehouseName is null) return NotFound<OutboundPeriodResponse>();

        var start = request.PeriodStartUtc!.Value;
        var end = request.PeriodEndUtc!.Value;
        var observed = DateTime.UtcNow;
        var dayStart = DateTime.SpecifyKind(start.AddHours(9).Date.AddHours(-9), DateTimeKind.Utc);
        var weekStart = dayStart.AddDays(-7);
        var dayEnd = dayStart.AddDays(1);
        var plans = 접근가능출고Query(userId).AsNoTracking().Where(plan => plan.출고창고Id == warehouseId);
        var facts = PeriodFacts(plans, observed);
        var current = facts.Where(plan => plan.CreatedAtUtc >= start && plan.CreatedAtUtc < end);
        var carryover = facts.Where(plan => plan.CreatedAtUtc < start
            && plan.OutboundStatus != 출고상태.출고완료 && plan.OutboundStatus != 출고상태.취소);
        var rows = facts.Where(plan => plan.CreatedAtUtc >= start && plan.CreatedAtUtc < end
            || plan.CreatedAtUtc < start && plan.OutboundStatus != 출고상태.출고완료 && plan.OutboundStatus != 출고상태.취소);

        var response = new OutboundPeriodResponse
        {
            WarehouseId = warehouseId, WarehouseName = warehouseName,
            PeriodStartUtc = start, PeriodEndUtc = end, ObservedAtUtc = observed,
            Current = await PeriodTotals(current).SingleOrDefaultAsync(cancellationToken) ?? new(),
            Carryover = await PeriodTotals(carryover).SingleOrDefaultAsync(cancellationToken) ?? new(),
            Today = await PeriodTotals(facts.Where(plan => plan.CreatedAtUtc >= dayStart && plan.CreatedAtUtc < dayEnd))
                .SingleOrDefaultAsync(cancellationToken) ?? new(),
            TotalCount = await rows.CountAsync(cancellationToken),
            Page = Math.Max(0, request.Page), PageSize = Math.Clamp(request.PageSize, 1, 50)
        };
        var days = await facts.Where(plan => plan.CreatedAtUtc >= weekStart && plan.CreatedAtUtc < dayStart)
            .GroupBy(plan => plan.CreatedAtUtc.AddHours(9).Date)
            .Select(group => new
            {
                Day = group.Key,
                Totals = new OutboundPeriodTotals
                {
                    PlanCount = group.Count(), ReceivedQuantity = group.Sum(plan => plan.Quantity > 0 ? plan.Quantity : 0),
                    PickedQuantity = group.Sum(plan => plan.PickedQuantity), PackedQuantity = group.Sum(plan => plan.PackedQuantity),
                    HandedOffQuantity = group.Sum(plan => plan.HandedOffQuantity), NeedsReviewCount = group.Count(plan => plan.NeedsReview)
                }
            }).ToArrayAsync(cancellationToken);
        response.PreviousDays = Enumerable.Range(0, 7).Select(index =>
        {
            var date = weekStart.AddDays(index).AddHours(9).Date;
            return new OutboundPeriodDay
            {
                DateKst = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Totals = days.FirstOrDefault(day => day.Day == date)?.Totals ?? new()
            };
        }).ToArray();

        var pageRows = await rows.OrderBy(plan => plan.CreatedAtUtc >= start)
            .ThenBy(plan => plan.CreatedAtUtc).ThenBy(plan => plan.Id)
            .Skip((int)Math.Min((long)response.Page * response.PageSize, int.MaxValue))
            .Take(response.PageSize).ToArrayAsync(cancellationToken);
        var pageIds = pageRows.Select(plan => plan.Id).ToList();
        var names = await db.피킹포장작업.AsNoTracking()
            .Where(task => task.출고예정Id.HasValue && pageIds.Contains(task.출고예정Id.Value)
                && task.창고Id == warehouseId && task.상태 != 피킹포장작업상태.취소
                && task.작업자표시명 != "" && task.작업자표시명 != task.작업자UserId)
            .Select(task => new { Id = task.출고예정Id!.Value, Name = task.작업자표시명, UserId = task.작업자UserId }).Distinct()
            .ToArrayAsync(cancellationToken);
        response.Items = pageRows.Select(plan => PeriodItem(plan, start,
            names.Where(name => name.Id == plan.Id && !string.IsNullOrWhiteSpace(name.Name)
                    && !string.Equals(name.Name.Trim(), name.UserId?.Trim(), StringComparison.Ordinal))
                .Select(name => name.Name.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())).ToArray();
        var openTasks = db.피킹포장작업.AsNoTracking().Where(task => task.창고Id == warehouseId
            && (task.상태 == 피킹포장작업상태.대기 || task.상태 == 피킹포장작업상태.진행중));
        response.Workforce = new OutboundPeriodWorkforce
        {
            RegisteredWorkerCount = await db.창고사용자.AsNoTracking().Where(member => member.창고Id == warehouseId && member.UserId != "")
                .Select(member => member.UserId).Distinct().CountAsync(cancellationToken),
            AssignedWorkerCount = await openTasks.Where(task => task.작업자UserId != "")
                .Select(task => task.작업자UserId).Distinct().CountAsync(cancellationToken),
            OpenTaskCount = await openTasks.CountAsync(cancellationToken), AttendanceConfirmed = false
        };
        var workflow = await ReadPeriodWorkflowAsync(request, plans, rows, observed, cancellationToken);
        if (workflow.IsFailed) return Result.Fail<OutboundPeriodResponse>(workflow.Errors);
        response.Workflow = workflow.Value;
        return Result.Ok(response);
    }

    private IQueryable<PeriodPlanFact> PeriodFacts(IQueryable<출고예정> plans, DateTime observed)
    {
        var taskRows = from task in db.피킹포장작업.AsNoTracking()
                       join plan in plans on task.출고예정Id equals (long?)plan.Id
                       where task.상태 != 피킹포장작업상태.취소
                       select new
                       {
                           task, plan.Id,
                           Invalid = task.창고Id != plan.출고창고Id || task.수량 <= 0
                               || task.작업유형 != 피킹포장작업유형.피킹 && task.작업유형 != 피킹포장작업유형.포장
                               || task.상태 != 피킹포장작업상태.대기 && task.상태 != 피킹포장작업상태.진행중 && task.상태 != 피킹포장작업상태.완료
                               || task.상태 == 피킹포장작업상태.완료 && (!task.완료일시Utc.HasValue
                                   || task.완료일시Utc <= DateTime.MinValue || task.완료일시Utc > observed
                                   || task.완료일시Utc < plan.CreatedAt || task.완료일시Utc < task.CreatedAt)
                       };
        var tasks = from row in taskRows
                    group row by row.Id into groupedTasks
                    select new
                    {
                        Id = groupedTasks.Key, Count = (int?)groupedTasks.Count(),
                        PickingCount = (int?)groupedTasks.Count(row => row.task.작업유형 == 피킹포장작업유형.피킹),
                        PackingCount = (int?)groupedTasks.Count(row => row.task.작업유형 == 피킹포장작업유형.포장),
                        PickingTotal = (long?)groupedTasks.Sum(row => row.task.작업유형 == 피킹포장작업유형.피킹 ? (long)row.task.수량 : 0),
                        PackingTotal = (long?)groupedTasks.Sum(row => row.task.작업유형 == 피킹포장작업유형.포장 ? (long)row.task.수량 : 0),
                        Picked = (long?)groupedTasks.Sum(row => row.task.작업유형 == 피킹포장작업유형.피킹 && row.task.상태 == 피킹포장작업상태.완료 ? (long)row.task.수량 : 0),
                        Packed = (long?)groupedTasks.Sum(row => row.task.작업유형 == 피킹포장작업유형.포장 && row.task.상태 == 피킹포장작업상태.완료 ? (long)row.task.수량 : 0),
                        InvalidCount = (int?)groupedTasks.Count(row => row.Invalid),
                        InvalidPickingCount = (int?)groupedTasks.Count(row => row.task.작업유형 == 피킹포장작업유형.피킹 && row.Invalid),
                        InvalidPackingCount = (int?)groupedTasks.Count(row => row.task.작업유형 == 피킹포장작업유형.포장 && row.Invalid)
                    };
        var raw = from plan in plans
                  join task in tasks on plan.Id equals task.Id into taskGroup
                  from task in taskGroup.DefaultIfEmpty()
                  select new
                  {
                      Plan = plan, TaskCount = task.Count ?? 0,
                      Picked = task.Picked ?? 0, Packed = task.Packed ?? 0,
                      TaskInvalid = (task.InvalidCount ?? 0) > 0 || (task.PickingTotal ?? 0) > plan.수량 || (task.PackingTotal ?? 0) > plan.수량,
                      PickingInvalid = (task.InvalidPickingCount ?? 0) > 0 || (task.PickingTotal ?? 0) > plan.수량,
                      PackingInvalid = (task.InvalidPackingCount ?? 0) > 0 || (task.PackingTotal ?? 0) > plan.수량,
                      MissingStage = (task.PickingCount ?? 0) == 0 || (task.PackingCount ?? 0) == 0,
                      SourceInvalid = plan.수량 <= 0 || plan.CreatedAt <= DateTime.MinValue || plan.CreatedAt > observed,
                      HandoffInvalid = plan.상태 == 출고상태.출고완료 && (!plan.출고처리일시.HasValue
                          || plan.출고처리일시 <= DateTime.MinValue || plan.출고처리일시 > observed || plan.출고처리일시 < plan.CreatedAt)
                  };
        return raw.Select(row => new PeriodPlanFact
        {
            Id = row.Plan.Id, ProductName = row.Plan.상품명, OrderReference = row.Plan.주문참조번호,
            Quantity = row.Plan.수량, CreatedAtUtc = row.Plan.CreatedAt, UpdatedAtUtc = row.Plan.UpdatedAt,
            OutboundStatus = row.Plan.상태, TransportLinked = row.Plan.운송의뢰Id != null && row.Plan.운송의뢰Id != "",
            TaskCount = row.TaskCount, TaskInvalid = row.TaskInvalid, SourceInvalid = row.SourceInvalid, HandoffInvalid = row.HandoffInvalid,
            MissingStage = row.MissingStage,
            PickedQuantity = row.Plan.상태 != 출고상태.취소 && !row.PickingInvalid && !row.SourceInvalid ? row.Picked : 0,
            PackedQuantity = row.Plan.상태 != 출고상태.취소 && !row.PackingInvalid && !row.SourceInvalid ? row.Packed : 0,
            HandedOffQuantity = row.Plan.상태 == 출고상태.출고완료 && !row.HandoffInvalid && !row.SourceInvalid ? row.Plan.수량 : 0,
            NeedsReview = row.SourceInvalid || row.TaskInvalid || row.HandoffInvalid
                || row.MissingStage && row.Plan.상태 != 출고상태.취소
                || row.Plan.상태 != 출고상태.예정 && row.Plan.상태 != 출고상태.준비중 && row.Plan.상태 != 출고상태.출고완료 && row.Plan.상태 != 출고상태.취소
        });
    }

    private static IQueryable<OutboundPeriodTotals> PeriodTotals(IQueryable<PeriodPlanFact> query)
        => query.GroupBy(_ => 1).Select(group => new OutboundPeriodTotals
        {
            PlanCount = group.Count(), ReceivedQuantity = group.Sum(plan => plan.Quantity > 0 ? plan.Quantity : 0),
            PickedQuantity = group.Sum(plan => plan.PickedQuantity), PackedQuantity = group.Sum(plan => plan.PackedQuantity),
            HandedOffQuantity = group.Sum(plan => plan.HandedOffQuantity), NeedsReviewCount = group.Count(plan => plan.NeedsReview)
        });

    private static OutboundPeriodItem PeriodItem(PeriodPlanFact plan, DateTime start, string[] names)
    {
        var reason = plan.SourceInvalid ? "접수 시각 또는 출고 수량 확인 필요" : plan.TaskInvalid ? "연결 작업의 창고·수량·완료 근거 확인 필요"
            : plan.HandoffInvalid ? "출고 인계 완료 시각 확인 필요" : plan.MissingStage && plan.OutboundStatus != 출고상태.취소 ? "작업 연결 미확인"
            : plan.NeedsReview ? "출고 상태 확인 필요" : string.Empty;
        var actor = "창고 출고 담당자";
        var waiting = reason;
        if (plan.OutboundStatus == 출고상태.취소) waiting = "출고 취소 원장";
        else if (reason.Length == 0 && plan.OutboundStatus == 출고상태.출고완료) waiting = "출고 인계 완료";
        else if (reason.Length == 0 && plan.PickedQuantity < plan.Quantity) { actor = "피킹 담당자"; waiting = "연결된 피킹 작업 완료 확인 필요"; }
        else if (reason.Length == 0 && plan.PackedQuantity < plan.Quantity) { actor = "포장 담당자"; waiting = "연결된 포장 작업 완료 확인 필요"; }
        else if (reason.Length == 0) waiting = plan.TransportLinked ? "연결 운송의 출고 인계 확인 필요" : "운송 연결 확인 필요";
        return new OutboundPeriodItem
        {
            OutboundPlanId = plan.Id, ProductName = plan.ProductName, OrderReference = plan.OrderReference,
            Quantity = plan.Quantity, CreatedAtUtc = AsUtc(plan.CreatedAtUtc), UpdatedAtUtc = AsUtc(plan.UpdatedAtUtc),
            OutboundStatus = plan.OutboundStatus, IsCarryover = plan.CreatedAtUtc < start,
            CanOpenReview = plan.OutboundStatus != 출고상태.취소,
            PickedQuantity = plan.PickedQuantity, PackedQuantity = plan.PackedQuantity, HandedOffQuantity = plan.HandedOffQuantity,
            WorkerDisplayNames = names, NextActor = actor, WaitingReason = waiting, NeedsReview = plan.NeedsReview, ReviewReason = reason
        };
    }

    private sealed class PeriodPlanFact
    {
        public long Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string OrderReference { get; set; } = string.Empty;
        public long Quantity { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string OutboundStatus { get; set; } = string.Empty;
        public bool TransportLinked { get; set; }
        public int TaskCount { get; set; }
        public bool TaskInvalid { get; set; }
        public bool MissingStage { get; set; }
        public bool SourceInvalid { get; set; }
        public bool HandoffInvalid { get; set; }
        public long PickedQuantity { get; set; }
        public long PackedQuantity { get; set; }
        public long HandedOffQuantity { get; set; }
        public bool NeedsReview { get; set; }
    }
}
