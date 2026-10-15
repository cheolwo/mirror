using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace 살뜰.도메인.마트;

public sealed record 마트고객수령근거Envelope
{
    public int Version { get; init; } = 1;
    public 마트고객수령확정근거? ConfirmedProof { get; init; }
    public 마트고객수령보류근거? Hold { get; init; }
    // 원수령·보류 기록을 수정하지 않는 실제 당사자의 해결 진술 이력입니다.
    public IReadOnlyList<마트수령문제해결근거> Resolutions { get; init; } = [];

    public static 마트고객수령근거Envelope? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 65_536) return null;
        try { return JsonSerializer.Deserialize<마트고객수령근거Envelope>(json); }
        catch (JsonException) { return null; }
    }
    public string Write() => JsonSerializer.Serialize(this);
    public static string Digest<T>(T input)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(input))));
    public static string ConfirmDigest(long orderId, Guid requestId, string attemptId, long revision, bool full)
        => Digest(new { Kind = "ConfirmReceipt", OrderId = orderId, ClientRequestId = requestId,
            ExpectedAttemptStableId = attemptId, ExpectedRevision = revision, FullReceiptConfirmed = full });
    public static string IssueDigest(long orderId, Guid requestId, string attemptId, long revision, string reason)
        => Digest(new { Kind = "ReportIssue", OrderId = orderId, ClientRequestId = requestId,
            ExpectedAttemptStableId = attemptId, ExpectedRevision = revision, ReasonCode = reason });
}

public sealed record 마트고객수령행근거(long MartLineId, long OutboundId, long InventoryItemId,
    long WarehouseId, string Sku, string ProductName, int Quantity, long HistoryId, long MovementId,
    DateTime OutboundAtUtc, DateTime HistoryAtUtc, DateTime MovementAtUtc);

public sealed record 마트고객수령확정근거
{
    public string ReceiptId { get; init; } = "";
    public long MartOrderId { get; init; }
    public string OrderReference { get; init; } = "";
    public string BuyerUserId { get; init; } = "";
    public string AttemptStableId { get; init; } = "";
    public long DeliveredRevision { get; init; }
    public long ConfirmedRevision { get; init; }
    public DateTime DeliveredAtUtc { get; init; }
    public DateTime ConfirmedAtUtc { get; init; }
    public string LabelId { get; init; } = "";
    public long LabelRevision { get; init; }
    public DateTime PickedUpAtUtc { get; init; }
    public long ConfirmedQuantity { get; init; }
    public string SourceFingerprint { get; init; } = "";
    public IReadOnlyList<마트고객수령행근거> Lines { get; init; } = [];
    public Guid ClientRequestId { get; init; }
    public string InputDigest { get; init; } = "";
}

public sealed record 마트고객수령보류근거
{
    public long MartOrderId { get; init; }
    public string OrderReference { get; init; } = "";
    public string BuyerUserId { get; init; } = "";
    public string AttemptStableId { get; init; } = "";
    public long ExpectedRevision { get; init; }
    public long ResultRevision { get; init; }
    public string ReasonCode { get; init; } = "";
    public DateTime RecordedAtUtc { get; init; }
    public Guid ClientRequestId { get; init; }
    public string InputDigest { get; init; } = "";
}
