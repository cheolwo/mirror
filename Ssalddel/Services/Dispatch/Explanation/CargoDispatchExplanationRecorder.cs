using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Dispatch;
using 살뜰.Data;
using 살뜰.Infrastructure.Security;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;

namespace 살뜰.Services.Dispatch.Explanation;

/// <summary>호출자가 저장하는 SQL 변경 집합에 불변 설명 사건만 추가한다. 직접 저장/배차/발송하지 않는다.</summary>
public sealed class CargoDispatchExplanationRecorder(SsalddelContext db, IPersonalDataEncryptionService protection)
    : ICargoDispatchExplanationRecorder
{
    public const string EventType = 운송이벤트유형.화물배차설명기록;

    public async Task<운송이벤트?> ReserveAsync(운송원장 queue, string driverId, DateTime decisionAtUtc,
        string methodCode, 국내화물배차판정근거? sourceEvidence, CancellationToken ct = default,
        CargoDispatchExplanationDecisionFacts? decisionFacts = null)
    {
        ct.ThrowIfCancellationRequested();
        CargoDispatchExplanationCodec.ValidateIdentifier(driverId, 450);
        CargoDispatchExplanationCodec.ValidateIdentifier(queue.의뢰Id, 200);
        if (queue.Id <= 0 || queue.배차업무유형 != 상태값.배차업무유형.용달운송
            || queue.추천라운드 <= 0 || queue.현재추천대상기사Id != driverId
            || queue.배차큐단계 != 상태값.배차큐단계.배차추천 || queue.배차노출상태 != 상태값.배차노출상태.추천중
            || decisionAtUtc == default
            || string.IsNullOrWhiteSpace(methodCode) || methodCode.Length > 250 || methodCode.Any(char.IsControl)
            || !(methodCode.StartsWith("engine/", StringComparison.Ordinal) || methodCode.StartsWith("coordination/", StringComparison.Ordinal)
                || methodCode.StartsWith("manual/", StringComparison.Ordinal)))
            throw new ArgumentException("CargoDispatchExplanationCaptureInvalid");
        if (decisionFacts is { OwnPickupOrder: <= 0 or > 1000 } or { OwnDropoffOrder: <= 0 or > 1000 })
            throw new ArgumentException("CargoDispatchExplanationCaptureInvalid");

        // Freeze supplied input references before the first await. Keep only this request/driver's
        // references, not a batch's other candidates, scores or unstructured reasons.
        var inputEvidence = CargoDispatchInputEvidence.Capture(sourceEvidence, queue.의뢰Id, driverId);

        var local = db.운송이벤트.Local.Where(x => x.의뢰Id == queue.의뢰Id && x.이벤트타입 == EventType).ToArray();
        var stored = await db.운송이벤트.AsNoTracking()
            .Where(x => x.의뢰Id == queue.의뢰Id && x.이벤트타입 == EventType).ToArrayAsync(ct);
        if (local.Concat(stored).Any(x =>
            CargoDispatchExplanationCodec.Read(x, protection) is var prior
            && prior.QueueId == queue.Id && prior.DriverId == driverId && prior.RecommendationRound == queue.추천라운드)) return null;

        var source = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == queue.의뢰Id, ct)
            ?? throw new CargoDispatchExplanationNotFoundException();
        var utc = CargoDispatchExplanationCodec.Utc(decisionAtUtc);
        var snapshot = new CargoDispatchExplanationSnapshot
        {
            RequestId = queue.의뢰Id, QueueId = queue.Id, DriverId = driverId,
            OwnerUserId = source.화주Id, OrdererUserId = source.주문자UserId,
            RecommendationRound = queue.추천라운드, OccurredAtUtc = utc, MethodCode = methodCode,
            DecisionFacts = decisionFacts,
            InputFingerprint = inputEvidence?.InputFingerprint, InputEvidence = inputEvidence,
            CargoWeightKg = source.화물중량Kg, TransportMode = SafeValue(source.운송방식, "독차", "혼적"),
            Temperature = SafeValue(source.화물온도조건, "상온", "냉장", "냉동", "실온"),
            PickupDeadlineUtc = Deadline(source.픽업_시간창_종료일시),
            DropoffDeadlineUtc = Deadline(source.하차_시간창_종료일시)
        };
        snapshot.Basis = CargoDispatchEvidenceLibrary.Capture(snapshot);
        var raw = JsonSerializer.Serialize(snapshot, CargoDispatchExplanationCodec.Json);
        var encoded = protection.Protect(raw);
        if (string.IsNullOrWhiteSpace(encoded) || encoded == raw) throw new CargoDispatchExplanationUnavailableException();
        var row = new 운송이벤트 { 의뢰Id = queue.의뢰Id, 이벤트타입 = EventType, 이벤트시각 = utc, 메타데이터 = encoded };
        db.운송이벤트.Add(row);
        return row;
    }

    public static void DiscardUncommitted(SsalddelContext db, 운송이벤트? reserved)
    {
        if (reserved is not null && db.Entry(reserved).State == EntityState.Added)
            db.Entry(reserved).State = EntityState.Detached;
    }

    private static string? SafeValue(string? value, params string[] allowed)
        => allowed.Contains(value, StringComparer.Ordinal) ? value : null;

    private static DateTime? Deadline(DateTime? value)
        => value is { } date && date != default ? CargoDispatchExplanationCodec.Utc(date) : null;
}

