using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Notifications;
using 살뜰.Data;
using 살뜰.도메인.설정;

namespace 살뜰.Services.Notifications;

/// <summary>호출자의 전문 원장 저장에 알림 의도만 결속한다. 저장/발송/상태 변경을 대신하지 않는다.</summary>
public static class 업무진행알림Recorder
{
    public const string FeatureName = WorkflowProgressNotification.MessageType;
    public const string Target = "WorkflowParticipant";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Command알림Outbox? Reserve(SsalddelContext db, string stageCode, string sourceId,
        string occurrenceKey, string recipientUserId, string deepLink, DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId)) return null;
        var stage = WorkflowProgressNotification.Get(stageCode);
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 200
            || string.IsNullOrWhiteSpace(occurrenceKey) || occurrenceKey.Length > 300
            || recipientUserId.Length > 450 || !WorkflowProgressNotification.IsSafeRoute(deepLink))
            throw new ArgumentException("업무 알림의 원천·발생·수신자·경로가 올바르지 않습니다.");
        var key = EventId(stageCode, sourceId, occurrenceKey, recipientUserId);
        if (db.Command알림Outbox.Local.Any(row => row.FeatureName == FeatureName && row.TraceId == key)
            || db.Command알림Outbox.Any(row => row.FeatureName == FeatureName && row.TraceId == key)) return null;
        var row = new Command알림Outbox
        {
            CommandName = "WorkflowProgress", EventName = stageCode, FeatureName = FeatureName,
            Target = Target, TraceId = key, Status = "Pending",
            CreatedAt = occurredAtUtc, UpdatedAt = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                notificationType = WorkflowProgressNotification.MessageType,
                eventId = key, workflowKey = stage.WorkflowKey, stageCode, sourceId,
                targetUserId = recipientUserId, userId = recipientUserId,
                occurredAtUtc, deepLink, title = stage.Title, body = stage.Body,
                channels = new[] { "Push" }
            }, JsonOptions)
        };
        db.Command알림Outbox.Add(row);
        return row;
    }

    public static string EventId(string stageCode, string sourceId, string occurrenceKey, string recipientUserId)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new[] { "workflow-progress-v1", stageCode, sourceId, occurrenceKey, recipientUserId }))));
}
