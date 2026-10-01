using Hongdal.Application.Community;
using Hongdal.Application.Warehouse.Events;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ViewSettings;
using Hongdal.Services.Community;
using 홍달.Data;

namespace Hongdal.Application.Warehouse.Handlers;

public sealed class 창고작업경험치EventHandler :
    INotificationHandler<창고입고완료됨Event>,
    INotificationHandler<창고입고검수완료됨Event>,
    INotificationHandler<창고적재위치배정됨Event>,
    INotificationHandler<창고포장완료됨Event>,
    INotificationHandler<창고피킹완료됨Event>,
    INotificationHandler<창고재위탁운송생성됨Event>
{
    private readonly ICommunityExperienceEventRecorder _experienceEventRecorder;

    public 창고작업경험치EventHandler(ICommunityExperienceEventRecorder experienceEventRecorder)
    {
        _experienceEventRecorder = experienceEventRecorder;
    }

    public Task Handle(창고입고완료됨Event notification, CancellationToken cancellationToken)
        => RecordAsync(
            notification.사용자Id,
            notification.역할명,
            CommunityLedgerExperienceEventCodes.WarehouseInboundCompleted,
            "WarehouseInbound",
            notification.입고Id.ToString(),
            notification.입고Id.ToString(),
            notification.Route,
            notification.TraceId,
            notification.발생시각Utc,
            notification.AppKey,
            cancellationToken);

    public Task Handle(창고입고검수완료됨Event notification, CancellationToken cancellationToken)
        => RecordAsync(
            notification.사용자Id,
            notification.역할명,
            CommunityLedgerExperienceEventCodes.WarehouseInboundInspected,
            "WarehouseInventory",
            notification.입고상품Id.ToString(),
            notification.입고상품Id.ToString(),
            notification.Route,
            notification.TraceId,
            notification.발생시각Utc,
            notification.AppKey,
            cancellationToken);

    public Task Handle(창고적재위치배정됨Event notification, CancellationToken cancellationToken)
        => RecordAsync(
            notification.사용자Id,
            notification.역할명,
            CommunityLedgerExperienceEventCodes.WarehousePutAwayCompleted,
            "WarehouseInventory",
            notification.입고상품Id.ToString(),
            notification.입고상품Id.ToString(),
            notification.Route,
            notification.TraceId,
            notification.발생시각Utc,
            notification.AppKey,
            cancellationToken);

    public Task Handle(창고포장완료됨Event notification, CancellationToken cancellationToken)
        => RecordAsync(
            notification.사용자Id,
            notification.역할명,
            CommunityLedgerExperienceEventCodes.WarehouseInventoryPacked,
            "WarehouseInventory",
            notification.입고상품Id.ToString(),
            notification.입고상품Id.ToString(),
            notification.Route,
            notification.TraceId,
            notification.발생시각Utc,
            notification.AppKey,
            cancellationToken);

    public Task Handle(창고피킹완료됨Event notification, CancellationToken cancellationToken)
        => RecordAsync(
            notification.사용자Id,
            notification.역할명,
            CommunityLedgerExperienceEventCodes.WarehousePickingCompleted,
            "WarehousePickingTask",
            string.IsNullOrWhiteSpace(notification.피킹작업Key)
                ? (notification.창고Id?.ToString() ?? string.Empty)
                : notification.피킹작업Key,
            string.IsNullOrWhiteSpace(notification.피킹작업Key)
                ? (notification.창고Id?.ToString() ?? string.Empty)
                : notification.피킹작업Key,
            notification.Route,
            notification.TraceId,
            notification.발생시각Utc,
            notification.AppKey,
            cancellationToken);

    public Task Handle(창고재위탁운송생성됨Event notification, CancellationToken cancellationToken)
        => RecordAsync(
            notification.사용자Id,
            notification.역할명,
            CommunityLedgerExperienceEventCodes.WarehouseReconsignmentCreated,
            "WarehouseReconsignment",
            notification.입고상품Id.ToString(),
            notification.의뢰Id,
            notification.Route,
            notification.TraceId,
            notification.발생시각Utc,
            notification.AppKey,
            cancellationToken);

    private Task RecordAsync(
        string userId,
        string roleName,
        string eventCode,
        string sourceKind,
        string sourceId,
        string sourceDisplayId,
        string route,
        string traceId,
        DateTime occurredAtUtc,
        string appKey,
        CancellationToken cancellationToken)
        => _experienceEventRecorder.RecordAsync(
            new CommunityExperienceAwardRequest(
                userId,
                string.IsNullOrWhiteSpace(roleName) ? 역할명.창고관리자 : roleName,
                eventCode,
                sourceKind,
                sourceId,
                sourceDisplayId,
                route,
                traceId,
                occurredAtUtc,
                string.IsNullOrWhiteSpace(appKey) ? App식별자.HongdalApp : appKey),
            "창고 작업",
            cancellationToken);
}
