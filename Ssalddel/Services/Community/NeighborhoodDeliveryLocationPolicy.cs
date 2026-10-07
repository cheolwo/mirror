using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Community;

/// <summary>생활 배송의 현재 배정 시각과 최근 GPS만 판정합니다. 기사 전체 근무 이력을 투영하지 않습니다.</summary>
public static class NeighborhoodDeliveryLocationPolicy
{
    public const string AssignmentEventType = "NeighborhoodDeliveryDriverAssigned";
    public static readonly TimeSpan MaximumLocationAge = TimeSpan.FromMinutes(10);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsNeighborhoodDelivery(화주운송의뢰 request)
        => request.클라이언트요청Id?.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal) == true;

    /// <summary>수락 원장의 동일 SQL 저장에 추가하며 사후 알림 저장 성공에 의존하지 않습니다.</summary>
    public static 운송이벤트? CreateAssignmentEvent(화주운송의뢰 request, 운송원장 queue, DateTime assignedAtUtc)
    {
        if (!IsNeighborhoodDelivery(request) || !HasActiveAssignment(request, queue)) return null;
        return new 운송이벤트
        {
            의뢰Id = request.의뢰Id, 이벤트타입 = AssignmentEventType, 이벤트시각 = assignedAtUtc,
            메타데이터 = JsonSerializer.Serialize(new AssignmentEvidence
            {
                SchemaVersion = 1, RequestId = request.의뢰Id, DriverId = queue.확정기사Id!,
                RecommendationRound = queue.추천라운드, AssignedAtUtc = assignedAtUtc
            }, JsonOptions)
        };
    }

    public static bool IsClosed(화주운송의뢰 request, 운송원장? queue)
        => request.상태 != 상태값.의뢰상태.생성됨
           || request.배차상태 is 상태값.배차상태.취소 or 상태값.배차상태.인수완료 or 상태값.배차상태.하차완료
           || queue?.상태 is 상태값.배차상태.취소 or 상태값.배차상태.인수완료 or 상태값.배차상태.하차완료
           || queue?.배차큐단계 == 상태값.배차큐단계.종료;

    public static bool HasActiveAssignment(화주운송의뢰 request, 운송원장? queue)
        => !IsClosed(request, queue) && !string.IsNullOrWhiteSpace(queue?.확정기사Id)
           && queue.의뢰Id == request.의뢰Id && queue.배차업무유형 == 상태값.배차업무유형.용달운송
           && queue.배차큐단계 == 상태값.배차큐단계.확정;

    public static DateTime? CurrentAssignmentAt(화주운송의뢰 request, 운송원장? queue, 운송이벤트? latestEvent, DateTime nowUtc)
    {
        if (!IsNeighborhoodDelivery(request) || !HasActiveAssignment(request, queue)
            || latestEvent?.의뢰Id != request.의뢰Id || latestEvent.이벤트타입 != AssignmentEventType) return null;
        try
        {
            var evidence = JsonSerializer.Deserialize<AssignmentEvidence>(latestEvent.메타데이터, JsonOptions);
            return evidence is { SchemaVersion: 1 } && evidence.RequestId == request.의뢰Id
                && evidence.DriverId == queue!.확정기사Id && evidence.RecommendationRound == queue.추천라운드
                && evidence.AssignedAtUtc != default && evidence.AssignedAtUtc <= nowUtc
                && evidence.AssignedAtUtc == latestEvent.이벤트시각
                ? evidence.AssignedAtUtc : null;
        }
        catch (JsonException) { return null; }
    }

    public static bool IsRecent(기사위치기록? location, string driverId, DateTime assignmentAtUtc, DateTime nowUtc)
        => location is not null && location.기사Id == driverId && CoordinatesValid(location.위도, location.경도)
           && (location.정확도_m is null or >= 0)
           && location.기록시각 >= assignmentAtUtc && location.CreatedAt >= assignmentAtUtc
           && location.기록시각 <= nowUtc && location.CreatedAt <= nowUtc
           && location.기록시각 >= nowUtc - MaximumLocationAge && location.CreatedAt >= nowUtc - MaximumLocationAge;

    public static bool CoordinatesValid(decimal? latitude, decimal? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    public static async Task<NeighborhoodDeliveryLocationResult> ReadAsync(
        SsalddelContext db, 화주운송의뢰 request, 운송원장? queue, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        if (IsClosed(request, queue)) return new(NeighborhoodDeliveryMapStates.Closed, null);
        if (!HasActiveAssignment(request, queue)) return new(NeighborhoodDeliveryMapStates.NotAssigned, null);
        // 최신 근거가 손상되거나 현재 기사·추천 차수와 다르면 과거 허가로 돌아가지 않습니다.
        var latestEvent = await db.운송이벤트.AsNoTracking()
            .Where(x => x.의뢰Id == request.의뢰Id && x.이벤트타입 == AssignmentEventType)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        var assignmentAt = CurrentAssignmentAt(request, queue, latestEvent, nowUtc);
        if (!assignmentAt.HasValue) return new(NeighborhoodDeliveryMapStates.AssignmentEvidenceMissing, null);
        var driverId = queue!.확정기사Id!;
        var cutoff = nowUtc - MaximumLocationAge;
        var location = await db.기사위치기록.AsNoTracking()
            .Where(x => x.기사Id == driverId && x.CreatedAt >= assignmentAt.Value && x.기록시각 >= assignmentAt.Value
                && x.CreatedAt >= cutoff && x.기록시각 >= cutoff && x.CreatedAt <= nowUtc && x.기록시각 <= nowUtc
                && x.위도 >= -90 && x.위도 <= 90 && x.경도 >= -180 && x.경도 <= 180
                && (!x.정확도_m.HasValue || x.정확도_m.Value >= 0))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
        if (location is not null)
        {
            // 조회 중 배정이 바뀌면 이전 기사의 좌표도 버립니다.
            var currentRequest = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == request.의뢰Id, cancellationToken);
            var currentQueue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == request.의뢰Id, cancellationToken);
            if (currentRequest is null || IsClosed(currentRequest, currentQueue)) return new(NeighborhoodDeliveryMapStates.Closed, null);
            if (!HasActiveAssignment(currentRequest, currentQueue)) return new(NeighborhoodDeliveryMapStates.NotAssigned, null);
            if (currentQueue!.확정기사Id != driverId || currentQueue.추천라운드 != queue.추천라운드)
                return new(NeighborhoodDeliveryMapStates.AssignmentEvidenceMissing, null);
        }
        return IsRecent(location, driverId, assignmentAt.Value, nowUtc)
            ? new(NeighborhoodDeliveryMapStates.Available, location, assignmentAt)
            : new(NeighborhoodDeliveryMapStates.NoRecentLocation, null, assignmentAt);
    }

    private sealed class AssignmentEvidence
    {
        public AssignmentEvidence() { }
        public int SchemaVersion { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string DriverId { get; set; } = string.Empty;
        public int RecommendationRound { get; set; }
        public DateTime AssignedAtUtc { get; set; }
    }
}

public sealed record NeighborhoodDeliveryLocationResult(string StateCode, 기사위치기록? Location, DateTime? AssignedAtUtc = null);
