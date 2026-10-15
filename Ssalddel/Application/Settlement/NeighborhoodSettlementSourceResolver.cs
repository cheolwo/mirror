using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.Warehouse;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Contracts.Common.Settlement;
using Ssalddel.Contracts.Food;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.기사;
using 살뜰.도메인.음식;
using 살뜰.도메인.운송;
using 살뜰.도메인.창고;
using 살뜰.도메인.화주;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;

namespace Ssalddel.Application.Settlement;

/// <summary>기존 업무 투영과 동결 정산을 읽습니다. 상태·금액·지급 원본을 수정하지 않습니다.</summary>
public sealed partial class NeighborhoodSettlementSourceResolver(SsalddelContext db, TimeProvider clock)
    : INeighborhoodSettlementSourceResolver
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private IQueryable<T> Query<T>() where T : class => db.Set<T>().AsNoTracking();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<NeighborhoodSettlementSourceSnapshot> ResolveAsync(string sourceKind, string sourceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || sourceId.Length > 120)
            throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceInvalid", 400);
        return sourceKind switch
        {
            C.DistributedLot => Lot(sourceId, ct),
            C.DistributedOrder => Order(sourceId, ct),
            C.FoodOrder => Food(sourceId, ct),
            C.CargoRequest => Cargo(sourceId, ct),
            C.MartOrder => Mart(sourceId, ct),
            _ => throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceInvalid", 400)
        };
    }

    private async Task<NeighborhoodSettlementSourceSnapshot> Lot(string id, CancellationToken ct)
    {
        var lot = await Query<분산재고LotRecord>().SingleOrDefaultAsync(x => x.LotId == id, ct)
            ?? throw Missing();
        var site = await Query<분산거점Record>().SingleAsync(x => x.SiteId == lot.SiteId, ct);
        var parties = new Parties(); LotParties(parties, lot, site);
        // 출고 기사·반환 담당자가 추가돼도 별도 입고 약정의 당사자 판본을 바꾸지 않습니다.
        var history = await LotWork(lot, site, new Parties(), ct);
        var intervals = await StorageIntervals(lot, history, ct);
        var work = history.Where(x => x.Kind == C.Inbound).ToArray();
        foreach (var inbound in work) parties.Add(inbound.ActorUserId, "Receiver");
        // 기존 입고 이력의 양수 수량 시각을 사용하며 계약 시작일·CreatedAt으로 대신하지 않습니다.
        var storedAt = work.Where(x => x.Kind == C.Inbound).Select(x => (DateTime?)x.OccurredAtUtc).Min();
        return Snapshot(C.DistributedLot, id, C.StoredGoods, parties, work, lot.ReceivedQuantity,
            storageStartedAt: storedAt, storageIntervals: intervals.Continuous, storageQuantityIntervals: intervals.Quantities,
            storageEvidence: history.SelectMany(x => x.EvidenceReferences).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), workflow: new()
            {
                Kind = NeighborhoodSettlementWorkflowCodes.InboundStorage,
                StateCode = lot.ReceivedQuantity == 0 ? "Planned" : lot.ReceivedQuantity == lot.ExpectedQuantity ? "Received" : "PartiallyReceived",
                CompletionConfirmed = lot.ExpectedQuantity > 0 && lot.ReceivedQuantity == lot.ExpectedQuantity
                    && work.Sum(x => x.Quantity) == lot.ReceivedQuantity
                    && work.All(x => x.OccurredAtUtc is { Kind: DateTimeKind.Utc } at && at <= Now),
                // 부분 입고도 증빙된 개별 작업은 정산할 수 있습니다. 약정 동의·항목 확인은 별도 관문입니다.
                PaymentReleaseReady = work.Any(x => x.Quantity > 0 && x.OccurredAtUtc is { Kind: DateTimeKind.Utc } at && at <= Now),
                WaitingReasonCode = work.Length == 0 ? "NeighborhoodSettlementInboundWorkEvidenceRequired" : null,
                InventoryLotIds = [id]
            });
    }

    private async Task<NeighborhoodSettlementSourceSnapshot> Order(string id, CancellationToken ct)
    {
        var order = await Query<분산배송주문Record>().SingleOrDefaultAsync(x => x.OrderId == id, ct)
            ?? throw Missing();
        var wave = await Query<분산배송WaveRecord>().SingleAsync(x => x.WaveId == order.WaveId, ct);
        var reservations = await Query<분산재고예약Record>()
            .Where(x => x.OrderId == id && x.WaveId == wave.WaveId).ToArrayAsync(ct);
        var parties = new Parties(); parties.Add(order.CustomerUserId, "Buyer"); parties.Add(order.SellerUserId, "Seller");
        var work = new List<NeighborhoodSettlementCompletedWork>();
        var inventory = new List<NeighborhoodSettlementInventoryReservation>();
        foreach (var reservation in reservations)
        {
            var workStart = work.Count;
            var lot = await Query<분산재고LotRecord>().SingleAsync(x => x.LotId == reservation.LotId, ct);
            var site = await Query<분산거점Record>().SingleAsync(x => x.SiteId == lot.SiteId, ct);
            LotParties(parties, lot, site);
            // 전체 Lot의 입고 담당자·다른 주문 피킹자는 이 주문의 정산 권한을 얻지 않습니다.
            var lotWork = await LotWork(lot, site, new Parties(), ct);
            // 신규 묶음은 저장한 행별 근거로 연결합니다. 구형 묶음의 총수량을 주문마다 복제하지 않습니다.
            var receipts = await Query<분산물류CommandReceipt>().Where(x => x.AggregateId == wave.WaveId
                && (x.Kind == "Pick" || x.Kind == "PickBatch")).ToArrayAsync(ct);
            foreach (var receipt in receipts)
            {
                var lines = CompletionLines(receipt);
                if (lines is not null)
                {
                    foreach (var line in lines.Where(x => x.ReservationId == reservation.ReservationId && x.OrderId == id && x.LotId == lot.LotId))
                    {
                        var exact = lotWork.Where(x => x.Kind == C.Picking && x.OriginKind == "InventoryHistory"
                            && x.OriginId == line.InventoryHistoryId!.Value.ToString(CultureInfo.InvariantCulture)
                            && x.ActorUserId == receipt.ActorUserId && x.Quantity == line.Quantity
                            && x.OriginRevision == receipt.Revision && x.RelatedOrderId == id).ToArray();
                        if (exact.Length == 1) { work.Add(exact[0]); parties.Add(exact[0].ActorUserId, "Picker"); }
                    }
                    continue;
                }
                if (receipt.ReservationId != reservation.ReservationId) continue;
                var photos = await BoundPhotos(receipt.ActorUserId, receipt.RequestId, ct);
                if (receipt.Quantity <= 0 || !photos.Any(x => PhotoIncludes(x, lot.LotId, id))) continue;
                var evidence = photos.Where(x => x.Kind == "Pick" && PhotoIncludes(x, lot.LotId, id))
                    .Select(x => "photo:" + x.EvidenceId).ToArray();
                var candidates = lotWork.Where(x => x.Kind == C.Picking && x.ActorUserId == receipt.ActorUserId
                    && x.Quantity == receipt.Quantity && x.EvidenceReferences.Intersect(evidence).Any()).ToArray();
                // 구형 주문도 실제 재고 이력 ID를 보존하며 입고 정산에는 이 피킹을 다시 노출하지 않습니다.
                if (candidates.Length == 1) { work.Add(candidates[0]); parties.Add(candidates[0].ActorUserId, "Picker"); }
            }
            var picks = work.Skip(workStart).Where(x => x.Kind == C.Picking && x.RelatedOrderId == id
                && x.EvidenceReferences.Count > 0 && lotWork.Any(l => l.OriginId == x.OriginId && l.Kind == C.Picking)).ToArray();
            inventory.Add(new(reservation.ReservationId, reservation.LotId, reservation.Quantity,
                picks.Sum(x => x.Quantity) == reservation.Quantity ? picks.Min(x => x.OccurredAtUtc) : null));
        }
        var cargo = await CargoDetails(order.CargoRequestId, ct);
        var validDriver = cargo.Transport?.확정기사Id is { Length: > 0 } driver && driver == wave.DriverId;
        if (validDriver) parties.Add(wave.DriverId, "Driver", "Picker");
        var delivered = order.StatusCode == "Delivered" && validDriver && reservations.Length > 0 && reservations.All(x => x.Quantity > 0 && x.DeliveredQuantity == x.Quantity
            && x.PickedQuantity == x.Quantity && x.ReturnedQuantity == 0 && x.ReleasedQuantity == 0)
            && Delivered(cargo.Transport!);
        if (delivered)
        {
            var delivery = DeliveryWork(cargo.Transport!, order.CargoRequestId);
            var proof = await DeliveryCompletion(wave, order, reservations, ct);
            if (proof is { } exact) delivery = delivery with { OriginRevision = exact.Revision, RelatedOrderId = id,
                EvidenceReferences = delivery.EvidenceReferences.Concat(exact.Evidence).Distinct().ToArray() };
            work.Add(delivery);
        }
        if (delivered && order.CustomerAcknowledged)
        {
            foreach (var reservation in reservations)
            {
                var lot = await Query<분산재고LotRecord>().SingleAsync(x => x.LotId == reservation.LotId, ct);
                var site = await Query<분산거점Record>().SingleAsync(x => x.SiteId == lot.SiteId, ct);
                // 개별 수령 확인 시각은 원본에 없으므로 null로 남기고 조회 시각과 혼동하지 않습니다.
                work.Add(new(C.ProductSale, "DistributedReservation", reservation.ReservationId, reservation.DeliveredQuantity, null,
                    ["distributed-order-received:" + id, "distributed-reservation:" + reservation.ReservationId,
                        "receipt-time:unknown", "cargo-transport:" + cargo.Transport!.Id])
                    { GoodsOwnerUserId = lot.OwnerUserId, SellerUserId = lot.SellerUserId, SpaceOwnerUserId = site.OwnerUserId });
            }
        }
        // 자유 문자열 OrderReference와 현재 판매상품 가격은 역사적 상품대금의 근거가 아닙니다.
        return Snapshot(C.DistributedOrder, id, C.StoredGoods, parties, work,
            delivered ? reservations.Sum(x => x.DeliveredQuantity) : 0, fare: validDriver ? cargo.Fare : null,
            canonical: "cargo:" + order.CargoRequestId, externalPayment: cargo.PaymentStatus, inventoryReservations: inventory,
            rewardOutcome: delivered && order.CustomerAcknowledged
                ? new(reservations.Sum(x => x.DeliveredQuantity), ["distributed-order-received:" + id,
                    .. reservations.Select(x => "distributed-reservation:" + x.ReservationId), "cargo-transport:" + cargo.Transport!.Id]) : null,
            workflow: new()
            {
                Kind = NeighborhoodSettlementWorkflowCodes.OutboundDelivery,
                StateCode = delivered ? order.CustomerAcknowledged ? "Received" : "DeliveredAwaitingReceipt" : order.StatusCode,
                CompletionConfirmed = delivered && order.CustomerAcknowledged,
                PaymentReleaseReady = delivered && order.CustomerAcknowledged,
                WaitingReasonCode = delivered && order.CustomerAcknowledged ? null : "NeighborhoodSettlementCustomerReceiptRequired",
                InventoryLotIds = reservations.Select(x => x.LotId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            });
    }

    private async Task<NeighborhoodSettlementSourceSnapshot> Food(string id, CancellationToken ct)
    {
        var order = await Query<음식주문>().SingleOrDefaultAsync(x => x.주문번호 == id, ct) ?? throw Missing();
        var parties = new Parties(); parties.Add(order.주문자UserId, "Buyer");
        var restaurantId = order.음식점Id.ToString(CultureInfo.InvariantCulture);
        var operators = await (from claim in db.UserClaims.AsNoTracking()
            join role in db.UserRoles.AsNoTracking() on claim.UserId equals role.UserId
            join definition in db.Roles.AsNoTracking() on role.RoleId equals definition.Id
            where claim.ClaimType == 음식점접근ClaimTypes.음식점Id && claim.ClaimValue == restaurantId
                && definition.Name == 역할명.음식점
            select claim.UserId).Distinct().ToArrayAsync(ct);
        // 음식점 운영 접근권은 재산 소유권 증거가 아니므로 Owner 역할은 임의 부여하지 않습니다.
        foreach (var op in operators) parties.Add(op, "Seller");
        var settlement = await Query<음식주문기사정산>().SingleOrDefaultAsync(x => x.음식주문Id == order.Id, ct);
        var work = new List<NeighborhoodSettlementCompletedWork>();
        decimal? fare = null; string? paymentStatus = null;
        if (settlement is not null)
        {
            var attempt = await Query<음식배달시도>().SingleOrDefaultAsync(x => x.Id == settlement.배달시도Id, ct);
            if (attempt is not null && attempt.주문번호 == id && attempt.기사Id == settlement.기사Id
                && attempt.중단시각Utc is null && attempt.전달완료시각Utc is { } deliveredAt)
            {
                parties.Add(settlement.기사Id, "Driver"); fare = settlement.세전대금; paymentStatus = settlement.지급상태Code;
                work.Add(new(C.Delivery, "FoodSettlement", settlement.정산StableId, 1, Utc(deliveredAt),
                    ["food-attempt:" + attempt.시도StableId, "food-settlement:" + settlement.정산StableId]) { ActorUserId = settlement.기사Id });
            }
        }
        var receipt = await Query<음식주문상태이력>().Where(x => x.음식주문Id == order.Id && x.다음상태 == "수령확인")
            .OrderByDescending(x => x.전이시각Utc).FirstOrDefaultAsync(ct);
        if (receipt is not null && order.상태 == "수령확인")
            work.Add(new(C.ProductSale, "FoodOrderReceipt", receipt.Id.ToString(CultureInfo.InvariantCulture), 1,
                Utc(receipt.전이시각Utc), ["food-order:" + id, "food-receipt:" + receipt.Id]));
        return Snapshot(C.FoodOrder, id, C.PreparedFood, parties, work, receipt is null ? 0 : 1,
            sale: order.총주문금액, fare: fare, canonical: "food:" + id, externalPayment: paymentStatus);
    }

    private async Task<NeighborhoodSettlementSourceSnapshot> Cargo(string id, CancellationToken ct)
    {
        var request = await Query<화주운송의뢰>().SingleOrDefaultAsync(x => x.의뢰Id == id, ct) ?? throw Missing();
        var cargo = await CargoDetails(id, ct);
        var parties = new Parties(); parties.Add(request.화주Id, "Seller"); parties.Add(request.주문자UserId, "Buyer");
        if (cargo.Transport?.확정기사Id is { Length: > 0 } driver) parties.Add(driver, "Driver");
        var work = cargo.Transport is { } transport && Delivered(transport)
            ? new[] { DeliveryWork(transport, id) } : Array.Empty<NeighborhoodSettlementCompletedWork>();
        return Snapshot(C.CargoRequest, id, C.TransportOnly, parties, work, work.Length,
            fare: cargo.Fare, canonical: "cargo:" + id, externalPayment: cargo.PaymentStatus);
    }

    private async Task<(운송원장? Transport, decimal? Fare, string? PaymentStatus)> CargoDetails(string id, CancellationToken ct)
    {
        var request = await Query<화주운송의뢰>().SingleOrDefaultAsync(x => x.의뢰Id == id, ct) ?? throw Missing();
        var transports = await Query<운송원장>().Where(x => x.의뢰Id == id && x.배차업무유형 != 상태값.배차업무유형.음식배달)
            .Take(2).ToArrayAsync(ct);
        if (transports.Length > 1) throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceAmbiguous");
        var fare = await Query<운임구성>().SingleOrDefaultAsync(x => x.의뢰Id == id, ct);
        var transport = transports.SingleOrDefault();
        var linkedFare = fare is not null && request.운임구성Id == fare.Id && fare.의뢰Id == request.의뢰Id;
        var validAgreement = string.IsNullOrWhiteSpace(fare?.합의요금표SnapshotJson)
            || transport is not null && 기사지급준비UseCase.저장합의조회(fare, request, transport) is not null;
        // 일반 화물의 권위는 운임구성입니다. 음식배차 전용 transport 필드로 화물 운임을 비교하지 않습니다.
        decimal? amount = linkedFare && validAgreement && !request.모의처리처수행
            && !string.IsNullOrWhiteSpace(transport?.확정기사Id) ? fare!.기사지급예정운임 : null;
        var payments = transport is null ? [] : await Query<기사운송대금지급요청>().Where(x => x.운송Id == transport.Id)
            .OrderByDescending(x => x.승인일시Utc).Take(1).ToArrayAsync(ct);
        return (transport, amount, payments.FirstOrDefault()?.상태코드);
    }

    private async Task<List<NeighborhoodSettlementCompletedWork>> LotWork(분산재고LotRecord lot, 분산거점Record site, Parties parties, CancellationToken ct)
    {
        var result = new List<NeighborhoodSettlementCompletedWork>();
        var history = await Query<재고이력>().Where(x => x.입고상품Id == lot.InboundItemId && x.원인유형 == "분산물류실물영수증"
            && ((x.이력유형 == "분할입고" && x.변경수량 > 0) || (x.이력유형 == "기사직접피킹" && x.변경수량 < 0)
                || (x.이력유형 == "실물반환" && x.변경수량 > 0))).ToArrayAsync(ct);
        foreach (var row in history)
        {
            var ids = row.메모.StartsWith("photos:", StringComparison.Ordinal) ? row.메모[7..].Split(',', StringSplitOptions.RemoveEmptyEntries) : [];
            if (ids.Length == 0) continue;
            var photos = await Query<분산사진증빙Record>().Where(x => Enumerable.Contains(ids, x.EvidenceId)
                && x.ActorUserId == row.처리UserId && x.BoundAtUtc != null).ToArrayAsync(ct);
            var photoKind = row.이력유형 == "실물반환" ? "Return" : row.변경수량 > 0 ? "Receive" : "Pick";
            photos = photos.Where(x => PhotoIncludes(x, lot.LotId) && x.Kind == photoKind).ToArray();
            var backed = new List<분산사진증빙Record>();
            분산물류CommandReceipt? exactReceipt = null; 분산물류완료행? exactLine = null;
            foreach (var photo in photos)
            {
                var receipt = await Query<분산물류CommandReceipt>().SingleOrDefaultAsync(x => x.ActorUserId == row.처리UserId
                    && x.RequestId == photo.CompletionRequestId && x.AggregateId == photo.AggregateId
                    && (x.Kind == photoKind || x.Kind == photoKind + "Batch") && x.Quantity >= Math.Abs(row.변경수량), ct);
                if (receipt is null) continue;
                var lines = CompletionLines(receipt);
                if (lines is null) { backed.Add(photo); continue; }
                var matching = lines.Where(x => x.InventoryHistoryId == row.Id && x.LotId == lot.LotId
                    && x.InboundItemId == lot.InboundItemId && x.Quantity == Math.Abs(row.변경수량)
                    && x.PhotoEvidenceIds.Order(StringComparer.Ordinal).SequenceEqual(ids.Order(StringComparer.Ordinal))
                    && x.PhotoEvidenceIds.All(id => photos.Any(p => p.EvidenceId == id && PhotoIncludes(p, lot.LotId, x.OrderId)))).ToArray();
                if (matching.Length != 1) continue;
                var line = matching[0];
                if (line.ReservationId is not null && !await Query<분산재고예약Record>().AnyAsync(x => x.ReservationId == line.ReservationId
                    && x.LotId == lot.LotId && x.OrderId == line.OrderId && x.WaveId == receipt.AggregateId, ct)) continue;
                if (exactReceipt is not null && exactReceipt.RequestId != receipt.RequestId)
                    throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceEvidenceInvalid");
                exactReceipt = receipt; exactLine = matching[0]; backed.Add(photo);
            }
            if (backed.Count == 0) continue;
            var kind = photoKind == "Return" ? "ReturnToStorage" : row.변경수량 > 0 ? C.Inbound : C.Picking;
            parties.Add(row.처리UserId, photoKind == "Pick" ? "Picker" : "Receiver");
            result.Add(new(kind, "InventoryHistory", row.Id.ToString(CultureInfo.InvariantCulture), Math.Abs(row.변경수량),
                Utc(row.처리일시), backed.Select(x => "photo:" + x.EvidenceId).ToArray())
                { ActorUserId = row.처리UserId, GoodsOwnerUserId = lot.OwnerUserId, SellerUserId = lot.SellerUserId, SpaceOwnerUserId = site.OwnerUserId,
                    OriginRevision = exactReceipt?.Revision ?? 0, RelatedOrderId = exactLine?.OrderId });
        }
        return result;
    }

    private static IReadOnlyList<분산물류완료행>? CompletionLines(분산물류CommandReceipt receipt)
    {
        if (receipt.CompletionLinesJson is null) return null;
        try
        {
            var detail = JsonSerializer.Deserialize<분산물류완료근거>(receipt.CompletionLinesJson, Json);
            var kind = receipt.Kind.EndsWith("Batch", StringComparison.Ordinal) ? receipt.Kind[..^5] : receipt.Kind;
            var lines = detail?.Lines;
            if (detail?.SchemaVersion != 1 || receipt.Revision <= 0 || lines is not { Count: > 0 and <= 100 }
                || kind is not ("Receive" or "Pick" or "Deliver" or "Return")
                || lines.Any(x => x is null || x.WorkKind != kind || x.ActorUserId != receipt.ActorUserId
                    || string.IsNullOrWhiteSpace(x.LineKey) || string.IsNullOrWhiteSpace(x.LotId) || x.InboundItemId <= 0 || x.Quantity <= 0
                    || (kind == "Deliver" ? x.InventoryHistoryId is not null : x.InventoryHistoryId is not > 0)
                    || (kind == "Receive" ? x.ReservationId is not null || x.OrderId is not null
                        : string.IsNullOrWhiteSpace(x.ReservationId) || string.IsNullOrWhiteSpace(x.OrderId) || x.LineKey != x.ReservationId)
                    || x.PhotoEvidenceIds is not { Count: >= 1 and <= 3 } || x.PhotoEvidenceIds.Any(string.IsNullOrWhiteSpace)
                    || x.PhotoEvidenceIds.Distinct(StringComparer.Ordinal).Count() != x.PhotoEvidenceIds.Count)
                || lines.Select(x => x.LineKey).Distinct(StringComparer.Ordinal).Count() != lines.Count
                || lines.Where(x => x.InventoryHistoryId.HasValue).Select(x => x.InventoryHistoryId).Distinct().Count() != lines.Count(x => x.InventoryHistoryId.HasValue)
                || lines.Sum(x => (long)x.Quantity) != receipt.Quantity)
                throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceEvidenceInvalid");
            return lines;
        }
        catch (JsonException) { throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceEvidenceInvalid"); }
    }

    private async Task<(long Revision, IReadOnlyList<string> Evidence)?> DeliveryCompletion(분산배송WaveRecord wave,
        분산배송주문Record order, IReadOnlyList<분산재고예약Record> reservations, CancellationToken ct)
    {
        var receipts = await Query<분산물류CommandReceipt>().Where(x => x.AggregateId == wave.WaveId && x.ActorUserId == wave.DriverId
            && (x.Kind == "Deliver" || x.Kind == "DeliverBatch")).ToArrayAsync(ct);
        var quantities = reservations.ToDictionary(x => x.ReservationId, _ => 0L);
        var evidence = new HashSet<string>(StringComparer.Ordinal); long revision = 0;
        foreach (var receipt in receipts)
        {
            var lines = CompletionLines(receipt); if (lines is null) continue;
            var photos = await BoundPhotos(receipt.ActorUserId, receipt.RequestId, ct);
            foreach (var line in lines.Where(x => x.OrderId == order.OrderId))
            {
                var reservation = reservations.SingleOrDefault(x => x.ReservationId == line.ReservationId && x.LotId == line.LotId);
                if (reservation is null) continue;
                var lot = await Query<분산재고LotRecord>().SingleAsync(x => x.LotId == line.LotId, ct);
                if (lot.InboundItemId != line.InboundItemId || !line.PhotoEvidenceIds.All(id => photos.Any(photo => photo.EvidenceId == id
                    && photo.Kind == "Deliver" && PhotoIncludes(photo, line.LotId, order.OrderId)))) continue;
                quantities[reservation.ReservationId] += line.Quantity;
                foreach (var id in line.PhotoEvidenceIds) evidence.Add("photo:" + id);
                evidence.Add("distributed-completion:" + receipt.RequestId.ToString("D")); revision = Math.Max(revision, receipt.Revision);
            }
        }
        // 구형/신규가 섞인 전달이나 누락·초과 근거를 전체 주문 성과로 승격하지 않습니다.
        return revision > 0 && reservations.All(x => quantities[x.ReservationId] == x.DeliveredQuantity)
            ? (revision, evidence.Order(StringComparer.Ordinal).ToArray()) : null;
    }

    private async Task<(IReadOnlyList<NeighborhoodSettlementStorageInterval> Continuous,
        IReadOnlyList<NeighborhoodSettlementStorageQuantityInterval> Quantities)> StorageIntervals(분산재고LotRecord lot,
        IReadOnlyList<NeighborhoodSettlementCompletedWork> work, CancellationToken ct)
    {
        // 잔여 반출은 기존 입고 누계를 줄이지 않습니다. 반출 사진까지의 기간 연속성이 재구성되기 전에는
        // 누계로 열린 보관기간·수량을 추정하지 않으며, 기수행 입고 보수 근거와 금전 원본은 그대로 둡니다.
        if (await Query<재고이력>().AnyAsync(x => x.입고상품Id == lot.InboundItemId
            && x.원인유형 == "분산물류실물영수증" && x.이력유형 == "잔여재고반출", ct)) return ([], []);
        var reservations = await Query<분산재고예약Record>().Where(x => x.LotId == lot.LotId).ToArrayAsync(ct);
        var picked = reservations.Sum(x => x.PickedQuantity);
        var returned = reservations.Sum(x => x.ReturnedQuantity);
        // 전체 실물 변화가 사진·영수증 이력과 일치할 때만 기간을 인증합니다. 누락 이력은 추정하지 않습니다.
        if (work.Where(x => x.Kind == C.Inbound).Sum(x => x.Quantity) != lot.ReceivedQuantity
            || work.Where(x => x.Kind == C.Picking).Sum(x => x.Quantity) != picked
            || work.Where(x => x.Kind == "ReturnToStorage").Sum(x => x.Quantity) != returned) return ([], []);
        var intervals = new List<NeighborhoodSettlementStorageInterval>();
        var quantities = new List<NeighborhoodSettlementStorageQuantityInterval>();
        decimal balance = 0, minimum = 0; DateTime? start = null, quantityStart = null;
        foreach (var item in work.OrderBy(x => x.OccurredAtUtc).ThenBy(x => long.Parse(x.OriginId, CultureInfo.InvariantCulture)))
        {
            if (item.OccurredAtUtc is not { Kind: DateTimeKind.Utc } at || at > Now || item.Quantity <= 0) return ([], []);
            var before = balance;
            // 같은 시각의 변화는 실제 이력 순서로 검증하되 길이 0인 수량 구간은 만들지 않습니다.
            if (quantityStart is { } quantityFrom && at > quantityFrom && before > 0)
                quantities.Add(new(quantityFrom, at, before));
            balance += item.Kind == C.Picking ? -item.Quantity : item.Quantity;
            if (balance < 0) return ([], []);
            quantityStart = at;
            if (before == 0 && balance > 0) { start = at; minimum = balance; }
            else if (balance > 0) minimum = Math.Min(minimum, balance);
            else if (start is { } from) { intervals.Add(new(from, at, minimum)); start = null; }
        }
        if (balance != lot.ReceivedQuantity - picked + returned) return ([], []);
        if (start is { } current) intervals.Add(new(current, null, minimum));
        if (quantityStart is { } quantityCurrent && balance > 0) quantities.Add(new(quantityCurrent, null, balance));
        return (intervals, quantities);
    }

    private Task<분산사진증빙Record[]> BoundPhotos(string actor, Guid id, CancellationToken ct)
        => Query<분산사진증빙Record>().Where(x => x.ActorUserId == actor && x.CompletionRequestId == id && x.BoundAtUtc != null).ToArrayAsync(ct);
    private static bool PhotoIncludes(분산사진증빙Record row, string lotId, string? orderId = null)
    {
        try
        {
            var binding = DistributedPhotoEvidenceService.Details(row).Binding;
            return binding.ActorUserId == row.ActorUserId && binding.RequestId == row.CompletionRequestId
                && binding.Kind == row.Kind && binding.AggregateId == row.AggregateId
                && binding.LotIds.Contains(lotId) && (orderId is null || binding.OrderIds.Contains(orderId));
        }
        catch (Exception ex) when (ex is JsonException or DistributedFulfillmentException)
        { throw new NeighborhoodSettlementException("NeighborhoodSettlementSourceEvidenceInvalid"); }
    }
    private static bool Delivered(운송원장 row) => row.상태 == "인수완료" && row.도착.HasValue && !string.IsNullOrWhiteSpace(row.확정기사Id);
    private static NeighborhoodSettlementCompletedWork DeliveryWork(운송원장 row, string cargoId)
        => new(C.Delivery, "CargoTransport", cargoId, 1, Utc(row.도착!.Value), ["cargo-transport:" + row.Id]) { ActorUserId = row.확정기사Id };
    private static void LotParties(Parties parties, 분산재고LotRecord lot, 분산거점Record site)
    { parties.Add(lot.OwnerUserId, "Owner"); parties.Add(lot.SellerUserId, "Seller"); parties.Add(site.OwnerUserId, "SpaceOwner"); }
    private static NeighborhoodSettlementException Missing() => new("NeighborhoodSettlementSourceNotFound", 404);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private NeighborhoodSettlementSourceSnapshot Snapshot(string kind, string id, string activity, Parties parties,
        IReadOnlyList<NeighborhoodSettlementCompletedWork> work, decimal completed, decimal? sale = null, decimal? fare = null,
        string? canonical = null, string? externalPayment = null, DateTime? storageStartedAt = null,
        IReadOnlyList<NeighborhoodSettlementStorageInterval>? storageIntervals = null,
        NeighborhoodSettlementRewardOutcome? rewardOutcome = null, NeighborhoodSettlementWorkflowDto? workflow = null,
        IReadOnlyList<string>? storageEvidence = null,
        IReadOnlyList<NeighborhoodSettlementInventoryReservation>? inventoryReservations = null,
        IReadOnlyList<NeighborhoodSettlementStorageQuantityInterval>? storageQuantityIntervals = null,
        NeighborhoodSettlementCustomerReceiptDto? customerReceipt = null)
    {
        var participants = parties.Values();
        // 업무 수량·지급 상태의 진전은 합의 가격과 당사자 판본을 바꾸지 않습니다.
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            { kind, id, activity, participants, sale, fare, canonical }, Json)))).ToLowerInvariant();
        return new() { SourceKind = kind, SourceId = id, SourceVersion = digest, SourceDigest = digest,
            AllowedActivityKinds = [activity], Participants = participants, CompletedWork = work,
            ActualCompletedUnits = completed, CompletionEvidenceReferences = work.SelectMany(x => x.EvidenceReferences).Distinct().ToArray(),
            HistoricalSaleTotalKrw = sale, FrozenDriverFareKrw = fare, CanonicalDeliveryOrigin = canonical,
            OriginalExternalPaymentStatus = externalPayment, StorageStartedAtUtc = storageStartedAt,
            StorageIntervals = storageIntervals ?? [], StorageEvidenceReferences = storageEvidence ?? [],
            StorageQuantityIntervals = storageQuantityIntervals ?? [],
            ObservedAtUtc = Now, RewardOutcome = rewardOutcome, CustomerReceipt = customerReceipt,
            Workflow = workflow, InventoryReservations = inventoryReservations ?? [] };
    }
    private sealed class Parties
    {
        private readonly Dictionary<string, HashSet<string>> roles = new(StringComparer.Ordinal);
        public void Add(string? user, params string[] values)
        {
            if (string.IsNullOrWhiteSpace(user)) return;
            if (!roles.TryGetValue(user, out var found)) roles[user] = found = new(StringComparer.Ordinal);
            found.UnionWith(values);
        }
        public NeighborhoodSettlementSourceParty[] Values() => roles.OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => new NeighborhoodSettlementSourceParty(x.Key, x.Value.Order(StringComparer.Ordinal).ToArray())).ToArray();
    }
}
