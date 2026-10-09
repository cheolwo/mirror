using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public async Task<DistributedEvidenceBinding> AuthorizeEvidenceAsync(DistributedActor actor, DistributedEvidenceWorkRequest work, CancellationToken ct)
    {
        Gate(actor);
        Require(work is not null && new object?[] { work.Receive, work.Pick, work.Deliver, work.Return, work.ImportLot }.Count(x => x is not null) == 1, "DistributedEvidenceWorkInvalid", 400);
        DistributedCommandRequest request; 분산재고LotRecord[] lots; string[] siteIds; string[] orderIds; string[] partyIds; string scope; object lines;
        switch (work!.Kind)
        {
            case "Receive" when work.Receive is { } r:
                var receive = await PrepareReceive(actor, work.AggregateId, r, ct); request = r; lots = receive.Lots;
                siteIds = [receive.Site.SiteId]; orderIds = []; scope = receive.Location.LocationId;
                partyIds = [receive.Site.OwnerUserId, receive.Site.ManagerUserId, .. lots.Select(x => x.OwnerUserId), .. lots.Select(x => x.SellerUserId)];
                lines = new { agreementId = receive.Agreement.AgreementId, receive.Agreement.TermsVersion,
                    termsDigest = DistributedEvidenceDigests.Payload(Deserialize<DistributedStorageTerms>(receive.Agreement.TermsJson)),
                    items = r.Lines.OrderBy(x => x.LineKey, StringComparer.Ordinal).Select(x => new { x.LineKey, x.Sku, x.Quantity }).ToArray() }; break;
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
}
