using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ssalddel.Contracts.Common.Warehouse;

public sealed class DistributedEvidenceWorkRequest
{
    public string Kind { get; set; } = "";
    public string AggregateId { get; set; } = "";
    public DistributedInboundReceiptBatchRequest? Receive { get; set; }
    public DistributedPickBatchRequest? Pick { get; set; }
    public DistributedDeliveryBatchRequest? Deliver { get; set; }
    public DistributedReturnBatchRequest? Return { get; set; }
    public DistributedExistingLotRequest? ImportLot { get; set; }
    public DistributedStockReleaseHandoverRequest? StockRelease { get; set; }
}
public sealed record DistributedEvidenceBinding
{
    public string ActorUserId { get; init; } = "";
    public Guid RequestId { get; init; }
    public string Kind { get; init; } = "";
    public string AggregateId { get; init; } = "";
    public long ExpectedRevision { get; init; }
    public string ScopeId { get; init; } = "";
    public string InputDigest { get; init; } = "";
    public string LinesDigest { get; init; } = "";
    public IReadOnlyList<string> ParticipantUserIds { get; init; } = [];
    public IReadOnlyList<string> LotIds { get; init; } = [];
    public IReadOnlyList<string> OrderIds { get; init; } = [];
    public IReadOnlyList<string> SiteIds { get; init; } = [];
}
public sealed class DistributedPhotoUploadRequest
{
    public Guid UploadRequestId { get; set; }
    public DistributedEvidenceWorkRequest Work { get; set; } = new();
    public string ContentType { get; set; } = "image/jpeg";
    public string ImageBase64 { get; set; } = "";
    public DateTime? CapturedAtUtc { get; set; }
}
public sealed record DistributedPhotoEvidenceDto(string EvidenceId, string Kind, string AggregateId,
    Guid CompletionRequestId, string BindingDigest, string StatusCode, DateTime UploadedAtUtc,
    bool ReadAllowed, string ContentType, long Bytes, string InputDigest = "");
public static class DistributedEvidenceDigests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static string Payload(object value)
    {
        var node = JsonSerializer.SerializeToNode(value, value.GetType(), Json)!;
        Strip(node);
        return Hash(node.ToJsonString(Json));
    }
    private static void Strip(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("photoEvidenceIds");
            foreach (var value in obj.Select(pair => pair.Value).Where(value => value is not null).ToArray()) Strip(value!);
        }
        else if (node is JsonArray array)
            foreach (var value in array.Where(value => value is not null)) Strip(value!);
    }
    public static string Binding(DistributedEvidenceBinding b) => Payload(new
    {
        b.ActorUserId, b.RequestId, b.Kind, b.AggregateId, b.ExpectedRevision, b.ScopeId, b.InputDigest, b.LinesDigest,
        participantUserIds = b.ParticipantUserIds.Order(StringComparer.Ordinal).ToArray(),
        lotIds = b.LotIds.Order(StringComparer.Ordinal).ToArray(), orderIds = b.OrderIds.Order(StringComparer.Ordinal).ToArray(),
        siteIds = b.SiteIds.Order(StringComparer.Ordinal).ToArray()
    });
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
