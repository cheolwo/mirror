using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.창고;

namespace 살뜰.Services.Dispatch.Recommendation;

public sealed record 화물분산순차인계근거(string WaveId, long Revision, string 기사Id, string 후보의뢰Id, string 계획지문)
{
    public string? 실차근거지문 { get; init; }
}
public sealed record 화물분산순차인계결과(bool 분산업무있음, 화물분산순차인계근거? 근거 = null, string? 오류Code = null);

/// <summary>다중 상차를 소유한 기존 분산 전문 계획을 읽는다. 일반 B 우선 경로 또는 작업시간을 만들지 않는다.</summary>
public static class 화물분산순차인계Policy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<화물분산순차인계결과> 조회Async(SsalddelContext db, string driver, string candidateId,
        DateTime now, CancellationToken ct = default)
    {
        화물분산순차인계결과 Hold() => new(true, null, "FreightDistributedHandoff.EvidenceMissingOrChanged");
        var member = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => x.CargoRequestId == candidateId).ToArrayAsync(ct);
        if (member.Length == 0) return new(false);
        if (member.Length != 1 || member[0].StatusCode is "Cancelled" or "Delivered") return Hold();
        var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.WaveId == member[0].WaveId, ct);
        if (wave is null || wave.DriverId != driver || wave.Revision <= 0 || wave.StatusCode is not ("Planned" or "AcceptancePending")
            || now > wave.PickupEndUtc || now > wave.DeliveryEndUtc || wave.PickupServiceMinutes < 0 || wave.DeliveryServiceMinutes < 0
            || wave.TotalDistanceKm is not >= 0m || wave.TotalElapsedMinutes is not >= 0m) return Hold();
        var orders = await db.Set<분산배송주문Record>().AsNoTracking().Where(x => x.WaveId == wave.WaveId
            && x.StatusCode != "Cancelled" && x.StatusCode != "Delivered").OrderBy(x => x.OrderId).ToArrayAsync(ct);
        var ids = orders.Select(x => x.CargoRequestId).ToArray();
        if (ids.Length == 0 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
            || orders.Select(x => x.OrderId).Distinct(StringComparer.Ordinal).Count() != ids.Length) return Hold();
        var active = await db.운송원장.AsNoTracking().Where(x => (x.기사_운송자 == driver || x.확정기사Id == driver)
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != "인수완료").OrderBy(x => x.의뢰Id).ToArrayAsync(ct);
        if (active.Any(x => !ids.Contains(x.의뢰Id, StringComparer.Ordinal) || x.배차업무유형 != 상태값.배차업무유형.용달운송
            || x.상태 is "취소" or "반환대기" || !string.IsNullOrWhiteSpace(x.기사_운송자) && x.기사_운송자 != driver
            || !string.IsNullOrWhiteSpace(x.확정기사Id) && x.확정기사Id != driver)
            || active.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != active.Length) return Hold();
        var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        if (requests.Count != ids.Length || orders.Any(x => x.CargoEvidenceDigest != 분산물류UseCase.CargoDigest(requests[x.CargoRequestId]))) return Hold();
        var orderIdsForPlan = orders.Select(x => x.OrderId).ToArray();
        var reservations = await db.Set<분산재고예약Record>().AsNoTracking().Where(x => x.WaveId == wave.WaveId
            && Enumerable.Contains(orderIdsForPlan, x.OrderId)).OrderBy(x => x.ReservationId).ToArrayAsync(ct);
        if (reservations.Length == 0 || orders.Any(x => !reservations.Any(r => r.OrderId == x.OrderId))
            || reservations.Any(x => x.Quantity <= 0 || x.PickedQuantity < 0 || x.PickedQuantity > x.Quantity
                || x.ReturnedQuantity > 0 || x.ReleasedQuantity > 0)) return Hold();
        var lotIds = reservations.Select(x => x.LotId).Distinct(StringComparer.Ordinal).ToArray();
        var lots = await db.Set<분산재고LotRecord>().AsNoTracking().Where(x => Enumerable.Contains(lotIds, x.LotId))
            .ToDictionaryAsync(x => x.LotId, StringComparer.Ordinal, ct);
        if (lots.Count != lotIds.Length) return Hold();
        var siteIds = lots.Values.Select(x => x.SiteId).Distinct(StringComparer.Ordinal).ToArray();
        var sites = await db.Set<분산거점Record>().AsNoTracking().Where(x => Enumerable.Contains(siteIds, x.SiteId))
            .ToDictionaryAsync(x => x.SiteId, StringComparer.Ordinal, ct);
        if (sites.Count != siteIds.Length || sites.Values.Any(x => x.StatusCode != "Active")) return Hold();
        var warehouseIds = sites.Values.Select(x => x.WarehouseId).ToArray();
        var warehouses = await db.창고.AsNoTracking().Where(x => Enumerable.Contains(warehouseIds, x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (warehouses.Count != warehouseIds.Distinct().Count() || warehouses.Values.Any(x => !x.IsActive || x.위도 is null || x.경도 is null)) return Hold();
        DistributedStopDto[]? stops;
        try
        {
            using var route = JsonDocument.Parse(wave.RouteJson);
            using var evidence = JsonDocument.Parse(wave.RouteEvidenceJson);
            if (!Unique(route.RootElement) || !Unique(evidence.RootElement)) return Hold();
            stops = route.RootElement.Deserialize<DistributedStopDto[]>(Json);
            var root = evidence.RootElement;
            if (stops is null || stops.Length == 0 || stops.Any(x => x is null || string.IsNullOrWhiteSpace(x.StopId) || x.OrderIds is null) || root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("source", out var source) || source.GetString() != "ExistingTypedRouteProvider"
                || !root.TryGetProperty("feasibleAttemptCount", out var count) || !count.TryGetInt32(out var attempts) || attempts <= 0
                || !root.TryGetProperty("startsUtc", out var begins) || !begins.TryGetDateTime(out var starts)
                || !root.TryGetProperty("selected", out var chosen) || chosen.ValueKind != JsonValueKind.Array) return Hold();
            var pending = stops.Where(x => x.StatusCode == "Pending").OrderBy(x => x.Sequence).ToArray();
            if (pending.Length == 0 || stops.Select(x => x.StopId).Distinct(StringComparer.Ordinal).Count() != stops.Length
                || !stops.OrderBy(x => x.Sequence).Select(x => x.Sequence).SequenceEqual(Enumerable.Range(1, stops.Length))
                || chosen.EnumerateArray().Any(x => x.ValueKind != JsonValueKind.String)
                || !chosen.EnumerateArray().Select(x => x.GetString()).SequenceEqual(pending.Select(x => x.StopId))) return Hold();
            var orderIds = orders.Select(x => x.OrderId).ToHashSet(StringComparer.Ordinal);
            if (!orderIds.SetEquals(pending.SelectMany(x => x.OrderIds ?? []))) return Hold();
            foreach (var required in reservations.Where(x => x.PickedQuantity < x.Quantity)
                .Select(x => (x.OrderId, Site: lots[x.LotId].SiteId)).Distinct())
                if (pending.Count(x => x.Kind == "Pickup" && x.SiteId == required.Site && x.OrderIds.Contains(required.OrderId, StringComparer.Ordinal)) != 1) return Hold();
            var completed = starts;
            var pickedStops = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in stops.OrderBy(x => x.Sequence))
            {
                if (row.OrderIds is null || row.OrderIds.Count == 0 || row.OrderIds.Distinct(StringComparer.Ordinal).Count() != row.OrderIds.Count
                    || row.Kind is not ("Pickup" or "Delivery") || row.StatusCode is not ("Pending" or "Completed")) return Hold();
                if (row.StatusCode == "Completed")
                { if (row.Kind == "Pickup") pickedStops.Add(row.StopId); continue; }
                if (row.OrderIds.Any(x => !orderIds.Contains(x)) || row.Latitude is null || row.Longitude is null
                    || row.Latitude is < -90m or > 90m || row.Longitude is < -180m or > 180m
                    || row.ExpectedArrivalUtc < completed || row.ExpectedArrivalUtc < row.WindowStartUtc
                    || row.ServiceMinutes != (row.Kind == "Pickup" ? wave.PickupServiceMinutes : wave.DeliveryServiceMinutes)
                    || row.ServiceMinutes > (decimal)(DateTime.MaxValue - row.ExpectedArrivalUtc).TotalMinutes
                    || row.ExpectedArrivalUtc.AddMinutes((double)row.ServiceMinutes) > row.WindowEndUtc) return Hold();
                if (row.Kind == "Pickup")
                {
                    if (string.IsNullOrWhiteSpace(row.SiteId) || !sites.TryGetValue(row.SiteId, out var site)) return Hold();
                    var warehouse = warehouses[site.WarehouseId];
                    if (row.Latitude != warehouse.위도 || row.Longitude != warehouse.경도
                        || row.OrderIds.Any(order => !reservations.Any(r => r.OrderId == order && r.PickedQuantity < r.Quantity && lots[r.LotId].SiteId == row.SiteId))) return Hold();
                    pickedStops.Add(row.StopId);
                }
                else if (row.OrderIds.Any(order => !stops.Any(s => s.Kind == "Pickup" && s.OrderIds.Contains(order, StringComparer.Ordinal))
                    || stops.Where(s => s.Kind == "Pickup" && s.OrderIds.Contains(order, StringComparer.Ordinal)).Any(s => !pickedStops.Contains(s.StopId))))
                    return Hold();
                var rowSources = row.OrderIds.Select(order => requests[orders.Single(x => x.OrderId == order).CargoRequestId]).ToArray();
                var pickup = row.Kind == "Pickup";
                if (!pickup && rowSources.Any(x => row.Latitude != x.하차_위도 || row.Longitude != x.하차_경도)) return Hold();
                var expectedStart = pickup ? rowSources.Select(x => x.픽업_시간창_시작일시).Append(wave.PickupStartUtc).Max()
                    : rowSources.Where(x => x.하차_시간창_시작일시.HasValue).Select(x => x.하차_시간창_시작일시!.Value).Append(wave.DeliveryStartUtc).Max();
                if (!pickup && rowSources.Any(x => !x.하차_시간창_시작일시.HasValue || !x.하차_시간창_종료일시.HasValue)) return Hold();
                var expectedEnd = pickup ? rowSources.Select(x => x.픽업_시간창_종료일시).Append(wave.PickupEndUtc).Min()
                    : rowSources.Select(x => x.하차_시간창_종료일시!.Value).Append(wave.DeliveryEndUtc).Min();
                if (row.WindowStartUtc < expectedStart || row.WindowEndUtc > expectedEnd || row.WindowStartUtc >= row.WindowEndUtc
                    || row.ExpectedArrivalUtc < expectedStart || row.ExpectedArrivalUtc.AddMinutes((double)row.ServiceMinutes) > expectedEnd) return Hold();
                completed = row.ExpectedArrivalUtc.AddMinutes((double)row.ServiceMinutes);
            }
            if (orders.Any(x => pending.Count(s => s.Kind == "Delivery" && s.OrderIds.Contains(x.OrderId, StringComparer.Ordinal)) != 1)) return Hold();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or ArgumentException)
        { return Hold(); }
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            wave.WaveId, wave.Revision, wave.DriverId, wave.StatusCode, wave.RouteJson, wave.RouteEvidenceJson,
            wave.PickupServiceMinutes, wave.DeliveryServiceMinutes, wave.PickupStartUtc, wave.PickupEndUtc,
            wave.DeliveryStartUtc, wave.DeliveryEndUtc,
            Orders = orders.Select(x => new { x.OrderId, x.CargoRequestId, x.CargoEvidenceDigest, x.Revision, x.StatusCode }),
            Reservations = reservations.Select(x => new { x.ReservationId, x.OrderId, x.LotId, x.Quantity, x.PickedQuantity, x.DeliveredQuantity, x.ReturnedQuantity, x.ReleasedQuantity, x.Revision }),
            Sites = sites.Values.OrderBy(x => x.SiteId).Select(x => new { x.SiteId, x.WarehouseId, x.StatusCode, x.Revision }),
            Warehouses = warehouses.Values.OrderBy(x => x.Id).Select(x => new { x.Id, x.IsActive, x.위도, x.경도 }),
            Active = active.Select(x => new { x.Id, x.의뢰Id, x.상태, x.기사_운송자, x.확정기사Id, x.RowVersion, x.UpdatedAt })
        })));
        return new(true, new(wave.WaveId, wave.Revision, driver, candidateId, fingerprint));
    }

    public static async Task<bool> 현재근거확인Async(SsalddelContext db, 화물분산순차인계근거 expected, DateTime now, CancellationToken ct,
        I기사실차적재근거조회Service? vehicles = null)
    {
        // 범위 조회만으로 실차까지 확인한 인계가 되지 않는다. 적격성 검사가 같은 provider의 지문을 결속해야 한다.
        if (string.IsNullOrWhiteSpace(expected.실차근거지문)) return false;
        var current = await 조회Async(db, expected.기사Id, expected.후보의뢰Id, now, ct);
        if (current.근거 is null) return false;
        var actual = await (vehicles ?? new 기사실차적재근거조회Service(db)).조회Async(expected.기사Id, ct);
        if (actual.유효제원 is null || actual.근거지문 != expected.실차근거지문) return false;
        return current.근거 with { 실차근거지문 = expected.실차근거지문 } == expected;
    }

    private static bool Unique(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == element.EnumerateObject().Count()
            && element.EnumerateObject().All(x => Unique(x.Value)),
        JsonValueKind.Array => element.EnumerateArray().All(Unique),
        _ => true
    };
}
