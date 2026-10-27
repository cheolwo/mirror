using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.도메인.창고;
using static Ssalddel.Application.Warehouse.분산물류UseCase;

namespace Ssalddel.Application.Warehouse;

/// <summary>보관 합의의 조건·동의와 입고 시점 합의 근거만 변경합니다. 저장 transaction과 영수증은 facade가 소유합니다.</summary>
internal sealed class DistributedStorageAgreementCommands(SsalddelContext db, TimeProvider clock)
{
    private IQueryable<T> Query<T>() where T : class => db.Set<T>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private async Task<분산거점Record> Site(string id, CancellationToken ct)
        => await Query<분산거점Record>().SingleOrDefaultAsync(x => x.SiteId == id, ct) ?? throw new DistributedFulfillmentException("DistributedSiteNotFound", 404);

    internal async Task<(string, long, string, string?, int)> CreateAsync(DistributedActor actor, string siteId, DistributedStorageAgreementRequest r, CancellationToken ct)
    {
        var site = await Site(siteId, ct);
        Require(actor.UserId == site.OwnerUserId || actor.UserId == r.GoodsOwnerUserId
            || await db.입고상품.AnyAsync(x => x.소유자UserId == r.GoodsOwnerUserId && x.판매자UserId == actor.UserId, ct), "DistributedAgreementPartyRequired", 403);
        Require(r.ExpectedRevision == 0, "DistributedRevisionConflict");
        Require(await Query<분산적재위치Record>().AnyAsync(x => x.LocationId == r.LocationId && x.SiteId == siteId, ct), "DistributedLocationMismatch");
        var goodsOwner = Text(r.GoodsOwnerUserId, 191); ValidateTerms(r.Terms);
        Require(!await Query<분산보관합의Record>().AnyAsync(x => x.LocationId == r.LocationId && x.GoodsOwnerUserId == goodsOwner, ct), "DistributedAgreementAlreadyExists");
        var agreement = new 분산보관합의Record { AgreementId = Id("agreement"), SiteId = siteId, LocationId = r.LocationId,
            SpaceOwnerUserId = site.OwnerUserId, GoodsOwnerUserId = goodsOwner, CreatorUserId = actor.UserId, TermsJson = Serialize(r.Terms), StartsAtUtc = r.Terms.StartsAtUtc, EndsAtUtc = r.Terms.EndsAtUtc };
        AddAgreementHistory(agreement, actor.UserId, "Draft"); db.Add(agreement);
        return (agreement.AgreementId, agreement.Revision, agreement.StatusCode, (string?)null, 0);
    }

    internal async Task<(string, long, string, string?, int)> TermsAsync(DistributedActor actor, string id, DistributedStorageAgreementRequest r, CancellationToken ct)
    {
        var agreement = await Agreement(id, ct); RequireAgreementParty(actor, agreement); Revision(agreement.Revision, r);
        Require(r.LocationId == agreement.LocationId && r.GoodsOwnerUserId == agreement.GoodsOwnerUserId, "DistributedAgreementPartiesImmutable");
        ValidateTerms(r.Terms);
        if (agreement.StatusCode == "Active" && agreement.SpaceOwnerConsented && agreement.GoodsOwnerConsented)
        {
            agreement.PendingTermsVersion = NextAgreementTermsVersion(agreement);
            agreement.PendingTermsJson = Serialize(r.Terms); agreement.PendingSpaceOwnerConsented = false; agreement.PendingGoodsOwnerConsented = false;
            agreement.Revision++; AddAgreementHistory(agreement, actor.UserId, "TermsProposed");
            return (id, agreement.Revision, AgreementStatus(agreement), (string?)null, 0);
        }
        AddAgreementHistory(agreement, actor.UserId, "Superseded");
        agreement.TermsVersion = NextAgreementTermsVersion(agreement); agreement.TermsJson = Serialize(r.Terms); agreement.StartsAtUtc = r.Terms.StartsAtUtc; agreement.EndsAtUtc = r.Terms.EndsAtUtc;
        ClearPendingTerms(agreement);
        agreement.SpaceOwnerConsented = false; agreement.GoodsOwnerConsented = false; agreement.StatusCode = "Draft"; agreement.Revision++;
        AddAgreementHistory(agreement, actor.UserId, "Draft"); return (id, agreement.Revision, agreement.StatusCode, (string?)null, 0);
    }

