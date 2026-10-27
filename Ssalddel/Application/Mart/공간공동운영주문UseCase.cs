using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.Community;
using 살뜰.Data;
using 살뜰.Services.Options;

namespace Ssalddel.Application.Mart;

public interface I공간공동운영주문UseCase
{
    Task<공간공동운영주문후보목록Response> 후보Async(string ledgerId, string cycleId, CancellationToken ct = default);
    Task<공간공동운영주문목록Response> 목록Async(string ledgerId, CancellationToken ct = default);
    Task<공간공동운영주문Response?> 조회Async(string ledgerId, Guid orderRequestId, CancellationToken ct = default);
    Task<공간공동운영주문Response?> 요청결과Async(string ledgerId, Guid requestId, CancellationToken ct = default);
    Task<bool> 원주문연결검증Async(string ledgerId, long expectedRevision, Guid orderRequestId, 커뮤니티원장Dto root, CancellationToken ct = default);
    Task<Result<공간공동운영주문Response>> 변경Async(string ledgerId, Guid orderRequestId, 공간공동운영주문명령Request request, CancellationToken ct = default);
}

/// <summary>비구속 요청의 양측 확인을 기존 주문 원장·분산 예약에 명시적으로 인계합니다. 새 재고·배차 권위가 아닙니다.</summary>
public sealed partial class 공간공동운영주문UseCase(SsalddelContext db, ICurrentUserAccessor currentUser,
    I공간공동운영연결Query access, I공간공동운영Store store, I커뮤니티원장저장소 ledgers,
    IDistributedFulfillmentUseCase fulfillment, ISsalddelExecutionModePolicy execution,
    IOptions<DistributedFulfillmentOptions> options, TimeProvider clock) : I공간공동운영주문UseCase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<공간공동운영주문목록Response> 목록Async(string ledgerId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct);
        return new() { LedgerId = record.LedgerId, Revision = record.Revision,
            Items = Extension(record).Orders.Where(o => Party(o, actor)).Select(o => Response(record, o, actor)).ToArray() };
    }
    public async Task<공간공동운영주문Response?> 조회Async(string ledgerId, Guid orderRequestId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct);
        var order = Extension(record).Orders.SingleOrDefault(o => o.Terms.OrderRequestId == orderRequestId && Party(o, actor));
        return order is null ? null : Response(record, order, actor);
    }
    public async Task<공간공동운영주문Response?> 요청결과Async(string ledgerId, Guid requestId, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct);
        var order = Extension(record).Orders.SingleOrDefault(o => Party(o, actor) && o.Receipts.Any(r => r.Actor == actor && r.RequestId == requestId));
        return order is null ? null : Response(record, order, actor, true);
    }

    /// <summary>현재 당사자의 보호 주문 정본으로 전달받은 원장 판본을 검증만 하며 생성·교체하지 않습니다.</summary>
    public async Task<bool> 원주문연결검증Async(string ledgerId, long expectedRevision, Guid orderRequestId,
        커뮤니티원장Dto root, CancellationToken ct = default)
    {
        var (record, actor) = await Owned(ledgerId, ct);
        if (record.Revision != expectedRevision) return false;
        var order = Extension(record).Orders.SingleOrDefault(o => o.Terms.OrderRequestId == orderRequestId && Party(o, actor));
        return order is not null && order.BuyerConsentedAtUtc.HasValue && order.SellerConsentedAtUtc.HasValue
            && root.원장Id == order.RootOrderLedgerId && root.원장Id == RootId(orderRequestId)
            && 원주문과일치(record.LedgerId, order, root);
    }

    public async Task<Result<공간공동운영주문Response>> 변경Async(string ledgerId, Guid orderRequestId,
        공간공동운영주문명령Request request, CancellationToken ct = default)
    {
        try
        {
            Require(request is not null && request.ClientRequestId != Guid.Empty && orderRequestId != Guid.Empty,
                "SpaceOrderRequestInvalid", 400);
            var (record, actor) = await Owned(ledgerId, ct);
            var data = Extension(record);
            var fingerprint = Digest(new { orderRequestId, request });
            var replayOrder = data.Orders.SingleOrDefault(o => o.Receipts.Any(r => r.RequestId == request.ClientRequestId && r.Actor == actor));
            if (replayOrder is not null)
            {
                var prior = replayOrder.Receipts.Single(r => r.RequestId == request.ClientRequestId && r.Actor == actor);
                Require(prior.Fingerprint == fingerprint && Party(replayOrder, actor), "SpaceOrderRequestContentConflict");
                return Result.Ok(Response(record, replayOrder, actor, true));
            }
            Require(record.Revision == request.ExpectedRevision, "SpaceOrderRevisionConflict");
            Require(record.StatusCode is not ("paused" or "closed") && record.ExecutionMode == "Simulation", "SpaceOrderCooperationNotReady");
            var order = data.Orders.SingleOrDefault(o => o.Terms.OrderRequestId == orderRequestId);
            if (request.Action == "propose")
            {
                Require(request.Terms is not null && request.Terms.OrderRequestId == orderRequestId && request.ConfirmTerms,
                    "SpaceOrderTermsConfirmationRequired", 400);
                Require(order is null || order.StatusCode == "proposed", "SpaceOrderHandoffAlreadyStarted");
                if (order is not null) Require(Party(order, actor) && order.TermsVersion == request.ExpectedTermsVersion, "SpaceOrderTermsChanged");
                var source = await ReadSource(record, actor, request.Terms!, true, ct);
                if (order is null)
                {
                    Require(data.Orders.Count < 32, "SpaceOrderLimitReached");
                    order = new() { BuyerUserId = source.BuyerUserId, SellerUserId = source.SellerUserId };
                    data.Orders.Add(order);
                }
                order.Terms = Copy(request.Terms!); order.TermsVersion++;
                order.BuyerUserId = source.BuyerUserId; order.SellerUserId = source.SellerUserId;
                order.CooperationTermsRevision = record.TermsRevision;
                order.ProductName = source.ProductName; order.SourceDigest = source.Digest;
                order.BuyerConsentedAtUtc = null; order.SellerConsentedAtUtc = null; order.HoldCode = null;
                order.RootOrderLedgerId = RootId(orderRequestId);
            }
            else
            {
                Require(order is not null && Party(order, actor), "SpaceOrderNotFound", 404);
                Require(order!.TermsVersion == request.ExpectedTermsVersion, "SpaceOrderTermsChanged");
                Require(order.CooperationTermsRevision == record.TermsRevision
                    && record.Participants.Any(p => p.UserId == order.BuyerUserId && p.RoleCode == 공간공동운영역할Codes.Resident && p.StateCode == "accepted" && p.AgreedTermsRevision == record.TermsRevision)
                    && record.Participants.Any(p => p.UserId == order.SellerUserId && p.RoleCode == 공간공동운영역할Codes.Supplier && p.StateCode == "accepted" && p.AgreedTermsRevision == record.TermsRevision),
                    "SpaceOrderCurrentPartiesConfirmationRequired", 403);
                Require(request.ConfirmTerms, "SpaceOrderTermsConfirmationRequired", 400);
                if (request.Action == "agree")
                {
                    Require(order.StatusCode == "proposed", "SpaceOrderHandoffAlreadyStarted");
                    var source = await ReadSource(record, actor, order.Terms, true, ct);
                    Require(source.Digest == order.SourceDigest && source.BuyerUserId == order.BuyerUserId
                        && source.SellerUserId == order.SellerUserId, "SpaceOrderSourceChanged");
                    if (actor == order.BuyerUserId) order.BuyerConsentedAtUtc = Now;
                    if (actor == order.SellerUserId) order.SellerConsentedAtUtc = Now;
                }
                else if (request.Action is "handoff" or "resume")
                {
                    Require(actor == order.SellerUserId && order.BuyerConsentedAtUtc.HasValue && order.SellerConsentedAtUtc.HasValue,
                        "SpaceOrderBothPartiesConsentRequired", 403);
                    Require(request.Action == "handoff" ? order.StatusCode == "proposed"
                        : order.StatusCode == "handoff-pending" || order.StatusCode == "confirmed" && order.OrderLedgerConnectionStatusCode == "Pending", "SpaceOrderResumeNotAllowed");
                    if (request.Action == "handoff")
                    {
                        var source = await ReadSource(record, actor, order.Terms, true, ct);
                        Require(source.Digest == order.SourceDigest, "SpaceOrderSourceChanged");
                        var wave = await db.Set<살뜰.도메인.창고.분산배송WaveRecord>().AsNoTracking().SingleAsync(w => w.WaveId == order.Terms.WaveId, ct);
                        order.HandoffRequestId = StableGuid("space-order-handoff:" + orderRequestId.ToString("N"));
                        order.HandoffWaveRevision = wave.Revision;
                        order.StatusCode = "handoff-pending";
                    }
                }
                else throw new SpaceOrderException("SpaceOrderActionInvalid", 400);
            }
            Require(order!.Receipts.Count < 128, "SpaceOrderReceiptLimitReached");
            order.Receipts.Add(new(request.ClientRequestId, actor, fingerprint));
            await Save(record, data, actor, request.ExpectedRevision, ct);
            // 동의 저장과 실행은 별도입니다. 확인·로그인 완료에서는 자동 실행하지 않습니다.
            if (request.Action is "handoff" or "resume") return Result.Ok(await Handoff(record, data, order, actor, ct));
            return Result.Ok(Response(record, order, actor));
        }
        catch (SpaceOrderException e) { return Fail(e.Code, e.Status); }
        catch (DistributedFulfillmentException e) { return Fail(e.Code, e.Status); }
    }

    private async Task<공간공동운영주문Response> Handoff(공간공동운영Record record, OrderExtension data, OrderRecord order, string actor, CancellationToken ct)
    {
        try
        {
            var root = await EnsureRoot(record, order, actor, ct);
            var sourceId = order.Terms.OrderRequestId;
            var existingReceipt = await db.Set<살뜰.도메인.창고.분산물류CommandReceipt>().AsNoTracking()
                .SingleOrDefaultAsync(r => r.ActorUserId == actor && r.RequestId == order.HandoffRequestId, ct);
            if (existingReceipt is null)
            {
                var currentSource = await ReadSource(record, actor, order.Terms, true, ct, allowConfirmationPending: true);
                Require(currentSource.Digest == order.SourceDigest, "SpaceOrderSourceChanged");
            }
            else Require(existingReceipt.Kind == "AddOrder" && existingReceipt.AggregateId == order.Terms.WaveId,
                "SpaceOrderExistingReceiptConflict");
            var source = await db.마트주문요청.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sourceId, ct);
            Require(source is not null && source.요청자UserId == order.BuyerUserId && SourceDigest(source) == order.SourceDigest,
                "SpaceOrderSourceChanged");
            if (source!.상태코드 == 마트주문요청상태코드.제출됨)
            {
                var changed = await db.마트주문요청.Where(x => x.Id == sourceId && x.상태코드 == 마트주문요청상태코드.제출됨
                    && x.공개상품Id == order.Terms.PublicProductId && x.수량 == order.Terms.Quantity && x.단가Snapshot == order.Terms.UnitPriceKrw
                    && x.요청자UserId == order.BuyerUserId && x.상품명Snapshot == order.ProductName
                    && x.판매단위Snapshot == source.판매단위Snapshot && x.합계Snapshot == source.합계Snapshot
                    && x.통화 == source.통화 && x.CreatedAtUtc == source.CreatedAtUtc
                    && x.비구속주문요청확인 == source.비구속주문요청확인 && x.안내버전 == source.안내버전)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.상태코드, 마트주문요청상태코드.확정확인중), ct);
                Require(changed == 1, "SpaceOrderSourceChanged");
            }
            else Require(source.상태코드 is 마트주문요청상태코드.확정확인중 or 마트주문요청상태코드.확정됨, "SpaceOrderSourceChanged");
            order.HandoffRootRevision ??= root.Revision;
            // 원 요청의 판본과 payload를 먼저 보호 저장하므로 응답 유실 뒤 동일 명령만 재개합니다.
            await Save(record, data, actor, record.Revision, ct);
            var result = await fulfillment.OrderAsync(new(actor), order.Terms.WaveId, new()
            {
                RequestId = order.HandoffRequestId, ExpectedRevision = order.HandoffWaveRevision,
                OrderReference = "space-order:" + sourceId.ToString("N"), CargoRequestId = order.Terms.CargoRequestId,
                Lines = [new() { LotId = order.Terms.LotId, Quantity = order.Terms.Quantity }],
                RootOrderLedgerId = order.RootOrderLedgerId, ExpectedRootRevision = order.HandoffRootRevision
            }, ct);
            var actual = await db.Set<살뜰.도메인.창고.분산배송주문Record>().AsNoTracking().SingleOrDefaultAsync(x => x.WaveId == order.Terms.WaveId
                && x.CargoRequestId == order.Terms.CargoRequestId && x.OrderReference == "space-order:" + sourceId.ToString("N"), ct);
            Require(actual is not null && actual.SellerUserId == order.SellerUserId && actual.CustomerUserId == order.BuyerUserId,
                "SpaceOrderReservationResultPending");
            var changedFinal = await db.마트주문요청.Where(x => x.Id == sourceId && x.상태코드 == 마트주문요청상태코드.확정확인중)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.상태코드, 마트주문요청상태코드.확정됨), ct);
            Require(changedFinal == 1 || await db.마트주문요청.AsNoTracking().AnyAsync(x => x.Id == sourceId && x.상태코드 == 마트주문요청상태코드.확정됨, ct),
                "SpaceOrderConfirmationResultPending");
            order.DistributedOrderId = actual!.OrderId; order.StatusCode = "confirmed";
            order.OrderLedgerConnectionStatusCode = result.OrderLedgerConnectionStatusCode ?? "Pending"; order.HoldCode = result.OrderLedgerConnectionErrorCode;
            await Save(record, data, actor, record.Revision, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            // 실물 명령의 결과가 미확정일 수 있습니다. 자동 해제·추가 주문·자동 POST를 하지 않습니다.
            order.HoldCode = e is DistributedFulfillmentException d ? d.Code : e is SpaceOrderException s ? s.Code : "SpaceOrderHandoffResultPending";
            order.StatusCode = order.DistributedOrderId is null ? "handoff-pending" : "confirmed";
            try { await Save(record, data, actor, record.Revision, ct); }
            catch (SpaceOrderException) { /* 조회 가능한 기존 의도와 영수증은 남겨 둡니다. */ }
        }
        return Response(record, order, actor);
    }

    private async Task<(공간공동운영Record Record, string Actor)> Owned(string id, CancellationToken ct)
    {
        Require(options.Value.Enabled && options.Value.Profile == DistributedFulfillmentCodes.Profile && execution.IsSimulation
            && db.Database.IsRelational(), "SpaceOrderWorkflowDisabled", 404);
        var actor = currentUser.UserId?.Trim(); Require(!string.IsNullOrWhiteSpace(actor), "SpaceOrderAuthenticationRequired", 401);
        var record = await access.조회Async(id, actor!, ct); Require(record is not null, "SpaceOrderCooperationNotFound", 404);
        return (record!, actor!);
    }
    private async Task Save(공간공동운영Record record, OrderExtension data, string actor, long expected, CancellationToken ct)
    {
        var oldJson = record.OrderExtensionJson; var oldUpdatedAt = record.UpdatedAtUtc;
        record.OrderExtensionJson = JsonSerializer.Serialize(data, Json); record.Revision = expected + 1; record.UpdatedAtUtc = Now;
        if (!await store.교체Async(record, expected, actor, ct))
        {
            record.OrderExtensionJson = oldJson; record.Revision = expected; record.UpdatedAtUtc = oldUpdatedAt;
            throw new SpaceOrderException("SpaceOrderRevisionConflict");
        }
    }
    private static OrderExtension Extension(공간공동운영Record r) => string.IsNullOrEmpty(r.OrderExtensionJson) ? new()
        : JsonSerializer.Deserialize<OrderExtension>(r.OrderExtensionJson, Json) ?? throw new SpaceOrderException("SpaceOrderStoredDataInvalid");
    private static bool Party(OrderRecord o, string a) => a == o.BuyerUserId || a == o.SellerUserId;
    private static string Digest<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, Json), Json)!;
    private static Guid StableGuid(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    private static string RootId(Guid id) => "space-order:" + id.ToString("N");
    private static void Require(bool value, string code, int status = 409) { if (!value) throw new SpaceOrderException(code, status); }
    private static Result<공간공동운영주문Response> Fail(string code, int status) => Result.Fail<공간공동운영주문Response>(
        new Error(code).WithMetadata("StatusCode", status).WithMetadata("code", code));
    private static 공간공동운영주문Response Response(공간공동운영Record record, OrderRecord order, string actor, bool replay = false) => new()
    {
        LedgerId = record.LedgerId, Revision = record.Revision, OrderRequestId = order.Terms.OrderRequestId,
        TermsVersion = order.TermsVersion, Terms = Copy(order.Terms), ProductName = order.ProductName,
        TotalKrw = order.Terms.UnitPriceKrw * order.Terms.Quantity, BuyerConsented = order.BuyerConsentedAtUtc.HasValue,
        SellerConsented = order.SellerConsentedAtUtc.HasValue, StatusCode = order.StatusCode,
        RootOrderLedgerId = order.RootOrderLedgerId, DistributedOrderId = order.DistributedOrderId,
        OrderLedgerConnectionStatusCode = order.OrderLedgerConnectionStatusCode, HoldCode = order.HoldCode,
        Replayed = replay, InventoryReserved = order.StatusCode == "confirmed", PaymentConfirmed = false,
        AllowedActions = order.StatusCode == "handoff-pending" || order.StatusCode == "confirmed" && order.OrderLedgerConnectionStatusCode == "Pending"
            ? actor == order.SellerUserId ? ["resume"] : []
            : order.StatusCode != "proposed" ? []
            : order.BuyerConsentedAtUtc.HasValue && order.SellerConsentedAtUtc.HasValue ? actor == order.SellerUserId ? ["handoff", "propose"] : ["propose"]
            : ["agree", "propose"]
    };

    public sealed class OrderExtension { public List<OrderRecord> Orders { get; set; } = []; }
    public sealed class OrderRecord
    {
        public 공간공동운영주문조건Dto Terms { get; set; } = new();
        public int TermsVersion { get; set; }
        public long CooperationTermsRevision { get; set; }
        public string BuyerUserId { get; set; } = "";
        public string SellerUserId { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string SourceDigest { get; set; } = "";
        public DateTimeOffset? BuyerConsentedAtUtc { get; set; }
        public DateTimeOffset? SellerConsentedAtUtc { get; set; }
        public string StatusCode { get; set; } = "proposed";
        public string RootOrderLedgerId { get; set; } = "";
        public Guid HandoffRequestId { get; set; }
        public long HandoffWaveRevision { get; set; }
        public long? HandoffRootRevision { get; set; }
        public string? DistributedOrderId { get; set; }
        public string? OrderLedgerConnectionStatusCode { get; set; }
        public string? HoldCode { get; set; }
        public List<OrderReceipt> Receipts { get; set; } = [];
    }
    public sealed record OrderReceipt(Guid RequestId, string Actor, string Fingerprint);
}

public sealed class SpaceOrderException(string code, int status = 409) : Exception(code)
{ public string Code { get; } = code; public int Status { get; } = status; }
