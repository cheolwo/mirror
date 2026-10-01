using Hongdal.Application.Community;
using Hongdal.Application.Warehouse.Events;
using Hongdal.Application.Warehouse.Handlers;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.ViewSettings;
using Hongdal.Services.Community;
using Microsoft.Extensions.Logging.Abstractions;
using 홍달.Data;

namespace Hongdal.Tests.Application.Warehouse;

public sealed class 창고작업경험치EventHandlerTests
{
    [Fact]
    public async Task Handle_창고상태변경Events_창고경험치코드를기록한다()
    {
        var service = new FakeCommunityExperienceAwardService();
        var handler = new 창고작업경험치EventHandler(CreateRecorder(service));
        var occurredAt = new DateTime(2026, 7, 12, 5, 0, 0, DateTimeKind.Utc);

        await handler.Handle(
            new 창고입고완료됨Event("warehouse-user", 역할명.창고관리자, 10, 2, "/inbounds/10/complete", "trace-1", occurredAt, App식별자.HongdalApp),
            CancellationToken.None);
        await handler.Handle(
            new 창고입고검수완료됨Event("warehouse-user", 역할명.창고관리자, 20, 8, 1, "/inventory/20/inspect", "trace-2", occurredAt, App식별자.HongdalApp),
            CancellationToken.None);
        await handler.Handle(
            new 창고적재위치배정됨Event("warehouse-user", 역할명.창고관리자, 30, "A-1", "/inventory/30/put-away", "trace-3", occurredAt, App식별자.HongdalApp),
            CancellationToken.None);
        await handler.Handle(
            new 창고포장완료됨Event("warehouse-user", 역할명.창고관리자, 40, 3, "/inventory/40/pack", "trace-4", occurredAt, App식별자.HongdalApp),
            CancellationToken.None);
        await handler.Handle(
            new 창고피킹완료됨Event("warehouse-user", 역할명.창고관리자, "PICK-20-001", 20, 4, "/picking-tasks/PICK-20-001/complete", "trace-4-pick", occurredAt, App식별자.WarehouseManagerApp),
            CancellationToken.None);
        await handler.Handle(
            new 창고재위탁운송생성됨Event("warehouse-user", 역할명.창고관리자, 50, 1, "HD-50", "/inventory/reconsignment", "trace-5", occurredAt, App식별자.HongdalApp),
            CancellationToken.None);

        Assert.Collection(
            service.Requests,
            request => AssertRequest(request, CommunityLedgerExperienceEventCodes.WarehouseInboundCompleted, "WarehouseInbound", "10", "10", "/inbounds/10/complete"),
            request => AssertRequest(request, CommunityLedgerExperienceEventCodes.WarehouseInboundInspected, "WarehouseInventory", "20", "20", "/inventory/20/inspect"),
            request => AssertRequest(request, CommunityLedgerExperienceEventCodes.WarehousePutAwayCompleted, "WarehouseInventory", "30", "30", "/inventory/30/put-away"),
            request => AssertRequest(request, CommunityLedgerExperienceEventCodes.WarehouseInventoryPacked, "WarehouseInventory", "40", "40", "/inventory/40/pack"),
            request => AssertRequest(request, CommunityLedgerExperienceEventCodes.WarehousePickingCompleted, "WarehousePickingTask", "PICK-20-001", "PICK-20-001", "/picking-tasks/PICK-20-001/complete", App식별자.WarehouseManagerApp),
            request => AssertRequest(request, CommunityLedgerExperienceEventCodes.WarehouseReconsignmentCreated, "WarehouseReconsignment", "50", "HD-50", "/inventory/reconsignment"));
    }

    private static void AssertRequest(
        CommunityExperienceAwardRequest request,
        string eventCode,
        string sourceKind,
        string sourceId,
        string sourceDisplayId,
        string route,
        string appKey = App식별자.HongdalApp)
    {
        Assert.Equal("warehouse-user", request.UserId);
        Assert.Equal(역할명.창고관리자, request.RoleName);
        Assert.Equal(appKey, request.AppKey);
        Assert.Equal(eventCode, request.EventCode);
        Assert.Equal(sourceKind, request.SourceKind);
        Assert.Equal(sourceId, request.SourceId);
        Assert.Equal(sourceDisplayId, request.SourceDisplayId);
        Assert.Equal(route, request.Route);
    }

    private sealed class FakeCommunityExperienceAwardService : ICommunityExperienceAwardService
    {
        public List<CommunityExperienceAwardRequest> Requests { get; } = [];

        public Task<CommunityExperienceAwardResult> RecordAsync(
            CommunityExperienceAwardRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(new CommunityExperienceAwardResult(
                true,
                request.EventCode,
                1,
                "ok"));
        }
    }

    private static CommunityExperienceEventRecorder CreateRecorder(
        ICommunityExperienceAwardService service)
        => new(service, NullLogger<CommunityExperienceEventRecorder>.Instance);
}