    internal async Task<(string, long, string, string?, int)> ConsentAsync(DistributedActor actor, string id, DistributedStorageAgreementConsentRequest r, CancellationToken ct)
    {
        var agreement = await Agreement(id, ct); RequireAgreementParty(actor, agreement); Revision(agreement.Revision, r);
        if (agreement.PendingTermsVersion == r.TermsVersion && agreement.PendingTermsJson is not null)
        {
            var pending = Deserialize<DistributedStorageTerms>(agreement.PendingTermsJson);
            if (r.Granted) Require(Now < pending.EndsAtUtc, "DistributedAgreementExpired");
            if (actor.UserId == agreement.SpaceOwnerUserId) agreement.PendingSpaceOwnerConsented = r.Granted;
            if (actor.UserId == agreement.GoodsOwnerUserId) agreement.PendingGoodsOwnerConsented = r.Granted;
            AddAgreementHistory(agreement, actor.UserId, r.Granted ? "PendingGranted" : "PendingRejected");
            if (!r.Granted) ClearPendingTerms(agreement);
            else if (agreement.PendingSpaceOwnerConsented && agreement.PendingGoodsOwnerConsented)
            {
                AddAgreementHistory(agreement, actor.UserId, "Superseded");
                agreement.TermsVersion = r.TermsVersion; agreement.TermsJson = agreement.PendingTermsJson;
                agreement.StartsAtUtc = pending.StartsAtUtc; agreement.EndsAtUtc = pending.EndsAtUtc;
                agreement.SpaceOwnerConsented = true; agreement.GoodsOwnerConsented = true; agreement.StatusCode = "Active";
                ClearPendingTerms(agreement); AddAgreementHistory(agreement, actor.UserId, "Activated");
            }
            agreement.Revision++; return (id, agreement.Revision, AgreementStatus(agreement), (string?)null, 0);
        }
        Require(r.TermsVersion == agreement.TermsVersion, "DistributedAgreementTermsVersionMismatch");
        if (r.Granted) Require(Now < agreement.EndsAtUtc, "DistributedAgreementExpired");
        if (actor.UserId == agreement.SpaceOwnerUserId) agreement.SpaceOwnerConsented = r.Granted;
        if (actor.UserId == agreement.GoodsOwnerUserId) agreement.GoodsOwnerConsented = r.Granted;
        agreement.StatusCode = !r.Granted ? "Revoked" : agreement.SpaceOwnerConsented && agreement.GoodsOwnerConsented ? "Active" : "AwaitingOtherParty";
        if (!r.Granted) ClearPendingTerms(agreement);
        agreement.Revision++; AddAgreementHistory(agreement, actor.UserId, r.Granted ? "Granted" : "Revoked");
        return (id, agreement.Revision, AgreementStatus(agreement), (string?)null, 0);
    }

