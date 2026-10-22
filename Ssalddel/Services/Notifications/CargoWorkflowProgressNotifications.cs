using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Shipper.Request;
using 살뜰.Data;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Notifications;

/// <summary>기존 화물 알림의 업무 식별자를 보존하며, 호출자의 저장에 진행 알림 의도만 보탠다.</summary>
public static class CargoWorkflowProgressNotifications
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? StageForState(string state) => state switch
    {
        "상차지도착" => "cargo.arrived-pickup",
        "상차완료" => "cargo.picked-up",
        "하차지도착" => "cargo.arrived-dropoff",
        "인수완료" => "cargo.delivered",
        _ => null
    };

    public static string Recipient(화주운송의뢰 source)
        => string.IsNullOrWhiteSpace(source.주문자UserId) ? source.화주Id : source.주문자UserId;

    public static bool IsCurrentDriver(운송원장 transport, string driverId)
        => !string.IsNullOrWhiteSpace(driverId)
            && (!string.IsNullOrWhiteSpace(transport.기사_운송자) || !string.IsNullOrWhiteSpace(transport.확정기사Id))
            && (string.IsNullOrWhiteSpace(transport.기사_운송자) || transport.기사_운송자 == driverId)
            && (string.IsNullOrWhiteSpace(transport.확정기사Id) || transport.확정기사Id == driverId);

    /// <summary>실패한 호출이 직접 예약한 새 행만 정리한다. 다른 작업의 변경·트랜잭션은 건드리지 않는다.</summary>
    public static void DiscardUncommitted(SsalddelContext db, Command알림Outbox? reserved)
    {
        if (reserved is not null && db.Entry(reserved).State == EntityState.Added)
            db.Entry(reserved).State = EntityState.Detached;
    }

    public static async Task<Command알림Outbox?> ReserveAsync(SsalddelContext db, string stageCode,
        화주운송의뢰 source, long transportId, string driverId, DateTime occurredAtUtc,
        string traceId, CancellationToken cancellationToken = default)
    {
        var recipient = Recipient(source);
        if (string.IsNullOrWhiteSpace(recipient) || recipient == driverId) return null;
        var row = Create(stageCode, source, transportId, driverId, occurredAtUtc, traceId);
        if (db.Command알림Outbox.Local.Any(existing => SameOccurrence(existing, row, source.의뢰Id, transportId))) return null;

        // 기존 payload에는 eventId가 없다. 같은 의뢰·운송의 이전 알림도 재발송하지 않는다.
        var requestFragment = "\"requestId\":" + JsonSerializer.Serialize(source.의뢰Id, JsonOptions);
        var existingRows = await db.Command알림Outbox.AsNoTracking()
            .Where(existing => existing.FeatureName == row.FeatureName && existing.Target == row.Target
                && existing.EventName == row.EventName && existing.PayloadJson.Contains(requestFragment))
            .ToArrayAsync(cancellationToken);
        if (existingRows.Any(existing => SameOccurrence(existing, row, source.의뢰Id, transportId))) return null;
        db.Command알림Outbox.Add(row);
        return row;
    }

    public static async Task<Command알림Outbox?> ReserveAcceptedAsync(SsalddelContext db,
        화주운송의뢰 source, string driverId, DateTime occurredAtUtc, string traceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Recipient(source))) return null;
        var row = Create("cargo.accepted", source, null, driverId, occurredAtUtc, traceId);
        if (db.Command알림Outbox.Local.Any(existing => SameAcceptance(existing, row, source.의뢰Id, driverId, occurredAtUtc))) return null;
        var requestFragment = "\"requestId\":" + JsonSerializer.Serialize(source.의뢰Id, JsonOptions);
        var existingRows = await db.Command알림Outbox.AsNoTracking()
            .Where(existing => existing.FeatureName == row.FeatureName && existing.EventName == row.EventName
                && existing.Target == row.Target && existing.PayloadJson.Contains(requestFragment))
            .ToArrayAsync(cancellationToken);
        if (existingRows.Any(existing => SameAcceptance(existing, row, source.의뢰Id, driverId, occurredAtUtc))) return null;
        db.Command알림Outbox.Add(row);
        return row;
    }

    /// <remarks>driverId는 기존 알림톡 변수에 필요한 비공개 행위자 참조다. 전화·주소가 아니며 FCM 진행 알림 데이터에는 전달하지 않는다.</remarks>
    public static Command알림Outbox Create(string stageCode, 화주운송의뢰 source, long? transportId,
        string driverId, DateTime occurredAtUtc, string traceId = "")
    {
        var stage = WorkflowProgressNotification.Get(stageCode);
        var recipient = Recipient(source);
        var descriptor = Descriptor(stageCode);
        occurredAtUtc = Utc(occurredAtUtc);
        // 일반 운송 단계는 같은 담당자의 같은 운송에서 한 번, 수락과 사건은 해당 발생에 결속한다.
        var occurrence = stageCode is "cargo.accepted" or "cargo.issue"
            ? $"{transportId}:{driverId}:{occurredAtUtc.Ticks}"
            : $"{transportId}:{driverId}";
        var eventId = 업무진행알림Recorder.EventId(stageCode, source.의뢰Id, occurrence, recipient);
        return new Command알림Outbox
        {
            CommandName = descriptor.CommandName, EventName = descriptor.EventName,
            FeatureName = descriptor.FeatureName, Target = "Shipper", TraceId = traceId,
            Status = "Pending", CreatedAt = occurredAtUtc, UpdatedAt = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                notificationType = WorkflowProgressNotification.MessageType,
                eventId, workflowKey = stage.WorkflowKey, stageCode, sourceId = source.의뢰Id,
                targetUserId = recipient, userId = recipient, requestId = source.의뢰Id, transportId, driverId,
                occurredAtUtc = occurredAtUtc.ToString("O", CultureInfo.InvariantCulture),
                deepLink = ShipperRequestDetailPageRoutes.SummaryFor(source.의뢰Id),
                title = stage.Title, body = stage.Body, channels = new[] { "Push", "AlimTalk" }
            }, JsonOptions)
        };
    }

    private static bool SameOccurrence(Command알림Outbox existing, Command알림Outbox proposed, string requestId, long transportId)
    {
        if (existing.FeatureName != proposed.FeatureName || existing.EventName != proposed.EventName || existing.Target != proposed.Target)
            return false;
        try
        {
            using var prior = JsonDocument.Parse(existing.PayloadJson);
            using var next = JsonDocument.Parse(proposed.PayloadJson);
            var root = prior.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            if (!root.TryGetProperty("requestId", out var request) || request.ValueKind != JsonValueKind.String
                || request.GetString() != requestId)
                return false;
            if (!root.TryGetProperty("transportId", out var transport))
                return existing.FeatureName == Command알림FeatureNames.상차접근;
            if (transport.ValueKind != JsonValueKind.Number || !transport.TryGetInt64(out var id) || id != transportId) return false;
            return !root.TryGetProperty("eventId", out var eventId)
                || eventId.ValueKind == JsonValueKind.String && eventId.GetString() == next.RootElement.GetProperty("eventId").GetString();
        }
        catch (JsonException) { return false; }
    }

    private static bool SameAcceptance(Command알림Outbox existing, Command알림Outbox proposed,
        string requestId, string driverId, DateTime occurredAtUtc)
    {
        if (existing.FeatureName != proposed.FeatureName || existing.EventName != proposed.EventName || existing.Target != proposed.Target)
            return false;
        try
        {
            using var prior = JsonDocument.Parse(existing.PayloadJson);
            using var next = JsonDocument.Parse(proposed.PayloadJson);
            var root = prior.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("requestId", out var request)
                || request.ValueKind != JsonValueKind.String || request.GetString() != requestId) return false;
            if (root.TryGetProperty("eventId", out var eventId))
                return eventId.ValueKind == JsonValueKind.String && eventId.GetString() == next.RootElement.GetProperty("eventId").GetString();
            if (!root.TryGetProperty("driverId", out var driver) || driver.ValueKind != JsonValueKind.String || driver.GetString() != driverId)
                return false;
            if (!root.TryGetProperty("acceptedAtUtc", out var accepted)) return true;
            return accepted.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(accepted.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
                && time.UtcTicks / 10 == Utc(occurredAtUtc).Ticks / 10;
        }
        catch (JsonException) { return false; }
    }

    private static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static (string CommandName, string EventName, string FeatureName) Descriptor(string code) => code switch
    {
        "cargo.accepted" => ("배차수락Command", "배차수락됨Event", Command알림FeatureNames.배차수락),
        "cargo.approaching" => ("위치갱신Command", "DriverApproachingPickup", Command알림FeatureNames.상차접근),
        "cargo.arrived-pickup" => ("운송상차지도착Command", "운송상차지도착됨Event", Command알림FeatureNames.운송상차지도착),
        "cargo.picked-up" => ("운송상차완료Command", "운송상차완료됨Event", Command알림FeatureNames.운송상차완료),
        "cargo.arrived-dropoff" => ("운송하차지도착Command", "운송하차지도착됨Event", Command알림FeatureNames.운송하차지도착),
        "cargo.delivered" => ("운송인수완료Command", "운송인수완료됨Event", Command알림FeatureNames.운송인수완료),
        "cargo.issue" => ("운송문제신고Command", "운송문제신고됨Event", Command알림FeatureNames.운송현장예외신고),
        _ => throw new ArgumentException("지원하지 않는 화물 진행 알림입니다.", nameof(code))
    };
}
