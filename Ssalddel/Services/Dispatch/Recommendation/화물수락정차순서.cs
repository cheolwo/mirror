using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Shipper.Request;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화물;
using 살뜰.도메인.화주;

namespace 살뜰.Services.Dispatch.Recommendation;

/// <summary>수락 직전 검사한 한 시도다. 표시 문자열에서 정차를 복원하지 않는다.</summary>
public sealed record 화물수락정차검증자료(
    IReadOnlyList<국내화물계획경유점> 정차목록,
    IReadOnlyDictionary<string, string> 조건지문,
    string 실차근거지문,
    화물구간적재검증결과 구간검증)
{
    public IReadOnlyDictionary<string, 화물상하차작업시간> 작업시간 { get; init; }
        = new Dictionary<string, 화물상하차작업시간>();
}

public sealed record 화물수락정차업무근거(long 운송Id, string 의뢰Id, string 조건지문, bool 이미상차됨);

/// <summary>기존 원장 JSON의 부가 근거다. 상태·완료·주소를 새 원장으로 복제하지 않는다.</summary>
public sealed record 화물수락정차순서기록
{
    public int 판본 { get; init; } = 1;
    public string 수락판본 { get; init; } = "";
    public string 기사Id { get; init; } = "";
    public DateTime 수락시각Utc { get; init; }
    public string 실차근거지문 { get; init; } = "";
    public IReadOnlyList<화물수락정차업무근거> 업무목록 { get; init; } = [];
    public IReadOnlyList<국내화물계획경유점> 정차목록 { get; init; } = [];
    public IReadOnlyDictionary<string, 화물상하차작업시간> 작업시간 { get; init; }
        = new Dictionary<string, 화물상하차작업시간>();
    public string 기록지문 { get; init; } = "";
}

public sealed record 화물수락정차저장준비결과(string? 오류Code, IReadOnlyList<(운송원장 원장, string Json)> 변경목록);

/// <summary>검증된 정차를 같은 SQL transaction의 기존 JSON에 보존하고 현재 미완료 정차에만 투영한다.</summary>
public static class 화물수락정차순서
{
    public const string JsonProperty = "수락정차순서";
    private static readonly string[] PickedStates = ["상차완료", "운송중", "하차지도착", "인수완료"];
    private static readonly string[] AllowedStates = ["확정", "배차확정", "매칭중", "상차지도착", "상차완료", "운송중", "하차지도착", "인수완료"];

    public static 화물수락정차검증자료? 검증자료작성(
        운송삽입시도결과 attempt,
        IReadOnlyDictionary<string, 화주운송의뢰> requests,
        IReadOnlyDictionary<string, 화물요구조건> cargo,
        IReadOnlyDictionary<string, 화물상하차작업시간>? handling,
        기사실차적재조회결과 vehicle,
        화물구간적재검증결과 capacity)
    {
        if (!attempt.전체완수가능여부 || !attempt.전체실제경로확인됨
            || string.IsNullOrWhiteSpace(attempt.경로옵션) || string.IsNullOrWhiteSpace(vehicle.근거지문)
            || !capacity.확인됨 || !capacity.적합여부 || handling is null || requests.Count == 0) return null;
        var rows = attempt.도착예상목록.OrderBy(x => x.순서).ToArray();
        if (rows.Any(x => x.예상도착시각 is null || x.시간위반여부 || !requests.ContainsKey(x.의뢰Id)
            || !handling.TryGetValue(x.의뢰Id, out var t) || t is null
            || (x.단계유형 == "pickup" ? t.상차분 : t.하차분) is not >= 0m)) return null;
        var stops = rows.Select(x => new 국내화물계획경유점(x.의뢰Id, x.단계유형, x.순서,
            x.예상도착시각, x.예상도착시각!.Value.AddMinutes((double)(x.단계유형 == "pickup"
                ? handling[x.의뢰Id].상차분!.Value : handling[x.의뢰Id].하차분!.Value)))).ToArray();
        return new(stops, requests.ToDictionary(x => x.Key, x => 조건지문(x.Value, cargo.GetValueOrDefault(x.Key)), StringComparer.Ordinal),
            vehicle.근거지문, capacity)
        {
            작업시간 = requests.Keys.Order(StringComparer.Ordinal).ToDictionary(x => x, x => handling[x], StringComparer.Ordinal)
        };
    }