    internal async Task<분산보관합의Record> Agreement(string id, CancellationToken ct)
        => await Query<분산보관합의Record>().SingleOrDefaultAsync(x => x.AgreementId == id, ct)
            ?? throw new DistributedFulfillmentException("DistributedAgreementNotFound", 404);
    private static void RequireAgreementParty(DistributedActor actor, 분산보관합의Record agreement)
        => Require(actor.UserId == agreement.SpaceOwnerUserId || actor.UserId == agreement.GoodsOwnerUserId, "DistributedAgreementPartyRequired", 403);
    private static void ValidateTerms(DistributedStorageTerms terms)
    {
        Require(terms is not null && terms.AllowedSkus is { Count: > 0 and <= 30 } && terms.AllowedSkus.All(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 100)
            && terms.AllowedSkus.Distinct(StringComparer.Ordinal).Count() == terms.AllowedSkus.Count && terms.MaximumStoredQuantity is > 0 and <= 1000000
            && terms.StartsAtUtc.Kind == DateTimeKind.Utc && terms.EndsAtUtc.Kind == DateTimeKind.Utc && terms.StartsAtUtc < terms.EndsAtUtc,
            "DistributedAgreementTermsInvalid", 400);
        Text(terms!.AccessHours, 200); Text(terms.PriceOrFreeTerms, 500); Text(terms.CustodianAndCare, 500); Text(terms.LossDamageAndReturnTerms, 1000);
    }
    internal string AgreementStatus(분산보관합의Record a) => a.StatusCode == "Active" ? Now >= a.EndsAtUtc ? "Expired" : Now < a.StartsAtUtc ? "NotStarted" : "Active" : a.StatusCode;
    private static void ClearPendingTerms(분산보관합의Record agreement)
    {
        agreement.PendingTermsJson = null; agreement.PendingTermsVersion = null;
        agreement.PendingSpaceOwnerConsented = false; agreement.PendingGoodsOwnerConsented = false;
    }
    private static int NextAgreementTermsVersion(분산보관합의Record agreement)
        => Math.Max(Math.Max(agreement.TermsVersion, agreement.PendingTermsVersion ?? 0),
            Deserialize<List<DistributedAgreementHistory>>(agreement.HistoryJson).Select(x => x.TermsVersion).DefaultIfEmpty(0).Max()) + 1;
    private void AddAgreementHistory(분산보관합의Record agreement, string actorId, string action)
    {
        var history = Deserialize<List<DistributedAgreementHistory>>(agreement.HistoryJson);
        var pending = action is "TermsProposed" or "PendingGranted" or "PendingRejected";
        history.Add(new(pending ? agreement.PendingTermsVersion!.Value : agreement.TermsVersion, actorId, action,
            pending ? agreement.PendingSpaceOwnerConsented : agreement.SpaceOwnerConsented,
            pending ? agreement.PendingGoodsOwnerConsented : agreement.GoodsOwnerConsented,
            pending ? agreement.PendingTermsJson! : agreement.TermsJson, Now));
        agreement.HistoryJson = Serialize(history);
    }
    internal async Task<분산보관합의Record> ActiveAgreement(분산재고LotRecord lot, CancellationToken ct)
    {
        var agreement = await Query<분산보관합의Record>().SingleOrDefaultAsync(x => x.LocationId == lot.LocationId && x.GoodsOwnerUserId == lot.OwnerUserId, ct);
        Require(agreement is not null && AgreementStatus(agreement) == "Active" && agreement.SpaceOwnerConsented && agreement.GoodsOwnerConsented
            && agreement.SiteId == lot.SiteId && Enumerable.Contains(Deserialize<DistributedStorageTerms>(agreement.TermsJson).AllowedSkus, lot.Sku), "DistributedStorageAgreementRequired");
        return agreement!;
    }
    internal void SnapshotAgreement(분산재고LotRecord lot, 분산보관합의Record agreement, int quantity)
    {
        lot.StorageAgreementId = agreement.AgreementId;
        var history = Deserialize<List<DistributedLotAgreementSnapshot>>(lot.StorageAgreementSnapshotJson);
        history.Add(new(agreement.AgreementId, agreement.TermsVersion, agreement.SpaceOwnerUserId, agreement.GoodsOwnerUserId, agreement.TermsJson, quantity, Now));
        lot.StorageAgreementSnapshotJson = Serialize(history);
    }
    internal async Task AgreementCapacity(분산보관합의Record agreement, int addingQuantity, CancellationToken ct)
    {
        var relatedLots = await Query<분산재고LotRecord>().Where(x => x.LocationId == agreement.LocationId && x.OwnerUserId == agreement.GoodsOwnerUserId).ToArrayAsync(ct);
        var ids = relatedLots.Select(x => x.LotId).ToArray();
        var custody = await Query<분산재고예약Record>().Where(x => Enumerable.Contains(ids, x.LotId)).ToArrayAsync(ct);
        var stored = relatedLots.Sum(x => x.ReceivedQuantity) - custody.Sum(x => x.PickedQuantity - x.ReturnedQuantity);
        Require(stored + addingQuantity <= Deserialize<DistributedStorageTerms>(agreement.TermsJson).MaximumStoredQuantity, "DistributedStorageAgreementCapacityExceeded");
    }
}
