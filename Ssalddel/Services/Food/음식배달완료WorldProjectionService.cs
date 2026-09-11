using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Outbox;
using Ssalddel.WorkflowRules.Contracts;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;

namespace Ssalddel.Services.Food;

public interface I음식배달완료WorldProjectionService
{
    Task<int> 대기항목처리Async(int take = 100, CancellationToken cancellationToken = default);
}

public sealed class 음식배달완료WorldProjectionService(
    SsalddelContext db,
    I음식배달완료WorldAreaResolver areaResolver,
    ILogger<음식배달완료WorldProjectionService> logger)
    : I음식배달완료WorldProjectionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan SnapshotLifetime = TimeSpan.FromHours(1);

    public async Task<int> 대기항목처리Async(
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        await 만료사본정리Async(cancellationToken);

        var now = DateTime.UtcNow;
        var retryCutoff = now - OutboxProcessingPolicy.RetryDelay;
        var leaseCutoff = now - OutboxProcessingPolicy.LeaseTimeout;
        var itemIds = await db.음식마트원장동기화Outbox
            .AsNoTracking()
            .Where(x => x.동기화유형 == 음식마트원장동기화유형코드.음식배달완료WorldProjection
                        && ((x.처리상태 == OutboxProcessingStatuses.Pending
                             && (x.시도횟수 == 0 || x.UpdatedAtUtc <= retryCutoff))
                            || (x.처리상태 == OutboxProcessingStatuses.Processing
                                && x.UpdatedAtUtc <= leaseCutoff)))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var itemId in itemIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            db.ChangeTracker.Clear();
            var item = await db.음식마트원장동기화Outbox
                .SingleOrDefaultAsync(x => x.Id == itemId
                                           && x.동기화유형 == 음식마트원장동기화유형코드.음식배달완료WorldProjection
                                           && ((x.처리상태 == OutboxProcessingStatuses.Pending
                                                && (x.시도횟수 == 0 || x.UpdatedAtUtc <= retryCutoff))
                                               || (x.처리상태 == OutboxProcessingStatuses.Processing
                                                   && x.UpdatedAtUtc <= leaseCutoff)),
                    cancellationToken);
            if (item is null) continue;
            if (!await 점유Async(item, now, cancellationToken)) continue;

            processed++;
            try
            {
                await 상태사본발행Async(item, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                await 실패기록Async(item.Id, ex, cancellationToken);
            }
        }

        return processed;
    }

    private async Task<bool> 점유Async(
        음식마트원장동기화Outbox item,
        DateTime now,
        CancellationToken cancellationToken)
    {
        item.처리상태 = OutboxProcessingStatuses.Processing;
        item.시도횟수 += 1;
        item.마지막시도시각Utc = now;
        item.UpdatedAtUtc = now;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.Entry(item).State = EntityState.Detached;
            return false;
        }
    }

    private async Task 상태사본발행Async(
        음식마트원장동기화Outbox item,
        CancellationToken cancellationToken)
    {
        var existing = await db.음식배달완료WorldSnapshot
            .SingleOrDefaultAsync(x => x.원천OutboxId == item.Id, cancellationToken);
        if (existing is null)
        {
            var expectedRevision = ExpectedRevision(item.PayloadJson);
            var order = await db.음식주문
                .Include(x => x.상태이력)
                .SingleOrDefaultAsync(x => x.주문번호 == item.원천Id, cancellationToken)
                ?? throw new InvalidOperationException("완료 상태 사본의 원천 음식 주문을 찾을 수 없습니다.");
            var transport = await db.운송원장
                .Where(x => x.배차업무유형 == 상태값.배차업무유형.음식배달
                            && (x.원본의뢰Id == item.원천Id || x.의뢰Id == item.원천Id))
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (transport is null || string.IsNullOrWhiteSpace(transport.확정기사Id))
            {
                throw new InvalidOperationException("정상 완료 주기에 결속된 확정 음식 배달 기사가 없습니다.");
            }

            var milestones = ValidateAndBuildMilestones(order, expectedRevision);
            var completedAt = order.상태이력
                .Where(x => x.다음상태 == 음식주문상태코드.수령확인)
                .OrderByDescending(x => x.전이시각Utc)
                .ThenByDescending(x => x.Id)
                .Select(x => x.전이시각Utc)
                .First();
            var publishedAt = DateTime.UtcNow;
            var snapshotToken = Guid.NewGuid().ToString("N");
            existing = new 살뜰.도메인.음식.음식배달완료WorldSnapshot
            {
                원천OutboxId = item.Id,
                SnapshotStableId = $"food-delivery-completed:{snapshotToken}",
                AreaStableId = areaResolver.Resolve(order, transport),
                LifecycleRevision = expectedRevision,
                OutcomeCode = 음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed,
                CompletedAtUtc = completedAt,
                PublishedAtUtc = publishedAt,
                ExpiresAtUtc = publishedAt.Add(SnapshotLifetime),
                OrdererActorStableId = $"actor:synthetic:{snapshotToken}:orderer",
                RestaurantActorStableId = $"actor:synthetic:{snapshotToken}:restaurant",
                DriverActorStableId = $"actor:synthetic:{snapshotToken}:driver",
                MilestonesJson = JsonSerializer.Serialize(milestones, JsonOptions)
            };
            db.음식배달완료WorldSnapshot.Add(existing);
        }

        item.처리상태 = OutboxProcessingStatuses.Succeeded;
        item.마지막오류 = string.Empty;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task 실패기록Async(
        long outboxId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var item = await db.음식마트원장동기화Outbox
            .SingleAsync(x => x.Id == outboxId, cancellationToken);
        var retry = OutboxProcessingPolicy.CanRetry(item.시도횟수);
        item.처리상태 = retry ? OutboxProcessingStatuses.Pending : OutboxProcessingStatuses.Failed;
        var errorMessage = exception.GetBaseException().Message;
        item.마지막오류 = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogWarning(
            exception,
            "음식 배달 완료 World 상태 사본 발행 실패. OutboxId={OutboxId}, Attempt={Attempt}, WillRetry={WillRetry}",
            item.Id,
            item.시도횟수,
            retry);
    }

    private async Task 만료사본정리Async(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var expired = await db.음식배달완료WorldSnapshot
            .Where(x => x.ExpiresAtUtc <= now)
            .Take(500)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0) return;
        db.음식배달완료WorldSnapshot.RemoveRange(expired);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static long ExpectedRevision(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty("orderRevision", out var revision)
            || !revision.TryGetInt64(out var value)
            || value <= 0)
        {
            throw new InvalidOperationException("완료 상태 사본 발행 요청의 주문 판본이 올바르지 않습니다.");
        }

        return value;
    }

    private static 음식배달완료WorldMilestone[] ValidateAndBuildMilestones(
        살뜰.도메인.음식.음식주문 order,
        long expectedRevision)
    {
        var histories = order.상태이력
            .OrderBy(x => x.전이시각Utc)
            .ThenBy(x => x.Id)
            .ToArray();
        if (histories.LongLength != expectedRevision
            || 음식주문상태코드.Normalize(order.상태) != 음식주문상태코드.수령확인)
        {
            throw new InvalidOperationException("발행 요청 판본과 음식 주문의 완료 판본이 일치하지 않습니다.");
        }

        if (histories.Any(x => x.다음상태 == 음식주문상태코드.거절
                               || x.다음상태 == 음식주문상태코드.취소))
        {
            throw new InvalidOperationException("첫 수직 조각은 거절·취소 없는 정상 완료 주기만 발행합니다.");
        }

        var requiredStages = new[]
        {
            음식주문상태코드.주문대기,
            음식주문상태코드.조리중,
            음식주문상태코드.픽업대기,
            음식주문상태코드.기사배정,
            음식주문상태코드.픽업완료,
            음식주문상태코드.전달완료,
            음식주문상태코드.수령확인
        };
        var selected = new List<살뜰.도메인.음식.음식주문상태이력>(requiredStages.Length);
        var searchFrom = 0;
        foreach (var stage in requiredStages)
        {
            var found = Array.FindIndex(
                histories,
                searchFrom,
                x => string.Equals(x.다음상태, stage, StringComparison.Ordinal));
            if (found < 0)
            {
                throw new InvalidOperationException($"정상 완료 주기의 필수 단계가 없거나 순서가 다릅니다. 단계={stage}");
            }

            selected.Add(histories[found]);
            searchFrom = found + 1;
        }

        var startedAt = selected[0].전이시각Utc;
        var completionProof = new 운영업무완료증명
        {
            OperatingSystemId = "FoodDeliveryOS",
            WorkStableId = $"food-order:{order.주문번호}",
            Revision = expectedRevision,
            OutcomeCode = 음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed,
            CompletedAtUtc = selected[^1].전이시각Utc,
            Stages = selected.Select((history, index) => new 운영업무완료단계
            {
                Sequence = index + 1,
                StageCode = history.다음상태,
                OccurredAtUtc = history.전이시각Utc
            }).ToArray()
        };
        var proofErrors = 운영업무완료증명Validator.Validate(
            completionProof,
            new 운영업무완료프로필
            {
                OperatingSystemId = "FoodDeliveryOS",
                OutcomeCode = 음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed,
                RequiredStageCodes = requiredStages
            });
        if (proofErrors.Length > 0)
        {
            throw new InvalidOperationException(
                "음식배달 OS 완료 증명이 공통 검증을 통과하지 못했습니다. " + string.Join(",", proofErrors));
        }

        return selected.Select((history, index) => new 음식배달완료WorldMilestone
        {
            Sequence = index + 1,
            StageCode = history.다음상태,
            ElapsedSeconds = Math.Max(0L, (long)(history.전이시각Utc - startedAt).TotalSeconds)
        }).ToArray();
    }
}
