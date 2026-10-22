using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using 살뜰.Services.Notifications;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Dispatch.Notification;

public sealed class 상차접근알림Service : I상차접근알림Service
{
    private static readonly HashSet<string> 상차접근검사대상상태 = new(StringComparer.Ordinal)
    {
        "매칭중",
        "이동중",
        "상차지도착"
    };

    private readonly SsalddelContext _db;
    private readonly I배차추천경로Service _routeService;
    private readonly ILogger<상차접근알림Service> _logger;

    public 상차접근알림Service(
        SsalddelContext db,
        I배차추천경로Service routeService,
        ILogger<상차접근알림Service> logger)
    {
        _db = db;
        _routeService = routeService;
        _logger = logger;
    }

    public async Task<int> 상차지접근알림검사Async(
        DriverLocationSnapshot location,
        decimal 접근반경Km = 10m,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(location.DriverId) || 접근반경Km <= 0m)
        {
            return 0;
        }

        var activeTransports = await _db.운송원장
            .AsNoTracking()
            .Where(x => x.기사_운송자 == location.DriverId
                        && 상차접근검사대상상태.Contains(x.상태))
            .OrderByDescending(x => x.UpdatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);
        if (activeTransports.Count == 0)
        {
            return 0;
        }

        var requestIds = activeTransports
            .Select(x => x.운송번호)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requestIds.Length == 0)
        {
            return 0;
        }

        var requests = await _db.화주운송의뢰
            .AsNoTracking()
            .Where(x => Enumerable.Contains(requestIds, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, cancellationToken);

        var currentPoint = new 배차경로좌표(location.Latitude, location.Longitude);
        var created = 0;
        var reservations = new List<Command알림Outbox>();
        try
        {
            foreach (var transport in activeTransports)
            {
                if (!requests.TryGetValue(transport.운송번호, out var request)
                    || !CargoWorkflowProgressNotifications.IsCurrentDriver(transport, location.DriverId)
                    || request.상태 == "취소" || request.배차상태 == "취소"
                    || !request.픽업_위도.HasValue
                    || !request.픽업_경도.HasValue)
                {
                    continue;
                }

                var pickupPoint = new 배차경로좌표(request.픽업_위도.Value, request.픽업_경도.Value);
                var distanceKm = _routeService.CalculateDistanceKm(currentPoint, pickupPoint);
                if (!distanceKm.HasValue || distanceKm.Value > 접근반경Km)
                {
                    continue;
                }

                var reserved = await CargoWorkflowProgressNotifications.ReserveAsync(_db, "cargo.approaching", request,
                        transport.Id, location.DriverId, DateTime.UtcNow,
                        Activity.Current?.TraceId.ToString() ?? string.Empty, cancellationToken);
                if (reserved is not null)
                {
                    reservations.Add(reserved);
                    created++;
                }
            }

            if (created > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "상차지 접근 알림 의도 적재 완료. DriverId={DriverId} Count={Count}",
                    location.DriverId,
                    created);
            }

            return created;
        }
        catch
        {
            foreach (var reserved in reservations) CargoWorkflowProgressNotifications.DiscardUncommitted(_db, reserved);
            throw;
        }
    }

}