    public static async Task<화물수락정차저장준비결과> 저장준비Async(SsalddelContext db, string driverId,
        운송원장 candidate, 화물수락정차검증자료 evidence, DateTime now, CancellationToken ct = default,
        I기사실차적재근거조회Service? 실차조회 = null)
    {
        화물수락정차저장준비결과 Fail(string code) => new("FreightAcceptedStopOrder." + code, []);
        if (evidence.조건지문.Count == 0 || !evidence.조건지문.ContainsKey(candidate.의뢰Id)
            || !evidence.구간검증.확인됨 || !evidence.구간검증.적합여부) return Fail("EvidenceMissing");
        var ids = evidence.조건지문.Keys.ToArray();
        var rows = await db.운송원장.Where(x => Enumerable.Contains(ids, x.의뢰Id)).ToArrayAsync(ct);
        if (rows.Length != ids.Length || rows.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != ids.Length
            || !rows.Any(x => x.Id == candidate.Id && x.의뢰Id == candidate.의뢰Id)) return Fail("SourceChanged");
        var activeIds = await db.운송원장.AsNoTracking().Where(x => (x.기사_운송자 == driverId || x.확정기사Id == driverId)
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != "인수완료" && x.의뢰Id != candidate.의뢰Id)
            .Select(x => x.의뢰Id).ToArrayAsync(ct);
        if (!activeIds.Order(StringComparer.Ordinal).SequenceEqual(ids.Where(x => x != candidate.의뢰Id).Order(StringComparer.Ordinal)))
            return Fail("ActiveWorkChanged");
        if (rows.Any(x => x.Id <= 0 || x.배차업무유형 != 상태값.배차업무유형.용달운송
            || x.Id != candidate.Id && (!Owned(x, driverId) || x.배차큐단계 != 상태값.배차큐단계.확정
                || !AllowedStates.Contains(x.상태, StringComparer.Ordinal) || x.상태 == "인수완료"))) return Fail("OwnershipChanged");
        var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        var cargo = await db.화물요구조건.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        if (requests.Count != ids.Length || requests.Any(x => evidence.조건지문[x.Key] != 조건지문(x.Value, cargo.GetValueOrDefault(x.Key))))
            return Fail("SourceChanged");
        var actualVehicle = await (실차조회 ?? new 기사실차적재근거조회Service(db)).조회Async(driverId, ct);
        if (actualVehicle.유효제원 is null || actualVehicle.근거지문 != evidence.실차근거지문) return Fail("VehicleChanged");
        // 차량 조회를 기다리는 동안 바뀐 약속·화물 근거를 예전 검증값으로 저장하지 않는다.
        var latestRequests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        var latestCargo = await db.화물요구조건.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        if (latestRequests.Count != ids.Length || latestRequests.Any(x => evidence.조건지문[x.Key] != 조건지문(x.Value, latestCargo.GetValueOrDefault(x.Key))))
            return Fail("SourceChanged");
        var jobs = rows.Select(x => new 화물수락정차업무근거(x.Id, x.의뢰Id, evidence.조건지문[x.의뢰Id],
            x.Id != candidate.Id && PickedStates.Contains(x.상태, StringComparer.Ordinal))).OrderBy(x => x.의뢰Id, StringComparer.Ordinal).ToArray();
        var record = new 화물수락정차순서기록
        {
            수락판본 = Guid.NewGuid().ToString("N"), 기사Id = driverId, 수락시각Utc = now,
            실차근거지문 = evidence.실차근거지문, 업무목록 = jobs, 정차목록 = evidence.정차목록.ToArray(),
            작업시간 = evidence.작업시간
        };
        if (!ValidStops(record)) return Fail("Invalid");
        record = record with { 기록지문 = RecordHash(record) };
        var updates = new List<(운송원장, string)>();
        foreach (var row in rows)
        {
            if (!TryObject(row.추천판정근거Json, out var root)) return Fail("Invalid");
            root![JsonProperty] = JsonSerializer.SerializeToNode(record);
            updates.Add((row, root.ToJsonString()));
        }
        return new(null, updates);
    }

