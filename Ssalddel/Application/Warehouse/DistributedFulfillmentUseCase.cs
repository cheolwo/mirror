using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Ssalddel.Services.Dispatch.Participation;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Data;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed class DistributedFulfillmentOptions
{
    public const string SectionName = "DistributedFulfillment";
    public bool Enabled { get; set; }
    public string Profile { get; set; } = "";
    public bool UseExistingRouteProvider { get; set; }
    public int MaximumRouteStops { get; set; } = 8;
}
public sealed record DistributedActor(string UserId, bool IsDriver = false, bool IsAdmin = false);
public sealed class DistributedFulfillmentException(string code, int status = 409) : Exception(code)
{ public string Code { get; } = code; public int Status { get; } = status; }
public interface IDistributedFulfillmentUseCase
{
    Task<DistributedWorkspaceDto> WorkspaceAsync(DistributedActor actor, string roleKey, CancellationToken ct);
    Task<분산공간재고PageDto> 공간재고Async(DistributedActor actor, string siteId, string roleKey, int skip, int take, CancellationToken ct);
    Task<DistributedWaveDto> WaveAsync(DistributedActor actor, string id, string roleKey, CancellationToken ct);
    Task<DistributedRootOrderCandidatesDto> RootOrderCandidatesAsync(DistributedActor actor, string waveId,
        string cargoRequestId, string roleKey, CancellationToken ct);
    Task<DistributedWorldProjectionDto> WorldAsync(DistributedActor actor, string roleKey, CancellationToken ct);
    Task<DistributedReceiptDto> ReceiptAsync(DistributedActor actor, Guid id, CancellationToken ct);
    Task<DistributedEvidenceBinding> AuthorizeEvidenceAsync(DistributedActor actor, DistributedEvidenceWorkRequest work, CancellationToken ct);
    Task<DistributedCommandResponse> AgreementAsync(DistributedActor actor, string siteId, DistributedStorageAgreementRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> AgreementTermsAsync(DistributedActor actor, string id, DistributedStorageAgreementRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> AgreementConsentAsync(DistributedActor actor, string id, DistributedStorageAgreementConsentRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> SiteAcceptancePolicyAsync(DistributedActor actor, string id, DistributedSiteAcceptancePolicyRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> InboundProposalAsync(DistributedActor actor, string id, string lineKey, DistributedInboundProposalRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> InboundConsentAsync(DistributedActor actor, string id, string lineKey, DistributedInboundConsentRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> InboundDecisionAsync(DistributedActor actor, string id, string lineKey, DistributedInboundDecisionRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> InboundNoteAsync(DistributedActor actor, string id, string lineKey, DistributedInboundNoteRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> ReceiveBatchAsync(DistributedActor actor, string id, DistributedInboundReceiptBatchRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> PickBatchAsync(DistributedActor actor, string id, DistributedPickBatchRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> DeliverBatchAsync(DistributedActor actor, string id, DistributedDeliveryBatchRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> ReturnBatchAsync(DistributedActor actor, string id, DistributedReturnBatchRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> SiteAsync(DistributedActor actor, DistributedSiteCreateRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> ConsentAsync(DistributedActor actor, string id, DistributedConsentRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> StockReleaseProposalAsync(DistributedActor actor, string siteId, DistributedStockReleaseProposalRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> StockReleaseConsentAsync(DistributedActor actor, string siteId, DistributedStockReleaseConsentRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> StockReleaseHandoverAsync(DistributedActor actor, string siteId, DistributedStockReleaseHandoverRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> CloseSiteAsync(DistributedActor actor, string siteId, DistributedSiteCloseRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> LocationAsync(DistributedActor actor, string id, DistributedLocationCreateRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> ImportLotAsync(DistributedActor actor, string id, DistributedExistingLotRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> InboundAsync(DistributedActor actor, DistributedInboundPlanRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> ReceiveAsync(DistributedActor actor, string id, DistributedInboundReceiptRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> CreateWaveAsync(DistributedActor actor, DistributedWaveCreateRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> OrderAsync(DistributedActor actor, string id, DistributedOrderRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> LinkOrderLedgerAsync(DistributedActor actor, string waveId, string orderId, DistributedOrderLedgerLinkRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> CloseAsync(DistributedActor actor, string id, DistributedCommandRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> PlanAsync(DistributedActor actor, string id, DistributedRoundPlanRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> AcceptAsync(DistributedActor actor, string id, DistributedRoundAcceptRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> PickAsync(DistributedActor actor, string id, DistributedPickRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> DeliverAsync(DistributedActor actor, string id, DistributedDeliveryRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> CancelAsync(DistributedActor actor, string id, DistributedCancelRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> ReturnAsync(DistributedActor actor, string id, DistributedReturnRequest request, CancellationToken ct);
    Task<DistributedCommandResponse> AcknowledgeAsync(DistributedActor actor, string id, DistributedAcknowledgementRequest request, CancellationToken ct);
}

public sealed partial class 분산물류UseCase(SsalddelContext db, ISsalddelExecutionModePolicy execution,
    IOptions<DistributedFulfillmentOptions> options, I분산배송경로Planner planner, I분산화물연결Service cargo,
    TimeProvider clock, IDriverLocationStore driverLocations, IDistributedBuildingCandidateProvider buildingCandidates,
    IDistributedPhotoEvidenceBinder photoEvidence, I분산주문원장연결Adapter? orderLedgers = null,
    ISharedDriverWorkAdmissionService? sharedAdmission = null) : IDistributedFulfillmentUseCase
{
    private readonly DistributedStorageAgreementCommands agreementCommands = new(db, clock);
    private readonly DistributedInventoryCommandExecution inventoryCommands = new(db, clock, photoEvidence);
    private readonly DistributedPickingCommandExecution pickingCommands = new(db, clock, cargo);
    private readonly DistributedDeliveryCommandExecution deliveryCommands = new(db, clock, cargo);
    private readonly DistributedCompletionRecorder completionRecorder = new(db, clock);
    private readonly DistributedInboundCompletion inboundCompletion = new(db);
    private IQueryable<T> Query<T>() where T : class => db.Set<T>();
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    internal static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json)!;
    private void Gate(DistributedActor actor)
    {
        Require(options.Value.Enabled && execution.IsSimulation && options.Value.Profile == DistributedFulfillmentCodes.Profile,
            "DistributedFulfillmentDisabled", 404);
        Require(!string.IsNullOrWhiteSpace(actor.UserId), "DistributedAuthenticationRequired", 401);
        Require(db.Database.IsRelational(), "DistributedRelationalStorageRequired");
    }
    internal static void Require(bool value, string code, int status = 409)
    { if (!value) throw new DistributedFulfillmentException(code, status); }
    internal static string Text(string? value, int max = 120)
    {
        var result = value?.Trim() ?? ""; Require(result.Length > 0 && result.Length <= max, "DistributedInputInvalid", 400); return result;
    }
    internal static string Id(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");
    internal static void Revision(long current, DistributedCommandRequest request)
        => Require(current == request.ExpectedRevision, "DistributedRevisionConflict");
    private static bool Manages(DistributedActor a, 분산거점Record s) => a.IsAdmin || a.UserId == s.OwnerUserId || a.UserId == s.ManagerUserId;
    private async Task<분산거점Record> Site(string id, CancellationToken ct)
        => await Query<분산거점Record>().SingleOrDefaultAsync(x => x.SiteId == id, ct) ?? throw new DistributedFulfillmentException("DistributedSiteNotFound", 404);
    private async Task<분산배송WaveRecord> Wave(string id, CancellationToken ct)
        => await Query<분산배송WaveRecord>().SingleOrDefaultAsync(x => x.WaveId == id, ct) ?? throw new DistributedFulfillmentException("DistributedWaveNotFound", 404);
    private async Task Operator(DistributedActor actor, CancellationToken ct)
        => Require(actor.IsAdmin || await Query<분산거점Record>().AnyAsync(x => x.OwnerUserId == actor.UserId || x.ManagerUserId == actor.UserId, ct), "DistributedOperatorRequired", 403);
    private static void OwnWave(DistributedActor actor, 분산배송WaveRecord wave)
        => Require(actor.IsAdmin || wave.CreatorUserId == actor.UserId, "DistributedWaveOperatorRequired", 403);
    private Task TouchSite(string id, CancellationToken ct)
        => deliveryCommands.TouchSite(id, ct);
    private async Task<DistributedCommandResponse> Execute<T>(DistributedActor actor, string kind, string target, T request,
        Func<Task<(string Id, long Revision, string Status, string? Reservation, int Quantity)>> action, CancellationToken ct)
        where T : DistributedCommandRequest
    {
        var response = await db.Database.CreateExecutionStrategy().ExecuteAsync(() => ExecuteOnce(actor, kind, target, request, action, ct));
        // Mongo 포함 연결은 SQL 업무와 별개로 재개합니다. 연결 실패로 이미 확정된 예약을 되돌리지 않습니다.
        return await ResumeOrderLedger(actor, kind, target, response, ct);
    }
    private async Task<DistributedCommandResponse> ExecuteOnce<T>(DistributedActor actor, string kind, string target, T request,
        Func<Task<(string Id, long Revision, string Status, string? Reservation, int Quantity)>> action, CancellationToken ct)
        where T : DistributedCommandRequest
    {
        Gate(actor); Require(request.RequestId != Guid.Empty, "DistributedRequestIdRequired", 400);
        db.ChangeTracker.Clear();
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Serialize(new { kind, target, request }))));
        var replay = await Query<분산물류CommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor.UserId && x.RequestId == request.RequestId, ct);
        if (replay is not null)
        {
            Require(replay.InputDigest == digest, "DistributedRequestContentConflict");
            return new(request.RequestId, replay.AggregateId, replay.Revision, replay.StatusCode, true);
        }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var result = await action();
            // 행별 근거를 포함한 영수증을 최종 결과와 함께 한 번만 저장합니다.
            var receipt = db.ChangeTracker.Entries<분산물류CommandReceipt>().Select(x => x.Entity)
                .SingleOrDefault(x => x.ActorUserId == actor.UserId && x.RequestId == request.RequestId)
                ?? new 분산물류CommandReceipt { ActorUserId = actor.UserId, RequestId = request.RequestId };
            if (db.Entry(receipt).State == EntityState.Detached) db.Add(receipt);
            receipt.InputDigest = digest; receipt.Kind = kind; receipt.RecordedAtUtc = Now;
            receipt.AggregateId = result.Id; receipt.Revision = result.Revision; receipt.StatusCode = result.Status;
            receipt.ReservationId = result.Reservation; receipt.Quantity = result.Quantity;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); await tx.DisposeAsync();
            await cargo.FlushCommittedEventsAsync(CancellationToken.None);
            return new(request.RequestId, result.Id, result.Revision, result.Status, false);
        }
        catch (DbUpdateConcurrencyException) { await tx.RollbackAsync(ct); cargo.DiscardUncommittedEvents(); db.ChangeTracker.Clear(); throw new DistributedFulfillmentException("DistributedRevisionConflict"); }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); cargo.DiscardUncommittedEvents(); db.ChangeTracker.Clear(); throw new DistributedFulfillmentException("DistributedStorageConflict"); }
        catch { await tx.RollbackAsync(ct); cargo.DiscardUncommittedEvents(); db.ChangeTracker.Clear(); throw; }
    }
    public Task<DistributedCommandResponse> SiteAsync(DistributedActor actor, DistributedSiteCreateRequest r, CancellationToken ct)
        => Execute(actor, "CreateSite", "", r, async () =>
        {
            var publicLabel = Text(r.PublicLabel);
            var warehouse = await db.창고.SingleOrDefaultAsync(x => x.Id == r.WarehouseId, ct);
            if (r.NewWarehouse is { } newSpace)
            {
                Require(r.WarehouseId == 0 && newSpace.AddressConfirmed && newSpace.CoordinatesConfirmed
                    && newSpace.Latitude is >= -90 and <= 90 && newSpace.Longitude is >= -180 and <= 180
                    && r.SpaceConfirmed && r.PickupPointConfirmed, "DistributedNewSpaceEvidenceRequired", 400);
                Require(string.IsNullOrWhiteSpace(r.ManagerUserId) || r.ManagerUserId == actor.UserId, "DistributedNewSpaceSelfManagerRequired", 403);
                warehouse = new 창고 { 소유자UserId = actor.UserId, 창고명 = Text(newSpace.Name, 200), 주소 = Text(newSpace.ConfirmedRoadAddress, 500),
                    위도 = newSpace.Latitude, 경도 = newSpace.Longitude, 창고유형 = "생활공간", 기본창고여부 = false };
                db.Add(warehouse); await db.SaveChangesAsync(ct);
            }
            Require(warehouse is not null && warehouse.소유자UserId == actor.UserId && warehouse.IsActive, "DistributedWarehouseOwnerRequired", 403);
            Require(r.ExpectedRevision == 0 && r.SpaceConfirmed && r.PickupPointConfirmed, "DistributedSpaceConfirmationRequired", 400);
            Require(!System.Text.RegularExpressions.Regex.IsMatch(publicLabel, @"(?:로|길|동)\s*\d|\d+\s*(?:호|층)|@|\d{2,3}-\d{3,4}-\d{4}"), "DistributedPublicLabelContainsPrivateAddress", 400);
            if (!string.IsNullOrWhiteSpace(r.PublicCandidateRef))
            {
                var candidates = await buildingCandidates.FindAsync(warehouse!.Id, warehouse.주소, ct);
                Require(candidates.Any(x => x.PlaceId == r.PlaceId && x.PublicCandidateRef == r.PublicCandidateRef), "DistributedBuildingCandidateMismatch");
            }
            else Require(r.PlaceId == $"warehouse:{warehouse!.Id}" || r.NewWarehouse is not null && string.IsNullOrWhiteSpace(r.PlaceId), "DistributedPlaceEvidenceMissing");
            var managerId = r.NewWarehouse is not null ? actor.UserId : r.ManagerUserId;
            Require(managerId == actor.UserId || await db.창고사용자.AnyAsync(x => x.창고Id == warehouse!.Id && x.UserId == managerId, ct), "DistributedRegisteredManagerRequired", 403);
            var site = new 분산거점Record { SiteId = Id("site"), WarehouseId = warehouse!.Id, PlaceId = Text(string.IsNullOrWhiteSpace(r.PlaceId) ? $"warehouse:{warehouse.Id}" : r.PlaceId),
                PublicCandidateRef = r.PublicCandidateRef?.Trim() ?? "", PublicLabel = publicLabel, OwnerUserId = actor.UserId,
                ManagerUserId = Text(managerId, 191), SpaceConfirmed = true, PickupPointConfirmed = true,
                PrivateDetailsJson = Serialize(new DistributedSitePrivate(Text(r.Floor, 60), Text(r.Subspace), Text(r.PickupInstructions, 400))) };
            db.Add(site); return (site.SiteId, site.Revision, site.StatusCode, (string?)null, 0);
        }, ct);
    public Task<DistributedCommandResponse> ConsentAsync(DistributedActor actor, string id, DistributedConsentRequest r, CancellationToken ct)
        => Execute(actor, "Consent", id, r, async () =>
        {
            var site = await Site(id, ct); Revision(site.Revision, r);
            Require(site.StatusCode != "Closed", "DistributedSiteClosed");
            Require((r.ConsentKind == "Owner" && site.OwnerUserId == actor.UserId) || (r.ConsentKind == "Manager" && site.ManagerUserId == actor.UserId), "DistributedConsentPartyMismatch", 403);
            var evidence = Text(r.EvidenceReference, 200);
            if (r.ConsentKind == "Owner") site.OwnerConsented = r.Granted; else site.ManagerConsented = r.Granted;
            var history = Deserialize<List<DistributedConsentEntry>>(site.ConsentEvidenceJson);
            history.Add(new(actor.UserId, r.ConsentKind, r.Granted, evidence, Now)); site.ConsentEvidenceJson = Serialize(history);
            var stockIds = await Query<분산재고LotRecord>().Where(x => x.SiteId == id).Select(x => x.InboundItemId).ToArrayAsync(ct);
            var custody = await db.입고상품.AnyAsync(x => Enumerable.Contains(stockIds, x.Id) && (x.가용수량 > 0 || x.예약수량 > 0 || x.불량수량 > 0), ct)
                || await (from reservation in Query<분산재고예약Record>() join lot in Query<분산재고LotRecord>() on reservation.LotId equals lot.LotId
                    where lot.SiteId == id && reservation.PickedQuantity > reservation.DeliveredQuantity + reservation.ReturnedQuantity select reservation).AnyAsync(ct);
            site.StatusCode = site.OwnerConsented && site.ManagerConsented ? "Active" : custody ? "RevocationPendingCustody" : "Revoked";
            site.Revision++; return (id, site.Revision, site.StatusCode, (string?)null, 0);
        }, ct);
    public Task<DistributedCommandResponse> LocationAsync(DistributedActor actor, string id, DistributedLocationCreateRequest r, CancellationToken ct)
        => Execute(actor, "CreateLocation", id, r, async () =>
        {
            var site = await Site(id, ct); Require(Manages(actor, site), "DistributedSiteManagerRequired", 403); Revision(site.Revision, r);
            Require(site.StatusCode == "Active", "DistributedConsentRequired");
            var location = new 분산적재위치Record { LocationId = Id("location"), SiteId = id, Barcode = "DLOC-" + Guid.NewGuid().ToString("N"), Label = Text(r.LocationLabel, 100) };
            db.Add(location); site.Revision++; return (id, site.Revision, site.StatusCode, (string?)null, 0);
        }, ct);
    public Task<DistributedCommandResponse> InboundAsync(DistributedActor actor, DistributedInboundPlanRequest r, CancellationToken ct)
        => Execute(actor, "PlanInbound", "", r, async () =>
        {
            Require(r.ExpectedRevision == 0 && r.Lines is { Count: > 0 and <= 100 } && r.Lines.All(x => x is not null) && r.Lines.Select(x => x.LineKey).Distinct().Count() == r.Lines.Count, "DistributedInboundLinesInvalid", 400);
            var plan = new 분산입고계획Record { PlanId = Id("inbound"), SourceReference = Text(r.SourceReference), CreatorUserId = actor.UserId }; db.Add(plan);
            입고요청? declaredSource = null;
            if (r.NewSource is { } declaration)
            {
                Require(r.Lines.All(x => x.SourceInboundId == 0) && declaration.ExpectedQuantity > 0
                    && declaration.StockOwnerIsCurrentSellerConfirmed && !string.IsNullOrWhiteSpace(declaration.OwnershipEvidenceReference), "DistributedInboundOwnershipEvidenceMissing");
                var originSite = await Site(r.Lines[0].SiteId, ct); Require(originSite.StatusCode == "Active", "DistributedConsentRequired");
                declaredSource = new 입고요청 { 창고Id = originSite.WarehouseId, 판매자UserId = actor.UserId, 주문자UserId = actor.UserId,
                    예정상품명 = Text(declaration.ProductName, 200), 예정SKU = Text(declaration.Sku, 100), 예정수량 = declaration.ExpectedQuantity,
                    입고흐름유형 = "DeclaredOwnedStock", 입고생성경로 = "DistributedOwnedSource", 계약선행여부 = false, 상태 = "분할입고예정" };
                db.Add(declaredSource); await db.SaveChangesAsync(ct);
                var metadata = new 입고상품 { 입고요청Id = declaredSource.Id, 창고Id = declaredSource.창고Id, 판매자UserId = actor.UserId, 소유자UserId = actor.UserId,
                    상품명 = declaredSource.예정상품명, SKU = declaredSource.예정SKU, 상태 = "분할계획원천" };
                db.Add(metadata); await db.SaveChangesAsync(ct); db.Entry(metadata).State = EntityState.Detached;
                db.재고이력.Add(new 재고이력 { 입고상품Id = metadata.Id, 이력유형 = "분할입고소유선언", 변경수량 = 0, 변경후수량 = 0,
                    원인유형 = "판매자명시소유확인", 처리UserId = actor.UserId, 메모 = Text(declaration.OwnershipEvidenceReference, 200), 처리일시 = Now });
            }
            foreach (var group in r.Lines.GroupBy(x => x.SourceInboundId))
            {
                var sourceId = group.Key == 0 && declaredSource is not null ? declaredSource.Id : group.Key;
                var source = await db.입고요청.SingleOrDefaultAsync(x => x.Id == sourceId, ct);
                Require(source is not null && source.판매자UserId == actor.UserId && source.예정수량 > 0, "DistributedInboundSourceOwnerRequired", 403);
                var sourceStock = await db.입고상품.AsNoTracking().SingleOrDefaultAsync(x => x.입고요청Id == sourceId, ct);
                if (sourceStock is null)
                {
                    Require(group.All(x => x.StockOwnerIsCurrentSellerConfirmed && !string.IsNullOrWhiteSpace(x.OwnershipEvidenceReference)), "DistributedInboundOwnershipEvidenceMissing");
                    sourceStock = new 입고상품 { 입고요청Id = source!.Id, 창고Id = source.창고Id, 소유자UserId = actor.UserId, 판매자UserId = source.판매자UserId,
                        상품명 = source.예정상품명, SKU = source.예정SKU, 상태 = "분할계획원천" };
                    db.Add(sourceStock); await db.SaveChangesAsync(ct); db.Entry(sourceStock).State = EntityState.Detached;
                    db.재고이력.Add(new 재고이력 { 입고상품Id = sourceStock.Id, 이력유형 = "분할입고소유선언", 변경수량 = 0, 변경후수량 = 0,
                        원인유형 = "판매자명시소유확인", 처리UserId = actor.UserId, 메모 = Text(group.First().OwnershipEvidenceReference, 200), 처리일시 = Now });
                }
                Require(sourceStock is not null && sourceStock.입고수량 == 0 && sourceStock.가용수량 == 0 && sourceStock.예약수량 == 0
                    && sourceStock.판매자UserId == actor.UserId && !string.IsNullOrWhiteSpace(sourceStock.소유자UserId), "DistributedInboundOwnershipEvidenceMissing");
                var allocated = (await Query<분산재고LotRecord>().Where(x => x.SourceInboundId == sourceId).ToArrayAsync(ct))
                    .Where(x => !ClosedInbound(x)).Sum(x => x.ExpectedQuantity);
                Require(allocated + group.Sum(x => x.ExpectedQuantity) <= source!.예정수량, "DistributedInboundSourceOverallocated");
                source.상태 = "분할입고예정";
                foreach (var line in group)
                {
                    var site = await Site(line.SiteId, ct); Require(site.StatusCode == "Active", "DistributedConsentRequired");
                    var location = await Query<분산적재위치Record>().SingleOrDefaultAsync(x => x.LocationId == line.LocationId && x.SiteId == line.SiteId, ct);
                    Require(location is not null && line.ExpectedQuantity > 0 && Text(line.Sku, 100) == source.예정SKU, "DistributedInboundLocationOrSkuMismatch", 400);
                    var target = new 입고요청 { 창고Id = site.WarehouseId, 입고흐름유형 = source.입고흐름유형, 입고생성경로 = "DistributedSplitInbound",
                        주문참조번호 = source.주문참조번호, 원주문참조번호 = source.주문참조번호, 판매자UserId = source.판매자UserId, 주문자UserId = source.주문자UserId,
                        예정상품명 = source.예정상품명, 예정SKU = source.예정SKU, 예정수량 = line.ExpectedQuantity, 상태 = "입고예정", 보관조건 = source.보관조건,
                        계약번호 = source.계약번호, 계약유형 = source.계약유형, 정산방식 = source.정산방식, 계약선행여부 = source.계약선행여부, 비고 = $"source-inbound:{source.Id};plan:{plan.PlanId}" };
                    db.Add(target); await db.SaveChangesAsync(ct);
                    var stock = new 입고상품 { 입고요청Id = target.Id, 창고Id = site.WarehouseId, 소유자UserId = sourceStock!.소유자UserId, 판매자UserId = source.판매자UserId,
                        상품명 = target.예정상품명, SKU = target.예정SKU, 보관위치 = location!.Barcode, 계약번호 = source.계약번호, 계약유형 = source.계약유형, 상태 = "입고예정" };
                    db.Add(stock); await db.SaveChangesAsync(ct);
                    db.Add(new 분산재고LotRecord { LotId = Id("lot"), PlanId = plan.PlanId, LineKey = Text(line.LineKey, 64), SiteId = site.SiteId,
                        LocationId = location.LocationId, SourceInboundId = source.Id, TargetInboundId = target.Id, InboundItemId = stock.Id,
                        OwnerUserId = sourceStock.소유자UserId, SellerUserId = source.판매자UserId, Sku = stock.SKU, LotCode = Text(line.LotCode, 100), ExpectedQuantity = line.ExpectedQuantity,
                        InboundCollaborationJson = site.AcceptancePolicyJson is not null ? Serialize(new DistributedInboundCollaborationDto { StatusCode = "Draft" }) : null }); site.Revision++;
                }
            }
            return (plan.PlanId, plan.Revision, plan.StatusCode, (string?)null, 0);
        }, ct);
    public Task<DistributedCommandResponse> ReceiveAsync(DistributedActor actor, string id, DistributedInboundReceiptRequest r, CancellationToken ct)
        => Execute(actor, "Receive", id, r, () => ReceiveCore(actor, id, ReceiveBatch(r), ct), ct);
    private static DistributedInboundReceiptBatchRequest ReceiveBatch(DistributedInboundReceiptRequest r)
        => new() { RequestId = r.RequestId, ExpectedRevision = r.ExpectedRevision, LocationBarcode = r.LocationBarcode,
            PhotoEvidenceIds = r.PhotoEvidenceIds, Lines = [new() { LineKey = r.LineKey, Sku = r.Sku, Quantity = r.Quantity }] };
}
internal sealed record DistributedSitePrivate(string Floor, string Subspace, string PickupInstructions);
internal sealed record DistributedConsentEntry(string ActorId, string Kind, bool Granted, string Evidence, DateTime AtUtc);
