using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Services.Privacy;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Community;

public interface I생활배송기사정보제공동의Service
{
    Task<Result<NeighborhoodDeliveryDisclosureResponse>> 기록Async(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken cancellationToken = default);
    Task<NeighborhoodDeliveryDisclosureResponse?> 내상태Async(string requestId, CancellationToken cancellationToken = default);
    Task<bool> 유효한기사제공동의인가Async(string requestId, string driverId, string ownerUserId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 한 생활 배송의 현재 확정 기사·추천 차수에 대한 제공 동의/철회를 기존 운송 이벤트에 기록합니다.
/// 주소·전화번호 원문을 이벤트에 복사하지 않고, 신청 동의·배차 수락·결제 사실과 분리합니다.
/// </summary>
public sealed class 생활배송기사정보제공동의Service(SsalddelContext db, ICurrentUserAccessor currentUser, TimeProvider clock,
    I신청개인정보동의증적Service? applicationConsent = null) : I생활배송기사정보제공동의Service
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim[] ConsentGates = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<Result<NeighborhoodDeliveryDisclosureResponse>> 기록Async(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return Fail("AuthenticationRequired", "정보 제공 동의는 로그인 후 확인할 수 있습니다.", 401);
        if (request is null || request.ClientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.ConfirmedDriverId)
            || request.ConfirmedDriverId.Length > 160 || request.NoticeVersion != NeighborhoodDeliveryDisclosureNotice.Version)
            return Fail("InvalidDriverDisclosure", "선정된 기사와 현재 정보 제공 안내를 다시 확인해 주세요.", 400);
        var gateKey = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(requestId));
        var gate = ConsentGates[gateKey[0] % ConsentGates.Length];
        await gate.WaitAsync(cancellationToken);
        try
        {
            var entity = await 본인의뢰Async(requestId, cancellationToken);
            if (entity is null) return Fail("RequestNotFound", "본인 배송 의뢰를 찾을 수 없습니다.", 404);
            var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
            var events = await EventsAsync(requestId, cancellationToken);
            var prior = events.FirstOrDefault(x => x.ClientRequestId == request.ClientRequestId);
            if (prior is not null)
            {
                if (prior.OwnerUserId != currentUser.UserId || prior.ConfirmedDriverId != request.ConfirmedDriverId
                    || prior.RecommendationRound != request.ExpectedRecommendationRound || prior.Consented != request.Consented
                    || prior.NoticeVersion != request.NoticeVersion)
                    return Fail("IdempotencyConflict", "같은 요청 번호를 다른 정보 제공 결정에 사용할 수 없습니다.", 409);
                return Result.Ok((await 내상태Async(requestId, cancellationToken))!);
            }
            if (request.Consented && (!ActiveAssignment(entity, queue)
                    || queue!.확정기사Id != request.ConfirmedDriverId || queue.추천라운드 != request.ExpectedRecommendationRound))
                return Fail("AssignedDriverChanged", "현재 선정된 기사 또는 배송 상태가 바뀌었습니다. 배송 상세를 다시 확인해 주세요.", 409);
            if (request.Consented && string.IsNullOrWhiteSpace(await DriverNameAsync(request.ConfirmedDriverId, cancellationToken)))
                return Fail("AssignedDriverUnavailable", "선정된 기사의 이름을 확인한 후 정보 제공 여부를 결정해 주세요.", 409);
            if (request.Consented && !await ApplicationConsentValidAsync(entity, cancellationToken))
                return Fail("PrivacyConsentRequired", "이 배송 신청의 개인정보 수집·이용 동의를 다시 확인해 주세요.", 400);
            var now = clock.GetUtcNow().UtcDateTime;
            var evidence = new DisclosureEvidence
            {
                ClientRequestId = request.ClientRequestId, RequestId = requestId, OwnerUserId = currentUser.UserId!,
                ConfirmedDriverId = request.ConfirmedDriverId, RecommendationRound = request.ExpectedRecommendationRound,
                Consented = request.Consented, NoticeVersion = request.NoticeVersion, RecordedAtUtc = now,
                ExpiresAtUtc = now.AddHours(72), Purpose = NeighborhoodDeliveryDisclosureNotice.Purpose,
                Fields = NeighborhoodDeliveryDisclosureNotice.Fields.ToArray(), RetentionNotice = NeighborhoodDeliveryDisclosureNotice.Retention
            };
            db.운송이벤트.Add(new 운송이벤트
            {
                의뢰Id = requestId, 이벤트타입 = NeighborhoodDeliveryDisclosureNotice.EventType,
                이벤트시각 = now, 메타데이터 = JsonSerializer.Serialize(evidence, JsonOptions)
            });
            await db.SaveChangesAsync(cancellationToken);
            return Result.Ok((await 내상태Async(requestId, cancellationToken))!);
        }
        finally { gate.Release(); }
    }

    public async Task<NeighborhoodDeliveryDisclosureResponse?> 내상태Async(string requestId, CancellationToken cancellationToken = default)
    {
        var entity = await 본인의뢰Async(requestId, cancellationToken);
        if (entity is null) return null;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        var evidence = (await EventsAsync(requestId, cancellationToken)).FirstOrDefault();
        var driverId = queue?.확정기사Id;
        var driverName = await DriverNameAsync(driverId, cancellationToken);
        var applicationConsentActive = await ApplicationConsentValidAsync(entity, cancellationToken);
        return new NeighborhoodDeliveryDisclosureResponse
        {
            RequestId = requestId, ConfirmedDriverId = driverId, ConfirmedDriverName = driverName,
            RecommendationRound = queue?.추천라운드 ?? 0,
            CanRecordConsent = applicationConsentActive && !string.IsNullOrWhiteSpace(driverName) && ActiveAssignment(entity, queue),
            Consented = applicationConsentActive && !string.IsNullOrWhiteSpace(driverName) && Valid(evidence, entity, queue, driverId, entity.주문자UserId),
            ExpiresAtUtc = evidence?.ExpiresAtUtc
        };
    }

    public async Task<bool> 유효한기사제공동의인가Async(string requestId, string driverId, string ownerUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(driverId) || string.IsNullOrWhiteSpace(ownerUserId)) return false;
        var entity = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId
            && x.주문자UserId == ownerUserId && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), cancellationToken);
        if (entity is null) return false;
        if (!await ApplicationConsentValidAsync(entity, cancellationToken)) return false;
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, cancellationToken);
        if (string.IsNullOrWhiteSpace(await DriverNameAsync(driverId, cancellationToken))) return false;
        return Valid((await EventsAsync(requestId, cancellationToken)).FirstOrDefault(), entity, queue, driverId, ownerUserId);
    }

    private bool Valid(DisclosureEvidence? evidence, 화주운송의뢰 entity, 운송원장? queue, string? driverId, string owner)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return ActiveAssignment(entity, queue) && !string.IsNullOrWhiteSpace(driverId)
            && queue!.확정기사Id == driverId && evidence is { Consented: true }
            && evidence.RequestId == entity.의뢰Id && evidence.OwnerUserId == owner && entity.주문자UserId == owner
            && evidence.ConfirmedDriverId == driverId && evidence.RecommendationRound == queue.추천라운드
            && evidence.NoticeVersion == NeighborhoodDeliveryDisclosureNotice.Version
            && evidence.Purpose == NeighborhoodDeliveryDisclosureNotice.Purpose
            && evidence.Fields is not null && evidence.Fields.SequenceEqual(NeighborhoodDeliveryDisclosureNotice.Fields, StringComparer.Ordinal)
            && evidence.RetentionNotice == NeighborhoodDeliveryDisclosureNotice.Retention
            && evidence.RecordedAtUtc <= now && evidence.ExpiresAtUtc > now
            && evidence.ExpiresAtUtc <= evidence.RecordedAtUtc.AddHours(72);
    }

    private static bool ActiveAssignment(화주운송의뢰 entity, 운송원장? queue)
        => entity.상태 == 상태값.의뢰상태.생성됨 && !string.IsNullOrWhiteSpace(queue?.확정기사Id)
           && queue.배차업무유형 == 상태값.배차업무유형.용달운송 && queue.배차큐단계 == 상태값.배차큐단계.확정
           && entity.배차상태 is not (상태값.배차상태.취소 or 상태값.배차상태.하차완료 or 상태값.배차상태.인수완료);

    private Task<화주운송의뢰?> 본인의뢰Async(string requestId, CancellationToken cancellationToken)
        => db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId && x.주문자UserId == currentUser.UserId
            && x.클라이언트요청Id.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix), cancellationToken);

    private Task<string?> DriverNameAsync(string? driverId, CancellationToken cancellationToken)
        => db.용달기사.AsNoTracking().Where(x => x.기사Id == driverId).Select(x => (string?)x.기사명).SingleOrDefaultAsync(cancellationToken);

    private async Task<bool> ApplicationConsentValidAsync(화주운송의뢰 entity, CancellationToken cancellationToken)
    {
        if (applicationConsent is null) return false;
        var memo = entity.정산메모 ?? string.Empty;
        var index = memo.LastIndexOf(":consent:", StringComparison.Ordinal);
        if (!memo.StartsWith("neighborhood-delivery:v1:", StringComparison.Ordinal) || index < 0
            || !Guid.TryParseExact(memo[(index + ":consent:".Length)..], "N", out var evidenceId) || evidenceId == Guid.Empty) return false;
        try
        {
            await applicationConsent.유효한동의요구Async(evidenceId, 신청개인정보업무Codes.운송대행,
                NeighborhoodDeliveryRoutes.PrivacyConsentSourceCode, entity.주문자UserId, cancellationToken);
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    private async Task<IReadOnlyList<DisclosureEvidence>> EventsAsync(string requestId, CancellationToken cancellationToken)
    {
        var rows = await db.운송이벤트.AsNoTracking().Where(x => x.의뢰Id == requestId
            && x.이벤트타입 == NeighborhoodDeliveryDisclosureNotice.EventType).OrderByDescending(x => x.Id)
            .Select(x => x.메타데이터).ToListAsync(cancellationToken);
        var results = new List<DisclosureEvidence>();
        foreach (var row in rows)
        {
            try
            {
                // 손상된 최신 기록을 건너뛰고 과거 허가로 돌아가지 않는다. 미확인 기록은 거절로 유지한다.
                results.Add(JsonSerializer.Deserialize<DisclosureEvidence>(row, JsonOptions) ?? new());
            }
            catch (JsonException) { results.Add(new()); }
        }
        return results;
    }

    private static Result<NeighborhoodDeliveryDisclosureResponse> Fail(string code, string message, int status)
        => Result.Fail<NeighborhoodDeliveryDisclosureResponse>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status));

    private sealed class DisclosureEvidence
    {
        public DisclosureEvidence() { }
        public Guid ClientRequestId { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public string ConfirmedDriverId { get; set; } = string.Empty;
        public int RecommendationRound { get; set; }
        public bool Consented { get; set; }
        public string NoticeVersion { get; set; } = string.Empty;
        public DateTime RecordedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string[] Fields { get; set; } = [];
        public string RetentionNotice { get; set; } = string.Empty;
    }
}