    public static async Task<기사운송일정계획> 반영Async(SsalddelContext db, 기사운송일정계획 plan,
        IReadOnlyList<운송원장> active, CancellationToken ct = default, I기사실차적재근거조회Service? 실차조회 = null)
    {
        기사운송일정계획 Hold(string code) => plan with { 수락정차순서반영됨 = false, 수락정차순서보류Code = "FreightAcceptedStopOrder." + code };
        var records = new List<화물수락정차순서기록>();
        foreach (var row in active)
        {
            if (!TryObject(row.추천판정근거Json, out var root)) return Hold("Invalid");
            if (!root!.ContainsKey(JsonProperty)) continue;
            try
            {
                var item = root[JsonProperty]?.Deserialize<화물수락정차순서기록>();
                if (item is null || !ValidStops(item) || item.기록지문 != RecordHash(item)) return Hold("Invalid");
                records.Add(item);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
            { return Hold("Invalid"); }
        }
        if (records.Count == 0) return plan;
        if (records.Count != active.Count || records.Select(x => x.수락판본).Distinct(StringComparer.Ordinal).Count() != 1
            || records.Select(x => x.기록지문).Distinct(StringComparer.Ordinal).Count() != 1) return Hold("ActiveWorkChanged");
        var record = records[0];
        if (record.기사Id != plan.기사Id) return Hold("OwnershipChanged");
        var ids = record.업무목록.Select(x => x.의뢰Id).ToArray();
        var rows = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id)).ToArrayAsync(ct);
        var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        var cargo = await db.화물요구조건.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .ToDictionaryAsync(x => x.의뢰Id, StringComparer.Ordinal, ct);
        if (rows.Length != ids.Length || requests.Count != ids.Length || rows.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != ids.Length)
            return Hold("SourceChanged");
        var map = rows.ToDictionary(x => x.의뢰Id, StringComparer.Ordinal);
        foreach (var job in record.업무목록)
        {
            var row = map[job.의뢰Id];
            if (row.Id != job.운송Id || !Owned(row, plan.기사Id) || row.배차큐단계 != 상태값.배차큐단계.확정
                || row.배차업무유형 != 상태값.배차업무유형.용달운송) return Hold("OwnershipChanged");
            if (!AllowedStates.Contains(row.상태, StringComparer.Ordinal)
                || job.이미상차됨 && !PickedStates.Contains(row.상태, StringComparer.Ordinal)) return Hold("StateChanged");
            if (job.조건지문 != 조건지문(requests[job.의뢰Id], cargo.GetValueOrDefault(job.의뢰Id))) return Hold("SourceChanged");
        }
        if (!active.Select(x => x.Id).Order().SequenceEqual(rows.Where(x => x.상태 != "인수완료").Select(x => x.Id).Order()))
            return Hold("ActiveWorkChanged");
        var vehicle = await (실차조회 ?? new 기사실차적재근거조회Service(db)).조회Async(plan.기사Id, ct);
        if (vehicle.유효제원 is null || vehicle.근거지문 != record.실차근거지문) return Hold("VehicleChanged");
        var remaining = record.정차목록.Where(x => map[x.의뢰Id].상태 != "인수완료"
            && (x.단계유형 != "pickup" || !PickedStates.Contains(map[x.의뢰Id].상태, StringComparer.Ordinal))).ToArray();
        var current = plan.항목목록;
        if (current.Select(x => (x.의뢰Id, x.단계유형)).Distinct().Count() != current.Count
            || remaining.Length != current.Count) return Hold("Invalid");
        var currentMap = current.ToDictionary(x => (x.의뢰Id, x.단계유형));
        if (remaining.Any(x => !currentMap.ContainsKey((x.의뢰Id, x.단계유형)))) return Hold("Invalid");
        return plan with
        {
            항목목록 = remaining.Select((x, i) => currentMap[(x.의뢰Id, x.단계유형)] with
            {
                순서 = i,
                작업시간분 = x.단계유형 == "pickup" ? record.작업시간[x.의뢰Id].상차분 : record.작업시간[x.의뢰Id].하차분
            }).ToArray(),
            상하차작업시간 = active.Select(x => x.의뢰Id).ToDictionary(x => x, x => record.작업시간[x], StringComparer.Ordinal),
            수락정차순서반영됨 = true,
            수락정차순서보류Code = null
        };
    }

    private static bool Owned(운송원장 row, string driver) => (row.기사_운송자 == driver || row.확정기사Id == driver)
        && (string.IsNullOrWhiteSpace(row.기사_운송자) || row.기사_운송자 == driver)
        && (string.IsNullOrWhiteSpace(row.확정기사Id) || row.확정기사Id == driver);

    private static bool ValidStops(화물수락정차순서기록 r)
    {
        if (r.판본 != 1 || !Guid.TryParseExact(r.수락판본, "N", out _) || string.IsNullOrWhiteSpace(r.기사Id)
            || r.수락시각Utc == default || r.수락시각Utc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(r.실차근거지문)
            || r.업무목록 is null || r.정차목록 is null || r.작업시간 is null || r.업무목록.Count == 0
            || r.업무목록.Any(x => x is null || x.운송Id <= 0 || string.IsNullOrWhiteSpace(x.의뢰Id) || string.IsNullOrWhiteSpace(x.조건지문))
            || r.업무목록.Select(x => x.의뢰Id).Distinct(StringComparer.Ordinal).Count() != r.업무목록.Count
            || r.업무목록.Select(x => x.운송Id).Distinct().Count() != r.업무목록.Count
            || r.정차목록.Any(x => x is null || x.단계유형 is not ("pickup" or "dropoff") || !r.업무목록.Any(j => j.의뢰Id == x.의뢰Id)
                || x.도착시각Utc is null || x.출발시각Utc is null || x.출발시각Utc < x.도착시각Utc)
            || !r.정차목록.Select(x => x.순서).SequenceEqual(Enumerable.Range(0, r.정차목록.Count))) return false;
        foreach (var job in r.업무목록)
        {
            var pickup = r.정차목록.Where(x => x.의뢰Id == job.의뢰Id && x.단계유형 == "pickup").ToArray();
            var drop = r.정차목록.Where(x => x.의뢰Id == job.의뢰Id && x.단계유형 == "dropoff").ToArray();
            if (!r.작업시간.TryGetValue(job.의뢰Id, out var time) || time is null || time.하차분 is not >= 0m
                || !job.이미상차됨 && time.상차분 is not >= 0m
                || pickup.Length != (job.이미상차됨 ? 0 : 1) || drop.Length != 1
                || pickup.Length == 1 && pickup[0].순서 >= drop[0].순서) return false;
        }
        for (var i = 0; i < r.정차목록.Count; i++)
        {
            var stop = r.정차목록[i];
            var duration = r.작업시간[stop.의뢰Id];
            var minutes = stop.단계유형 == "pickup" ? duration.상차분!.Value : duration.하차분!.Value;
            if (minutes > (decimal)(DateTime.MaxValue - stop.도착시각Utc!.Value).TotalMinutes
                || stop.도착시각Utc.Value.AddMinutes((double)minutes) != stop.출발시각Utc
                || i > 0 && r.정차목록[i - 1].출발시각Utc > stop.도착시각Utc) return false;
        }
        return true;
    }

    private static bool TryObject(string? json, out JsonObject? root)
    {
        root = null;
        if (string.IsNullOrWhiteSpace(json)) { root = new(); return true; }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!UniqueProperties(document.RootElement)) return false;
            root = JsonNode.Parse(json) as JsonObject;
            return root is not null;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException) { return false; }
    }

    private static bool UniqueProperties(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() == element.EnumerateObject().Count()
            && element.EnumerateObject().All(x => UniqueProperties(x.Value)),
        JsonValueKind.Array => element.EnumerateArray().All(UniqueProperties),
        _ => true
    };

    private static string RecordHash(화물수락정차순서기록 r) => Hash(new
    { r.판본, r.수락판본, r.기사Id, r.수락시각Utc, r.실차근거지문, r.업무목록, r.정차목록, r.작업시간 });

    private static string 조건지문(화주운송의뢰 r, 화물요구조건? cargo) => Hash(new
    {
        r.의뢰Id, r.화주Id, r.픽업_도로명주소, r.픽업_위도, r.픽업_경도,
        픽업시작Ticks = r.픽업_시간창_시작일시.Ticks, 픽업종료Ticks = r.픽업_시간창_종료일시.Ticks,
        r.하차_도로명주소, r.하차_위도, r.하차_경도,
        하차시작Ticks = r.하차_시간창_시작일시?.Ticks, 하차종료Ticks = r.하차_시간창_종료일시?.Ticks,
        r.픽업_작업시간분, r.하차_작업시간분, r.운송방식, r.차량종류, r.서비스레벨, r.요청사항, r.화물파손주의여부,
        r.최종운임, r.운임구성Id, Loading = 화주운송의뢰매퍼.ReadLoadingEvidence(r).입력지문,
        r.화물부피Cbm, r.화물부피근거Code, r.화물적재계산판본,
        Cargo = cargo is null ? null : new
        {
            cargo.의뢰Id, cargo.화물길이Mm, cargo.화물폭Mm, cargo.화물높이Mm, cargo.화물무게Kg, cargo.팔레트개수,
            cargo.비맞으면안됨, cargo.냉장필요, cargo.냉동필요, cargo.리프트필요, cargo.측면상하차필요,
            cargo.장재물, cargo.혼적허용, cargo.독차필수, cargo.주의사항
        }
    });

    private static string Hash<T>(T value)
    {
        // 관계형 저장은 10m을 10.0m로 읽을 수 있다. 수치가 같은 scale 차이는 조건 변경이 아니다.
        using var document = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(value));
        using var bytes = new MemoryStream();
        using (var writer = new Utf8JsonWriter(bytes)) WriteCanonical(writer, document.RootElement);
        return Convert.ToHexStringLower(SHA256.HashData(bytes.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number when element.TryGetDecimal(out var number):
                writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
