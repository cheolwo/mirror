using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Driver.DispatchAction;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;

namespace Ssalddel.Application.Warehouse;

public sealed record DistributedCargoAcceptanceResult(string CargoRequestId, bool Accepted, string? ErrorCode,
    string? ErrorMessage, int? StatusCode, IReadOnlyList<DistributedAcceptanceWarningDto> Warnings);
public interface I분산화물연결Service
{
    Task<DistributedCargoAcceptanceResult> AcceptAsync(string driverId, string cargoRequestId, IReadOnlyList<string> acknowledgedWarnings, CancellationToken ct);
    Task ArrivePickupAsync(string driverId, string cargoRequestId, CancellationToken ct);
    Task PickupAsync(string driverId, string cargoRequestId, CancellationToken ct);
    Task DeliverAsync(string driverId, string cargoRequestId, CancellationToken ct);
    Task CancelAsync(string actorId, string cargoRequestId, bool returnRequired, string evidence, CancellationToken ct);
    Task FlushCommittedEventsAsync(CancellationToken ct);
    void DiscardUncommittedEvents();
}
/// <summary>새 배차 성공을 합성하지 않고 기존 기사 수락과 실제 원장 상태 전이 명령을 소비한다.</summary>
public sealed class 분산화물연결Service(SsalddelContext db, ISender sender, I기사운송상태변경CommandExecutor executor,
    IPublisher publisher, Microsoft.Extensions.Logging.ILogger<분산화물연결Service> logger) : I분산화물연결Service
{
    private readonly List<INotification> committedEvents = [];
    public void DiscardUncommittedEvents() => committedEvents.Clear();
    public async Task FlushCommittedEventsAsync(CancellationToken ct)
    {
        var events = committedEvents.ToArray(); committedEvents.Clear();
        foreach (var notification in events)
            try { await publisher.Publish(notification, ct); }
            catch (Exception ex) { Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, ex, "분산 운송의 저장 후 투영 발행 실패. SQL 인수 사실을 재조회합니다."); }
    }
    public async Task<DistributedCargoAcceptanceResult> AcceptAsync(string driverId, string cargoRequestId, IReadOnlyList<string> acknowledgedWarnings, CancellationToken ct)
    {
        var row = await db.운송원장.AsNoTracking().SingleAsync(x => x.의뢰Id == cargoRequestId, ct);
        var result = await sender.Send(new 배차수락Command(driverId, cargoRequestId, row.추천라운드, acknowledgedWarnings), ct);
        var error = result.Errors.FirstOrDefault();
        var codes = error?.Metadata.GetValueOrDefault("WarningCodes") as IEnumerable<string> ?? [];
        var messages = (error?.Metadata.GetValueOrDefault("WarningMessages") as IEnumerable<string> ?? []).ToArray();
        var warnings = codes.Select(code => new DistributedAcceptanceWarningDto(cargoRequestId, code,
            messages.Length > 0 ? string.Join(" ", messages) : error?.Message ?? "주의사항 설명을 다시 조회해 주세요.")).ToArray();
        return new(cargoRequestId, result.IsSuccess, error is null ? null : error.Metadata.GetValueOrDefault("ErrorCode") as string ?? "DistributedCargoAcceptanceFailed",
            error?.Message, error?.Metadata.GetValueOrDefault("StatusCode") as int?, warnings);
    }
    public Task ArrivePickupAsync(string driverId, string cargoRequestId, CancellationToken ct) => Change(driverId, cargoRequestId, "상차지도착", ct);
    public Task PickupAsync(string driverId, string cargoRequestId, CancellationToken ct) => Change(driverId, cargoRequestId, "상차완료", ct);
    public async Task DeliverAsync(string driverId, string cargoRequestId, CancellationToken ct)
    { await Change(driverId, cargoRequestId, "하차지도착", ct); await Change(driverId, cargoRequestId, "인수완료", ct); }
    public async Task CancelAsync(string actorId, string cargoRequestId, bool returnRequired, string evidence, CancellationToken ct)
    {
        // Caller has already checked the persisted order parties inside the same serializable transaction.
        // Preserve payment/settlement facts: this is custody cancellation, never a refund certificate.
        var row = await db.운송원장.SingleAsync(x => x.의뢰Id == cargoRequestId, ct);
        var source = await db.화주운송의뢰.SingleAsync(x => x.의뢰Id == cargoRequestId, ct);
        분산물류UseCase.Require(row.상태 != "인수완료", "DistributedCargoAlreadyDelivered");
        var previousDriverId = row.확정기사Id ?? row.기사_운송자;
        row.상태 = returnRequired ? "반환대기" : 살뜰.도메인.공통.상태값.배차상태.취소;
        row.배차큐단계 = 살뜰.도메인.공통.상태값.배차큐단계.종료;
        row.배차노출상태 = 살뜰.도메인.공통.상태값.배차노출상태.종료;
        row.현재추천대상기사Id = null; row.추천만료시각 = null; row.추천라운드++;
        source.배차상태 = row.상태;
        if (!returnRequired)
        {
            source.상태 = 살뜰.도메인.공통.상태값.의뢰상태.취소;
            row.기사_운송자 = ""; row.확정기사Id = null;
        }
        db.운송이벤트.Add(new() { 의뢰Id = cargoRequestId, 이벤트타입 = "DistributedCustodyCancelled",
            메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { actorId, previousDriverId, returnRequired, evidence }) });
    }
    private async Task Change(string driverId, string cargoRequestId, string state, CancellationToken ct)
    {
        var row = await db.운송원장.SingleAsync(x => x.의뢰Id == cargoRequestId && x.기사_운송자 == driverId, ct);
        if (row.상태 == state || (state == "상차지도착" && row.상태 is "상차완료" or "하차지도착" or "인수완료")) return;
        var result = await executor.실행Async(new(driverId, row.Id, driverId, 살뜰.도메인.사용자.살뜰역할유형.기사,
            state, "DistributedCargoCustody", x => state switch
            {
                "상차지도착" => new 운송상차지도착됨Event(driverId, x.운송.Id, x.이전상태, state, x.발생시각Utc, x.TraceId),
                "상차완료" => new 운송상차완료됨Event(driverId, x.운송.Id, x.운송.운송번호, x.운송.픽업_도로명주소, x.운송.하차_도로명주소, x.이전상태, state, x.발생시각Utc, x.TraceId, null),
                "하차지도착" => new 운송하차지도착됨Event(driverId, x.운송.Id, x.이전상태, state, x.발생시각Utc, x.TraceId),
                _ => new 운송인수완료됨Event(x.운송.Id, x.운송.운송번호, driverId, x.운송.픽업_도로명주소, x.운송.하차_도로명주소, state, x.발생시각Utc, x.TraceId, null)
            }, Commit이후발행수집: committedEvents.Add), ct);
        분산물류UseCase.Require(result.IsSuccess, "DistributedCargoStateTransitionBlocked");
    }
}
