using System.Text.Json;

namespace 홍달.Services.Notifications;

public static class Command알림FeatureNames
{
    public const string 배차수락 = "DispatchAccepted";
    public const string 상차접근 = "DispatchPickupApproach";
    public const string 운송완료입금요청 = "TransportSettlementDepositReminder";
    public const string 운송상차지도착 = "TransportArrivedPickup";
    public const string 운송상차완료 = "TransportPickupCompleted";
    public const string 운송하차지도착 = "TransportArrivedDropoff";
    public const string 운송인수완료 = "TransportDropoffCompleted";
    public const string 운송현장예외신고 = "TransportFieldIssueReported";
    public const string 공동수입원장등록 = "CommunityGroupImportRegistered";
    public const string 공동구매원장변경 = "CommunityGroupPurchaseLedgerChanged";

    public static readonly string[] 발송지원목록 =
    [
        배차수락,
        상차접근,
        운송완료입금요청,
        운송상차지도착,
        운송상차완료,
        운송하차지도착,
        운송인수완료,
        운송현장예외신고,
        공동수입원장등록,
        공동구매원장변경
    ];
}

public static class Command알림TargetNames
{
    public const string 공동구매원장관계자 = "CommunityLedgerStakeholder";
}

public sealed record Command알림Payload(
    string NotificationType,
    string TargetUserId,
    string DriverId,
    string RequestId,
    string PaymentId,
    string OrderId,
    string LedgerId,
    string HsCodes,
    string DeepLink,
    string PaymentFlow,
    int Amount,
    int ReminderDay,
    string CargoType,
    string PickupAddress,
    string DropoffAddress,
    string PickupContactPhone,
    string RecipientPhone,
    string PickupWindowText,
    string Title,
    string Body,
    DateTime? ScheduledAtUtc,
    IReadOnlySet<string> Channels)
{
    public string AmountText => Amount <= 0 ? string.Empty : $"{Amount:N0}원";

    public bool IsScheduledForFuture(DateTime nowUtc)
        => ScheduledAtUtc is { } scheduledAtUtc && scheduledAtUtc > nowUtc;

    public static Command알림Payload Parse(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var pickupWindowStart = ReadString(root, "pickupWindowStartUtc");
        var pickupWindowEnd = ReadString(root, "pickupWindowEndUtc");
        var pickupWindowText = string.IsNullOrWhiteSpace(pickupWindowStart) && string.IsNullOrWhiteSpace(pickupWindowEnd)
            ? "상차 시간 협의"
            : $"{pickupWindowStart} ~ {pickupWindowEnd}";

        return new Command알림Payload(
            ReadString(root, "notificationType", ReadString(root, "알림유형", Command알림FeatureNames.배차수락)),
            ReadString(
                root,
                "targetUserId",
                ReadString(root, "targetBrokerParticipantId", ReadString(root, "참여자Id", ReadString(root, "shipperUserId")))),
            ReadString(root, "driverId"),
            ReadString(root, "requestId"),
            ReadString(root, "paymentId"),
            ReadString(root, "orderId"),
            ReadString(root, "ledgerId"),
            ReadString(root, "hsCodes"),
            ReadString(root, "deepLink"),
            ReadString(root, "paymentFlow"),
            ReadInt(root, "amount"),
            ReadInt(root, "reminderDay"),
            ReadString(root, "cargoType"),
            ReadString(root, "pickupAddress"),
            ReadString(root, "dropoffAddress"),
            ReadString(root, "pickupContactPhone"),
            ReadString(root, "recipientPhone", ReadString(root, "pickupContactPhone")),
            pickupWindowText,
            ReadString(root, "title", "기사님이 운송 의뢰를 수락했습니다."),
            ReadString(root, "body", "기사님이 운송 의뢰를 수락했습니다. 상차 준비를 확인해 주세요."),
            ReadDateTime(root, "scheduledAtUtc"),
            ReadStringSet(root, "channels", new[] { "Push" }));
    }

    private static string ReadString(JsonElement root, string propertyName, string fallback = "")
    {
        if (!TryGetProperty(root, propertyName, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return fallback;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : value.ToString();
    }

    private static bool TryGetProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IReadOnlySet<string> ReadStringSet(JsonElement root, string propertyName, IReadOnlyList<string> fallback)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(fallback, StringComparer.OrdinalIgnoreCase);
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            {
                result.Add(item.GetString()!);
            }
        }

        return result.Count == 0
            ? new HashSet<string>(fallback, StringComparer.OrdinalIgnoreCase)
            : result;
    }

    private static int ReadInt(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return 0;
        }

        if (value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static DateTime? ReadDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var parsed))
        {
            return parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime();
        }

        return DateTime.TryParse(value.ToString(), out parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }
}
