using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Services.Dispatch.Compliance;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Contracts.Common.Metadata;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;
using 살뜰.도메인.화물;

namespace 살뜰.Services.Dispatch.Coordination;

public interface I화물배차상태SnapshotService
{
    Task<국내화물배차판정근거> 조회Async(string requestId, string driverId, DateTime 기준시각Utc, CancellationToken ct = default);
    Task<bool> 현재근거확인Async(국내화물배차판정근거 expected, CancellationToken ct = default);
}

/// <summary>전문 원장의 일시적인 비교 근거다. 조회 구간을 기록하며 새 원장이나 원자적 SQL/GPS snapshot을 만들지 않는다.</summary>
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.OperationalDispatchCore, SsalddelCodeLayer.Application,
    "같은 기준시각에서 화물·기사·보수·실차와 현재 수행의 순차 관측 근거를 비교한다.",
    StepKey = "application.cargo-dispatch-state-evidence", FlowOrder = 46,
    ExecutionStage = SsalddelCodeExecutionStage.Preview, Effects = SsalddelCodeEffect.PersistentRead,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Boundary = "관측과 지문 확인만 수행하며 배차·수락·실차 확인·정산 또는 원자적 snapshot을 확정하지 않는다.")]
public sealed class 화물배차상태SnapshotService(SsalddelContext db, I국내화물운송기사상태Store states,
    I기사실차적재근거조회Service vehicles, I운영배차공통UseCase availability,
    I신규화물업무자격Guard qualification, ISharedDriverWorkAdmissionService admission,
    I화물후속픽업기준Service pickupBasis, IOptions<배차큐정책Options> options, TimeProvider? clock = null)
    : I화물배차상태SnapshotService
{
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<국내화물배차판정근거> 조회Async(string requestId, string driverId, DateTime 기준시각Utc, CancellationToken ct = default)
    {
        var started = Now;
        var missing = new List<string>();
        var blocked = new List<string>();
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        var driver = await db.용달기사.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == driverId, ct);
        var state = await states.GetAsync(driverId, ct);
        var profile = await db.Set<DriverWorkParticipationRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.UserId == driverId, ct);
        var fare = request?.운임구성Id is { } fareId
            ? await db.운임구성.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fareId && x.의뢰Id == requestId, ct) : null;
        var cargo = await db.화물요구조건.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        var active = await db.운송원장.AsNoTracking().Where(x => (x.기사_운송자 == driverId || x.확정기사Id == driverId)
            && x.배차큐단계 == 상태값.배차큐단계.확정 && x.상태 != 상태값.배차상태.인수완료)
            .OrderBy(x => x.의뢰Id).ToArrayAsync(ct);
        var ids = active.Select(x => x.의뢰Id).ToArray();
        var activeRequests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .OrderBy(x => x.의뢰Id).ToArrayAsync(ct);
        var activeFares = await db.운임구성.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id)).OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.의뢰Id, x.기사지급예정운임, x.합의요금표SnapshotJson, x.UpdatedAt }).ToArrayAsync(ct);
        var activeCargo = await db.화물요구조건.AsNoTracking().Where(x => Enumerable.Contains(ids, x.의뢰Id))
            .OrderBy(x => x.의뢰Id).ToArrayAsync(ct);
        var held = await (from wave in db.Set<분산배송WaveRecord>().AsNoTracking()
                          join order in db.Set<분산배송주문Record>().AsNoTracking() on wave.WaveId equals order.WaveId
                          join reservation in db.Set<분산재고예약Record>().AsNoTracking() on order.OrderId equals reservation.OrderId
                          where wave.DriverId == driverId && reservation.PickedQuantity > reservation.DeliveredQuantity + reservation.ReturnedQuantity
                          orderby order.OrderId, reservation.ReservationId
                          select new { order.OrderId, order.CargoRequestId, order.StatusCode, reservation.PickedQuantity,
                              reservation.DeliveredQuantity, reservation.ReturnedQuantity }).ToArrayAsync(ct);
        if (request is null || queue is null) blocked.Add("DispatchEvidence.RequestMissing");
        if (driver is null || state is null) blocked.Add("DispatchEvidence.DriverMissing");
        if (state is null || !state.Latitude.HasValue || !state.Longitude.HasValue
            || !기사위치신선도정책.유효한가(state.위치수신시각Utc, 기준시각Utc, options.Value.기사위치유효시간분))
            blocked.Add("DispatchEvidence.LocationUnverified");
        if (state?.운행상태 != 상태값.기사운행상태.운행중 || driver?.상태 != "활동중") blocked.Add("DispatchEvidence.DriverInactive");
        var receiving = await availability.수신상태조회Async(driverId, ct);
        if (!살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(receiving)) blocked.Add("FreightDispatchReceivingOff");
        var qualified = await qualification.평가Async(driverId, ct);
        if (!qualified.CanReceiveNewWork) blocked.Add(qualified.ReasonCode);
        if (queue is not null)
        {
            if (!admission.Enabled) missing.Add("DispatchEvidence.ParticipationGuardDisabled");
            var participation = await admission.CheckRecommendationAsync(driverId, queue, ct);
            if (participation.IsFailed) blocked.AddRange(participation.Errors.Select(x => x.Metadata.GetValueOrDefault("ErrorCode") as string ?? "DispatchEvidence.ParticipationChanged"));
        }
        var actual = await vehicles.조회Async(driverId, ct);
        if (!actual.실차확인됨) missing.Add(actual.사유Code);
        if (actual.유효제원 is null) blocked.Add(actual.사유Code);
        if (fare?.기사지급예정운임 is not > 0 || decimal.Truncate(fare.기사지급예정운임.Value) != fare.기사지급예정운임.Value)
            blocked.Add("FreightDriverPayoutAgreementRequired");
        if (queue?.기사지급예정액 is { } offered && fare?.기사지급예정운임 != offered) blocked.Add("FreightDriverPayoutAgreementChanged");
        if (activeRequests.Length != active.Length) blocked.Add("DispatchEvidence.ActiveCargoMissing");
        if (held.Any(x => x.StatusCode is "ReturnRequired" or "Cancelled")) blocked.Add("DriverWork.ReturnCustodyPending");
        화물후속픽업기준결과? basis = null;
        if (state?.Latitude is { } latitude && state.Longitude is { } longitude)
        {
            basis = await pickupBasis.조회Async(driverId, new(latitude, longitude), 기준시각Utc, ct);
            if (!basis.확인됨 || string.IsNullOrWhiteSpace(basis.원장판본)) blocked.Add(basis.사유Code);
        }
        var requestHash = 원장지문(queue, request, fare, cargo);
        var fixedDriverHash = 지문(new
        {
            Driver = driver is null ? null : new { driver.기사Id, driver.상태, driver.운행상태, driver.차량, driver.현재실차참조 },
            State = state is null ? null : new { state.DriverId, state.ShiftId, state.운행상태, state.상차접근허용반경Km, state.복귀콜선호 },
            AdmissionEnabled = admission.Enabled, Profile = profile is null ? null : new { profile.Revision, profile.FoodDelivery,
                profile.QuickDelivery, profile.CargoTransport, profile.DirectPicking, profile.MaxConcurrentJobs,
                profile.CapacityKg, profile.CapacityLitres, profile.ReceiveAdditionalOffers, profile.FoodCargoSeparationConfirmed },
            Receiving = new { receiving.수신의사Code, receiving.실효상태Code, receiving.실효사유Code,
                receiving.수신의사판본Token, receiving.수신의사변경시각Utc, receiving.실효상태변경시각Utc,
                receiving.미응답자동중지여부, receiving.연속미응답횟수, receiving.미응답자동중지시각Utc },
            Qualification = qualified, ActualRevision = actual.판본, ActualDigest = actual.근거지문
        });
        var activeHash = 지문(new
        {
            Active = active.Select(x => new { x.의뢰Id, x.상태, x.확정기사Id, x.기사_운송자, x.RowVersion, x.UpdatedAt, x.출발_픽업, x.도착, x.기사지급예정액 }),
            Cargo = activeRequests.Select(x => new { x.의뢰Id, Loading = 화주운송의뢰매퍼.ReadLoadingEvidence(x).입력지문,
                x.픽업_시간창_시작일시, x.픽업_시간창_종료일시, x.하차_시간창_시작일시, x.하차_시간창_종료일시,
                x.픽업_작업시간분, x.하차_작업시간분, x.UpdatedAt }),
            ActiveFares = activeFares, ActiveCargo = activeCargo, Held = held, ActiveBasis = basis?.원장판본
        });
        var positionHash = 위치관측지문(state);
        var driverHash = 지문(new { fixedDriverHash, activeHash, positionHash });
        return new()
        {
            기준시각Utc = 기준시각Utc, 조회시작시각Utc = started, 조회완료시각Utc = Now,
            조회범위 = "SQL request/queue/fare/profile/active/custody + driver location/receiving/qualification/actual vehicle; sequential",
            원자적조회여부 = false, 의뢰근거 = new Dictionary<string, string> { [requestId] = requestHash },
            기사근거 = new Dictionary<string, string> { [driverId] = driverHash },
            입력지문 = 지문(new { requestId, driverId, requestHash, driverHash }),
            기사기본지문 = 기사원본지문(driver), 실차제원지문 = 지문(actual.유효제원),
            지문판본 = "cargo-dispatch-state-r77-v1", 기사고정조건지문 = fixedDriverHash, 현재수행지문 = activeHash,
            위치관측지문 = positionHash, 위치기록시각Utc = state?.위치기록시각Utc, 위치수신시각Utc = state?.위치수신시각Utc,
            대기순위기준시각Utc = state?.Aging기준시각Utc, 대기순위점수 = state?.Aging점수, 순위관측지문 = 순위지문(state),
            미확인사유 = missing.Distinct(StringComparer.Ordinal).ToArray(), 차단사유 = blocked.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    public async Task<bool> 현재근거확인Async(국내화물배차판정근거 expected, CancellationToken ct = default)
    {
        if (expected.차단사유.Count != 0
            || expected.미확인사유.Contains("DispatchEvidence.ParticipationGuardDisabled", StringComparer.Ordinal)
            || string.IsNullOrWhiteSpace(expected.입력지문)
            || expected.의뢰근거.Count != 1 || expected.기사근거.Count != 1) return false;
        var current = await 조회Async(expected.의뢰근거.Keys.Single(), expected.기사근거.Keys.Single(), Now, ct);
        return current.차단사유.Count == 0
            && !current.미확인사유.Contains("DispatchEvidence.ParticipationGuardDisabled", StringComparer.Ordinal)
            && current.입력지문 == expected.입력지문;
    }

    internal static string 원장지문(운송원장? queue, 화주운송의뢰? request, 운임구성? fare, 화물요구조건? cargo = null) => 지문(new
    {
        // 추천 저장 자체가 바꾸는 라운드·시각·RowVersion은 기존 수락 관문에서 별도로 검사한다.
        Queue = queue is null ? null : new { queue.의뢰Id, queue.상태, queue.확정기사Id, queue.기사_운송자,
            queue.생활배송선택판본, queue.배차업무유형, queue.픽업_위도, queue.픽업_경도, queue.하차_위도, queue.하차_경도 },
        Request = request is null ? null : new { request.의뢰Id, Loading = 화주운송의뢰매퍼.ReadLoadingEvidence(request).입력지문,
            request.화물부피Cbm, request.화물부피근거Code, request.화물적재계산판본, request.차량종류, request.운송방식, request.서비스레벨,
            request.픽업_위도, request.픽업_경도, request.하차_위도, request.하차_경도, request.픽업_시간창_시작일시,
            request.픽업_시간창_종료일시, request.하차_시간창_시작일시, request.하차_시간창_종료일시,
            request.픽업_작업시간분, request.하차_작업시간분, request.상태,
            request.결제상태, request.정산상태, request.배차상태, request.UpdatedAt },
        Cargo = cargo,
        Fare = fare is null ? null : new { fare.Id, fare.의뢰Id, fare.기사지급예정운임, fare.최종운임, fare.합의요금표SnapshotJson, fare.UpdatedAt }
    });

    internal static string 위치지문(국내화물운송기사상태Snapshot? state) => 지문(state is null ? null : new
    { state.DriverId, state.ShiftId, state.운행상태, state.Latitude, state.Longitude, state.AccuracyM,
        state.상차접근허용반경Km, state.복귀콜선호 });
    internal static string 위치관측지문(국내화물운송기사상태Snapshot? state) => 지문(state is null ? null : new
    { state.DriverId, state.Latitude, state.Longitude, state.AccuracyM });
    internal static string? 순위지문(국내화물운송기사상태Snapshot? state) => state is null ? null
        : 지문(new { state.DriverId, state.Aging기준시각Utc, state.Aging점수 });
    internal static string 기사원본지문(용달기사? driver) => 지문(driver is null ? null : new
        { driver.기사Id, driver.상태, driver.운행상태, driver.차량, driver.현재실차참조 });
    internal static string 지문<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).ToLowerInvariant();
}
