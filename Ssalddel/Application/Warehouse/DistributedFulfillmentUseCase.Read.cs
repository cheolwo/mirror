using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

public sealed partial class 분산물류UseCase
{
    public async Task<DistributedRootOrderCandidatesDto> RootOrderCandidatesAsync(DistributedActor actor, string waveId,
        string cargoRequestId, string roleKey, CancellationToken ct)
    {
        Gate(actor); Require(roleKey == "shipper", "DistributedRoleScopeInvalid", 403);
        var wave = await Query<분산배송WaveRecord>().AsNoTracking().SingleOrDefaultAsync(x => x.WaveId == waveId, ct);
        Require(wave is not null && wave.StatusCode == "Open" && Now < wave.CutoffUtc, "DistributedCutoffPassed");
        var request = await db.화주운송의뢰.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == cargoRequestId, ct);
        Require(request is not null && request.화주Id == actor.UserId && !string.IsNullOrWhiteSpace(request.주문자UserId), "DistributedCargoOwnerRequired", 403);
        Require(Ssalddel.Application.Shipper.Request.화주운송배차진입정책.판정(request!).가능, "DistributedCargoSettlementNotReady");
        Require(!await Query<분산배송주문Record>().AnyAsync(x => x.CargoRequestId == cargoRequestId, ct), "DistributedCargoAlreadyAssigned");
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == cargoRequestId
            && x.배차업무유형 == 살뜰.도메인.공통.상태값.배차업무유형.용달운송, ct);
        Require(queue is not null && string.IsNullOrWhiteSpace(queue.기사_운송자) && string.IsNullOrWhiteSpace(queue.확정기사Id)
            && queue.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료, "DistributedCargoAlreadyAssigned");
        Require(orderLedgers is not null, "DistributedOrderLedgerAdapterUnavailable", 503);
        var candidates = await orderLedgers!.CandidatesAsync(actor.UserId, request!.화주Id!, request.주문자UserId!, ct);
        return new(wave!.WaveId, request.의뢰Id, actor.UserId, wave.Revision, candidates);
    }
    public async Task<DistributedWorkspaceDto> WorkspaceAsync(DistributedActor actor, string roleKey, CancellationToken ct)
    {
        Gate(actor); Require(roleKey is "warehouse" or "shipper" or "driver" or "orderer", "DistributedRoleScopeInvalid", 400);
        if (roleKey == "driver") Require(actor.IsDriver, "DistributedDriverRoleRequired", 403);
        var allSites = await Query<분산거점Record>().AsNoTracking().ToArrayAsync(ct);
        var lots = await Query<분산재고LotRecord>().AsNoTracking().ToArrayAsync(ct);
        var orders = await Query<분산배송주문Record>().AsNoTracking().ToArrayAsync(ct);
        var waves = await Query<분산배송WaveRecord>().AsNoTracking().ToArrayAsync(ct);
        var reservations = await Query<분산재고예약Record>().AsNoTracking().ToArrayAsync(ct);
        var allAgreements = await Query<분산보관합의Record>().AsNoTracking().ToArrayAsync(ct);
        var cargoIdsForActor = orders.Where(x => x.StatusCode is not "Delivered" and not "Cancelled").Select(x => x.CargoRequestId).ToArray();
        var currentAssignments = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(cargoIdsForActor, x.의뢰Id)
            && x.기사_운송자 == actor.UserId && x.확정기사Id == actor.UserId && x.상태 != "인수완료"
            && x.배차큐단계 != 살뜰.도메인.공통.상태값.배차큐단계.종료).Select(x => x.의뢰Id).ToArrayAsync(ct);
        bool ValidGrant(분산배송WaveRecord wave) => actor.IsDriver && wave.DriverId == actor.UserId
            && wave.StatusCode == "Accepted" && Now <= wave.DeliveryEndUtc
            && orders.Where(x => x.WaveId == wave.WaveId && x.StatusCode is not "Delivered" and not "Cancelled")
                .All(x => Enumerable.Contains(currentAssignments, x.CargoRequestId));
        bool VisibleWave(분산배송WaveRecord wave) => roleKey switch
        {
            "warehouse" => actor.IsAdmin || wave.CreatorUserId == actor.UserId || reservations.Any(r => r.WaveId == wave.WaveId
                && lots.Any(l => l.LotId == r.LotId && allSites.Any(s => s.SiteId == l.SiteId && Manages(actor, s)))),
            "driver" => wave.DriverId == actor.UserId,
            "shipper" => wave.StatusCode == "Open" || orders.Any(x => x.WaveId == wave.WaveId && (x.SellerUserId == actor.UserId || reservations.Any(y => y.OrderId == x.OrderId && lots.Any(z => z.LotId == y.LotId && z.OwnerUserId == actor.UserId)))),
            _ => orders.Any(x => x.WaveId == wave.WaveId && x.CustomerUserId == actor.UserId)
        };
        var visibleWaves = waves.Where(VisibleWave).ToArray(); var waveIds = visibleWaves.Select(x => x.WaveId).ToArray();
        var waveReservations = reservations.Where(x => Enumerable.Contains(waveIds, x.WaveId)).ToArray();
        var visibleLots = lots.Where(lot => roleKey switch
        {
            "warehouse" => allSites.Any(s => s.SiteId == lot.SiteId && Manages(actor, s)) || visibleWaves.Any(w => (w.CreatorUserId == actor.UserId || actor.IsAdmin) && waveReservations.Any(x => x.WaveId == w.WaveId && x.LotId == lot.LotId)),
            "shipper" => lot.SellerUserId == actor.UserId || lot.OwnerUserId == actor.UserId,
            "driver" => visibleWaves.Any(x => ValidGrant(x) && waveReservations.Any(r => r.WaveId == x.WaveId && r.LotId == lot.LotId)),
            _ => false
        }).ToArray();
        var sites = allSites.Where(site => roleKey == "warehouse" ? Manages(actor, site) || visibleLots.Any(x => x.SiteId == site.SiteId)
            : roleKey == "shipper" ? site.StatusCode == "Active" || visibleLots.Any(x => x.SiteId == site.SiteId) : visibleLots.Any(x => x.SiteId == site.SiteId)).ToArray();
        var locations = await Query<분산적재위치Record>().AsNoTracking().ToArrayAsync(ct);
        var stockIds = visibleLots.Select(x => x.InboundItemId).ToArray();
        var stocks = await db.입고상품.AsNoTracking().Where(x => Enumerable.Contains(stockIds, x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var allowedOwnerIds = visibleLots.Select(x => x.OwnerUserId).Distinct().ToArray();
        var ownerNames = await db.살뜰참여자.AsNoTracking().Where(x => Enumerable.Contains(allowedOwnerIds, x.Id) && x.활성화여부)
            .ToDictionaryAsync(x => x.Id, x => x.표시이름, ct);
        var plans = await Query<분산입고계획Record>().AsNoTracking().ToArrayAsync(ct);
        var siteDtos = sites.Select(site =>
        {
            var canManage = roleKey == "warehouse" && Manages(actor, site);
            var canPick = roleKey == "driver" && visibleLots.Any(x => x.SiteId == site.SiteId);
            var privateDetails = canManage || canPick ? Deserialize<DistributedSitePrivate>(site.PrivateDetailsJson) : null;
            var actions = new List<string>();
            if (canManage)
            {
                if (site.OwnerUserId == actor.UserId) actions.Add("create-agreement");
                if (site.OwnerUserId == actor.UserId) actions.Add("set-acceptance-policy");
                if (site.OwnerUserId == actor.UserId) actions.Add(site.OwnerConsented ? "revoke-owner" : "consent-owner");
                if (site.ManagerUserId == actor.UserId) actions.Add(site.ManagerConsented ? "revoke-manager" : "consent-manager");
                if (site.StatusCode == "Active") { actions.AddRange(["create-location", "receive"]); if (site.AcceptancePolicyJson is null) actions.Add("import-lot"); }
                if (site.StatusCode == "Closed") actions.Clear();
            }
            if (roleKey == "shipper") actions.Add("create-agreement");
            return new DistributedSiteDto { SiteId = site.SiteId, WarehouseId = site.WarehouseId, PlaceId = site.PlaceId,
                PublicLabel = site.PublicLabel, PublicCandidateRef = site.PublicCandidateRef, StatusCode = site.StatusCode, Revision = site.Revision,
                OwnerConsented = site.OwnerConsented, ManagerConsented = site.ManagerConsented, Floor = privateDetails?.Floor,
                AcceptancePolicy = site.AcceptancePolicyJson is null ? null : Deserialize<DistributedSiteAcceptancePolicy>(site.AcceptancePolicyJson),
                AcceptancePolicyRevision = site.AcceptancePolicyJson is null ? null : site.AcceptancePolicyRevision,
                Subspace = privateDetails?.Subspace, PickupInstructions = privateDetails?.PickupInstructions, AllowedActions = actions,
                Locations = canManage || canPick || roleKey == "shipper" ? locations.Where(x => x.SiteId == site.SiteId)
                    .Select(x => new DistributedLocationDto(x.LocationId, x.SiteId, canManage || canPick ? x.Barcode : "", canManage || canPick ? x.Label : "선택 가능한 적재 위치")).ToArray() : [] };
        }).ToArray();
        var workspace = new DistributedWorkspaceDto { CurrentUserId = actor.UserId, IsSimulation = true, Sites = siteDtos,
            Lots = visibleLots.Where(x => stocks.ContainsKey(x.InboundItemId)).Select(x => new DistributedLotDto(x.LotId, x.SiteId, x.LocationId, x.OwnerUserId,
                x.SellerUserId, x.Sku, x.LotCode, x.InboundItemId, stocks[x.InboundItemId].입고수량, stocks[x.InboundItemId].가용수량, stocks[x.InboundItemId].예약수량,
                ownerNames.GetValueOrDefault(x.OwnerUserId, "등록된 상품 소유자"), stocks[x.InboundItemId].상품명)).ToArray(),
            InboundPlans = roleKey is "warehouse" or "shipper" ? plans.Where(x => visibleLots.Any(l => l.PlanId == x.PlanId)).Select(x => new DistributedInboundDto(x.PlanId, x.SourceReference, x.StatusCode, x.Revision,
                visibleLots.Where(l => l.PlanId == x.PlanId).Select(l => MapInboundLine(actor, l, allSites.Single(s => s.SiteId == l.SiteId), allAgreements)).ToArray(),
                roleKey == "warehouse" && visibleLots.Any(l => l.PlanId == x.PlanId && sites.Any(s => s.SiteId == l.SiteId && Manages(actor, s)
                    && s.StatusCode == "Active" && l.ReceivedQuantity < l.ExpectedQuantity && InboundApprovalReasons(l, s, allAgreements).Count == 0)) ? ["receive"] : [])).ToArray() : [],
            Receipts = (await Query<분산물류CommandReceipt>().AsNoTracking().Where(x => x.ActorUserId == actor.UserId).OrderByDescending(x => x.RecordedAtUtc).Take(200).ToArrayAsync(ct)).Select(ReceiptDto).ToArray() };
        var allowed = new List<string>();
        if (roleKey is "warehouse" or "shipper")
        {
            var delegatedOwnerIds = await db.입고상품.AsNoTracking().Where(x => x.판매자UserId == actor.UserId).Select(x => x.소유자UserId).Distinct().ToArrayAsync(ct);
            var agreements = allAgreements.Where(x => x.SpaceOwnerUserId == actor.UserId || x.GoodsOwnerUserId == actor.UserId || x.CreatorUserId == actor.UserId).ToArray();
            workspace.StorageAgreements = agreements.Select(x => new DistributedStorageAgreementDto(x.AgreementId, x.SiteId, x.LocationId,
                x.SpaceOwnerUserId, x.GoodsOwnerUserId, x.TermsVersion, x.Revision, AgreementStatus(x), x.SpaceOwnerConsented, x.GoodsOwnerConsented,
                Deserialize<DistributedStorageTerms>(x.TermsJson), AgreementActions(actor, x))
                { PendingTerms = x.PendingTermsJson is null ? null : Deserialize<DistributedStorageTerms>(x.PendingTermsJson), PendingTermsVersion = x.PendingTermsVersion,
                    PendingSpaceOwnerConsented = x.PendingTermsJson is null ? null : x.PendingSpaceOwnerConsented,
                    PendingGoodsOwnerConsented = x.PendingTermsJson is null ? null : x.PendingGoodsOwnerConsented }).ToArray();
            var managedSiteIds = allSites.Where(x => x.OwnerUserId == actor.UserId).Select(x => x.SiteId).ToArray();
            var ownerCandidateIds = roleKey == "warehouse" ? lots.Where(x => Enumerable.Contains(managedSiteIds, x.SiteId)).Select(x => x.OwnerUserId)
                .Concat(delegatedOwnerIds).Append(actor.UserId).Distinct().ToArray() : delegatedOwnerIds.Append(actor.UserId).Distinct().ToArray();
            var ownerLabels = await db.살뜰참여자.AsNoTracking().Where(x => Enumerable.Contains(ownerCandidateIds, x.Id) && x.활성화여부).ToDictionaryAsync(x => x.Id, x => x.표시이름, ct);
            workspace.GoodsOwnerCandidates = ownerCandidateIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new DistributedPartyCandidateDto(x, x == actor.UserId ? "본인" : ownerLabels.GetValueOrDefault(x, "등록된 상품 소유자"))).ToArray();
        }
        var ownedWarehouses = await db.창고.AsNoTracking().Where(x => x.소유자UserId == actor.UserId && x.IsActive).ToArrayAsync(ct);
        if (roleKey == "warehouse") allowed.Add("create-site");
        if (roleKey == "warehouse" && allSites.Any(x => Manages(actor, x))) allowed.Add("create-wave");
        if (roleKey == "shipper") allowed.Add("plan-inbound");
        workspace.AllowedActions = allowed;
        if (roleKey == "warehouse")
        {
            var candidates = new List<DistributedWarehouseCandidateDto>();
            foreach (var warehouse in ownedWarehouses)
            {
                var managerIds = (await db.창고사용자.AsNoTracking().Where(x => x.창고Id == warehouse.Id).Select(x => x.UserId).ToArrayAsync(ct)).Append(actor.UserId).Distinct().ToArray();
                var users = await db.Users.AsNoTracking().Where(x => Enumerable.Contains(managerIds, x.Id)).Select(x => new { x.Id, x.UserName }).ToArrayAsync(ct);
                candidates.Add(new(warehouse.Id, warehouse.창고명, await buildingCandidates.FindAsync(warehouse.Id, warehouse.주소, ct),
                    managerIds.Select(x => new DistributedPartyCandidateDto(x, users.FirstOrDefault(u => u.Id == x)?.UserName ?? (x == actor.UserId ? "본인 관리" : "등록된 창고 담당자"))).ToArray()));
            }
            workspace.WarehouseCandidates = candidates;
            workspace.DriverCandidates = (await db.용달기사.AsNoTracking().Where(x => x.상태 == "활동중").ToArrayAsync(ct))
                .Where(x => driverLocations.TryGetLatest(x.기사Id, out _)).Select(x => new DistributedPartyCandidateDto(x.기사Id, x.기사명)).ToArray();
            var managedWarehouseIds = sites.Where(x => Manages(actor, x)).Select(x => x.WarehouseId).ToArray(); var bound = lots.Select(x => x.InboundItemId).ToArray();
            workspace.ExistingStockCandidates = (await db.입고상품.AsNoTracking().Where(x => Enumerable.Contains(managedWarehouseIds, x.창고Id) && x.입고수량 > 0 && !Enumerable.Contains(bound, x.Id)).ToArrayAsync(ct))
                .Select(x => new DistributedExistingStockCandidateDto(x.Id, sites.Single(s => s.WarehouseId == x.창고Id).SiteId, x.상품명, x.소유자UserId, x.판매자UserId, x.SKU, x.입고수량, x.가용수량)).ToArray();
        }
        if (roleKey == "shipper")
        {
            var source = await db.입고요청.AsNoTracking().Where(x => x.판매자UserId == actor.UserId && x.예정수량 > 0 && x.상태 != "입고완료").ToArrayAsync(ct);
            var ids = source.Select(x => x.Id).ToArray(); var metadata = await db.입고상품.AsNoTracking().Where(x => Enumerable.Contains(ids, x.입고요청Id)).ToArrayAsync(ct);
            workspace.InboundSourceCandidates = source.Select(x => new DistributedInboundSourceCandidateDto(x.Id, x.예정상품명, x.예정SKU, x.예정수량!.Value,
                x.판매자UserId, metadata.FirstOrDefault(s => s.입고요청Id == x.Id)?.소유자UserId)).ToArray();
            var cargoRequests = await db.화주운송의뢰.AsNoTracking().Where(x => x.화주Id == actor.UserId).ToArrayAsync(ct);
            workspace.CargoCandidates = cargoRequests.Where(x => !orders.Any(o => o.CargoRequestId == x.의뢰Id))
                .Select(x => new DistributedCargoCandidateDto(x.의뢰Id, x.화물종류, x.픽업_시간창_시작일시, x.픽업_시간창_종료일시, x.하차_시간창_시작일시, x.하차_시간창_종료일시)).ToArray();
            var sellerItemIds = visibleLots.Where(x => x.SellerUserId == actor.UserId).Select(x => x.InboundItemId).ToArray();
            var candidateOutbounds = await db.출고예정.AsNoTracking().Where(x => x.판매자UserId == actor.UserId
                && x.입고상품Id.HasValue && Enumerable.Contains(sellerItemIds, x.입고상품Id.Value) && x.수량 > 0 && (x.상태 == "예정" || x.상태 == "준비중")).ToArrayAsync(ct);
            var candidateCargoIds = cargoRequests.Where(x => !orders.Any(o => o.CargoRequestId == x.의뢰Id)).Select(x => x.의뢰Id).ToArray();
            var queues = await db.운송원장.AsNoTracking().Where(x => Enumerable.Contains(candidateCargoIds, x.의뢰Id)
                && x.배차업무유형 == 살뜰.도메인.공통.상태값.배차업무유형.용달운송).ToArrayAsync(ct);
            var links = await db.운송의뢰상품연결.AsNoTracking().Where(x => Enumerable.Contains(candidateCargoIds, x.운송의뢰Id)).ToArrayAsync(ct);
            var outboundCandidates = new List<DistributedExistingOutboundCandidateDto>();
            foreach (var outbound in candidateOutbounds)
            {
                var lot = visibleLots.SingleOrDefault(x => x.SellerUserId == actor.UserId && x.InboundItemId == outbound.입고상품Id);
                if (lot is null || reservations.Any(x => x.OutboundPlanId == outbound.Id) || !stocks.TryGetValue(lot.InboundItemId, out var stock)) continue;
                var site = allSites.Single(x => x.SiteId == lot.SiteId); var location = locations.Single(x => x.LocationId == lot.LocationId);
                var request = cargoRequests.SingleOrDefault(x => x.의뢰Id == outbound.운송의뢰Id && Enumerable.Contains(candidateCargoIds, x.의뢰Id));
                var queue = queues.SingleOrDefault(x => x.의뢰Id == outbound.운송의뢰Id);
                if (site.StatusCode != "Active" || stock.소유자UserId != lot.OwnerUserId || stock.판매자UserId != actor.UserId || stock.SKU != lot.Sku
                    || stock.창고Id != site.WarehouseId || outbound.출고창고Id != site.WarehouseId || stock.보관위치 != location.Barcode || stock.예약수량 < outbound.수량
                    || request is null || queue is null || !string.IsNullOrWhiteSpace(queue.기사_운송자) || !string.IsNullOrWhiteSpace(queue.확정기사Id)
                    || queue.배차큐단계 == 살뜰.도메인.공통.상태값.배차큐단계.종료) continue;
                var requestLinks = links.Where(x => x.운송의뢰Id == request.의뢰Id).ToArray();
                if (requestLinks.Length > 0 && !requestLinks.Any(x => x.입고상품Id == lot.InboundItemId && x.할당수량 == outbound.수량)) continue;
                outboundCandidates.Add(new(outbound.Id, request.의뢰Id, lot.LotId, lot.SiteId, lot.OwnerUserId, lot.SellerUserId, lot.Sku, outbound.수량,
                    stock.상품명, ownerNames.GetValueOrDefault(lot.OwnerUserId, "등록된 상품 소유자")));
            }
            workspace.ExistingOutboundCandidates = outboundCandidates;
        }
        var requestIds = orders.Select(x => x.CargoRequestId).ToArray(); var requests = await db.화주운송의뢰.AsNoTracking().Where(x => Enumerable.Contains(requestIds, x.의뢰Id)).ToDictionaryAsync(x => x.의뢰Id, ct);
        var managedSites = allSites.Where(x => Manages(actor, x)).Select(x => x.SiteId).ToHashSet();
        var linkStates = new Dictionary<string, DistributedOrderLedgerConnectionState>(StringComparer.Ordinal);
        foreach (var connection in visibleWaves.SelectMany(x => 분산주문원장Metadata.Connections(x).Values)
            .Where(x => x.CustomerUserId == actor.UserId || x.SellerUserId == actor.UserId))
            linkStates[connection.OrderId] = orderLedgers is null ? new("Pending", ErrorCode: "DistributedOrderLedgerAdapterUnavailable")
                : await orderLedgers.ReadAsync(actor.UserId, connection, ct);
        workspace.Waves = visibleWaves.Select(wave => MapWave(actor, roleKey, wave, orders.Where(x => x.WaveId == wave.WaveId).ToArray(), reservations.Where(x => x.WaveId == wave.WaveId).ToArray(), lots, requests, ValidGrant(wave), managedSites, workspace.Lots, linkStates)).ToArray();
        await PopulateStockReleaseWorkspace(actor, roleKey, workspace, allSites, ct);
        return workspace;
    }
    private DistributedWaveDto MapWave(DistributedActor actor, string role, 분산배송WaveRecord wave, 분산배송주문Record[] allOrders,
        분산재고예약Record[] allReservations, 분산재고LotRecord[] lots, IReadOnlyDictionary<string, 살뜰.도메인.화주.화주운송의뢰> requests, bool validGrant, IReadOnlySet<string> managedSites, IReadOnlyList<DistributedLotDto> labels,
        IReadOnlyDictionary<string, DistributedOrderLedgerConnectionState> linkStates)
    {
        var op = role == "warehouse" && (actor.IsAdmin || wave.CreatorUserId == actor.UserId);
        var driver = role == "driver" && wave.DriverId == actor.UserId; var acceptedDriver = driver && validGrant;
        var orders = allOrders.Where(x => op || driver || (role == "orderer" ? x.CustomerUserId == actor.UserId : role == "warehouse"
            ? allReservations.Any(r => r.OrderId == x.OrderId && lots.Any(l => l.LotId == r.LotId && Enumerable.Contains(managedSites, l.SiteId)))
            : x.SellerUserId == actor.UserId || allReservations.Any(r => r.OrderId == x.OrderId && lots.Any(l => l.LotId == r.LotId && l.OwnerUserId == actor.UserId)))).ToArray();
        var orderIds = orders.Select(x => x.OrderId).ToArray(); var reservations = allReservations.Where(x => Enumerable.Contains(orderIds, x.OrderId)
            && (role != "warehouse" || op || lots.Any(l => l.LotId == x.LotId && Enumerable.Contains(managedSites, l.SiteId)))).ToArray();
        var allowed = new List<string>();
        if (op) { if (wave.StatusCode == "Open") allowed.Add("close"); if (wave.StatusCode is "Closed" or "Planned" or "NeedsReplan" or "AcceptancePending") allowed.Add("plan"); }
        if (role == "shipper" && wave.StatusCode == "Open" && Now < wave.CutoffUtc) allowed.Add("add-order");
        if (driver && wave.StatusCode is "Planned" or "AcceptancePending") allowed.Add("accept");
        var stops = Stops(wave); var current = stops.FirstOrDefault(x => x.StatusCode == "Pending");
        var acceptance = op || driver ? LatestAcceptance(wave) : null;
        var acceptanceFailure = acceptance?.Results.FirstOrDefault(x => !x.Accepted);
        var ledgerConnections = 분산주문원장Metadata.Connections(wave);
        if (acceptedDriver && current is not null) allowed.Add(current.Kind == "Pickup" ? "pick" : "deliver");
        return new() { WaveId = wave.WaveId, RoundId = wave.RoundId, Label = wave.Label, StatusCode = wave.StatusCode, Revision = wave.Revision,
            DriverId = op || driver ? wave.DriverId : null, CutoffUtc = wave.CutoffUtc, PickupStartUtc = wave.PickupStartUtc, PickupEndUtc = wave.PickupEndUtc,
            DeliveryStartUtc = wave.DeliveryStartUtc, DeliveryEndUtc = wave.DeliveryEndUtc,
            TotalDistanceKm = op || driver ? wave.TotalDistanceKm : null, TotalElapsedMinutes = op || driver ? wave.TotalElapsedMinutes : null,
            OfferedDriverFareKrw = op || driver ? wave.OfferedDriverFareKrw : null, CurrentStopId = op || acceptedDriver ? current?.StopId : null, AllowedActions = allowed,
            BlockCodes = wave.StatusCode == "AcceptancePending" ? new[] { "DistributedCargoAcceptancePending", acceptanceFailure?.ErrorCode }.OfType<string>().Distinct().ToArray() : [],
            AcceptanceWarnings = acceptance?.Results.SelectMany(x => x.Warnings).ToArray() ?? [],
            AcceptanceErrorCode = acceptanceFailure?.ErrorCode, AcceptanceErrorMessage = acceptanceFailure?.ErrorMessage,
            Stops = stops.Where(x => role != "orderer" || (x.Kind == "Delivery" && x.OrderIds.Any(orderIds.Contains)))
                .Select(x => x with { OrderIds = x.OrderIds.Where(orderIds.Contains).ToArray(), Address = op || acceptedDriver ? x.Address : null,
                    Latitude = op || acceptedDriver ? x.Latitude : null, Longitude = op || acceptedDriver ? x.Longitude : null }).ToArray(),
            Orders = orders.Select(x => { var rr = reservations.Where(r => r.OrderId == x.OrderId).ToArray(); var req = requests[x.CargoRequestId];
                var actions = new List<string>(); if ((op || role == "shipper" && x.SellerUserId == actor.UserId || role == "orderer" && x.CustomerUserId == actor.UserId) && x.StatusCode is not "Delivered" and not "Cancelled") actions.Add("cancel");
                if (role == "orderer" && x.CustomerUserId == actor.UserId && x.StatusCode == "Delivered" && !x.CustomerAcknowledged) actions.Add("acknowledge");
                var canSeeLink = x.CustomerUserId == actor.UserId || x.SellerUserId == actor.UserId;
                var connection = canSeeLink ? ledgerConnections.GetValueOrDefault(x.OrderId) : null;
                var linkState = canSeeLink ? linkStates.GetValueOrDefault(x.OrderId) : null;
                if (orderLedgers is not null && canSeeLink && (role is "orderer" or "shipper") && linkState?.StatusCode is not "Connected" and not "Blocked")
                    actions.Add(connection is null ? "connect-order-ledger" : "retry-order-ledger");
                return new DistributedOrderDto(x.OrderId, x.OrderReference, x.CargoRequestId, x.StatusCode, rr.Sum(r => r.Quantity - r.PickedQuantity - r.ReleasedQuantity), rr.Sum(r => r.PickedQuantity),
                    rr.Sum(r => r.DeliveredQuantity), rr.Sum(r => r.ReturnedQuantity), x.CustomerUserId == actor.UserId, x.CustomerAcknowledged, req.하차_시간창_시작일시, req.하차_시간창_종료일시, actions)
                { RootOrderLedgerId = linkState?.StatusCode == "Blocked" ? null : connection?.RootOrderLedgerId,
                    RootOrderLedgerRevision = linkState?.StatusCode == "Blocked" ? null : linkState?.RootRevision,
                    OrderLedgerConnectionStatusCode = canSeeLink ? linkState?.StatusCode ?? "NotLinked" : null,
                    OrderLedgerConnectionErrorCode = linkState?.ErrorCode }; }).ToArray(),
            Reservations = role == "orderer" || driver && !acceptedDriver ? [] : reservations.Select(x => { var lot = lots.Single(l => l.LotId == x.LotId);
                var actions = new List<string>(); if (acceptedDriver && current?.Kind == "Pickup" && current.SiteId == lot.SiteId && Enumerable.Contains(current.OrderIds, x.OrderId) && x.PickedQuantity + x.ReleasedQuantity < x.Quantity) actions.Add("pick");
                if (acceptedDriver && current?.Kind == "Delivery" && Enumerable.Contains(current.OrderIds, x.OrderId) && x.DeliveredQuantity < x.PickedQuantity && reservations.Where(r => Enumerable.Contains(current.OrderIds, r.OrderId)).All(r => r.PickedQuantity == r.Quantity && r.ReturnedQuantity == 0 && r.ReleasedQuantity == 0)) actions.Add("deliver");
                if (role == "warehouse" && Enumerable.Contains(managedSites, lot.SiteId) && x.PickedQuantity > x.ReturnedQuantity + x.DeliveredQuantity && allOrders.Any(o => o.OrderId == x.OrderId && o.StatusCode == "ReturnRequired")) actions.Add("return");
                var label = labels.FirstOrDefault(l => l.LotId == lot.LotId);
                return new DistributedReservationDto(x.ReservationId, x.OrderId, lot.LotId, lot.SiteId, lot.LocationId, lot.OwnerUserId, lot.Sku, x.Quantity, x.PickedQuantity, x.DeliveredQuantity, x.ReleasedQuantity, x.ReturnedQuantity, x.OutboundPlanId, actions, label?.OwnerLabel ?? "", label?.ProductLabel ?? ""); }).ToArray() };
    }
    public async Task<DistributedWaveDto> WaveAsync(DistributedActor actor, string id, string roleKey, CancellationToken ct)
        => (await WorkspaceAsync(actor, roleKey, ct)).Waves.SingleOrDefault(x => x.WaveId == id) ?? throw new DistributedFulfillmentException("DistributedWaveNotFound", 404);
    private static DistributedReceiptDto ReceiptDto(분산물류CommandReceipt x) => new(x.RequestId, x.Kind, x.AggregateId, x.ReservationId, x.Quantity, x.Revision, x.RecordedAtUtc);
    public async Task<DistributedReceiptDto> ReceiptAsync(DistributedActor actor, Guid id, CancellationToken ct)
    {
        Gate(actor); var receipt = await Query<분산물류CommandReceipt>().AsNoTracking().SingleOrDefaultAsync(x => x.ActorUserId == actor.UserId && x.RequestId == id, ct);
        return receipt is null ? throw new DistributedFulfillmentException("DistributedReceiptNotFound", 404) : ReceiptDto(receipt);
    }
    public async Task<DistributedWorldProjectionDto> WorldAsync(DistributedActor actor, string roleKey, CancellationToken ct)
    {
        var workspace = await WorkspaceAsync(actor, roleKey, ct);
        return new(true, DistributedFulfillmentCodes.Profile,
            workspace.Sites.Select(x => new DistributedWorldSiteDto(x.SiteId, x.PlaceId, x.PublicLabel, x.StatusCode, x.Revision,
                workspace.Lots.Where(l => l.SiteId == x.SiteId).Select(l => l.LocationId).Distinct().Count(), workspace.Lots.Where(l => l.SiteId == x.SiteId).Sum(l => l.ReceivedQuantity),
                workspace.Lots.Where(l => l.SiteId == x.SiteId).Sum(l => l.ReservedQuantity), workspace.Waves.SelectMany(w => w.Reservations).Where(r => r.SiteId == x.SiteId).Sum(r => r.PickedQuantity))).ToArray(),
            workspace.Waves.Select(x => new DistributedWorldRoundDto(x.WaveId, x.RoundId, x.StatusCode, x.Revision,
                x.Stops.Where(s => s.Kind == "Delivery" || workspace.Sites.Any(site => site.SiteId == s.SiteId)).Select((s, index) => new DistributedWorldStopDto(s.StopId, index + 1, s.Kind, s.SiteId, s.StatusCode, s.OrderIds.Count,
                    x.Reservations.Where(r => Enumerable.Contains(s.OrderIds, r.OrderId) && (s.SiteId == null || r.SiteId == s.SiteId)).Sum(r => r.PickedQuantity),
                    x.Orders.Where(o => Enumerable.Contains(s.OrderIds, o.OrderId)).Sum(o => o.DeliveredQuantity))).ToArray())).ToArray());
    }
}
