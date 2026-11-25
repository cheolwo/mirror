using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentResults;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Inventory;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 출고예정검토UseCase
{
    private static Error? PeriodWorkflowRequestError(출고예정검토목록조회요청 request)
    {
        var stage = request.PeriodStage?.Trim();
        var key = request.BatchKey?.Trim();
        if (!string.IsNullOrEmpty(stage) && !OutboundWorkflowStageCodes.All.Contains(stage, StringComparer.Ordinal)
            || key?.Length > 256
            || !request.PeriodStartUtc.HasValue && !request.PeriodEndUtc.HasValue
                && (!string.IsNullOrEmpty(stage) || !string.IsNullOrEmpty(key)))
            return new Error("회차 조회 구간과 공정 선택을 확인해 주세요.").WithMetadata("StatusCode", StatusCodes.Status400BadRequest);
        return null;
    }

    private async Task<Result<OutboundPeriodWorkflow>> ReadPeriodWorkflowAsync(
        출고예정검토목록조회요청 request, IQueryable<출고예정> plans,
        IQueryable<PeriodPlanFact> rows, DateTime observed, CancellationToken cancellationToken)
    {
        var stage = string.IsNullOrWhiteSpace(request.PeriodStage) ? OutboundWorkflowStageCodes.Review : request.PeriodStage.Trim();
        var start = request.PeriodStartUtc!.Value;
        var end = request.PeriodEndUtc!.Value;
        var warehouseId = request.WarehouseId!.Value;
        var cohort = plans.Where(plan => plan.CreatedAt >= start && plan.CreatedAt < end
            || plan.CreatedAt < start && plan.상태 != 출고상태.출고완료 && plan.상태 != 출고상태.취소);
        // 목록 페이지와 검색 조건을 적용하지 않은 동일 원출고 집합에서 공정 연결을 읽는다.
        var sourcePlans = await cohort.ToArrayAsync(cancellationToken);
        var facts = (await rows.ToArrayAsync(cancellationToken)).ToDictionary(plan => plan.Id);
        var sourceIds = sourcePlans.Select(plan => plan.Id).ToHashSet();
        var cohortIds = cohort.Select(plan => plan.Id);
        var tasks = await db.피킹포장작업.AsNoTracking()
            .Where(task => task.출고예정Id.HasValue && cohortIds.Contains(task.출고예정Id.Value))
            .ToArrayAsync(cancellationToken);
        var validWarehouseTasks = tasks.Where(task => task.창고Id == warehouseId).ToArray();
        var tasksByPlan = tasks.ToLookup(task => task.출고예정Id!.Value);
        var tasksByKey = validWarehouseTasks.Where(task => !string.IsNullOrWhiteSpace(task.작업Key))
            .GroupBy(task => task.작업Key, StringComparer.Ordinal)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        var bundleIds = sourcePlans.Where(plan => plan.출고묶음Id.HasValue).Select(plan => plan.출고묶음Id!.Value)
            .Concat(validWarehouseTasks.Where(task => task.출고묶음Id.HasValue).Select(task => task.출고묶음Id!.Value)).Distinct().ToList();
        var bundles = await db.출고묶음.AsNoTracking().Where(bundle => bundle.출고창고Id == warehouseId && bundleIds.Contains(bundle.Id))
            .ToDictionaryAsync(bundle => bundle.Id, cancellationToken);
        var outsideBundleIds = await plans.Where(plan => plan.출고묶음Id.HasValue && bundleIds.Contains(plan.출고묶음Id.Value)
                && !(plan.CreatedAt >= start && plan.CreatedAt < end
                    || plan.CreatedAt < start && plan.상태 != 출고상태.출고완료 && plan.상태 != 출고상태.취소))
            .Select(plan => plan.출고묶음Id!.Value).Distinct().ToArrayAsync(cancellationToken);
        var outsideBundles = outsideBundleIds.ToHashSet();
        var outsideTaskBundleIds = await db.피킹포장작업.AsNoTracking()
            .Where(task => task.창고Id == warehouseId && task.출고묶음Id.HasValue && bundleIds.Contains(task.출고묶음Id.Value)
                && (!task.출고예정Id.HasValue || !cohortIds.Contains(task.출고예정Id.Value)))
            .Select(task => task.출고묶음Id!.Value).Distinct().ToArrayAsync(cancellationToken);
        outsideBundles.UnionWith(outsideTaskBundleIds);
        var packing = await ReadPackingWorkflowBatchesAsync(validWarehouseTasks, sourceIds, warehouseId, observed, cancellationToken);
        var entries = new List<PeriodWorkflowEntry>();
        foreach (var plan in sourcePlans)
        {
            var fact = facts[plan.Id];
            var sourceTasks = tasksByPlan[plan.Id].ToArray();
            foreach (var sourceStage in new[] { OutboundWorkflowStageCodes.Review, OutboundWorkflowStageCodes.Handoff })
            {
                var bundleValid = plan.출고묶음Id is { } bundleId && bundles.ContainsKey(bundleId);
                var outside = bundleValid && outsideBundles.Contains(plan.출고묶음Id!.Value);
                var missingBundle = plan.출고묶음Id.HasValue && !bundleValid;
                var kind = missingBundle ? "unconfirmed" : bundleValid ? "recorded-batch" : "source-ledger";
                var identity = bundleValid ? "outbound:" + Number(plan.출고묶음Id!.Value) : "source:" + Number(plan.Id);
                entries.Add(new(sourceStage, sourceStage + ":" + identity, kind,
                    bundleValid ? RecordedOutboundLabel(bundles[plan.출고묶음Id!.Value], sourceStage) : "단일 출고 원장", plan.Id, null,
                    SourceMember(plan, start), Math.Max(0, (long)plan.수량),
                    sourceStage == OutboundWorkflowStageCodes.Review ? null : fact.HandedOffQuantity,
                    fact.SourceInvalid || missingBundle || sourceStage == OutboundWorkflowStageCodes.Handoff && fact.HandoffInvalid,
                    SourceWorkflowReason(plan, fact, sourceStage, missingBundle, outside), !outside, outside));
            }
            foreach (var taskStage in new[] { OutboundWorkflowStageCodes.Picking, OutboundWorkflowStageCodes.Packing })
            {
                var workKind = taskStage == OutboundWorkflowStageCodes.Picking ? 피킹포장작업유형.피킹 : 피킹포장작업유형.포장;
                var linkedStageTasks = sourceTasks.Where(task => task.작업유형 == workKind).ToArray();
                var stageTasks = linkedStageTasks.Where(task => task.창고Id == warehouseId).ToArray();
                if (stageTasks.Length == 0)
                {
                    if (plan.상태 == 출고상태.취소 && linkedStageTasks.Length == 0 && !fact.SourceInvalid)
                    {
                        entries.Add(new(taskStage, taskStage + ":source:" + Number(plan.Id), "source-ledger", "취소된 출고",
                            plan.Id, null, SourceMember(plan, start), 0, null, false,
                            "취소된 출고이며 이 공정에 연결된 작업이 없습니다.", true, false));
                        continue;
                    }
                    entries.Add(new(taskStage, taskStage + ":unconfirmed:" + Number(plan.Id), "unconfirmed", "작업 연결 미확인",
                        plan.Id, null, SourceMember(plan, start), 0, 0, true,
                        linkedStageTasks.Length > 0 ? "이 공정의 작업 연결 범위 확인이 필요합니다." : "이 공정에 연결된 작업 원장을 확인하지 못했습니다.", false, false));
                    continue;
                }
                var stageInvalid = fact.SourceInvalid || linkedStageTasks.Where(task => task.상태 != 피킹포장작업상태.취소).Any(task => !WorkflowTaskValid(task, plan, observed))
                    || linkedStageTasks.Where(task => task.상태 != 피킹포장작업상태.취소).Sum(task => (long)task.수량) > plan.수량;
                foreach (var task in stageTasks)
                {
                    var cancelled = task.상태 == 피킹포장작업상태.취소 || plan.상태 == 출고상태.취소;
                    var quantity = !stageInvalid && !cancelled ? Math.Max(0, (long)task.수량) : 0;
                    var completed = task.상태 == 피킹포장작업상태.완료 ? quantity : 0;
                    var bundleId = task.출고묶음Id ?? plan.출고묶음Id;
                    var bundleValid = bundleId.HasValue && bundles.ContainsKey(bundleId.Value)
                        && (!task.출고묶음Id.HasValue || !plan.출고묶음Id.HasValue || task.출고묶음Id == plan.출고묶음Id);
                    var proof = packing.GetValueOrDefault(task.Id);
                    var completionProof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json);
                    var packingClaim = taskStage == OutboundWorkflowStageCodes.Packing && !string.IsNullOrWhiteSpace(task.완료근거Json)
                        && completionProof?.PackingBatchKey is { Length: > 0 };
                    var invalidProof = !string.IsNullOrWhiteSpace(task.완료근거Json) && (completionProof is null
                        || completionProof.Version != 1 || completionProof.TaskId != task.Id || completionProof.TaskKey != task.작업Key);
                    var invalid = stageInvalid || bundleId.HasValue && !bundleValid || packingClaim && proof is null || invalidProof;
                    var recorded = !invalid && (proof is not null || bundleValid);
                    var kind = invalid ? "unconfirmed" : recorded ? "recorded-batch" : "single-task";
                    var identity = proof is not null ? "packing:" + proof.Key
                        : bundleValid ? "outbound:" + Number(bundleId!.Value) : "task:" + Number(task.Id);
                    var outside = proof?.HasOutsideScopeMembers == true || bundleValid && outsideBundles.Contains(bundleId!.Value);
                    var reason = invalid ? "작업의 창고·수량·완료 또는 묶음 연결 근거 확인이 필요합니다."
                        : outside ? "조회 범위 밖의 구성 작업이 있어 현재 범위만 표시합니다." : recorded ? "기존 원장의 명시된 묶음 연결입니다." : "기존 작업 원장 한 건입니다.";
                    var member = TaskMember(task, plan, start, observed, tasksByKey, out var linksMissing);
                    if (linksMissing) reason += " 공정 사이의 일부 연결은 현재 범위에서 확인되지 않습니다.";
                    entries.Add(new(taskStage, taskStage + ":" + identity, kind,
                        invalid ? "작업 연결 확인 필요" : proof is not null ? "기존 포장 완료 묶음"
                            : bundleValid ? RecordedOutboundLabel(bundles[bundleId!.Value], taskStage) : "단일 작업",
                        plan.Id, task.Id, member, quantity, completed, invalid, reason, !outside && !invalid, outside));
                }
            }
        }
        var stageGroups = entries.GroupBy(entry => (entry.Stage, entry.Key)).Select(group => group.ToArray()).ToArray();
        var summaries = OutboundWorkflowStageCodes.All.Select(code =>
        {
            var groups = stageGroups.Where(group => group[0].Stage == code).ToArray();
            var stageEntries = entries.Where(entry => entry.Stage == code).ToArray();
            return new OutboundPeriodStageSummary
            {
                Code = code, Label = WorkflowStageLabel(code), SourcePlanCount = sourcePlans.Length,
                TaskCount = stageEntries.Count(entry => entry.TaskId.HasValue),
                RecordedBatchCount = groups.Count(group => WorkflowGroupKind(group) == "recorded-batch"),
                SingleWorkCount = groups.Count(group => WorkflowGroupKind(group) is "single-task" or "source-ledger"),
                UnconfirmedGroupCount = groups.Count(group => WorkflowGroupKind(group) == "unconfirmed"),
                CancelledCount = sourcePlans.Count(plan => plan.상태 == 출고상태.취소),
                SourceQuantity = sourcePlans.Sum(plan => Math.Max(0, (long)plan.수량)),
                WorkQuantity = stageEntries.Sum(entry => entry.Quantity),
                CompletedQuantity = code == OutboundWorkflowStageCodes.Review ? null : stageEntries.Sum(entry => entry.CompletedQuantity ?? 0)
            };
        }).ToArray();
        var ordered = stageGroups.Where(group => group[0].Stage == stage)
            .OrderBy(group => group.Min(entry => entry.Member.CreatedAtUtc)).ThenBy(group => group[0].Key, StringComparer.Ordinal).ToArray();
        var page = Math.Max(0, request.BatchPage);
        var pageSize = Math.Clamp(request.BatchPageSize, 1, 50);
        var response = new OutboundPeriodWorkflow
        {
            SelectedStage = stage, Stages = summaries, TotalGroups = ordered.Length, Page = page, PageSize = pageSize,
            Groups = ordered.Skip((int)Math.Min((long)page * pageSize, int.MaxValue)).Take(pageSize)
                .Select(group => WorkflowGroup(group, false, 0, 0)).ToArray()
        };
        if (!string.IsNullOrWhiteSpace(request.BatchKey))
        {
            var selected = ordered.SingleOrDefault(group => group[0].Key == request.BatchKey.Trim());
            if (selected is null) return NotFound<OutboundPeriodWorkflow>();
            response.SelectedGroupKey = selected[0].Key;
            response.SelectedGroup = WorkflowGroup(selected, true, Math.Max(0, request.MemberPage), Math.Clamp(request.MemberPageSize, 1, 50));
        }
        return Result.Ok(response);
    }

    private async Task<Dictionary<long, PeriodPackingBatch>> ReadPackingWorkflowBatchesAsync(
        피킹포장작업[] tasks, HashSet<long> cohortIds, long warehouseId, DateTime observed, CancellationToken cancellationToken)
    {
        var claims = tasks.Where(task => task.작업유형 == 피킹포장작업유형.포장 && task.상태 == 피킹포장작업상태.완료)
            .Select(task => new { Task = task, Proof = WarehouseActivityCompletionEvidence.Read(task.완료근거Json) })
            .Where(row => row.Proof is not null && !string.IsNullOrWhiteSpace(row.Proof.PackingBatchKey)
                && row.Proof.PackingBatchShares is { Count: > 0 and <= 64 }).ToArray();
        var ids = claims.SelectMany(row => row.Proof!.PackingBatchShares).Where(share => share is not null).Select(share => share.TaskId).Distinct().ToList();
        var members = await db.피킹포장작업.AsNoTracking().Where(task => task.창고Id == warehouseId && ids.Contains(task.Id))
            .ToDictionaryAsync(task => task.Id, cancellationToken);
        var planIds = members.Values.Where(task => task.출고예정Id.HasValue).Select(task => task.출고예정Id!.Value).Distinct().ToList();
        var plans = await db.출고예정.AsNoTracking().Where(plan => plan.출고창고Id == warehouseId && planIds.Contains(plan.Id))
            .ToDictionaryAsync(plan => plan.Id, cancellationToken);
        var inventoryIds = claims.Select(row => row.Proof!.InventoryItemId).Distinct().ToList();
        var inventories = await db.입고상품.AsNoTracking().Where(item => item.창고Id == warehouseId && inventoryIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var histories = await db.재고이력.AsNoTracking().Where(history => inventoryIds.Contains(history.입고상품Id)
            && history.이력유형 == "포장" && history.원인유형 == "포장작업").ToArrayAsync(cancellationToken);
        var result = new Dictionary<long, PeriodPackingBatch>();
        foreach (var claim in claims)
        {
            var proof = claim.Proof!;
            var shares = proof.PackingBatchShares;
            if (proof.Version != 1 || proof.PackingBatchQuantity is not > 0 || shares.Any(share => share is null || share.Quantity <= 0)
                || shares.Select(share => share.TaskId).Distinct().Count() != shares.Count
                || shares.Sum(share => (long)share.Quantity) != proof.PackingBatchQuantity
                || !shares.Any(share => share.TaskId == claim.Task.Id && share.Quantity == claim.Task.수량)
                || !inventories.TryGetValue(proof.InventoryItemId, out var inventory)
                || histories.Count(history => history.입고상품Id == inventory.Id && history.처리UserId == proof.ActorUserId
                    && history.변경후수량 == proof.PackingBatchQuantity && AsUtc(history.처리일시) == proof.CompletedAtUtc) != 1) continue;
            var valid = true;
            var outside = false;
            foreach (var share in shares)
            {
                if (!members.TryGetValue(share.TaskId, out var task) || !task.출고예정Id.HasValue
                    || !plans.TryGetValue(task.출고예정Id.Value, out var plan) || !WorkflowTaskValid(task, plan, observed)) { valid = false; break; }
                var other = WarehouseActivityCompletionEvidence.Read(task.완료근거Json);
                if (other is null || other.Version != 1 || task.작업유형 != 피킹포장작업유형.포장 || task.상태 != 피킹포장작업상태.완료
                    || task.입고상품Id != inventory.Id || plan.입고상품Id != inventory.Id
                    || task.SKU != inventory.SKU || plan.SKU != inventory.SKU || task.수량 != share.Quantity
                    || other.TaskId != task.Id || other.TaskKey != task.작업Key || other.WorkKind != task.작업유형
                    || other.OutboundId != plan.Id || other.OrderReference != task.주문참조번호 || other.WarehouseId != warehouseId
                    || other.InventoryItemId != inventory.Id || other.Quantity != task.수량 || string.IsNullOrWhiteSpace(other.ActorUserId)
                    || other.PackingBatchKey != proof.PackingBatchKey || other.ActorUserId != proof.ActorUserId
                    || other.PackingBatchQuantity != proof.PackingBatchQuantity || other.CompletedAtUtc != proof.CompletedAtUtc
                    || other.CompletedAtUtc.Kind != DateTimeKind.Utc || other.CompletedAtUtc != AsUtc(task.완료일시Utc!.Value)
                    || other.PackingBatchShares is null || !other.PackingBatchShares.SequenceEqual(shares)) { valid = false; break; }
                outside |= !cohortIds.Contains(plan.Id);
            }
            if (!valid) continue;
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(proof.PackingBatchKey!))).ToLowerInvariant();
            result[claim.Task.Id] = new(key, outside);
        }
        return result;
    }

    private static bool WorkflowTaskValid(피킹포장작업 task, 출고예정 plan, DateTime observed)
        => task.창고Id == plan.출고창고Id && task.출고예정Id == plan.Id && task.수량 > 0
            && plan.CreatedAt > DateTime.MinValue && plan.CreatedAt <= observed
            && task.수량 <= plan.수량 && task.CreatedAt > DateTime.MinValue && task.CreatedAt <= observed
            && task.상태 is 피킹포장작업상태.대기 or 피킹포장작업상태.진행중 or 피킹포장작업상태.완료
            && (task.상태 != 피킹포장작업상태.완료 || task.완료일시Utc.HasValue && task.완료일시Utc > DateTime.MinValue
                && task.완료일시Utc <= observed && task.완료일시Utc >= plan.CreatedAt && task.완료일시Utc >= task.CreatedAt);

    private static OutboundPeriodMember SourceMember(출고예정 plan, DateTime start) => new()
    {
        OutboundPlanId = plan.Id, ProductName = plan.상품명, Quantity = plan.수량, Status = plan.상태,
        IsCarryover = plan.CreatedAt < start, CreatedAtUtc = AsUtc(plan.CreatedAt), CanOpenReview = plan.상태 != 출고상태.취소
    };

    private static OutboundPeriodMember TaskMember(피킹포장작업 task, 출고예정 plan, DateTime start,
        DateTime observed, IReadOnlyDictionary<string, 피킹포장작업> tasks, out bool linksMissing)
    {
        var previous = ValidWorkflowLink(task, task.이전작업Key, false, tasks, plan, observed);
        var next = ValidWorkflowLink(task, task.다음작업Key, true, tasks, plan, observed);
        linksMissing = !string.IsNullOrWhiteSpace(task.이전작업Key) && previous is null || !string.IsNullOrWhiteSpace(task.다음작업Key) && next is null;
        return new OutboundPeriodMember
        {
            OutboundPlanId = plan.Id, ProductName = plan.상품명, TaskKey = task.작업Key, Quantity = task.수량, Status = task.상태,
            IsCarryover = plan.CreatedAt < start, CreatedAtUtc = AsUtc(task.CreatedAt),
            StartedAtUtc = task.작업유형 == 피킹포장작업유형.피킹 && task.시작일시Utc >= task.CreatedAt && task.시작일시Utc <= observed
                ? AsUtc(task.시작일시Utc) : null,
            CompletedAtUtc = WorkflowTaskValid(task, plan, observed) && task.상태 == 피킹포장작업상태.완료 ? AsUtc(task.완료일시Utc) : null,
            WorkerDisplayName = string.IsNullOrWhiteSpace(task.작업자표시명)
                || string.Equals(task.작업자표시명.Trim(), task.작업자UserId?.Trim(), StringComparison.Ordinal) ? "" : task.작업자표시명.Trim(),
            PreviousTaskKey = previous, NextTaskKey = next, CanOpenReview = plan.상태 != 출고상태.취소
        };
    }

    private static string? ValidWorkflowLink(피킹포장작업 task, string? key, bool next,
        IReadOnlyDictionary<string, 피킹포장작업> tasks, 출고예정 plan, DateTime observed)
    {
        if (string.IsNullOrWhiteSpace(key) || !tasks.TryGetValue(key, out var other)
            || task.상태 == 피킹포장작업상태.취소 || other.상태 == 피킹포장작업상태.취소
            || task.출고예정Id != other.출고예정Id || task.입고상품Id != other.입고상품Id
            || task.입고상품Id is not > 0 || task.입고상품Id != plan.입고상품Id
            || task.창고Id != other.창고Id || task.SKU != other.SKU || task.수량 <= 0 || task.수량 != other.수량
            || task.SKU != plan.SKU || task.상품명 != plan.상품명 || other.상품명 != plan.상품명
            || !WorkflowTaskValid(task, plan, observed) || !WorkflowTaskValid(other, plan, observed)
            || (next ? task.작업유형 != 피킹포장작업유형.피킹 || other.작업유형 != 피킹포장작업유형.포장
                : task.작업유형 != 피킹포장작업유형.포장 || other.작업유형 != 피킹포장작업유형.피킹)
            || (next ? other.이전작업Key : other.다음작업Key) != task.작업Key) return null;
        return other.작업Key;
    }

    private static OutboundPeriodGroup WorkflowGroup(PeriodWorkflowEntry[] entries, bool includeMembers, int page, int pageSize)
        => new()
        {
            Key = entries[0].Key, Kind = WorkflowGroupKind(entries),
            Label = WorkflowGroupLabel(entries),
            SourcePlanCount = entries.Select(entry => entry.PlanId).Distinct().Count(), TaskCount = entries.Count(entry => entry.TaskId.HasValue),
            Quantity = entries.Sum(entry => entry.Quantity),
            CompletedQuantity = entries[0].Stage == OutboundWorkflowStageCodes.Review || entries.All(entry => entry.CompletedQuantity is null)
                ? null : entries.Sum(entry => entry.CompletedQuantity ?? 0),
            NeedsReview = entries.Any(entry => entry.NeedsReview),
            Reason = string.Join(" ", entries.Select(entry => entry.Reason).Distinct(StringComparer.Ordinal)),
            MembershipComplete = WorkflowGroupKind(entries) != "unconfirmed" && entries.All(entry => entry.MembershipComplete),
            HasOutsideScopeMembers = entries.Any(entry => entry.HasOutsideScopeMembers),
            MemberCount = entries.Length, MemberPage = page, MemberPageSize = pageSize,
            Members = includeMembers ? entries.OrderBy(entry => entry.Member.CreatedAtUtc).ThenBy(entry => entry.PlanId).ThenBy(entry => entry.TaskId)
                .Skip((int)Math.Min((long)page * pageSize, int.MaxValue)).Take(pageSize).Select(entry => entry.Member).ToArray() : []
        };

    private static string WorkflowGroupKind(PeriodWorkflowEntry[] entries)
        => entries.Any(entry => entry.NeedsReview) || entries.Select(entry => entry.Kind).Distinct(StringComparer.Ordinal).Count() != 1
            ? "unconfirmed" : entries[0].Kind;

    private static string RecordedOutboundLabel(출고묶음 bundle, string stage)
        => (string.IsNullOrWhiteSpace(bundle.출고묶음번호) ? "출고묶음 번호 미확인" : bundle.출고묶음번호.Trim())
            + " · " + WorkflowStageLabel(stage);

    private static string WorkflowGroupLabel(PeriodWorkflowEntry[] entries)
    {
        var kind = WorkflowGroupKind(entries);
        if (kind == "recorded-batch" && entries[0].Key.StartsWith(entries[0].Stage + ":outbound:", StringComparison.Ordinal))
            return entries[0].Label;
        var names = entries.Select(entry => entry.Member.ProductName).Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var products = names.Length == 0 ? "상품명 미확인" : string.Join(" · ", names.Take(2));
        if (names.Length > 2) products += " 외 " + (names.Length - 2).ToString(CultureInfo.InvariantCulture) + "종";
        var label = kind == "unconfirmed" ? "작업·묶음 연결 확인 필요" : entries[0].Label;
        return label + " · " + products;
    }

    private static string SourceWorkflowReason(출고예정 plan, PeriodPlanFact fact, string stage, bool missingBundle, bool outside)
    {
        if (fact.SourceInvalid) return "원출고의 최초 접수 시각과 수량 확인이 필요합니다.";
        if (missingBundle) return "출고묶음 연결 확인이 필요합니다.";
        var scope = outside ? " 조회 범위 밖의 구성 출고가 있어 현재 범위만 표시합니다." : string.Empty;
        if (stage == OutboundWorkflowStageCodes.Review) return "원출고의 현재 검토 대상과 상태입니다." + scope;
        var handoff = fact.HandoffInvalid ? "창고 인계 완료 시각 확인이 필요합니다."
            : fact.HandedOffQuantity > 0 ? "원출고의 출고 처리 시각과 상태로 확인한 창고 인계 기록입니다."
            : "창고 인계 완료 기록을 확인해 주세요.";
        return handoff + (string.IsNullOrWhiteSpace(plan.운송의뢰Id) ? " 운송의뢰 연결 확인이 필요합니다." : " 운송의뢰 연결 기록이 있습니다.") + scope;
    }

    private static string WorkflowStageLabel(string stage) => stage switch
    {
        OutboundWorkflowStageCodes.Picking => "피킹", OutboundWorkflowStageCodes.Packing => "포장",
        OutboundWorkflowStageCodes.Handoff => "출고 인계", _ => "출고 검토"
    };
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private sealed record PeriodPackingBatch(string Key, bool HasOutsideScopeMembers);
    private sealed record PeriodWorkflowEntry(string Stage, string Key, string Kind, string Label, long PlanId, long? TaskId,
        OutboundPeriodMember Member, long Quantity, long? CompletedQuantity, bool NeedsReview, string Reason,
        bool MembershipComplete, bool HasOutsideScopeMembers);
}
