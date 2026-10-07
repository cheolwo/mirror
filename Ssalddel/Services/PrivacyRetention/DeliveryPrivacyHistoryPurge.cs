using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 살뜰.Data;
using 살뜰.도메인.운송;

namespace Ssalddel.Services.PrivacyRetention;

internal static class 배송개인정보이력파기
{
    private static readonly HashSet<string> AllowedEventFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Action", "DriverId", "DeliveryAttemptId", "Code", "DistanceKm", "DriverLocationAtUtc", "Policy",
        "previousState", "targetState", "completionProjectionToken", "OrderNo", "RequestId", "TransportId",
        "ReasonCode", "StatusCode", "Amount", "Currency", "OccurredAtUtc", "Revision", "State", "PaymentStatus"
    };
    public static string MinimizeEventJson(string json)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return "{\"privacyOperationalCopyPurged\":true}";
        var result = doc.RootElement.EnumerateObject().Where(x => AllowedEventFields.Contains(x.Name)
            && x.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)).ToDictionary(x => x.Name, x => x.Value.Clone());
        return JsonSerializer.Serialize(result);
    }

    public static async Task 파기Async(SsalddelContext db, IReadOnlyList<운송원장> transports, IReadOnlyList<string> ledgerIds, CancellationToken ct)
    {
        var requests = transports.Select(x => x.의뢰Id).Distinct().ToArray();
        var events = await db.운송이벤트.Where(x => Enumerable.Contains(requests, x.의뢰Id)).ToListAsync(ct);
        foreach (var e in events)
            if (e.이벤트타입 != 운송이벤트유형.배차엔진판단감사) e.메타데이터 = MinimizeEventJson(e.메타데이터);
        var states = await db.커뮤니티원장상태이벤트.Where(x => Enumerable.Contains(ledgerIds, x.커뮤니티원장Id)).ToListAsync(ct);
        foreach (var state in states)
        {
            state.변경사유 = null;
            state.SnapshotJson = JsonSerializer.Serialize(new
            { state.커뮤니티원장Id, state.원장템플릿Key, state.EventType, state.이전상태, state.상태, state.현재단계Key, state.OccurredAtUtc, 개인정보비식별투영 = true });
        }
    }
}
