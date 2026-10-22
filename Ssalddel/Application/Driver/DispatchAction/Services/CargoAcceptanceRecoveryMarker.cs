using System.Text.Json;
using System.Text.Json.Nodes;
using 살뜰.도메인.운송;

namespace Ssalddel.Application.Driver.DispatchAction;

public sealed class CargoAcceptanceRecoveryOptions
{
    public const string SectionName = "CargoAcceptanceRecovery";
    public bool Enabled { get; set; }
    public int BatchSize { get; set; } = 20;
    public int IntervalSeconds { get; set; } = 15;
    public int MaximumAttempts { get; set; } = 5;
    public int LeaseSeconds { get; set; } = 120;
    public int RetrySeconds { get; set; } = 30;
}

internal sealed class CargoAcceptanceRecoveryJob
{
    public int Version { get; set; } = 1;
    public long TransportId { get; set; }
    public string DriverId { get; set; } = "";
    public int RecommendationRound { get; set; }
    public string State { get; set; } = "Pending";
    public int Attempts { get; set; }
    public int TotalAttempts { get; set; }
    public int ManualRetries { get; set; }
    public int Revision { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public bool PromisePrepared { get; set; }
    public string PromisePreparationCode { get; set; } = "";
    public bool NotificationPrepared { get; set; }
    public bool RecommendationRefreshPrepared { get; set; }
    public string RecommendationRefreshCode { get; set; } = "";
    public string ReasonCode { get; set; } = "";
}

/// <summary>주소·연락처 사본 없이 수락 SQL transaction에 결속하는 후속 처리 의도입니다.</summary>
public static class CargoAcceptanceRecoveryMarker
{
    public const string EventType = "CargoDispatchAcceptanceFollowups";
    internal const string Key = "cargoAcceptanceFollowups";
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static 운송이벤트 Create(운송원장 transport, DateTime now)
        => new()
        {
            의뢰Id = transport.의뢰Id, 이벤트타입 = EventType, 이벤트시각 = now,
            메타데이터 = Write("{}", new()
            {
                TransportId = transport.Id, DriverId = transport.확정기사Id ?? "",
                RecommendationRound = transport.추천라운드, NextAttemptAtUtc = now, UpdatedAtUtc = now
            })
        };

    internal static CargoAcceptanceRecoveryJob? Read(string value)
    {
        try { return JsonNode.Parse(value)?[Key]?.Deserialize<CargoAcceptanceRecoveryJob>(Json) is { Version: 1 } job ? job : null; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { return null; }
    }

    internal static string Write(string value, CargoAcceptanceRecoveryJob job)
    {
        var root = JsonNode.Parse(value) as JsonObject ?? throw new InvalidOperationException("CargoAcceptanceMetadataInvalid");
        root[Key] = JsonSerializer.SerializeToNode(job, Json);
        return root.ToJsonString();
    }
}
