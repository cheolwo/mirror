using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Food;
using Ssalddel.Security;
using Ssalddel.Services.Food;
using Ssalddel.Services.Community;
using Ssalddel.Services.PrivacyRetention;
using 살뜰.Data;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.음식;

namespace Ssalddel.Application.Food;

public sealed class 음식주문취소합의Exception(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public interface I음식주문취소합의UseCase
{
    Task<IReadOnlyList<음식주문취소합의응답>> 목록Async(string orderNo, CancellationToken ct = default);
    Task<음식주문취소합의응답?> 조회Async(string orderNo, string agreementId, Guid? requestId = null, CancellationToken ct = default);
    Task<음식주문취소합의응답> 제안Async(string orderNo, 음식주문취소합의제안요청 request, CancellationToken ct = default);
    Task<음식주문취소합의응답> 동의Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct = default);
    Task<음식주문취소합의응답> 실행Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct = default);
}

/// <summary>조리 전·기사 미확정인 수락 주문만 양측의 실제 조건 동의 후 취소합니다. 금융 실행은 하지 않습니다.</summary>
public sealed class 음식주문취소합의UseCase(SsalddelContext db, IHttpContextAccessor http, TimeProvider clock)
    : I음식주문취소합의UseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Orderer = "Orderer";
    private const string Restaurant = "Restaurant";
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private string Actor
    {
        get
        {
            var user = http.HttpContext?.User;
            var id = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.FindFirstValue("sub");
            return user?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(id)
                ? id : throw Error("AuthenticationRequired", "로그인한 뒤 주문을 확인해 주세요.", 401);
        }
    }
    private static 음식주문취소합의Exception Error(string code, string message, int status = 409) => new(code, message, status);
    private sealed record Receipt(Guid RequestId, string Actor, string Kind, string Fingerprint, DateTime RecordedAtUtc);
    private static List<Receipt> Receipts(음식주문취소합의 row) => JsonSerializer.Deserialize<List<Receipt>>(row.영수증Json, Json) ?? [];
    private static string Fingerprint<T>(string kind, T request) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { kind, request }, Json)))).ToLowerInvariant();

    public async Task<IReadOnlyList<음식주문취소합의응답>> 목록Async(string orderNo, CancellationToken ct = default)
    {
        var order = await OrderAsync(orderNo, ct); var party = Party(order); var block = await BlockAsync(order, ct);
        var rows = await db.Set<음식주문취소합의>().AsNoTracking().Where(x => x.음식주문Id == order.Id)
            .OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.Id).Take(20).ToArrayAsync(ct);
        return rows.Select(row => Response(order, row, party, block)).ToArray();
    }

    public async Task<음식주문취소합의응답?> 조회Async(string orderNo, string agreementId, Guid? requestId = null, CancellationToken ct = default)
    {
        var order = await OrderAsync(orderNo, ct); var party = Party(order);
        var row = await AgreementAsync(order, agreementId, ct);
        if (row is null) return null;
        Receipt? receipt = null;
        if (requestId is { } id)
        {
            receipt = Receipts(row).SingleOrDefault(x => x.RequestId == id && x.Actor == Actor);
            if (receipt is null) return null; // 다른 당사자의 요청 존재 여부를 노출하지 않는다.
        }
        return Response(order, row, party, await BlockAsync(order, ct), receipt?.RequestId);
    }

    public Task<음식주문취소합의응답> 제안Async(string orderNo, 음식주문취소합의제안요청 request, CancellationToken ct = default)
        => TransactionAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            ValidateRequest(request.ClientRequestId, request.ExpectedOrderRevision);
            if (request.OrdererCostKrw is not { } buyer || request.RestaurantCostKrw is not { } seller
                || !Money(buyer) || !Money(seller) || string.IsNullOrWhiteSpace(request.CostBasisReference)
                || request.CostBasisReference != request.CostBasisReference.Trim() || request.CostBasisReference.Length > 200)
                throw Error("ExplicitCostTermsRequired", "양측 부담액과 비용 근거를 명시해 주세요. 미확정 비용은 0원으로 대신할 수 없습니다.", 400);
            var order = await OrderAsync(orderNo, ct); var party = Party(order);
            var id = request.ClientRequestId.ToString("N"); var digest = Fingerprint("Propose", request);
            var existing = await AgreementAsync(order, id, ct);
            if (existing is not null)
            {
                RequireReplay(existing, request.ClientRequestId, "Propose", digest);
                return Response(order, existing, party, await BlockAsync(order, ct), request.ClientRequestId, true);
            }
            RequireCurrent(order, request.ExpectedOrderRevision, await BlockAsync(order, ct));
            var row = new 음식주문취소합의
            {
                합의StableId = id, 음식주문Id = order.Id, 주문번호 = order.주문번호,
                주문판본 = request.ExpectedOrderRevision, 주문상태 = order.상태,
                주문자부담액 = buyer, 음식점부담액 = seller, 비용근거참조 = request.CostBasisReference,
                CreatedAtUtc = Now, UpdatedAtUtc = Now
            };
            Append(row, request.ClientRequestId, "Propose", digest);
            db.Set<음식주문취소합의>().Add(row); await db.SaveChangesAsync(ct);
            return Response(order, row, party, null, request.ClientRequestId);
        }, ct);

    public Task<음식주문취소합의응답> 동의Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct = default)
        => ChangeAsync(orderNo, agreementId, request, apply: false, ct);

    public Task<음식주문취소합의응답> 실행Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct = default)
        => ChangeAsync(orderNo, agreementId, request, apply: true, ct);

    private Task<음식주문취소합의응답> ChangeAsync(string orderNo, string agreementId, 음식주문취소합의변경요청 request, bool apply, CancellationToken ct)
        => TransactionAsync(async () =>
        {
            ArgumentNullException.ThrowIfNull(request); ValidateRequest(request.ClientRequestId, request.ExpectedOrderRevision);
            if (request.ExpectedAgreementRevision < 1 || apply && !request.Granted)
                throw Error("AgreementRevisionRequired", "현재 합의 판본과 명시적 취소 확인이 필요합니다.", 400);
            var order = await OrderAsync(orderNo, ct); var party = Party(order);
            var row = await AgreementAsync(order, agreementId, ct) ?? throw Error("AgreementNotFound", "취소 조건을 찾을 수 없습니다.", 404);
            var kind = apply ? "Apply" : "Consent"; var digest = Fingerprint(kind, request);
            if (Receipts(row).Any(x => x.RequestId == request.ClientRequestId))
            {
                RequireReplay(row, request.ClientRequestId, kind, digest);
                return Response(order, row, party, await BlockAsync(order, ct), request.ClientRequestId, true);
            }
            var block = await BlockAsync(order, ct);
            RequireCurrent(order, request.ExpectedOrderRevision, block);
            if (row.Revision != request.ExpectedAgreementRevision || row.주문판본 != request.ExpectedOrderRevision
                || row.주문상태 != order.상태 || row.취소반영시각Utc.HasValue)
                throw Error("CancellationAgreementStale", "주문 또는 합의가 바뀌었습니다. 최신 조건으로 다시 협의해 주세요.");
            if (Receipts(row).Count >= 500) throw Error("AgreementCommandLimitReached", "이 조건의 변경 한도에 도달했습니다. 새 조건을 제안해 주세요.");
            if (!apply)
            {
                if (party == Orderer) row.주문자동의 = request.Granted; else row.음식점동의 = request.Granted;
            }
            else
            {
                if (!row.주문자동의 || !row.음식점동의) throw Error("BothPartiesConsentRequired", "같은 비용 조건에 대한 주문자와 음식점의 동의가 모두 필요합니다.");
                음식배달업무상태전이Guard.허용확인(order.상태, 음식주문상태코드.취소);
                var previous = order.상태; order.상태 = 음식주문상태코드.취소; order.UpdatedAt = Now;
                order.상태이력.Add(new 음식주문상태이력
                {
                    클라이언트요청Id = request.ClientRequestId, 처리UserId = Actor,
                    이전상태 = previous, 다음상태 = 음식주문상태코드.취소,
                    사유 = "양측 합의 취소 · " + row.합의StableId, 전이시각Utc = Now
                });
                if (order.배차대기Id is { } queueId)
                {
                    var queue = await db.운송원장.SingleAsync(x => x.Id == queueId, ct);
                    queue.상태 = 상태값.배차상태.취소; queue.배차큐단계 = 상태값.배차큐단계.종료;
                    queue.배차노출상태 = 상태값.배차노출상태.종료; queue.현재추천대상기사Id = null;
                    queue.추천시작시각 = queue.추천만료시각 = null; queue.UpdatedAt = Now;
                }
                row.취소반영시각Utc = Now;
                // 기존 소비자가 현재 취소 운송과 전문 주문을 순서대로 투영한다. 고객 정보 사본을 넣지 않는다.
                db.음식마트원장동기화Outbox.Add(new 음식마트원장동기화Outbox
                {
                    멱등키 = "food-agreed-cancellation:" + row.합의StableId,
                    동기화유형 = 음식마트원장동기화유형코드.음식주문, 원천Id = order.주문번호, 변경자 = Actor,
                    PayloadJson = JsonSerializer.Serialize(new
                    { AgreedCancellation = new 음식합의취소투영의도(order.주문번호, row.합의StableId, order.배차대기Id) }, Json),
                    CreatedAtUtc = Now, UpdatedAtUtc = Now
                });
            }
            Append(row, request.ClientRequestId, kind, digest); row.Revision++; row.UpdatedAtUtc = Now;
            await db.SaveChangesAsync(ct);
            return Response(order, row, party, apply ? null : block, request.ClientRequestId);
        }, ct);

    private async Task<음식주문> OrderAsync(string orderNo, CancellationToken ct)
    {
        var actor = Actor;
        var user = http.HttpContext!.User;
        var restaurantId = user.IsInRole(역할명.음식점) ? 음식점접근범위Resolver.음식점Id조회(user) : null;
        if (string.IsNullOrWhiteSpace(orderNo) || orderNo != orderNo.Trim() || orderNo.Length > 100)
            throw Error("OrderNotFound", "주문을 찾을 수 없습니다.", 404);
        var order = await db.음식주문.Include(x => x.상태이력).SingleOrDefaultAsync(x => x.주문번호 == orderNo
                && (x.주문자UserId == actor || restaurantId.HasValue && x.음식점Id == restaurantId.Value), ct)
            ?? throw Error("OrderNotFound", "주문을 찾을 수 없습니다.", 404);
        // 같은 scope가 재조회되거나 재시도 전략이 다시 실행돼도 추적된 과거 상태로 합의하지 않는다.
        await db.Entry(order).ReloadAsync(ct);
        await db.Entry(order).Collection(x => x.상태이력).Query().LoadAsync(ct);
        return order;
    }

    private string Party(음식주문 order)
    {
        if (order.주문자UserId == Actor) return Orderer;
        var user = http.HttpContext!.User;
        if (user.IsInRole(역할명.음식점) && 음식점접근범위Resolver.음식점Id조회(user) == order.음식점Id) return Restaurant;
        throw Error("OrderNotFound", "본인이 참여한 주문을 찾을 수 없습니다.", 404);
    }

    private async Task<음식주문취소합의?> AgreementAsync(음식주문 order, string agreementId, CancellationToken ct)
    {
        var row = await db.Set<음식주문취소합의>().SingleOrDefaultAsync(x => x.음식주문Id == order.Id && x.합의StableId == agreementId, ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }

    private async Task<string?> BlockAsync(음식주문 order, CancellationToken ct)
    {
        if (order.모의처리처수행) return "ExternalFulfillmentReviewRequired";
        if (order.배차상태 is not (음식주문배차상태코드.미요청 or 음식주문배차상태코드.배차대기
            or 음식주문배차상태코드.추천중 or 음식주문배차상태코드.배차불가)) return "DispatchStateReviewRequired";
        if (order.상태 != 음식주문상태코드.주문확인 || !order.음식점수락시각Utc.HasValue
            || 음식주문현재조리Policy.계산(order) is { CookingStartedAtUtc: not null } or { ReadyAtUtc: not null } or { Round: > 1 })
            return "CookingOrDeliveryReviewRequired";
        if (await db.음식배달시도.AnyAsync(x => x.주문번호 == order.주문번호, ct)) return "DriverWorkReviewRequired";
        if (order.배차대기Id is not { } id) return order.배차상태 == 음식주문배차상태코드.미요청 ? null : "DispatchStateReviewRequired";
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return queue is not null && queue.원본의뢰Id == order.주문번호
            && 운송의뢰배차원천유형.Is음식점주문(queue.원본의뢰유형)
            && queue.배차업무유형 == 상태값.배차업무유형.음식배달
            && queue.상태 == 상태값.배차대기상태.대기
            && queue.배차큐단계 != 상태값.배차큐단계.확정 && queue.배차큐단계 != 상태값.배차큐단계.종료
            && string.IsNullOrWhiteSpace(queue.확정기사Id) ? null : "DispatchStateReviewRequired";
    }

    private static void ValidateRequest(Guid requestId, long revision)
    {
        if (requestId == Guid.Empty || revision < 1) throw Error("RequestRevisionRequired", "요청 번호와 현재 주문 판본이 필요합니다.", 400);
    }
    private static bool Money(decimal amount) => amount >= 0 && amount <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;
    private static void RequireCurrent(음식주문 order, long revision, string? block)
    {
        if (order.상태이력.Count != revision) throw Error("OrderRevisionConflict", "주문이 변경되었습니다. 최신 상태를 확인해 주세요.");
        if (block is not null) throw Error(block, "조리·기사 수행이 있는 주문은 취소와 비용을 지원 담당자와 협의해 주세요.");
    }
    private void Append(음식주문취소합의 row, Guid id, string kind, string fingerprint)
    {
        var receipts = Receipts(row); receipts.Add(new(id, Actor, kind, fingerprint, Now)); row.영수증Json = JsonSerializer.Serialize(receipts, Json);
    }
    private void RequireReplay(음식주문취소합의 row, Guid id, string kind, string fingerprint)
    {
        if (!Receipts(row).Any(x => x.RequestId == id && x.Actor == Actor && x.Kind == kind && x.Fingerprint == fingerprint))
            throw Error("IdempotencyConflict", "같은 요청 번호로 다른 조건이나 결정을 보낼 수 없습니다.");
    }
    private static 음식주문취소합의응답 Response(음식주문 order, 음식주문취소합의 row, string party, string? block, Guid? receipt = null, bool replay = false)
    {
        var applied = row.취소반영시각Utc.HasValue;
        var stale = row.주문판본 != order.상태이력.Count || row.주문상태 != order.상태 || block is not null;
        return new()
        {
            AgreementId = row.합의StableId, OrderNo = order.주문번호, Revision = row.Revision,
            FrozenOrderRevision = row.주문판본, CurrentOrderRevision = order.상태이력.Count, OrderStatusCode = order.상태,
            StateCode = applied ? "Cancelled" : stale ? "ReconfirmationRequired" : row.주문자동의 && row.음식점동의 ? "ReadyToApply" : "AwaitingConsent",
            OrdererCostKrw = row.주문자부담액, RestaurantCostKrw = row.음식점부담액, CostBasisReference = row.비용근거참조,
            OrdererAgreed = row.주문자동의, RestaurantAgreed = row.음식점동의, CurrentPartyCode = party,
            CanConsent = !applied && !stale, CanApply = !applied && !stale && row.주문자동의 && row.음식점동의,
            BlockReasonCode = applied ? "" : block ?? (stale ? "CancellationAgreementStale" : ""),
            AppliedAtUtc = row.취소반영시각Utc, ConfirmedClientRequestId = receipt, IsIdempotentReplay = replay
        };
    }
    private Task<음식주문취소합의응답> TransactionAsync(Func<Task<음식주문취소합의응답>> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) throw Error("RelationalStorageRequired", "주문 저장소를 확인해 주세요.");
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try { var response = await action(); await transaction.CommitAsync(ct); return response; }
            catch (DbUpdateException) { await transaction.RollbackAsync(ct); db.ChangeTracker.Clear(); throw Error("AgreementRevisionConflict", "주문 또는 합의가 먼저 변경되었습니다. 같은 요청으로 결과를 확인해 주세요."); }
        });
    }
}