internal sealed class CargoDispatchExplanationSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public string RequestId { get; set; } = "";
    public long QueueId { get; set; }
    public string DriverId { get; set; } = "";
    public string? OwnerUserId { get; set; }
    public string? OrdererUserId { get; set; }
    public int RecommendationRound { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string MethodCode { get; set; } = "";
    public string? InputFingerprint { get; set; }
    public decimal? CargoWeightKg { get; set; }
    public string? TransportMode { get; set; }
    public string? Temperature { get; set; }
    public DateTime? PickupDeadlineUtc { get; set; }
    public DateTime? DropoffDeadlineUtc { get; set; }
    public CargoDispatchExplanationDecisionFacts? DecisionFacts { get; set; }
    // Additive extension of the existing envelope: null means the old event never linked a catalog.
    public CargoDispatchBasisReference? Basis { get; set; }
    public CargoDispatchInputEvidence? InputEvidence { get; set; }
}

internal sealed record CargoDispatchInputEvidence
{
    public string? InputFingerprint { get; init; }
    public string? DriverBaseFingerprint { get; init; }
    public string? VehicleFingerprint { get; init; }
    public string? DriverFixedFingerprint { get; init; }
    public string? CurrentWorkFingerprint { get; init; }
    public string? LocationObservationFingerprint { get; init; }
    public string? FingerprintVersion { get; init; }
    public string? RequestFingerprint { get; init; }
    public string? DriverFingerprint { get; init; }
    public DateTime? ReferenceAtUtc { get; init; }
    public DateTime? QueryStartedAtUtc { get; init; }
    public DateTime? QueryCompletedAtUtc { get; init; }
    public DateTime? LocationRecordedAtUtc { get; init; }
    public DateTime? LocationReceivedAtUtc { get; init; }
    public string QueryScope { get; init; } = "Unknown";
    public bool AtomicRead { get; init; }

    internal static CargoDispatchInputEvidence? Capture(국내화물배차판정근거? source, string requestId, string driverId)
    {
        if (source is null) return null;
        static DateTime? At(DateTime? value) => value is { } date && date != default ? CargoDispatchExplanationCodec.Utc(date) : null;
        return new()
        {
            InputFingerprint = source.입력지문, DriverBaseFingerprint = source.기사기본지문,
            VehicleFingerprint = source.실차제원지문, DriverFixedFingerprint = source.기사고정조건지문,
            CurrentWorkFingerprint = source.현재수행지문, LocationObservationFingerprint = source.위치관측지문,
            FingerprintVersion = source.지문판본,
            RequestFingerprint = source.의뢰근거?.GetValueOrDefault(requestId),
            DriverFingerprint = source.기사근거?.GetValueOrDefault(driverId),
            ReferenceAtUtc = At(source.기준시각Utc), QueryStartedAtUtc = At(source.조회시작시각Utc),
            QueryCompletedAtUtc = At(source.조회완료시각Utc), QueryScope = source.조회범위,
            LocationRecordedAtUtc = At(source.위치기록시각Utc), LocationReceivedAtUtc = At(source.위치수신시각Utc),
            AtomicRead = source.원자적조회여부
        };
    }
}

internal static class CargoDispatchExplanationCodec
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static DateTime Utc(DateTime value) => value.Kind == DateTimeKind.Local
        ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    internal static void ValidateIdentifier(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max || value != value.Trim() || value.Any(char.IsControl))
            throw new ArgumentException("CargoDispatchExplanationIdentifierInvalid");
    }
    internal static CargoDispatchExplanationSnapshot Read(운송이벤트 row, IPersonalDataEncryptionService protection)
    {
        try
        {
            var raw = protection.Unprotect(row.메타데이터);
            if (raw == row.메타데이터) throw new CargoDispatchExplanationUnavailableException();
            var result = raw is null ? null : JsonSerializer.Deserialize<CargoDispatchExplanationSnapshot>(raw, Json);
            if (result is null || result.SchemaVersion != 1 || result.RequestId != row.의뢰Id
                || result.QueueId <= 0 || result.RecommendationRound <= 0 || string.IsNullOrWhiteSpace(result.DriverId)
                || result.OccurredAtUtc == default || row.이벤트타입 != CargoDispatchExplanationRecorder.EventType
                || Utc(result.OccurredAtUtc).Ticks / TimeSpan.TicksPerMillisecond != Utc(row.이벤트시각).Ticks / TimeSpan.TicksPerMillisecond
                || result.TransportMode is not (null or "독차" or "혼적")
                || result.Temperature is not (null or "상온" or "냉장" or "냉동" or "실온")
                || result.DecisionFacts is { OwnPickupOrder: <= 0 or > 1000 } or { OwnDropoffOrder: <= 0 or > 1000 }
                || !(result.MethodCode.StartsWith("engine/", StringComparison.Ordinal) || result.MethodCode.StartsWith("coordination/", StringComparison.Ordinal)
                    || result.MethodCode.StartsWith("manual/", StringComparison.Ordinal)))
                throw new CargoDispatchExplanationUnavailableException();
            return result;
        }
        catch (CargoDispatchExplanationUnavailableException) { throw; }
        catch (Exception ex) { throw new CargoDispatchExplanationUnavailableException(ex); }
    }
}
