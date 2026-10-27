using System.Data;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Services.Community;

public sealed partial class NeighborhoodMemberDeliveryUseCase
{
    public async Task RegisterAsync(string requestId, NeighborhoodDeliveryRequest request, CancellationToken ct)
    {
        if (!request.MemberDeliveryRequested) return;
        var pending = await ExecuteDatabaseMutationAsync(() => RegisterCoreAsync(requestId, request, ct), ct);
        // 원 등록 transaction을 폐기한 뒤 별도 멱등 fanout을 실행한다.
        if (pending)
        {
            db.ChangeTracker.Clear(); // Commit 뒤 다른 요청이 바꾼 정본을 별도 fanout에서 다시 읽는다.
            await FanoutAsync(requestId, ct);
        }
    }

    private async Task<bool> RegisterCoreAsync(string requestId, NeighborhoodDeliveryRequest request, CancellationToken ct)
    {
        Gate(); var actor = Actor();
        Require(request.MemberDeliveryDisclosureAgreed && request.MemberDeliveryNoticeVersion == NeighborhoodMemberDeliveryRoutes.NoticeVersion
            && request.PrivacyConsentEvidenceId is not null && request.DirectPaymentAgreed,
            "MemberDisclosureRequired", "선정된 배송 참여자에게 필요한 정보 제공과 직접 지급을 확인해 주세요.", 400);
        Require(request.DispatchMode is null && request.FulfillmentSelection is null && request.AutoFulfillment is null,
            "MemberDeliverySourceConflict", "일반 회원 배송과 전문 배차·처리처 선택은 함께 지정할 수 없습니다.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var source = await SourceAsync(requestId, ct);
        var row = await Deliveries.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        var queue = await db.운송원장.SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        Require(source.주문자UserId == actor && queue is { 일반회원배송전용: true } && queue.확정기사Id is null,
            "MemberDeliverySourceConflict", "본인의 일반 회원 배송 원천을 다시 확인해 주세요.");
        Require(queue!.생활배송협업Id == request.CollaborationId && queue.생활배송조건판본 == request.ExpectedTermsRevision
            && 생활배송출처Evidence.Read(source.정산메모) == request.SourcePostId,
            "IdempotencyConflict", "원 배송 의뢰의 협업·조건 판본·출처가 다릅니다.");
        if (row is not null)
        {
            Require(row.OwnerUserId == actor && row.ConsentEvidenceId == request.PrivacyConsentEvidenceId && row.FareKrw == request.AgreedFareKrw,
                "IdempotencyConflict", "원 배송 의뢰의 조건이 다릅니다.");
            if (row.StateCode != "Pending") { await tx.CommitAsync(ct); return false; }
            Require(source.상태 == 상태값.의뢰상태.생성됨 && queue.상태 == 상태값.배차대기상태.대기
                && queue.배차큐단계 != 상태값.배차큐단계.종료 && queue.일반회원수행자UserId is null,
                "MemberDeliverySourceConflict", "이미 변경된 배송의 대기 상태를 복원할 수 없습니다.");
            Require(await 회원협업연결확인Async(source, queue, ct),
                "MemberCollaborationPending", "협업의 배송 연결 결과를 확인한 뒤 같은 요청으로 다시 접수해 주세요.");
            if (!queue.생활배송수락준비완료)
            {
                queue.생활배송수락준비완료 = true; queue.생활배송선택판본++; queue.UpdatedAt = Now;
                await SaveAsync(ct);
            }
            await tx.CommitAsync(ct);
            return true;
        }
        Require(source.주문자UserId == actor && source.상태 == 상태값.의뢰상태.생성됨 && !source.모의처리처수행 && !source.자동처리처수행,
            "MemberDeliverySourceConflict", "본인의 대기 중인 생활 물품 배송만 요청할 수 있습니다.");
        Require(queue is not null && queue.일반회원배송전용 && queue.확정기사Id is null && queue.상태 == 상태값.배차대기상태.대기,
            "MemberDeliverySourceConflict", "현재 배송 담당과 대기 상태를 다시 확인해 주세요.");
        Require(await 회원협업연결확인Async(source, queue!, ct),
            "MemberCollaborationPending", "협업의 배송 연결 결과를 확인한 뒤 같은 요청으로 다시 접수해 주세요.");
        Require(source.최종운임 is >= 0 && source.최종운임 == request.AgreedFareKrw,
            "QuoteChanged", "서버 원장에 저장된 배송비를 다시 확인해 주세요.");
        var end = source.하차_시간창_종료일시 ?? source.픽업_시간창_종료일시;
        if (request.DesiredCompletionUtc is { } deadline && deadline < end) end = deadline;
        Require(end > Now && source.픽업_시간창_종료일시 > Now, "MemberDeliveryWindowExpired", "배송 가능 시간이 지났습니다.");
        var canonicalFare = await db.화주운송의뢰.AsNoTracking().Where(x => x.의뢰Id == requestId).Select(x => x.최종운임).SingleAsync(ct);
        row = new() { RequestId = requestId, OwnerUserId = actor, ConsentEvidenceId = request.PrivacyConsentEvidenceId!.Value,
            NoticeVersion = request.MemberDeliveryNoticeVersion!, FareKrw = canonicalFare!.Value, WindowEndUtc = end, UpdatedAtUtc = Now };
        row.DistanceKm = await db.운임구성.AsNoTracking().Where(x => x.의뢰Id == requestId).Select(x => x.예상거리Km).SingleOrDefaultAsync(ct);
        // 기존 원장의 실제 거리만 재사용한다. 활동점 직선거리를 배송비·주행거리로 바꾸지 않는다.
        var cost = await db.운송원장.AsNoTracking().Where(x => x.의뢰Id == requestId).Select(x => x.운임).SingleAsync(ct);
        Require(cost is null || cost == row.FareKrw, "QuoteChanged", "운송 원장 운임이 바뀌었습니다.");
        queue!.생활배송수락준비완료 = true; queue.생활배송선택판본++; queue.UpdatedAt = Now;
        db.Add(row); await RequireApplicationConsentAsync(row, ct); await SaveAsync(ct); await tx.CommitAsync(ct);
        return true;
    }

    public Task FanoutAsync(string requestId, CancellationToken ct)
        => ExecuteDatabaseMutationAsync(() => FanoutCoreAsync(requestId, ct), ct);

    private async Task FanoutCoreAsync(string requestId, CancellationToken ct)
    {
        if (!Enabled) return;
        Gate(); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await Deliveries.SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        if (row is null || row.StateCode != "Pending" || row.WindowEndUtc <= Now) return;
        await RequireApplicationConsentAsync(row, ct);
        var source = await SourceAsync(requestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == requestId, ct);
        if (queue is null || !queue.일반회원배송전용 || !queue.생활배송수락준비완료
            || queue.상태 != 상태값.배차대기상태.대기 || source.상태 != 상태값.의뢰상태.생성됨
            || !Coordinates(source.픽업_위도, source.픽업_경도) || row.OwnerUserId != source.주문자UserId
            || !await 회원협업연결확인Async(source, queue, ct)) return;
        var points = await Participants.AsNoTracking().Where(x => x.ReceiveOffers && x.ActiveRequestId == null && x.UserId != row.OwnerUserId).ToListAsync(ct);
        var existing = (await Offers.AsNoTracking().Where(x => x.RequestId == requestId).Select(x => x.MemberUserId).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        foreach (var member in points)
        {
            if (existing.Contains(member.UserId)) continue;
            var point = Open<ActivityPoint>(member.PrivatePayload);
            if (!Coordinates(point.Latitude, point.Longitude) || point.VehicleType != source.차량종류
                || point.MaxWeightKg < source.화물중량Kg || source.화물중량Kg is null or <= 0
                || DistanceKm(point.Latitude!.Value, point.Longitude!.Value, source.픽업_위도!.Value, source.픽업_경도!.Value) > point.RadiusKm) continue;
            if (await HasExistingDriverWorkAsync(member.UserId, ct)) continue;
            if (execution.IsOperational)
            {
                try { await AdultAsync(member.UserId, ct); }
                catch (NeighborhoodMemberDeliveryException) { continue; }
            }
            var expires = Now.AddMinutes(5); if (row.WindowEndUtc < expires) expires = row.WindowEndUtc;
            if (source.픽업_시간창_종료일시 < expires) expires = source.픽업_시간창_종료일시;
            if (expires <= Now) continue;
            db.Add(new NeighborhoodMemberOffer { OfferId = Stable("NMO-", requestId + ":" + member.UserId), RequestId = requestId,
                MemberUserId = member.UserId, ParticipationRevision = member.Revision, DeliveryRevision = row.Revision,
                CreatedAtUtc = Now, ExpiresAtUtc = expires });
        }
        await SaveAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<NeighborhoodMemberOfferResponse>> OffersAsync(CancellationToken ct = default)
    {
        var actor = Actor(); var rows = await Offers.AsNoTracking().Where(x => x.MemberUserId == actor)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
        var result = new List<NeighborhoodMemberOfferResponse>();
        foreach (var row in rows) result.Add(await OfferDtoAsync(row, ct)); return result;
    }
    public async Task<NeighborhoodMemberOfferResponse?> OfferAsync(string offerId, CancellationToken ct = default)
    {
        var actor = Actor(); var row = await Offers.AsNoTracking().SingleOrDefaultAsync(x => x.OfferId == offerId && x.MemberUserId == actor, ct);
        return row is null ? null : await OfferDtoAsync(row, ct);
    }
    private async Task<NeighborhoodMemberOfferResponse> OfferDtoAsync(NeighborhoodMemberOffer row, CancellationToken ct)
    {
        var delivery = await Deliveries.AsNoTracking().SingleAsync(x => x.RequestId == row.RequestId, ct);
        var source = await SourceAsync(row.RequestId, ct);
        var queue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == row.RequestId, ct);
        var point = await Participants.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == row.MemberUserId, ct);
        var state = row.StateCode;
        if (state == "Pending" && (source.상태 == 상태값.의뢰상태.취소 || delivery.StateCode == "Cancelled")) state = "Cancelled";
        else if (state == "Pending" && delivery.StateCode != "Pending") state = "AssignedElsewhere";
        else if (state == "Pending" && row.ExpiresAtUtc <= Now) state = "Expired";
        else if (state == "Pending" && (point?.ReceiveOffers != true || point.Revision != row.ParticipationRevision)) state = "Withdrawn";
        return new() { OfferId = row.OfferId, RequestId = row.RequestId, Revision = row.Revision, StateCode = state,
            CargoName = source.화물종류, Quantity = source.화물수량 ?? 1, WeightKg = source.화물중량Kg ?? 0,
            PickupArea = Area(source.픽업_도로명주소), DropoffArea = Area(source.하차_도로명주소), DistanceKm = delivery.DistanceKm,
            FareKrw = delivery.FareKrw, ExpiresAtUtc = row.ExpiresAtUtc, CanAccept = Enabled && state == "Pending"
                && queue is { 일반회원배송전용: true, 생활배송수락준비완료: true }
                && await 회원협업연결확인Async(source, queue, ct) };
    }
    public async Task<NeighborhoodMemberOfferResponse> DeclineAsync(string offerId, NeighborhoodMemberCommandRequest request, CancellationToken ct = default)
    {
        var actor = Actor(); var digest = Digest(request);
        await ExecuteDatabaseMutationAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await ReplayAsync(actor, request.ClientRequestId, "Decline", offerId, digest, ct)) return;
            var offer = await Offers.SingleOrDefaultAsync(x => x.OfferId == offerId && x.MemberUserId == actor, ct);
            Require(offer is not null, "MemberOfferNotFound", "본인의 배송 제안을 찾을 수 없습니다.", 404);
            Require(offer!.StateCode == "Pending" && offer.Revision == request.ExpectedRevision,
                "MemberRevisionConflict", "제안 상태가 바뀌었습니다.");
            offer.StateCode = "Declined"; offer.Revision++; Receipt(actor, request.ClientRequestId, "Decline", offerId, digest);
            await SaveAsync(ct); await tx.CommitAsync(ct);
        }, ct);
        return (await OfferAsync(offerId, ct))!;
    }

    private async Task<bool> HasExistingDriverWorkAsync(string actor, CancellationToken ct)
        => await db.운송원장.AsNoTracking().AnyAsync(x => (x.확정기사Id == actor || x.기사_운송자 == actor)
            && x.배차큐단계 == 상태값.배차큐단계.확정, ct)
            || await db.음식배달시도.AsNoTracking().AnyAsync(x => x.기사Id == actor && x.수락시각Utc != default
                && x.중단시각Utc == null && x.전달완료시각Utc == null, ct);
    private void RequireFreshGps(NeighborhoodMemberAcceptRequest request)
        => Require(Coordinates(request.Latitude, request.Longitude) && request.AccuracyM is >= 0 and <= 100
            && request.LocationAtUtc.Kind == DateTimeKind.Utc && request.LocationAtUtc != default
            && request.LocationAtUtc >= Now.AddMinutes(-5) && request.LocationAtUtc <= Now.AddSeconds(30),
            "MemberFreshLocationRequired", "최근 5분 이내 정확도 100m 이내의 현재 위치가 필요합니다.", 400);

    public async Task<NeighborhoodMemberWorkResponse> AcceptAsync(string offerId, NeighborhoodMemberAcceptRequest request, CancellationToken ct = default)
    {
        Gate(); var actor = Actor(); var digest = Digest(request);
        if (await ReplayAsync(actor, request.ClientRequestId, "Accept", offerId, digest, ct))
        {
            var old = await Offers.AsNoTracking().SingleAsync(x => x.OfferId == offerId && x.MemberUserId == actor, ct);
            return (await WorkAsync(old.RequestId, ct))!;
        }
        RequireFreshGps(request); await AdultAsync(actor, ct);
        var initial = await Offers.AsNoTracking().SingleOrDefaultAsync(x => x.OfferId == offerId && x.MemberUserId == actor, ct);
        Require(initial is not null, "MemberOfferNotFound", "본인의 배송 제안을 찾을 수 없습니다.", 404);
        var initialSource = await SourceAsync(initial!.RequestId, ct);
        var initialQueue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == initial.RequestId, ct);
        Require(initialQueue is { 일반회원배송전용: true, 생활배송수락준비완료: true }
            && await 회원협업연결확인Async(initialSource, initialQueue, ct),
            "MemberCollaborationPending", "협업의 배송 연결이 확인된 최신 제안만 수락할 수 있습니다.");
        var routeOption = RouteOption(initialSource.차량종류);
        Require(routeOption != "" && Coordinates(initialSource.픽업_위도, initialSource.픽업_경도)
            && Coordinates(initialSource.하차_위도, initialSource.하차_경도), "MemberActualRouteRequired", "현재 이동 수단의 실제 경로를 확인할 수 없습니다.");
        var pickup = new 배차경로좌표(initialSource.픽업_위도!.Value, initialSource.픽업_경도!.Value);
        var dropoff = new 배차경로좌표(initialSource.하차_위도!.Value, initialSource.하차_경도!.Value);
        var first = await routes.EstimateRouteAsync(new(request.Latitude, request.Longitude), pickup, routeOption, ct);
        var second = await routes.EstimateRouteAsync(pickup, dropoff, routeOption, ct);
        Require(Actual(first, routeOption) && Actual(second, routeOption), "MemberActualRouteRequired", "현재 이동 수단의 실제 경로와 소요 시간을 확인한 뒤 수락할 수 있습니다.");
        db.ChangeTracker.Clear(); // 외부 경로 조회 전 읽은 원 의뢰를 권위 있는 현재 SQL 상태로 다시 읽는다.
        // 외부 경로 견적은 재발송하지 않고 SQL 명령만 같은 요청 증적으로 회복한다.
        var assignedRequestId = await ExecuteDatabaseMutationAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await ReplayAsync(actor, request.ClientRequestId, "Accept", offerId, digest, ct)) return initial.RequestId;
            Require(!await NeighborhoodMemberWorkExclusion.LockAndHasMemberWorkAsync(db, actor, ct),
                "MemberWorkCapacityChanged", "이미 수락한 배송을 마친 뒤 다음 제안을 수락할 수 있습니다.");
            RequireFreshGps(request);
            var offer = await Offers.SingleAsync(x => x.OfferId == offerId && x.MemberUserId == actor, ct);
            var delivery = await Deliveries.SingleAsync(x => x.RequestId == offer.RequestId, ct);
            var source = await SourceAsync(offer.RequestId, ct);
            var currentQueue = await db.운송원장.AsNoTracking().SingleOrDefaultAsync(x => x.의뢰Id == offer.RequestId, ct);
            Require(currentQueue is { 일반회원배송전용: true, 생활배송수락준비완료: true }
                && await 회원협업연결확인Async(source, currentQueue, ct),
                "MemberCollaborationPending", "협업의 배송 연결이 바뀌었습니다. 최신 결과를 확인해 주세요.");
            var participant = await Participants.SingleOrDefaultAsync(x => x.UserId == actor, ct);
            Require(offer.StateCode == "Pending" && offer.ExpiresAtUtc > Now && offer.Revision == request.ExpectedRevision
                && delivery.StateCode == "Pending" && delivery.Revision == offer.DeliveryRevision && source.상태 == 상태값.의뢰상태.생성됨,
                "MemberOfferChanged", "제안이 만료되거나 다른 사람이 먼저 수락했습니다.");
            Require(participant is not null && participant.ReceiveOffers && participant.Revision == offer.ParticipationRevision
                && participant.ActiveRequestId is null && !await HasExistingDriverWorkAsync(actor, ct),
                "MemberWorkCapacityChanged", "참여 설정 또는 수행 중인 업무가 바뀌었습니다.");
            var activity = Open<ActivityPoint>(participant!.PrivatePayload);
            Require(activity.VehicleType == source.차량종류 && source.화물중량Kg is > 0 && activity.MaxWeightKg >= source.화물중량Kg,
                "MemberWorkCapacityChanged", "이동 수단과 운반 한도를 다시 확인해 주세요.");
            Require(source.픽업_위도 == pickup.Latitude && source.픽업_경도 == pickup.Longitude && source.하차_위도 == dropoff.Latitude
                && source.하차_경도 == dropoff.Longitude && source.최종운임 == delivery.FareKrw,
                "MemberOfferChanged", "배송 위치 또는 원장 금액이 바뀌었습니다.");
            var arrival = Now + first!.Duration!.Value;
            var departure = arrival < source.픽업_시간창_시작일시 ? source.픽업_시간창_시작일시 : arrival;
            var finish = departure.AddMinutes(3) + second!.Duration!.Value;
            if (source.하차_시간창_시작일시 is { } dropStart && finish < dropStart) finish = dropStart;
            Require(arrival <= source.픽업_시간창_종료일시 && finish <= delivery.WindowEndUtc,
                "MemberDeliveryWindowUnavailable", "현재 위치에서 배송 시간창 안에 수행할 수 없습니다.");
            await RequireApplicationConsentAsync(delivery, ct);
            var slot = await Participants.Where(x => x.UserId == actor && x.Revision == offer.ParticipationRevision && x.ReceiveOffers && x.ActiveRequestId == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveRequestId, offer.RequestId), ct);
            Require(slot == 1, "MemberWorkCapacityChanged", "다른 업무 수락이 먼저 완료되었습니다.");
            var changed = await db.운송원장.Where(x => x.의뢰Id == offer.RequestId && x.일반회원배송전용
                && x.상태 == 상태값.배차대기상태.대기 && x.확정기사Id == null && x.일반회원수행자UserId == null && x.생활배송수락준비완료)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.상태, 상태값.배차대기상태.확정)
                    .SetProperty(x => x.일반회원수행자UserId, actor).SetProperty(x => x.수행자종류, "CommunityMember")
                    .SetProperty(x => x.배차큐단계, 상태값.배차큐단계.확정).SetProperty(x => x.배차노출상태, 상태값.배차노출상태.확정)
                    .SetProperty(x => x.생활배송선택판본, x => x.생활배송선택판본 + 1).SetProperty(x => x.UpdatedAt, Now), ct);
            Require(changed == 1, "MemberOfferChanged", "다른 배차 확정이 먼저 저장되었습니다.");
            delivery.AssignedMemberUserId = actor; delivery.StateCode = "Assigned"; delivery.AssignedAtUtc = Now;
            delivery.DistanceKm = second.DistanceKm; delivery.Revision++; delivery.UpdatedAtUtc = Now;
            source.배차상태 = 상태값.배차상태.배차확정; source.UpdatedAt = Now;
            offer.StateCode = "Accepted"; offer.Revision++;
            Receipt(actor, request.ClientRequestId, "Accept", offerId, digest);
            db.운송이벤트.Add(new() { 의뢰Id = source.의뢰Id, 이벤트타입 = "NeighborhoodMemberAssigned",
                메타데이터 = System.Text.Json.JsonSerializer.Serialize(new { ActorUserId = actor, delivery.Revision, delivery.FareKrw }) });
            await SaveAsync(ct); await tx.CommitAsync(ct);
            return offer.RequestId;
        }, ct);
        db.ChangeTracker.Clear();
        await SyncLedgerAsync(assignedRequestId, actor, ct);
        return (await WorkAsync(assignedRequestId, ct))!;
    }
    private static bool Actual(배차경로예상결과? route, string option)
        => route is { 실제경로여부: true, DistanceKm: >= 0, Duration: not null } && route.Duration >= TimeSpan.Zero && route.RouteOption == option;

    private async Task<bool> 회원협업연결확인Async(화주운송의뢰 source, 운송원장 queue, CancellationToken ct)
    {
        // 저장된 신청 근거와 큐 연결을 모두 확인한다. 회원에게 전문 배차 모드를 부여하지 않는다.
        if (!queue.일반회원배송전용 || queue.확정기사Id is not null || queue.생활배송배차방식 is not null
            || source.모의처리처수행 || source.자동처리처수행) return false;
        var sourceBinding = new 운송원장();
        try { 생활배송배차Policy.큐초기화(sourceBinding, source.정산메모); }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or System.Text.Json.JsonException) { return false; }
        if (sourceBinding.생활배송배차방식 is not null) return false;
        if (sourceBinding.생활배송협업Id != queue.생활배송협업Id || sourceBinding.생활배송조건판본 != queue.생활배송조건판본) return false;
        if (queue.생활배송협업Id is null) return queue.생활배송조건판본 is null;
        if (!sourceBinding.일반회원배송전용 || queue.생활배송조건판본 is null or < 1 || memberCollaborations is null) return false;
        var work = await memberCollaborations.조회Async(queue.생활배송협업Id, ct);
        var intent = work?.DeliveryIntent;
        return work is not null && intent is { StateCode: "confirmed" }
            && work.LinkedDeliveryRequestId == source.의뢰Id && intent.ActorUserId == source.주문자UserId
            && (work.OwnerUserId == source.주문자UserId || work.RequesterUserId == source.주문자UserId)
            && work.Kind == NeighborhoodCollaborationKinds.Goods && work.Terms.TransferMethod == NeighborhoodTransferMethods.DriverDelivery
            && 생활협업UseCase.CurrentAgreement(work) && work.TermsRevision == queue.생활배송조건판본
            && intent.TermsRevision == work.TermsRevision && work.Terms.Quantity == source.화물수량
            && work.SourcePostId == 생활배송출처Evidence.Read(source.정산메모)
            && work.StatusCode is NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress
                or NeighborhoodCollaborationStates.CompletionProposed or NeighborhoodCollaborationStates.Completed
            && intent.Request.MemberDeliveryRequested && intent.Request.DispatchMode is null
            && intent.Request.FulfillmentSelection is null && intent.Request.AutoFulfillment is null
            && intent.Request.CollaborationId == work.StableId && intent.Request.ExpectedTermsRevision == work.TermsRevision
            && intent.Request.SourcePostId == work.SourcePostId && intent.Request.ClientRequestId == intent.ClientRequestId
            && source.클라이언트요청Id == NeighborhoodDeliveryRoutes.ClientRequestPrefix + intent.ClientRequestId.ToString("N")
            && source.정산메모?.StartsWith("neighborhood-delivery:v1:" + intent.Fingerprint + ":consent:", StringComparison.Ordinal) == true;
    }
}
