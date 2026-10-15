using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public async Task<DistributedEvidenceBinding> AuthorizeEvidenceAsync(DistributedActor actor, DistributedEvidenceWorkRequest work, CancellationToken ct)
    {
        Gate(actor);
        Require(work is not null && new object?[] { work.Receive, work.Pick, work.Deliver, work.Return, work.ImportLot, work.StockRelease }.Count(x => x is not null) == 1, "DistributedEvidenceWorkInvalid", 400);
        DistributedCommandRequest request; 분산재고LotRecord[] lots; string[] siteIds; string[] orderIds; string[] partyIds; string scope; object lines;
        switch (work!.Kind)
        {
            case "Receive" when work.Receive is { } r:
                var receive = await PrepareReceive(actor, work.AggregateId, r, ct); request = r; lots = receive.Lots;
                siteIds = [receive.Site.SiteId]; orderIds = []; scope = receive.Location.LocationId;
                partyIds = [receive.Site.OwnerUserId, receive.Site.ManagerUserId, .. lots.Select(x => x.OwnerUserId), .. lots.Select(x => x.SellerUserId)];
                var receiptItems = r.Lines.OrderBy(x => x.LineKey, StringComparer.Ordinal).Select(x => new { x.LineKey, x.Sku, x.Quantity }).ToArray();
                lines = receive.Lots.Any(x => x.InboundCollaborationJson is not null) ? (object)new { agreementId = receive.Agreement.AgreementId, receive.Agreement.TermsVersion,
                    termsDigest = DistributedEvidenceDigests.Payload(Deserialize<DistributedStorageTerms>(receive.Agreement.TermsJson)),
                    items = receiptItems, inboundApprovals = receive.Lots.OrderBy(x => x.LineKey, StringComparer.Ordinal).Select(x => new { x.LineKey, Collaboration = Collaboration(x) }).ToArray() }
                    : new { agreementId = receive.Agreement.AgreementId, receive.Agreement.TermsVersion,
                        termsDigest = DistributedEvidenceDigests.Payload(Deserialize<DistributedStorageTerms>(receive.Agreement.TermsJson)), items = receiptItems }; break;
            case "Pick" when work.Pick is { } r:
                var pick = await PreparePick(actor, work.AggregateId, r, ct); request = r; lots = pick.Lots; siteIds = [pick.Site.SiteId];
                orderIds = pick.Orders.Select(x => x.OrderId).ToArray(); scope = pick.Location.LocationId;
                partyIds = [pick.Site.OwnerUserId, pick.Site.ManagerUserId, .. lots.Select(x => x.OwnerUserId), .. lots.Select(x => x.SellerUserId)];
                lines = r.Lines.OrderBy(x => x.ReservationId, StringComparer.Ordinal).Select(x => new { x.ReservationId, x.OwnerUserId, x.Sku, x.Quantity }).ToArray(); break;
            case "Deliver" when work.Deliver is { } r:
                var deliver = await PrepareDelivery(actor, work.AggregateId, r, ct); request = r; lots = deliver.Lots; siteIds = lots.Select(x => x.SiteId).Distinct().ToArray();
                orderIds = deliver.Orders.Select(x => x.OrderId).ToArray(); scope = deliver.Orders[0].CustomerUserId;
                partyIds = [deliver.Wave.CreatorUserId, .. deliver.Orders.Select(x => x.CustomerUserId), .. deliver.Orders.Select(x => x.SellerUserId), .. lots.Select(x => x.OwnerUserId)];
                lines = r.Lines.OrderBy(x => x.ReservationId, StringComparer.Ordinal).Select(x => new { x.ReservationId, x.Quantity }).ToArray(); break;
            case "Return" when work.Return is { } r:
                var returned = await PrepareReturn(actor, work.AggregateId, r, ct); request = r; lots = returned.Lots; siteIds = [returned.Site.SiteId];
                orderIds = returned.Orders.Select(x => x.OrderId).ToArray(); scope = returned.Location.LocationId;
                partyIds = [returned.Site.OwnerUserId, returned.Site.ManagerUserId, .. lots.Select(x => x.OwnerUserId), .. lots.Select(x => x.SellerUserId)];
                lines = r.Lines.OrderBy(x => x.ReservationId, StringComparer.Ordinal).Select(x => new { x.ReservationId, x.Quantity }).ToArray(); break;
            case "ImportLot" when work.ImportLot is { } r:
                var import = await PrepareImport(actor, work.AggregateId, r, ct); request = r; lots = [];
                siteIds = [import.Site.SiteId]; orderIds = []; scope = import.Location.LocationId;
                partyIds = [import.Site.OwnerUserId, import.Site.ManagerUserId, import.Item.소유자UserId, import.Item.판매자UserId];
                lines = new { r.InboundItemId, ownerUserId = import.Item.소유자UserId, sellerUserId = import.Item.판매자UserId, sku = import.Item.SKU, quantity = import.Item.입고수량,
                    agreementId = import.Agreement.AgreementId, import.Agreement.TermsVersion, termsDigest = DistributedEvidenceDigests.Payload(Deserialize<DistributedStorageTerms>(import.Agreement.TermsJson)) }; break;
            case "StockRelease" when work.StockRelease is { } r:
                var released = await PrepareStockRelease(actor, work.AggregateId, r, ct); request = r; lots = [released.Lot];
                siteIds = [released.Site.SiteId]; orderIds = []; scope = released.Location.LocationId;
                partyIds = [released.Release.SpaceOwnerUserId, released.Release.GoodsOwnerUserId, released.Release.AssignedUserId];
                lines = new { release = released.Release, r.Quantity, lotId = released.Lot.LotId, sku = released.Lot.Sku }; break;
            default: throw new DistributedFulfillmentException("DistributedEvidenceWorkInvalid", 400);
        }
        Require(request.RequestId != Guid.Empty, "DistributedRequestIdRequired", 400);
        return new() { ActorUserId = actor.UserId, RequestId = request.RequestId, Kind = work.Kind, AggregateId = work.AggregateId,
            ExpectedRevision = request.ExpectedRevision, ScopeId = scope, InputDigest = DistributedEvidenceDigests.Payload(request),
            LinesDigest = DistributedEvidenceDigests.Payload(lines), ParticipantUserIds = partyIds.Append(actor.UserId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            LotIds = lots.Select(x => x.LotId).Distinct().Order(StringComparer.Ordinal).ToArray(), OrderIds = orderIds, SiteIds = siteIds };
    }
    private async Task BindWork(DistributedActor actor, DistributedEvidenceWorkRequest work, IReadOnlyList<string> ids, CancellationToken ct)
    {
        Require(ids is { Count: >= 1 and <= 3 } && ids.All(x => !string.IsNullOrWhiteSpace(x)) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count,
            "DistributedPhotoEvidenceRequired", 400);
        await photoEvidence.BindAsync(await AuthorizeEvidenceAsync(actor, work, ct), ids, ct);
    }
    private sealed record CompletionLineDraft(분산물류완료행 Line, 재고이력? History = null);
    private static 분산물류완료행 CompletionLine(DistributedActor actor, string workKind, string lineKey,
        분산재고LotRecord lot, int quantity, IReadOnlyList<string> photos,
        분산재고예약Record? reservation = null, 분산배송WaveRecord? wave = null)
        => new() { LineKey = lineKey, WorkKind = workKind, ActorUserId = actor.UserId, LotId = lot.LotId,
            InboundItemId = lot.InboundItemId, ReservationId = reservation?.ReservationId, OrderId = reservation?.OrderId,
            RootOrderLedgerId = reservation is not null && wave is not null
                ? 분산주문원장Metadata.Connections(wave).GetValueOrDefault(reservation.OrderId)?.RootOrderLedgerId : null,
            Quantity = quantity, PhotoEvidenceIds = photos.Order(StringComparer.Ordinal).ToArray() };
    private async Task SaveCompletionLines(DistributedActor actor, DistributedCommandRequest request,
        IReadOnlyList<CompletionLineDraft> lines, CancellationToken ct)
    {
        Require(lines.Count > 0 && lines.Select(x => x.Line.LineKey).Distinct(StringComparer.Ordinal).Count() == lines.Count,
            "DistributedCompletionLinesInvalid");
        // 기존 재고이력의 실제 ID를 사용합니다. 별도 작업 ID나 과거 행의 추정 연결을 만들지 않습니다.
        await db.SaveChangesAsync(ct);
        Require(!db.ChangeTracker.Entries<분산물류CommandReceipt>().Any(x => x.Entity.ActorUserId == actor.UserId
            && x.Entity.RequestId == request.RequestId), "DistributedCompletionEvidenceAlreadyRecorded");
        db.Add(new 분산물류CommandReceipt { ActorUserId = actor.UserId, RequestId = request.RequestId,
            CompletionLinesJson = Serialize(new 분산물류완료근거(1,
                lines.Select(x => x.Line with { InventoryHistoryId = x.History?.Id }).ToArray())) });
    }
}
