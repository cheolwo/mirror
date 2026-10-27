using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Mart;
using Ssalddel.Services.Community;
using 살뜰.도메인.마트;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Mart;

public sealed partial class 공간공동운영주문UseCase
{
    private async Task<SourceSnapshot> ReadSource(공간공동운영Record record, string actor,
        공간공동운영주문조건Dto terms, bool requireAvailable, CancellationToken ct, bool allowConfirmationPending = false)
    {
        Require(terms.OrderRequestId != Guid.Empty && terms.PublicProductId > 0 && terms.Quantity is > 0 and <= 100
            && terms.UnitPriceKrw >= 0 && terms.UnitPriceKrw <= 1_000_000_000m && terms.DeadlineUtc.Kind == DateTimeKind.Utc
            && terms.DeadlineUtc > Now.UtcDateTime && terms.ReceiptMethodCode == "delivery", "SpaceOrderTermsInvalid", 400);
        Require(new[] { terms.CycleId, terms.SiteId, terms.LotId, terms.WaveId, terms.CargoRequestId }
            .All(s => !string.IsNullOrWhiteSpace(s) && s == s.Trim() && s.Length <= 191), "SpaceOrderReferencesInvalid", 400);
        var cycle = record.Cycles.SingleOrDefault(c => c.CycleId == terms.CycleId);
        Require(cycle is not null && cycle.TermsRevision == record.TermsRevision && cycle.StatusCode is "prepared" or "running",
            "SpaceOrderCycleNotReady");
        Require(cycle!.References.Any(r => r.KindCode == "DistributedSite" && r.ReferenceId == terms.SiteId)
            && cycle.References.Any(r => r.KindCode == "DistributedWave" && r.ReferenceId == terms.WaveId), "SpaceOrderCycleReferenceMismatch");
        var source = await db.마트주문요청.AsNoTracking().SingleOrDefaultAsync(x => x.Id == terms.OrderRequestId, ct);
        Require(source is not null && (source.상태코드 == 마트주문요청상태코드.제출됨
                || allowConfirmationPending && source.상태코드 == 마트주문요청상태코드.확정확인중) && source.비구속주문요청확인
            && source.공개상품Id == terms.PublicProductId && source.수량 == terms.Quantity && source.단가Snapshot == terms.UnitPriceKrw
            && source.합계Snapshot == terms.UnitPriceKrw * terms.Quantity && source.통화 == "KRW", "SpaceOrderSourceChanged");
        var product = await db.마트공개상품.AsNoTracking().SingleOrDefaultAsync(p => p.Id == terms.PublicProductId, ct);
        Require(product is not null && product.공개여부 && product.판매허용여부 && product.판매상품Id.HasValue
            && product.판매가 == terms.UnitPriceKrw && product.상품명 == source!.상품명Snapshot
            && product.판매단위 == source.판매단위Snapshot, "SpaceOrderProductChanged");
        var sales = await db.판매상품.AsNoTracking().SingleOrDefaultAsync(p => p.Id == product!.판매상품Id, ct);
        var lot = await db.Set<분산재고LotRecord>().AsNoTracking().SingleOrDefaultAsync(l => l.LotId == terms.LotId, ct);
        Require(sales is not null && lot is not null && lot.SiteId == terms.SiteId && lot.InboundItemId == sales.입고상품Id
            && lot.SellerUserId == sales.소유자UserId, "SpaceOrderSalesAuthorityMismatch", 403);
        var seller = sales!.소유자UserId; var buyer = source!.요청자UserId;
        Require(seller != buyer && (actor == buyer || actor == seller), "SpaceOrderPartyRequired", 403);
        Require(record.Participants.Any(p => p.UserId == buyer && p.RoleCode == 공간공동운영역할Codes.Resident && p.StateCode == "accepted" && p.AgreedTermsRevision == record.TermsRevision)
            && record.Participants.Any(p => p.UserId == seller && p.RoleCode == 공간공동운영역할Codes.Supplier && p.StateCode == "accepted" && p.AgreedTermsRevision == record.TermsRevision),
            "SpaceOrderAcceptedPartiesRequired", 403);
        var site = await db.Set<분산거점Record>().AsNoTracking().SingleOrDefaultAsync(s => s.SiteId == terms.SiteId, ct);
        var stock = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sales.입고상품Id, ct);
        Require(site is not null && site.StatusCode == "Active" && site.OwnerConsented && site.ManagerConsented
            && stock is not null && stock.창고Id == site.WarehouseId && stock.SKU == lot!.Sku
            && stock.판매자UserId == seller && stock.소유자UserId == lot!.OwnerUserId
            && lot.ReceivedQuantity > 0, "SpaceOrderInventoryLineageMismatch");
        if (requireAvailable) Require(stock!.가용수량 >= terms.Quantity && product!.판매가능수량 >= terms.Quantity,
            "SpaceOrderQuantityUnavailable");
        var collaboration = lot!.InboundCollaborationJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<DistributedInboundCollaborationDto>(lot.InboundCollaborationJson, Json);
        Require(collaboration is not null && collaboration.StatusCode == "Confirmed" && collaboration.SpaceOwnerConsented
            && collaboration.GoodsOwnerConsented && collaboration.Terms.Goods.TemperatureCode == DistributedInboundCodes.Ambient
            && collaboration.Terms.Goods.PackagingCode is DistributedInboundCodes.Sealed or DistributedInboundCodes.Boxed
            && (collaboration.Terms.Goods.IsFood != true || collaboration.Terms.Goods.IsProcessed == true),
            "SpaceOrderAmbientPackagedGoodsRequired");
        var agreement = await db.Set<분산보관합의Record>().AsNoTracking().SingleOrDefaultAsync(a => a.AgreementId == lot.StorageAgreementId, ct);
        Require(agreement is not null && agreement.SiteId == site!.SiteId && agreement.GoodsOwnerUserId == lot.OwnerUserId
            && agreement.StatusCode == "Active" && agreement.SpaceOwnerConsented && agreement.GoodsOwnerConsented
            && agreement.StartsAtUtc <= Now.UtcDateTime && Now.UtcDateTime < agreement.EndsAtUtc, "SpaceOrderStorageAgreementRequired");
        var wave = await db.Set<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(w => w.WaveId == terms.WaveId, ct);
        var cargo = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(c => c.의뢰Id == terms.CargoRequestId, ct);
        Require(wave is not null && wave.StatusCode == "Open" && Now.UtcDateTime < wave.CutoffUtc
            && wave.DeliveryEndUtc <= terms.DeadlineUtc && cargo is not null && cargo.화주Id == seller && cargo.주문자UserId == buyer
            && cargo.화물수량 == terms.Quantity && cargo.하차_시간창_종료일시.HasValue
            && cargo.하차_시간창_종료일시.Value <= terms.DeadlineUtc, "SpaceOrderDeliverySourceRequired");
        return new(buyer, seller, source.상품명Snapshot, SourceDigest(source));
    }

    private async Task<커뮤니티원장Dto> EnsureRoot(공간공동운영Record cooperation, OrderRecord order, string actor, CancellationToken ct)
    {
        var fingerprint = Digest(new { cooperation.LedgerId, order.Terms, order.TermsVersion, order.BuyerUserId, order.SellerUserId,
            order.BuyerConsentedAtUtc, order.SellerConsentedAtUtc, order.SourceDigest });
        var existing = await ledgers.원장조회Async(order.RootOrderLedgerId, ct);
        if (existing is not null) return SameRoot(existing);
        try
        {
            return await ledgers.원장저장Async(new()
            {
                원장Id = order.RootOrderLedgerId, 기대Revision = 0, 커뮤니티Id = "platform",
                원장템플릿Key = CommunityLedgerTemplateKeys.Order, 제목 = order.ProductName + " · 공동운영 주문",
                생성자UserId = order.BuyerUserId, 생성자표시명 = "주문 참여자", 상태 = 커뮤니티원장상태.진행중,
                현재단계Key = "양측 조건 확인", 외부참조 = new Dictionary<string, string>
                {
                    ["space-cooperation-id"] = cooperation.LedgerId,
                    ["space-cooperation-order-request"] = order.Terms.OrderRequestId.ToString("N"),
                    ["space-cooperation-order-fingerprint"] = fingerprint
                },
                참여자목록 = [new() { UserId = order.BuyerUserId, DisplayName = "주문 참여자", RoleLabel = "구매자", ParticipationState = "참여중" },
                    new() { UserId = order.SellerUserId, DisplayName = "판매 담당자", RoleLabel = "판매자", ParticipationState = "참여중" }],
                블록목록 = [new() { BlockId = "space-order-terms", BlockType = CommunityLedgerBlockTypes.Order, Title = "양측이 확인한 상품·수량·금액·수령 조건",
                    State = "확인됨", Data = new Dictionary<string, string> { ["terms"] = System.Text.Json.JsonSerializer.Serialize(order.Terms, Json),
                        ["termsVersion"] = order.TermsVersion.ToString(System.Globalization.CultureInfo.InvariantCulture), ["sourceFingerprint"] = order.SourceDigest,
                        ["buyerConsentedAtUtc"] = order.BuyerConsentedAtUtc!.Value.ToString("O"), ["sellerConsentedAtUtc"] = order.SellerConsentedAtUtc!.Value.ToString("O") } }]
            }, actor, ct);
        }
        catch (InvalidOperationException)
        {
            var raced = await ledgers.원장조회Async(order.RootOrderLedgerId, ct);
            if (raced is null) throw;
            return SameRoot(raced);
        }
        커뮤니티원장Dto SameRoot(커뮤니티원장Dto root)
        {
            Require(원주문과일치(cooperation.LedgerId, order, root), "SpaceOrderExistingRootConflict");
            return root;
        }
    }

    internal static bool 원주문과일치(string ledgerId, OrderRecord order, 커뮤니티원장Dto root)
    {
        var fingerprint = Digest(new { LedgerId = ledgerId, order.Terms, order.TermsVersion, order.BuyerUserId, order.SellerUserId,
            order.BuyerConsentedAtUtc, order.SellerConsentedAtUtc, order.SourceDigest });
        var termsBlock = root.블록목록.SingleOrDefault(b => b.BlockId == "space-order-terms" && b.BlockType == CommunityLedgerBlockTypes.Order);
        return root.원장템플릿Key == CommunityLedgerTemplateKeys.Order && root.커뮤니티Id == "platform"
            && root.생성자UserId == order.BuyerUserId && root.외부참조.GetValueOrDefault("space-cooperation-id") == ledgerId
            && root.외부참조.GetValueOrDefault("space-cooperation-order-request") == order.Terms.OrderRequestId.ToString("N")
            && root.외부참조.GetValueOrDefault("space-cooperation-order-fingerprint") == fingerprint
            && termsBlock is not null && termsBlock.Data.GetValueOrDefault("terms") == System.Text.Json.JsonSerializer.Serialize(order.Terms, Json)
            && termsBlock.Data.GetValueOrDefault("sourceFingerprint") == order.SourceDigest
            && termsBlock.Data.GetValueOrDefault("buyerConsentedAtUtc") == order.BuyerConsentedAtUtc!.Value.ToString("O")
            && termsBlock.Data.GetValueOrDefault("sellerConsentedAtUtc") == order.SellerConsentedAtUtc!.Value.ToString("O")
            && root.참여자목록.Any(p => p.UserId == order.SellerUserId && p.RoleLabel == "판매자" && p.ParticipationState == "참여중")
            && root.참여자목록.Any(p => p.UserId == order.BuyerUserId && p.RoleLabel == "구매자" && p.ParticipationState == "참여중");
    }

    private static string SourceDigest(마트주문요청 source) => Digest(new { source.Id, source.요청자UserId, source.공개상품Id,
        source.상품명Snapshot, source.판매단위Snapshot, source.단가Snapshot, source.수량, source.합계Snapshot,
        source.통화, source.CreatedAtUtc, source.비구속주문요청확인, source.안내버전 });
    private sealed record SourceSnapshot(string BuyerUserId, string SellerUserId, string ProductName, string Digest);
}
