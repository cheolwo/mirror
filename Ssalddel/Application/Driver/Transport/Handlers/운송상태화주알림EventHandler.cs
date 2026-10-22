using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using 살뜰.Services.Notifications;
using 살뜰.도메인.설정;

namespace Ssalddel.Application.Driver.Transport;

/// <summary>구형 이벤트 재생의 알림 호환 경로. 운송·화주 원장의 상태를 다시 쓰지 않는다.</summary>
public sealed class 운송상태화주알림EventHandler :
    INotificationHandler<운송상차지도착됨Event>,
    INotificationHandler<운송상차완료됨Event>,
    INotificationHandler<운송하차지도착됨Event>,
    INotificationHandler<운송인수완료됨Event>,
    INotificationHandler<운송문제신고됨Event>
{
    private readonly SsalddelContext _db;
    private readonly ILogger<운송상태화주알림EventHandler> _logger;

    public 운송상태화주알림EventHandler(SsalddelContext db, ILogger<운송상태화주알림EventHandler> logger)
    { _db = db; _logger = logger; }

    public Task Handle(운송상차지도착됨Event value, CancellationToken ct)
        => 처리Async("cargo.arrived-pickup", value.운송Id, value.기사Id, value.현재상태, value.발생시각Utc, value.TraceId, ct);

    public Task Handle(운송상차완료됨Event value, CancellationToken ct)
        => 처리Async("cargo.picked-up", value.운송Id, value.기사Id, value.현재상태, value.발생시각Utc, value.TraceId, ct);

    public Task Handle(운송하차지도착됨Event value, CancellationToken ct)
        => 처리Async("cargo.arrived-dropoff", value.운송Id, value.기사Id, value.현재상태, value.발생시각Utc, value.TraceId, ct);

    public Task Handle(운송인수완료됨Event value, CancellationToken ct)
        => 처리Async("cargo.delivered", value.운송Id, value.기사Id, value.상태, value.발생시각Utc, value.TraceId, ct);

    public Task Handle(운송문제신고됨Event value, CancellationToken ct)
        => 처리Async("cargo.issue", value.운송Id, value.기사Id, null, value.발생시각Utc, value.TraceId, ct);

    private async Task 처리Async(string stageCode, long transportId, string driverId, string? eventState,
        DateTime occurredAtUtc, string traceId, CancellationToken cancellationToken)
    {
        Command알림Outbox? reserved = null;
        try
        {
            var transport = await _db.운송원장.AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == transportId, cancellationToken);
            if (transport is null || !CargoWorkflowProgressNotifications.IsCurrentDriver(transport, driverId)
                || eventState is not null && !string.Equals(transport.상태, eventState, StringComparison.Ordinal)) return;
            var requestId = string.IsNullOrWhiteSpace(transport.의뢰Id) ? transport.운송번호 : transport.의뢰Id;
            if (string.IsNullOrWhiteSpace(requestId)) return;
            var source = await _db.화주운송의뢰.AsNoTracking()
                .FirstOrDefaultAsync(row => row.의뢰Id == requestId, cancellationToken);
            if (source is null || source.상태 == "취소" || source.배차상태 == "취소") return;

            reserved = await CargoWorkflowProgressNotifications.ReserveAsync(_db, stageCode, source, transportId,
                driverId, occurredAtUtc, traceId, cancellationToken);
            if (reserved is not null)
                await _db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { CargoWorkflowProgressNotifications.DiscardUncommitted(_db, reserved); throw; }
        catch (Exception ex)
        {
            CargoWorkflowProgressNotifications.DiscardUncommitted(_db, reserved);
            _logger.LogWarning(ex, "운송 진행 알림 준비 실패. TransportId={TransportId} StageCode={StageCode}", transportId, stageCode);
        }
    }
}
