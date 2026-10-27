using System.Text.Json;
using System.Text.Json.Nodes;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    private static JsonObject StockReleaseRoot(분산거점Record site)
    {
        try { return JsonNode.Parse(site.PrivateDetailsJson) as JsonObject ?? throw new JsonException(); }
        catch (JsonException) { throw new DistributedFulfillmentException("DistributedStockReleaseMetadataInvalid", 503); }
    }
    private static JsonObject? StockReleaseEnvelope(JsonObject root)
    {
        if (!root.TryGetPropertyValue("stockRelease", out var node)) return null;
        Require(node is JsonObject envelope && envelope["schemaVersion"] is JsonValue schema
            && schema.TryGetValue<int>(out var version) && version == 1
            && envelope["plans"] is JsonArray, "DistributedStockReleaseMetadataInvalid", 503);
        return (JsonObject)node!;
    }
    private static IReadOnlyList<StockReleasePlan> StockReleases(분산거점Record site)
    {
        var envelope = StockReleaseEnvelope(StockReleaseRoot(site));
        if (envelope is null) return [];
        try
        {
            var plans = envelope["plans"]!.Deserialize<List<StockReleasePlan>>(Json) ?? throw new JsonException();
            Require(plans.Count <= 200 && plans.All(x => x is not null)
                && plans.Select(x => x.ReleaseId).Distinct(StringComparer.Ordinal).Count() == plans.Count
                && plans.All(x => !string.IsNullOrWhiteSpace(x.ReleaseId) && !string.IsNullOrWhiteSpace(x.LotId)
                    && !string.IsNullOrWhiteSpace(x.GoodsOwnerUserId) && !string.IsNullOrWhiteSpace(x.SpaceOwnerUserId)
                    && !string.IsNullOrWhiteSpace(x.AssignedUserId) && x.AgreedQuantity > 0
                    && x.ReleasedQuantity >= 0 && x.ReleasedQuantity <= x.AgreedQuantity
                    && Ssalddel.Contracts.Common.Warehouse.DistributedStockResolutionKinds.Known(x.ResolutionKind)
                    && x.StatusCode is "AwaitingConsent" or "Confirmed" or "PartiallyReleased" or "Completed" or "Cancelled"),
                "DistributedStockReleaseMetadataInvalid", 503);
            return plans;
        }
        catch (JsonException) { throw new DistributedFulfillmentException("DistributedStockReleaseMetadataInvalid", 503); }
    }
    private static StockReleasePlan StockRelease(분산거점Record site, string id)
        => StockReleases(site).SingleOrDefault(x => x.ReleaseId == id)
            ?? throw new DistributedFulfillmentException("DistributedStockReleaseNotFound", 404);
    private void SaveStockRelease(분산거점Record site, StockReleasePlan plan, string actor, string action, int quantity = 0, string? resolutionEvidence = null)
    {
        var root = StockReleaseRoot(site);
        var envelope = StockReleaseEnvelope(root);
        if (envelope is null)
        {
            envelope = new JsonObject { ["schemaVersion"] = 1, ["plans"] = new JsonArray() };
            root["stockRelease"] = envelope;
        }
        var plans = (JsonArray)envelope["plans"]!;
        var existing = plans.OfType<JsonObject>().SingleOrDefault(x => x["releaseId"]?.GetValue<string>() == plan.ReleaseId);
        if (existing is null) { existing = new JsonObject(); plans.Add(existing); }
        // 기존 공간 상세와 알 수 없는 envelope/계획/이력 필드는 그대로 남깁니다.
        var known = JsonSerializer.SerializeToNode(plan, Json)!.AsObject();
        foreach (var property in known) existing[property.Key] = property.Value?.DeepClone();
        if (existing["history"] is null) existing["history"] = new JsonArray();
        Require(existing["history"] is JsonArray, "DistributedStockReleaseMetadataInvalid", 503);
        Require(((JsonArray)existing["history"]!).Count < 500 || action is "HandedOver" or "Withdrawn" or "CancelledRemaining",
            "DistributedStockReleaseHistoryLimitReached");
        ((JsonArray)existing["history"]!).Add(JsonSerializer.SerializeToNode(new { actorUserId = actor, action, quantity, atUtc = Now, resolutionEvidenceReference = resolutionEvidence }, Json));
        site.PrivateDetailsJson = root.ToJsonString(Json);
    }
    private void SaveStockReleaseClosure(분산거점Record site, string actor, string evidence)
    {
        var root = StockReleaseRoot(site);
        // 종료 근거도 기존 메타데이터를 덮어쓰지 않는 독립 필드로 보존합니다.
        Require(!root.ContainsKey("stockReleaseClosure"), "DistributedSiteClosed");
        root["stockReleaseClosure"] = JsonSerializer.SerializeToNode(new { actorUserId = actor, evidenceReference = evidence, atUtc = Now }, Json);
        site.PrivateDetailsJson = root.ToJsonString(Json);
    }
    private sealed class StockReleasePlan
    {
        public string ReleaseId { get; set; } = "";
        public string LotId { get; set; } = "";
        public string LocationId { get; set; } = "";
        public string GoodsOwnerUserId { get; set; } = "";
        public string SpaceOwnerUserId { get; set; } = "";
        public string AssignedUserId { get; set; } = "";
        public int AgreedQuantity { get; set; }
        public int ReleasedQuantity { get; set; }
        public bool GoodsOwnerConsented { get; set; }
        public bool SpaceOwnerConsented { get; set; }
        public string HandoverTerms { get; set; } = "";
        public string Reason { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public string? ResolutionKind { get; set; }
        public string StatusCode { get; set; } = "AwaitingConsent";
        public DateTime CreatedAtUtc { get; set; }
    }
}
