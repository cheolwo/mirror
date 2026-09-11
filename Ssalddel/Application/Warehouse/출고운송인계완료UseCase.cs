using System.Text.Json;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse.Events;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Services.Community;
using Ssalddel.Services.Operations;
using 살뜰.Data;
using 살뜰.Services.Audit;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public interface I출고운송인계완료UseCase
{
    Task<Result<출고운송인계완료응답>> 완료Async(
        long outboundPlanId,
        출고운송인계완료요청 request,
        창고작업요청Context context,
        CancellationToken cancellationToken);
}

[SsalddelApiWorkflow(SsalddelWorkflow.WarehouseFulfillment)]
[SsalddelUseCase("출고 운송 인계 완료", Summary = "기사 본인 수락과 등록 차량을 서버 원장에서 확인한 뒤 예약 재고를 실제 출고로 한 번만 전환합니다.")]
[SsalddelUseCaseActor(SsalddelActor.WarehouseManager)]
public sealed class 출고운송인계완료UseCase(
    SsalddelContext db,
    ICurrentUserAccessor currentUserAccessor,
    I사용자행위로그Service activityLogService,
    I음식마트원장동기화OutboxService ledgerSyncOutbox,
    I출고화물운송운영체제인계Service operatingSystemHandoff,
    IPublisher publisher) : I출고운송인계완료UseCase
{
    public async Task<Result<출고운송인계완료응답>> 완료Async(
        long outboundPlanId,
        출고운송인계완료요청 request,
        창고작업요청Context context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.DriverIdentityConfirmed
            || !request.VehicleConfirmed
            || !request.CargoReleasedConfirmed)
        {
            return Invalid("기사 신원, 등록 차량, 상품 인계 확인을 모두 완료해 주세요.");
        }

        var memo = request.Memo?.Trim() ?? string.Empty;
        if (memo.Length > 400)
        {
            return Invalid("출고 인계 메모는 400자 이하로 입력해 주세요.");
        }

        var userId = CurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var plan = await 접근가능출고Query(userId)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == outboundPlanId && x.상태 != 출고상태.취소, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        var assignment = await ResolveAssignmentAsync(plan, cancellationToken);
        if (plan.상태 == 출고상태.출고완료)
        {
            await operatingSystemHandoff.인계Async(plan, cancellationToken);
            await SyncLedgerAsync(plan, userId, cancellationToken);
            return Result.Ok(ToResult(plan, assignment.DriverId, assignment.Vehicle, true));
        }

        if (plan.상태 != 출고상태.준비중)
        {
            return Conflict($"출고 준비 중 원장만 운송 인계를 완료할 수 있습니다. 현재 상태: {plan.상태}");
        }

        if (string.IsNullOrWhiteSpace(plan.운송의뢰Id)
            || plan.입고상품Id is not > 0)
        {
            return Conflict("출고예정에 운송의뢰와 입고상품이 모두 연결되어야 합니다.");
        }

        var transportRequest = await db.화주운송의뢰
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == plan.운송의뢰Id, cancellationToken);
        var transportLedger = await db.운송원장
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == plan.운송의뢰Id, cancellationToken);
        if (transportRequest is null || transportLedger is null)
        {
            return Conflict("같은 의뢰 ID의 운송의뢰와 운송 실행 원장을 모두 확인할 수 없습니다.");
        }

        if (!string.Equals(
                transportRequest.배차상태,
                상태값.배차상태.배차확정,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(transportLedger.확정기사Id))
        {
            return Conflict("기사 본인의 배차 수락이 서버 원장에 확정되기 전에는 출고할 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(assignment.DriverId)
            || string.IsNullOrWhiteSpace(assignment.Vehicle))
        {
            return Conflict("수락한 기사의 등록 차량을 확인할 수 없어 출고할 수 없습니다.");
        }
        if (string.IsNullOrWhiteSpace(transportRequest.차량종류)
            || !string.Equals(
                transportRequest.차량종류.Trim(),
                assignment.Vehicle,
                StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(
                $"요청 차량({transportRequest.차량종류})과 수락 기사 등록 차량({assignment.Vehicle})이 일치하지 않습니다.");
        }

        var allocation = await db.운송의뢰상품연결
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.운송의뢰Id == plan.운송의뢰Id
                     && x.입고상품Id == plan.입고상품Id.Value,
                cancellationToken);
        if (allocation is null || allocation.할당수량 != plan.수량)
        {
            return Conflict("출고예정 수량과 운송의뢰 상품 할당 수량이 일치하지 않습니다.");
        }

        var inventorySnapshot = await db.입고상품
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == plan.입고상품Id.Value, cancellationToken);
        if (inventorySnapshot is null
            || inventorySnapshot.창고Id != plan.출고창고Id
            || inventorySnapshot.예약수량 < plan.수량)
        {
            return Conflict("출고 창고의 예약 재고가 인계 수량과 일치하지 않습니다.");
        }

        var now = DateTime.UtcNow;
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (db.Database.IsRelational())
        {
            var changed = await db.출고예정
                .Where(x => x.Id == plan.Id && x.상태 == 출고상태.준비중)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.상태, 출고상태.출고완료)
                        .SetProperty(x => x.출고처리일시, now)
                        .SetProperty(x => x.UpdatedAt, now),
                    cancellationToken);
            if (changed == 0)
            {
                var current = await db.출고예정
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == plan.Id, cancellationToken);
                if (current?.상태 == 출고상태.출고완료)
                {
                    await transaction!.RollbackAsync(cancellationToken);
                    return Result.Ok(ToResult(current, assignment.DriverId, assignment.Vehicle, true));
                }

                return Conflict("다른 작업자가 출고예정 상태를 변경했습니다. 최신 원장을 다시 조회해 주세요.");
            }
        }
        else
        {
            var trackedPlan = await db.출고예정.SingleAsync(x => x.Id == plan.Id, cancellationToken);
            if (trackedPlan.상태 == 출고상태.출고완료)
            {
                await SyncLedgerAsync(trackedPlan, userId, cancellationToken);
                return Result.Ok(ToResult(trackedPlan, assignment.DriverId, assignment.Vehicle, true));
            }

            trackedPlan.상태 = 출고상태.출고완료;
            trackedPlan.출고처리일시 = now;
            trackedPlan.UpdatedAt = now;
        }

        var inventory = await db.입고상품
            .SingleAsync(x => x.Id == plan.입고상품Id.Value, cancellationToken);
        if (inventory.예약수량 < plan.수량)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return Conflict("출고 처리 중 예약 재고가 변경되었습니다. 최신 원장을 다시 조회해 주세요.");
        }

        inventory.예약수량 -= plan.수량;
        if (inventory.가용수량 == 0 && inventory.예약수량 == 0)
        {
            inventory.상태 = 출고상태.출고완료;
        }
        inventory.UpdatedAt = now;

        var historyMemo = string.IsNullOrWhiteSpace(memo)
            ? $"기사 {assignment.DriverId} · 차량 {assignment.Vehicle}에 {plan.수량:N0}개 인계"
            : $"기사 {assignment.DriverId} · 차량 {assignment.Vehicle}에 {plan.수량:N0}개 인계. {memo}";
        db.재고이력.Add(new 재고이력
        {
            입고상품Id = inventory.Id,
            이력유형 = 재고이동유형.출고,
            변경수량 = -plan.수량,
            변경후수량 = inventory.가용수량,
            원인유형 = "출고운송인계",
            원인Id = plan.Id,
            처리UserId = userId,
            메모 = historyMemo,
            처리일시 = now
        });
        db.재고이동.Add(new 재고이동
        {
            창고Id = inventory.창고Id,
            입고상품Id = inventory.Id,
            판매상품Id = plan.판매상품Id,
            상품명 = plan.상품명,
            SKU = plan.SKU,
            이동유형 = 재고이동유형.출고,
            수량 = plan.수량,
            주문Id = plan.주문Id,
            주문참조번호 = plan.주문참조번호,
            출고예정Id = plan.Id,
            입고요청Id = plan.입고요청Id,
            운송의뢰Id = plan.운송의뢰Id,
            처리UserId = userId,
            메모 = historyMemo,
            발생일시 = now
        });

        await db.SaveChangesAsync(cancellationToken);

        plan.상태 = 출고상태.출고완료;
        plan.출고처리일시 = now;
        plan.UpdatedAt = now;
        await operatingSystemHandoff.인계Async(plan, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        await activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = context.AppKey,
            UserId = context.UserId,
            UserName = context.UserName,
            RoleName = context.RoleName,
            ActionType = "WarehouseOutboundTransportHandoff",
            ActionName = "Completed",
            Route = context.Route,
            TraceId = context.TraceId,
            IsSuccess = true,
            ClientIp = context.ClientIp,
            UserAgent = context.UserAgent,
            OccurredAtUtc = now,
            MetadataJson = JsonSerializer.Serialize(new
            {
                outboundPlanId = plan.Id,
                inventoryItemId = inventory.Id,
                transportRequestId = plan.운송의뢰Id,
                driverId = assignment.DriverId,
                vehicle = assignment.Vehicle,
                quantity = plan.수량
            })
        }, cancellationToken);
        await publisher.Publish(
            new 창고출고운송인계완료됨Event(
                context.UserId,
                context.RoleName,
                plan.Id,
                inventory.Id,
                plan.운송의뢰Id,
                assignment.DriverId,
                assignment.Vehicle,
                plan.수량,
                context.Route,
                context.TraceId,
                now,
                context.AppKey,
                plan.주문참조번호,
                plan.입고요청Id,
                plan.커뮤니티원장Id ?? string.Empty),
            cancellationToken);
        await SyncLedgerAsync(plan, userId, cancellationToken);

        return Result.Ok(ToResult(plan, assignment.DriverId, assignment.Vehicle, false));
    }

    private IQueryable<출고예정> 접근가능출고Query(string userId)
    {
        var query = db.출고예정.AsQueryable();
        if (string.Equals(currentUserAccessor.Role, 역할명.서버관리자, StringComparison.OrdinalIgnoreCase))
        {
            return query;
        }

        return query.Where(plan =>
            db.창고.Any(warehouse =>
                warehouse.Id == plan.출고창고Id
                && warehouse.소유자UserId == userId)
            || db.창고사용자.Any(warehouseUser =>
                warehouseUser.창고Id == plan.출고창고Id
                && warehouseUser.UserId == userId));
    }

    private async Task<(string DriverId, string Vehicle)> ResolveAssignmentAsync(
        출고예정 plan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plan.운송의뢰Id))
        {
            return (string.Empty, string.Empty);
        }

        var driverId = await db.운송원장
            .AsNoTracking()
            .Where(x => x.의뢰Id == plan.운송의뢰Id)
            .Select(x => x.확정기사Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return (string.Empty, string.Empty);
        }

        var vehicle = await db.용달기사
            .AsNoTracking()
            .Where(x => x.기사Id == driverId && x.상태 == "활동중")
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => x.차량)
            .FirstOrDefaultAsync(cancellationToken);
        return (driverId.Trim(), vehicle?.Trim() ?? string.Empty);
    }

    private async Task SyncLedgerAsync(
        출고예정 plan,
        string userId,
        CancellationToken cancellationToken)
    {
        var inbound = plan.입고요청Id is > 0
            ? await db.입고요청
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == plan.입고요청Id.Value, cancellationToken)
            : null;
        await ledgerSyncOutbox.출고원장예약후즉시처리Async(
            [plan],
            inbound is null ? [] : [inbound],
            userId,
            $"warehouse-transport-handoff:{plan.Id}",
            currentStageKey: "출고 인계 완료",
            cancellationToken: cancellationToken);
    }

    private static 출고운송인계완료응답 ToResult(
        출고예정 plan,
        string driverId,
        string vehicle,
        bool replay)
        => new()
        {
            OutboundPlanId = plan.Id,
            TransportRequestId = plan.운송의뢰Id ?? string.Empty,
            OutboundStatus = plan.상태,
            AssignedDriverId = driverId,
            AssignedDriverVehicle = vehicle,
            HandoffCompletedAtUtc = AsUtc(plan.출고처리일시 ?? plan.UpdatedAt),
            IdempotentReplay = replay
        };

    private string? CurrentUserId()
    {
        var value = currentUserAccessor.UserId?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static Result<출고운송인계완료응답> Unauthorized()
        => Result.Fail<출고운송인계완료응답>(
            new Error("로그인 사용자 인증 정보가 필요합니다.")
                .WithMetadata("StatusCode", StatusCodes.Status401Unauthorized));

    private static Result<출고운송인계완료응답> NotFound()
        => Result.Fail<출고운송인계완료응답>(
            new Error("출고예정 원장을 찾을 수 없거나 현재 계정의 창고 작업 범위에 없습니다.")
                .WithMetadata("StatusCode", StatusCodes.Status404NotFound));

    private static Result<출고운송인계완료응답> Invalid(string message)
        => Result.Fail<출고운송인계완료응답>(
            new Error(message).WithMetadata("StatusCode", StatusCodes.Status400BadRequest));

    private static Result<출고운송인계완료응답> Conflict(string message)
        => Result.Fail<출고운송인계완료응답>(
            new Error(message).WithMetadata("StatusCode", StatusCodes.Status409Conflict));
}
