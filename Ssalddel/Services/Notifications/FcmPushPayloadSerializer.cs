using System.Text.Json;

namespace 살뜰.Services.Notifications;

public static class FcmPushPayloadSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string SerializeHttpV1(FcmPushMessage message)
    {
        if (message.DataOnly)
            return JsonSerializer.Serialize(new
            {
                message = new { token = message.Token, data = message.Data,
                    android = new { priority = message.HighPriority ? "HIGH" : "NORMAL" } }
            }, JsonOptions);

        object? androidNotification = null;
        object? apns = null;
        if (!string.IsNullOrWhiteSpace(message.ImageUrl))
        {
            androidNotification = new { image = message.ImageUrl };
            apns = new
            {
                payload = new { aps = new Dictionary<string, object> { ["mutable-content"] = 1 } },
                fcm_options = new { image = message.ImageUrl }
            };
        }
        return JsonSerializer.Serialize(new
        {
            message = new
            {
                token = message.Token,
                notification = new { title = message.Title, body = message.Body },
                data = message.Data,
                android = new { priority = message.HighPriority ? "HIGH" : "NORMAL", notification = androidNotification },
                apns
            }
        }, JsonOptions);
    }

    public static string SerializeLegacy(FcmPushMessage message)
    {
        if (message.DataOnly)
            return JsonSerializer.Serialize(new
            {
                to = message.Token, priority = message.HighPriority ? "high" : "normal", data = message.Data
            }, JsonOptions);
        return JsonSerializer.Serialize(new
        {
            to = message.Token,
            priority = message.HighPriority ? "high" : "normal",
            notification = new { title = message.Title, body = message.Body, image = message.ImageUrl },
            data = message.Data
        }, JsonOptions);
    }
}
