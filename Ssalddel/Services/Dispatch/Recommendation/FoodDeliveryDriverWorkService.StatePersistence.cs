using Ssalddel.Contracts.Common.Metadata;

using FluentResults;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Food.Events;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Operations;
using 살뜰.Services.Dispatch.Common;
using Ssalddel.Contracts.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.Food;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Settlement;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Recommendation;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "기사 동작의 SQL 원천·배달 시도·책임 판정과 transaction 저장을 처리한다.",
    ContractType = typeof(I음식배달기사업무Service),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.StateMutation,
    FlowOrder = 125,
    StepKey = "application.driver-state-persistence",
    DependsOnStepKeys = ["application.driver-actions"],
    ExecutionStage = SsalddelCodeExecutionStage.Persistence,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "동시성 충돌과 DB 저장 실패는 rollback한다. SQL 전문 상태와 Mongo 주문 원장의 관계 권위는 별개이며 저장 성공이 외부 후속 완료를 뜻하지 않는다.")]
public sealed partial class 음식배달기사업무Service
{
    private async Task<(운송원장 Queue, 음식주문 Order)?> LoadForActionAsync(
        string offerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(offerId))
        {
            return null;
        }

        var queue = await _db.운송원장
            .FirstOrDefaultAsync(x => x.의뢰Id == offerId
                                      && x.배차업무유형 == 상태값.배차업무유형.음식배달,
                cancellationToken);
        if (queue is null || 운송의뢰배차원천유형.Is살뜰마트음식주문(queue.원본의뢰유형))
        {
            return null;
        }

        var orderNo = ResolveOrderNo(queue);
        var order = await _db.음식주문
            .Include(x => x.상태이력)
            .FirstOrDefaultAsync(x => x.주문번호 == orderNo, cancellationToken);
        if (order?.모의처리처수행 == true && (_fulfillment is null
                || !await _fulfillment.내부수행허용Async(
                    new(Ssalddel.Contracts.Common.Fulfillment.FulfillmentSourceKinds.FoodOrder, orderNo),
                    ct: cancellationToken)))
            return null;
        return order is null ? null : (queue, order);
    }

    private async Task<음식배달시도> 수락시도생성Async(
        운송원장 queue,
        음식주문 order,
        string driverId,
        DateTime acceptedAtUtc,
        CancellationToken cancellationToken)
    {
        var previousAttempts = await _db.음식배달시도
            .Where(x => x.주문번호 == order.주문번호)
            .OrderByDescending(x => x.시도순번)
            .ToListAsync(cancellationToken);
        var sequence = previousAttempts.Count == 0 ? 1 : previousAttempts[0].시도순번 + 1;
        var followsPreparationDelay = previousAttempts.Any(x =>
            x.상태Code == 음식배달시도상태Code.중단
            && x.중단사유Code == 음식배달중단사유Code.조리지연);
        var displayedReadyAtUtc = order.조리예상완료시각Utc;
        if (followsPreparationDelay)
        {
            var minimumReadyAtUtc = acceptedAtUtc.AddMinutes(10);
            if (!displayedReadyAtUtc.HasValue || displayedReadyAtUtc.Value < minimumReadyAtUtc)
            {
                displayedReadyAtUtc = minimumReadyAtUtc;
            }
        }

        var attempt = new 음식배달시도
        {
            경험실행모드Code = _experienceExecution?.Mode.ToString(),
            시도StableId = 배달시도StableId(order.주문번호, sequence),
            주문번호 = order.주문번호,
            제안Id = queue.의뢰Id,
            기사Id = driverId,
            추천라운드 = queue.추천라운드,
            시도순번 = sequence,
            상태Code = 음식배달시도상태Code.수락,
            Revision = 1,
            수락시각Utc = acceptedAtUtc,
            표시준비예정시각Utc = displayedReadyAtUtc,
            조리지연재배차여부 = followsPreparationDelay,
            CreatedAtUtc = acceptedAtUtc,
            UpdatedAtUtc = acceptedAtUtc
        };
        _db.음식배달시도.Add(attempt);
        return attempt;
    }

    private async Task<음식배달시도> 현재시도조회또는추정Async(
        운송원장 queue,
        음식주문 order,
        string driverId,
        CancellationToken cancellationToken)
    {
        var attempt = await _db.음식배달시도
            .Where(x => x.주문번호 == order.주문번호
                        && x.제안Id == queue.의뢰Id
                        && x.기사Id == driverId)
            .OrderByDescending(x => x.시도순번)
            .FirstOrDefaultAsync(cancellationToken);
        if (attempt is not null)
        {
            return attempt;
        }

        var sequence = (await _db.음식배달시도
            .Where(x => x.주문번호 == order.주문번호)
            .MaxAsync(x => (int?)x.시도순번, cancellationToken) ?? 0) + 1;
        var acceptedAtUtc = order.상태이력
            .Where(x => x.다음상태 == 음식주문상태코드.기사배정)
            .OrderByDescending(x => x.전이시각Utc)
            .Select(x => x.전이시각Utc)
            .FirstOrDefault();
        if (acceptedAtUtc == default)
        {
            acceptedAtUtc = queue.UpdatedAt == default ? DateTime.UtcNow : queue.UpdatedAt;
        }

        attempt = new 음식배달시도
        {
            시도StableId = 배달시도StableId(order.주문번호, sequence),
            주문번호 = order.주문번호,
            제안Id = queue.의뢰Id,
            기사Id = driverId,
            추천라운드 = queue.추천라운드,
            시도순번 = sequence,
            상태Code = 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.픽업완료
                ? 음식배달시도상태Code.픽업완료
                : 음식배달시도상태Code.수락,
            Revision = 1,
            수락시각Utc = DateTime.SpecifyKind(acceptedAtUtc, DateTimeKind.Utc),
            표시준비예정시각Utc = order.조리예상완료시각Utc,
            픽업완료시각Utc = 음식주문상태코드.Normalize(order.상태) == 음식주문상태코드.픽업완료
                ? queue.출발_픽업
                : null,
            유산추정여부 = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.음식배달시도.Add(attempt);
        return attempt;
    }

    private async Task<string> 초기책임판정Async(
        string driverId,
        string reasonCode,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (reasonCode == 음식배달중단사유Code.조리지연)
        {
            return 운영배차책임Code.음식점;
        }

        if (reasonCode is 음식배달중단사유Code.사고
            or 음식배달중단사유Code.배터리부족
            or 음식배달중단사유Code.배달수단고장
            or 음식배달중단사유Code.위험기상)
        {
            return 운영배차책임Code.보호대상;
        }

        if (reasonCode != 음식배달중단사유Code.개인긴급)
        {
            return 운영배차책임Code.미확정;
        }

        var timeZone = 운영배차단기지표Calculator.대한민국시간대조회();
        var localDate = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), timeZone));
        var localStart = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localEnd = localDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, timeZone);
        var alreadyProtectedToday = await _db.음식배달시도.AnyAsync(x =>
            x.기사Id == driverId
            && x.중단사유Code == 음식배달중단사유Code.개인긴급
            && x.중단시각Utc >= startUtc
            && x.중단시각Utc < endUtc
            && x.책임Code == 운영배차책임Code.보호대상,
            cancellationToken);
        return alreadyProtectedToday ? 운영배차책임Code.미확정 : 운영배차책임Code.보호대상;
    }

    private static bool 픽업준비됨(음식주문 order)
        => 음식주문현재조리Policy.계산(order).ReadyAtUtc.HasValue;

    private static 운송이벤트 위치감사사건(
        운송원장 queue,
        string driverId,
        string action,
        FoodDeliveryLocationAuditResult locationAudit,
        DateTime occurredAtUtc,
        string attemptId)
        => new()
        {
            의뢰Id = queue.의뢰Id,
            이벤트타입 = 운송이벤트유형.음식배달위치감사,
            이벤트시각 = occurredAtUtc,
            메타데이터 = JsonSerializer.Serialize(new
            {
                Action = action,
                DriverId = driverId,
                DeliveryAttemptId = attemptId,
                locationAudit.Code,
                locationAudit.DistanceKm,
                locationAudit.DriverLocationAtUtc,
                locationAudit.DriverLatitude,
                locationAudit.DriverLongitude,
                locationAudit.TargetLatitude,
                locationAudit.TargetLongitude,
                Policy = "AdvisoryEvidenceOnly"
            })
        };

    private static string 배달시도StableId(string orderNo, int sequence)
    {
        var hash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes($"{orderNo}|{sequence}")))
            .ToLowerInvariant();
        return $"food-delivery-attempt:{hash}";
    }

    private async Task<Result> SaveTransactionAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _업무진행예약항목.Clear();
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            finally { 업무진행예약해제(); }
            _logger.LogInformation(ex, "음식 배달 상태 변경 중 동시성 충돌이 발생했습니다.");
            return Result.Fail("다른 기사 또는 운영자가 먼저 상태를 변경했습니다. 목록을 새로고침해 주세요.");
        }
        catch (DbUpdateException ex)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            finally { 업무진행예약해제(); }
            _logger.LogWarning(ex, "음식 배달 상태 저장에 실패했습니다.");
            return Result.Fail("음식 배달 상태를 저장하지 못했습니다.");
        }
        catch
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            finally { 업무진행예약해제(); }
            throw;
        }
    }

}
