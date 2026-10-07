using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Community.Collaboration;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Domain.Community;
using 살뜰.Data;

namespace Ssalddel.Services.Community;

/// <summary>공개 글과 별개의 보호된 당사자 합의 원장. 배송·보관 수행 권위는 해당 기존 원장을 조회합니다.</summary>
public sealed class 생활협업UseCase(I생활협업Store store, SsalddelContext db, ICurrentUserAccessor currentUser,
    TimeProvider clock, I생활협업공간Source? spaces = null, I생활협업보관상태Source? storage = null,
    I생활협업배송Source? deliveries = null, I생활배송배차선택Service? deliveryChoices = null,
    Ssalddel.Services.Commerce.I통신판매거래Guard? commerce = null) : I생활협업UseCase, I생활협업연결Query
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<NeighborhoodCollaborationResponse>> 신청Async(NeighborhoodCollaborationCreateRequest request, CancellationToken cancellationToken = default)
    {
        var actor = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(actor)) return Fail("AuthenticationRequired", "로그인 후 신청할 수 있습니다.", 401);
        if (request is null || request.ClientRequestId == Guid.Empty || !NeighborhoodCollaborationKinds.IsKnown(request.Kind))
            return Fail("InvalidCollaborationRequest", "신청 종류와 요청 번호를 확인해 주세요.", 400);
        var stableId = CreateStableId(actor, request.ClientRequestId);
        var fingerprint = Fingerprint(request);
        if (await store.조회Async(stableId, cancellationToken) is { } prior)
            return ReplayCreate(prior, actor, fingerprint);
        var now = clock.GetUtcNow().UtcDateTime;
        if (!생활협업Policy.조건유효(request.Terms, request.Kind, now))
            return Fail("InvalidCollaborationTerms", "물품·수량·시간·비용과 보관 조건을 확인해 주세요.", 400);

        var source = await ResolveSourceAsync(request.SourcePostId, request.StorageSpaceId ?? request.Terms.StorageSpaceId,
            request.Kind, cancellationToken);
        if (source.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(source.Errors);
        if (source.Value.OwnerUserId == actor) return Fail("SelfCollaborationNotAllowed", "본인의 글에는 신청할 수 없습니다.", 400);
        if (request.SourcePostId.HasValue && request.ExpectedSourceUpdatedAtUtc != source.Value.PostUpdatedAtUtc)
            return Fail("SourceChanged", "글의 최신 내용을 확인한 뒤 다시 신청해 주세요.", 409);
        if (request.Kind == NeighborhoodCollaborationKinds.Storage && request.ExpectedStorageRevision != source.Value.SpaceRevision)
            return Fail("StorageSpaceChanged", "보관 공간의 최신 조건을 확인해 주세요.", 409);
        if (request.Kind == NeighborhoodCollaborationKinds.Storage && request.Terms.StorageSpaceId != source.Value.SpaceId)
            return Fail("InvalidStorageSpace", "선택한 보관 공간과 조건이 다릅니다.", 400);
        var record = new 생활협업Record
        {
            StableId = stableId, Revision = 1, TermsRevision = 1, CreateRequestId = request.ClientRequestId,
            CreateFingerprint = fingerprint, SourcePostId = request.SourcePostId, SourceUpdatedAtUtc = source.Value.PostUpdatedAtUtc,
            SourceConditionsFingerprint = source.Value.ConditionsFingerprint,
            SpaceRevision = source.Value.SpaceRevision, SourceTitle = source.Value.Title, PublicNeighborhoodRegionKey = source.Value.RegionKey,
            OwnerUserId = source.Value.OwnerUserId, RequesterUserId = actor, Kind = request.Kind, Terms = Clone(request.Terms),
            CreatedAtUtc = now, UpdatedAtUtc = now, ExpiresAtUtc = request.Terms.UntilUtc
        };
        if (record.Terms.TransferMethod is not null)
        {
            var sourcePost = await PublicSourceQuery().SingleAsync(x => x.Id == request.SourcePostId, cancellationToken);
            record.TermsAuthorRoleCode = "requester";
            record.ProviderRoleCode = sourcePost.RoleTag == NeighborhoodExchange.Offer ? "owner" : "requester";
        }
        if (commerce is not null)
        {
            var seller = record.Kind is NeighborhoodCollaborationKinds.Goods or NeighborhoodCollaborationKinds.Food
                ? (record.ProviderRoleCode == "requester" ? actor : record.OwnerUserId) : null;
            try { await commerce.요구Async(actor, seller, request.CommerceProtection, "neighborhood-collaboration", request.ClientRequestId.ToString("N"), cancellationToken); }
            catch (Ssalddel.Services.Commerce.거래보호Exception ex) { return Fail(ex.Code, ex.Message, ex.Status); }
        }
        AddHistory(record, "create", "requester", now);
        if (!await store.생성Async(record, cancellationToken))
        {
            var existing = await store.조회Async(stableId, cancellationToken);
            return existing is null ? Fail("RevisionConflict", "신청 결과를 다시 확인해 주세요.", 409) : ReplayCreate(existing, actor, fingerprint);
        }
        return Result.Ok(ToResponse(record, actor));
    }

    public async Task<Result<NeighborhoodCollaborationResponse>> 변경Async(string stableId, NeighborhoodCollaborationCommandRequest request, CancellationToken cancellationToken = default)
    {
        var actor = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(actor)) return Fail("AuthenticationRequired", "로그인 후 확인할 수 있습니다.", 401);
        if (request is null || request.ClientRequestId == Guid.Empty || request.ExpectedRevision < 1
            || string.IsNullOrWhiteSpace(request.Action) || request.Action.Length > 60 || request.ApplicantUserId?.Length > 160
            || request.LinkedDeliveryRequestId?.Length > 160)
            return Fail("InvalidCollaborationCommand", "현재 업무와 요청 번호를 확인해 주세요.", 400);
        var record = await LoadCurrentAsync(stableId, cancellationToken);
        if (record is null) return Fail("CollaborationNotFound", "신청을 찾을 수 없습니다.", 404);
        var party = IsParty(record, actor);
        var participant = record.Participants.SingleOrDefault(x => x.UserId == actor);
        if (!party && participant is null && request.Action != NeighborhoodCollaborationActions.RequestParticipation)
            return Fail("CollaborationNotFound", "본인의 신청을 찾을 수 없습니다.", 404);
        var fingerprint = Fingerprint(request);
        var receipt = record.Receipts.SingleOrDefault(x => x.ClientRequestId == request.ClientRequestId);
        if (receipt is not null)
        {
            if (receipt.ActorUserId != actor || receipt.Fingerprint != fingerprint)
                return Fail("IdempotencyConflict", "같은 요청 번호로 다른 결정을 보낼 수 없습니다.", 409);
            return Result.Ok(ToResponse(record, actor, true));
        }
        if (request.ClientRequestId == record.CreateRequestId || record.StorageReservationIntents.Any(x => x.RequestId == request.ClientRequestId))
            return Fail("IdempotencyConflict", "신청 번호는 다른 결정에 재사용할 수 없습니다.", 409);
        if (record.Revision != request.ExpectedRevision) return Fail("RevisionConflict", "상대방이 내용을 변경했습니다. 최신 내용을 다시 확인해 주세요.", 409);
        if (record.Receipts.Count >= 500) return Fail("CommandLimitReached", "이 업무의 변경 기록 한도에 도달했습니다.", 409);
        if (!AllowedActions(record, actor).Contains(request.Action, StringComparer.Ordinal))
            return Fail("ActionNotAllowed", "현재 상태 또는 권한에서는 이 결정을 할 수 없습니다.", 409);
        var now = clock.GetUtcNow().UtcDateTime;
        if (commerce is not null && request.Action is NeighborhoodCollaborationActions.Agree or NeighborhoodCollaborationActions.UpdateTerms)
        {
            var seller = record.Kind is NeighborhoodCollaborationKinds.Goods or NeighborhoodCollaborationKinds.Food
                ? (record.ProviderRoleCode == "requester" ? record.RequesterUserId : record.OwnerUserId) : null;
            try { await commerce.요구Async(actor, seller, request.CommerceProtection, "neighborhood-collaboration-decision", request.ClientRequestId.ToString("N"), cancellationToken); }
            catch (Ssalddel.Services.Commerce.거래보호Exception ex) { return Fail(ex.Code, ex.Message, ex.Status); }
        }
        var owner = record.OwnerUserId == actor;
        if (record.Kind == NeighborhoodCollaborationKinds.Storage
            && request.Action is NeighborhoodCollaborationActions.UpdateTerms or NeighborhoodCollaborationActions.Cancel)
        {
            if (storage is null) return Fail("StorageAuthorityUnavailable", "보관 예약 상태를 확인할 수 없습니다.", 409);
            var reservation = await storage.예약조회Async(record.StableId, cancellationToken);
            if (reservation is not null && (request.Action == NeighborhoodCollaborationActions.UpdateTerms
                || reservation.StatusCode is "reserved" or "in-custody"))
                return Fail("StorageReservationOpen", "보관 예약을 먼저 정리해 주세요. 예약 조건 변경은 새 협업으로 신청해야 합니다.", 409);
        }
        if (request.Action == NeighborhoodCollaborationActions.UpdateTerms && !생활협업Policy.조건유효(request.Terms, record.Kind, now))
            return Fail("InvalidCollaborationTerms", "변경할 조건을 확인해 주세요.", 400);
        if (request.Action == NeighborhoodCollaborationActions.UpdateTerms)
        {
            var nextSource = await ResolveSourceAsync(record.SourcePostId, request.Terms!.StorageSpaceId, record.Kind, cancellationToken);
            if (nextSource.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(nextSource.Errors);
            if (nextSource.Value.OwnerUserId != record.OwnerUserId) return Fail("SourceOwnerChanged", "기존 상대방과 같은 출처가 아닙니다.", 409);
        }
        if (request.Action is NeighborhoodCollaborationActions.UpdateTerms or NeighborhoodCollaborationActions.Cancel or NeighborhoodCollaborationActions.WithdrawHandoverConsent
            && (record.LinkedDeliveryRequestId is not null || record.DeliveryIntent?.StateCode is "pending" or "confirmed"))
        {
            if (deliveryChoices is null) return Fail("DeliveryAuthorityUnavailable", "연결된 배송의 현재 상태를 확인해 주세요.", 409);
            var cancelled = await deliveryChoices.협업대기취소Async(record, cancellationToken);
            if (cancelled.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(cancelled.Errors);
            if (record.DeliveryIntent is not null) record.DeliveryIntent.StateCode = "cancelled";
            record.LinkedDeliveryRequestId = null;
        }
        switch (request.Action)
        {
            case NeighborhoodCollaborationActions.UpdateTerms:
            {
                if (!생활협업Policy.조건유효(request.Terms, record.Kind, now)) return Fail("InvalidCollaborationTerms", "변경할 조건을 확인해 주세요.", 400);
                var source = await ResolveSourceAsync(record.SourcePostId, request.Terms!.StorageSpaceId, record.Kind, cancellationToken);
                if (source.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(source.Errors);
                if (source.Value.OwnerUserId != record.OwnerUserId) return Fail("SourceOwnerChanged", "기존 상대방과 같은 출처가 아닙니다.", 409);
                if (request.Terms.TransferMethod is not null)
                {
                    var sourcePost = await PublicSourceQuery().SingleAsync(x => x.Id == record.SourcePostId, cancellationToken);
                    record.ProviderRoleCode = sourcePost.RoleTag == NeighborhoodExchange.Offer ? "owner" : "requester";
                }
                record.TermsAuthorRoleCode = Role(record, actor);
                record.Terms = Clone(request.Terms); record.TermsRevision++; record.OwnerAgreed = false; record.RequesterAgreed = false;
                record.OwnerPrivacyNoticeVersion = null; record.RequesterPrivacyNoticeVersion = null;
                record.OwnerPublicHistoryConsented = false; record.RequesterPublicHistoryConsented = false;
                record.SourceUpdatedAtUtc = source.Value.PostUpdatedAtUtc; record.SpaceRevision = source.Value.SpaceRevision;
                record.SourceConditionsFingerprint = source.Value.ConditionsFingerprint;
                record.SourceTitle = source.Value.Title; record.PublicNeighborhoodRegionKey = source.Value.RegionKey;
                record.StatusCode = NeighborhoodCollaborationStates.Requested; record.ExpiresAtUtc = request.Terms.UntilUtc;
                foreach (var item in record.Participants.Where(x => x.StatusCode is "requested" or "accepted"))
                { item.OwnerAccepted = false; item.RequesterAccepted = false; item.PublicHistoryConsented = false; item.StatusCode = "requested"; }
                break;
            }
            case NeighborhoodCollaborationActions.WithdrawHandoverConsent:
                if (owner) record.OwnerPrivacyNoticeVersion = null; else record.RequesterPrivacyNoticeVersion = null;
                record.OwnerAgreed = false; record.RequesterAgreed = false; record.StatusCode = NeighborhoodCollaborationStates.Requested;
                break;
            case NeighborhoodCollaborationActions.HandoverInfoConsent:
                if (request.PrivacyNoticeVersion != NeighborhoodGoodsHandoverNotice.Version || request.Consented != true)
                    return Fail("HandoverDisclosureNoticeRequired", "인계 정보 제공 안내를 확인해 주세요.", 400);
                if (owner) record.OwnerPrivacyNoticeVersion = request.PrivacyNoticeVersion; else record.RequesterPrivacyNoticeVersion = request.PrivacyNoticeVersion;
                break;
            case NeighborhoodCollaborationActions.Agree:
            case NeighborhoodCollaborationActions.Start:
            {
                var source = request.Action == NeighborhoodCollaborationActions.Start && record.Kind == NeighborhoodCollaborationKinds.Storage
                    && storage is not null && await storage.예약조회Async(record.StableId, cancellationToken) is { } existingReservation
                    && existingReservation.TermsRevision == record.TermsRevision && existingReservation.StatusCode is "reserved" or "in-custody" or "returned"
                    ? Result.Ok() : await CheckSourceAsync(record, cancellationToken);
                if (source.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(source.Errors);
                if (request.Action == NeighborhoodCollaborationActions.Start && !CurrentAgreement(record))
                    return Fail("StorageDisclosureNoticeRequired", "양측의 현재 조건과 인계 정보 제공 동의를 다시 확인해 주세요.", 409);
                if (request.Action == NeighborhoodCollaborationActions.Agree)
                {
                    if (record.Terms.TransferMethod is not null && request.PrivacyNoticeVersion != NeighborhoodGoodsHandoverNotice.Version)
                        return Fail("HandoverDisclosureNoticeRequired", "현재 전달 조건과 정보 제공 안내를 확인해 주세요.", 400);
                    if (record.Kind == NeighborhoodCollaborationKinds.Storage && request.PrivacyNoticeVersion != NeighborhoodCollaborationAgreementNotice.Version)
                        return Fail("StorageDisclosureNoticeRequired", "상대방 인계 정보 제공 안내를 확인한 뒤 동의해 주세요.", 400);
                    if (owner) record.OwnerAgreed = true; else record.RequesterAgreed = true;
                    if (record.Kind == NeighborhoodCollaborationKinds.Storage || record.Terms.TransferMethod is not null)
                    { if (owner) record.OwnerPrivacyNoticeVersion = request.PrivacyNoticeVersion; else record.RequesterPrivacyNoticeVersion = request.PrivacyNoticeVersion; }
                    if (생활협업Policy.전체합의(record.OwnerAgreed, record.RequesterAgreed)) record.StatusCode = NeighborhoodCollaborationStates.Agreed;
                }
                else record.StatusCode = NeighborhoodCollaborationStates.InProgress;
                break;
            }
            case NeighborhoodCollaborationActions.ProposeCompletion:
                if (record.Terms.TransferMethod is not null && record.ProviderRoleCode != Role(record, actor))
                    return Fail("ProviderHandoverRequired", "물건 제공자가 인계를 먼저 확인해 주세요.", 409);
                record.CompletionProposerUserId = actor; record.StatusCode = NeighborhoodCollaborationStates.CompletionProposed; break;
            case NeighborhoodCollaborationActions.ConfirmCompletion:
            {
                var authority = await CompletionAuthorityAsync(record, cancellationToken);
                if (authority.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(authority.Errors);
                record.StatusCode = NeighborhoodCollaborationStates.Completed; record.CompletedAtUtc = now; break;
            }
            case NeighborhoodCollaborationActions.Cancel: record.StatusCode = NeighborhoodCollaborationStates.Cancelled; break;
            case NeighborhoodCollaborationActions.Reject: record.StatusCode = NeighborhoodCollaborationStates.Rejected; break;
            case NeighborhoodCollaborationActions.PublicHistoryConsent:
                if (request.Consented is null) return Fail("ConsentDecisionRequired", "공개 기록 동의 여부를 선택해 주세요.", 400);
                if (owner) record.OwnerPublicHistoryConsented = request.Consented.Value;
                else if (record.RequesterUserId == actor) record.RequesterPublicHistoryConsented = request.Consented.Value;
                else participant!.PublicHistoryConsented = request.Consented.Value;
                break;
            case NeighborhoodCollaborationActions.RequestParticipation:
                if (!await PublicSourceQuery().AnyAsync(x => x.Id == record.SourcePostId && x.AuthorUserId == record.OwnerUserId, cancellationToken))
                    return Fail("SourcePostUnavailable", "공개 글의 현재 상대방을 확인할 수 없습니다.", 404);
                if (record.Participants.Count >= 20) return Fail("ParticipationLimitReached", "참여 신청 한도에 도달했습니다.", 409);
                record.Participants.Add(new 생활협업참여Record { UserId = actor }); break;
            case NeighborhoodCollaborationActions.AcceptParticipation:
            case NeighborhoodCollaborationActions.RejectParticipation:
            {
                var applicant = record.Participants.SingleOrDefault(x => x.UserId == request.ApplicantUserId && x.StatusCode == "requested");
                if (applicant is null) return Fail("ParticipantNotFound", "현재 참여 신청을 찾을 수 없습니다.", 404);
                if (request.Action == NeighborhoodCollaborationActions.RejectParticipation) applicant.StatusCode = "rejected";
                else
                {
                    if (owner) applicant.OwnerAccepted = true; else applicant.RequesterAccepted = true;
                    if (applicant.OwnerAccepted && applicant.RequesterAccepted) applicant.StatusCode = "accepted";
                }
                break;
            }
            case NeighborhoodCollaborationActions.WithdrawParticipation: participant!.StatusCode = "withdrawn"; participant.PublicHistoryConsented = false; break;
            case NeighborhoodCollaborationActions.LinkDelivery:
            {
                var source = await CheckSourceAsync(record, cancellationToken);
                if (source.IsFailed) return Result.Fail<NeighborhoodCollaborationResponse>(source.Errors);
                if (string.IsNullOrWhiteSpace(request.LinkedDeliveryRequestId) || deliveries is null) return Fail("DeliveryLinkUnavailable", "배송 연결을 확인할 수 없습니다.", 409);
                var delivery = await deliveries.조회Async(request.LinkedDeliveryRequestId, cancellationToken);
                if (delivery is null || !delivery.IsNeighborhoodDelivery || delivery.OwnerUserId != actor || delivery.Cancelled
                    || record.SourcePostId.HasValue && delivery.SourcePostId != record.SourcePostId)
                    return Fail("DeliveryLinkNotAllowed", "본인이 만든 현재 생활 배송 의뢰만 연결할 수 있습니다.", 403);
                if (record.LinkedDeliveryRequestId is not null && record.LinkedDeliveryRequestId != delivery.RequestId)
                    return Fail("DeliveryAlreadyLinked", "이미 연결한 배송 의뢰를 바꿀 수 없습니다.", 409);
                record.LinkedDeliveryRequestId = delivery.RequestId; break;
            }
        }
        record.Revision++; record.UpdatedAtUtc = now;
        record.Receipts.Add(new() { ClientRequestId = request.ClientRequestId, ActorUserId = actor, Fingerprint = fingerprint });
        AddHistory(record, request.Action, Role(record, actor), now);
        if (!await store.교체Async(record, request.ExpectedRevision, cancellationToken))
        {
            var concurrent = await store.조회Async(stableId, cancellationToken);
            var replay = concurrent?.Receipts.SingleOrDefault(x => x.ClientRequestId == request.ClientRequestId);
            if (concurrent is not null && replay?.ActorUserId == actor && replay.Fingerprint == fingerprint) return Result.Ok(ToResponse(concurrent, actor, true));
            return Fail("RevisionConflict", "다른 결정이 먼저 저장되었습니다. 최신 내용을 확인해 주세요.", 409);
        }
        return Result.Ok(ToResponse(record, actor));
    }

    public async Task<NeighborhoodCollaborationResponse?> 상세Async(string stableId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return null;
        var record = await LoadCurrentAsync(stableId, cancellationToken);
        return record is not null && CanRead(record, currentUser.UserId) ? ToResponse(record, currentUser.UserId) : null;
    }

    public async Task<NeighborhoodCollaborationResponse?> 요청결과Async(Guid clientRequestId, string? stableId = null, CancellationToken cancellationToken = default)
    {
        if (clientRequestId == Guid.Empty || string.IsNullOrWhiteSpace(currentUser.UserId)) return null;
        var actor = currentUser.UserId;
        var record = await LoadCurrentAsync(stableId ?? CreateStableId(actor, clientRequestId), cancellationToken);
        if (record is null || !CanRead(record, actor)) return null;
        return record.CreateRequestId == clientRequestId && record.RequesterUserId == actor
            || record.Receipts.Any(x => x.ClientRequestId == clientRequestId && x.ActorUserId == actor) ? ToResponse(record, actor, true) : null;
    }

    public async Task<NeighborhoodCollaborationListResponse> 내목록Async(string scope = "all", int page = 1, CancellationToken cancellationToken = default)
    {
        page = Math.Clamp(page, 1, 10000);
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return new() { Page = page };
        if (scope is not ("requested" or "undertaken" or "all")) scope = "all";
        var rows = await store.목록Async(new(currentUser.UserId, scope, Skip: (page - 1) * 20, Take: 21), cancellationToken);
        var items = new List<NeighborhoodCollaborationResponse>();
        foreach (var row in rows.Take(20))
        {
            var current = await LoadCurrentAsync(row.StableId, cancellationToken);
            if (current is not null && CanRead(current, currentUser.UserId)) items.Add(ToResponse(current, currentUser.UserId));
        }
        return new() { Page = page, HasMore = rows.Count > 20, Items = items };
    }

    public async Task<IReadOnlyList<NeighborhoodCollaborationOpportunityResponse>> 참여기회Async(long sourcePostId, CancellationToken cancellationToken = default)
    {
        var source = await PublicSourceQuery().SingleOrDefaultAsync(x => x.Id == sourcePostId, cancellationToken);
        if (source is null || string.IsNullOrWhiteSpace(source.AuthorUserId)) return [];
        var rows = await store.목록Async(new(SourcePostId: sourcePostId, Take: 100), cancellationToken);
        return rows.Where(x => x.OwnerUserId == source.AuthorUserId
            && (x.StatusCode is NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress) && x.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime)
            .Select(x => new NeighborhoodCollaborationOpportunityResponse { StableId = x.StableId, Revision = x.Revision, SourcePostId = sourcePostId,
                SourceTitle = source.Title, Kind = x.Kind, PublicNeighborhoodRegionKey = source.PublicNeighborhoodRegionKey, StatusCode = x.StatusCode }).ToArray();
    }

    public async Task<IReadOnlyList<NeighborhoodCollaborationPublicHistoryResponse>> 공개이력Async(long sourcePostId, CancellationToken cancellationToken = default)
    {
        var source = await PublicSourceQuery().SingleOrDefaultAsync(x => x.Id == sourcePostId, cancellationToken);
        if (source is null || string.IsNullOrWhiteSpace(source.AuthorUserId)) return [];
        var rows = await store.목록Async(new(SourcePostId: sourcePostId, Take: 100), cancellationToken);
        return rows.Where(x => x.OwnerUserId == source.AuthorUserId && PublicVisible(x)).Select(x => new NeighborhoodCollaborationPublicHistoryResponse
        { Kind = x.Kind, PublicNeighborhoodRegionKey = x.PublicNeighborhoodRegionKey, CompletedOnUtc = x.CompletedAtUtc!.Value.Date }).ToArray();
    }

    async Task<생활협업연결Context?> I생활협업연결Query.조회Async(string stableId, CancellationToken cancellationToken)
    {
        var item = await LoadCurrentAsync(stableId, cancellationToken);
        return item is null ? null : new(item.StableId, item.Revision, item.TermsRevision, item.OwnerUserId, item.RequesterUserId,
            item.Kind, item.StatusCode, CurrentAgreement(item) && !NeighborhoodCollaborationStates.IsTerminal(item.StatusCode),
            Clone(item.Terms), item.LinkedDeliveryRequestId, item.SourcePostId, item.SpaceRevision);
    }

    private async Task<생활협업Record?> LoadCurrentAsync(string stableId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stableId) || stableId.Length > 100) return null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var record = await store.조회Async(stableId, cancellationToken);
            if (record is null || !생활협업Policy.시작전(record.StatusCode) || record.ExpiresAtUtc > clock.GetUtcNow().UtcDateTime) return record;
            if (ReservationLocked(record)) return record;
            // 예약/물품을 둔 합의를 자동 폐쇄해 인계 권한을 없애지 않습니다. 기존 예약을 먼저 취소·반환합니다.
            if (record.Kind == NeighborhoodCollaborationKinds.Storage)
            {
                if (storage is null) throw new InvalidOperationException("Storage reservation authority is required for expiry.");
                var reservation = await storage.예약조회Async(record.StableId, cancellationToken);
                if (reservation?.TermsRevision == record.TermsRevision && reservation.StatusCode is "reserved" or "in-custody" or "returned") return record;
            }
            var revision = record.Revision; var now = clock.GetUtcNow().UtcDateTime;
            record.StatusCode = NeighborhoodCollaborationStates.Expired; record.Revision++; record.UpdatedAtUtc = now;
            AddHistory(record, "expire", "system", now);
            if (await store.교체Async(record, revision, cancellationToken)) return record;
        }
        throw new InvalidOperationException("Collaboration expiry could not be stored due to concurrent changes.");
    }

    private async Task<Result> CompletionAuthorityAsync(생활협업Record record, CancellationToken cancellationToken)
    {
        if (record.Kind == NeighborhoodCollaborationKinds.Storage)
        {
            if (storage is null || !await storage.반환확인Async(record.StableId, record.TermsRevision, cancellationToken))
                return Result.Fail(Error("StorageReturnRequired", "보관 물품 반환을 양측이 확인한 뒤 완료할 수 있습니다.", 409));
        }
        if (record.Kind == NeighborhoodCollaborationKinds.Transport || record.Terms.TransferMethod == NeighborhoodTransferMethods.DriverDelivery || record.LinkedDeliveryRequestId is not null)
        {
            var delivery = deliveries is null || record.LinkedDeliveryRequestId is null ? null : await deliveries.조회Async(record.LinkedDeliveryRequestId, cancellationToken);
            if (delivery is null || !delivery.IsNeighborhoodDelivery || !delivery.Completed || delivery.Cancelled
                || !IsParty(record, delivery.OwnerUserId) || record.SourcePostId.HasValue && delivery.SourcePostId != record.SourcePostId)
                return Result.Fail(Error("DeliveryCompletionRequired", "연결된 배송 원장의 완료가 확인되어야 합니다.", 409));
        }
        return Result.Ok();
    }

    private async Task<Result<SourceSnapshot>> ResolveSourceAsync(long? postId, string? spaceId, string kind, CancellationToken cancellationToken)
    {
        PlatformCommunityPost? post = null;
        if (postId is > 0)
        {
            post = await PublicSourceQuery().SingleOrDefaultAsync(x => x.Id == postId, cancellationToken);
            if (post is null) return Result.Fail<SourceSnapshot>(Error("SourcePostUnavailable", "공개 생활 교류 글을 찾을 수 없습니다.", 404));
            if (string.IsNullOrWhiteSpace(post.AuthorUserId)) return Result.Fail<SourceSnapshot>(Error("AuthenticatedAuthorRequired", "익명 글은 작성자 확인이 되지 않아 협업 신청을 받을 수 없습니다. 로그인 작성 글을 이용해 주세요.", 400));
        }
        if (kind == NeighborhoodCollaborationKinds.Storage)
        {
            var space = spaces is null || string.IsNullOrWhiteSpace(spaceId) ? null : await spaces.조회Async(spaceId, cancellationToken);
            if (space is null || !space.Available) return Result.Fail<SourceSnapshot>(Error("StorageSpaceUnavailable", "현재 신청할 수 있는 보관 공간을 찾을 수 없습니다.", 404));
            if (post is not null && post.AuthorUserId != space.OwnerUserId) return Result.Fail<SourceSnapshot>(Error("StorageOwnerMismatch", "글 작성자와 보관 공간 소유자가 다릅니다.", 409));
            return Result.Ok(new SourceSnapshot(space.OwnerUserId, post?.Title ?? space.Title, post?.PublicNeighborhoodRegionKey ?? space.PublicNeighborhoodRegionKey,
                post?.UpdatedAtUtc, space.StableId, space.Revision, post is null ? null : SourceConditionsFingerprint(post)));
        }
        return post is null ? Result.Fail<SourceSnapshot>(Error("SourcePostRequired", "생활 교류 글을 선택해 주세요.", 400))
            : Result.Ok(new SourceSnapshot(post.AuthorUserId!, post.Title, post.PublicNeighborhoodRegionKey, post.UpdatedAtUtc, null, null, SourceConditionsFingerprint(post)));
    }

    private async Task<Result> CheckSourceAsync(생활협업Record record, CancellationToken cancellationToken)
    {
        var source = await ResolveSourceAsync(record.SourcePostId, record.Terms.StorageSpaceId, record.Kind, cancellationToken);
        if (source.IsFailed) return Result.Fail(source.Errors);
        return source.Value.OwnerUserId == record.OwnerUserId && source.Value.ConditionsFingerprint == record.SourceConditionsFingerprint
            && source.Value.SpaceRevision == record.SpaceRevision ? Result.Ok()
            : Result.Fail(Error("SourceChanged", "출처 조건이 바뀌었습니다. 조건을 갱신하고 서로 다시 확인해 주세요.", 409));
    }

    private IQueryable<PlatformCommunityPost> PublicSourceQuery() => db.PlatformCommunityPosts.AsNoTracking()
        .Where(x => !x.IsDeleted && x.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Published
            && !x.IsReportBoardPost && x.Category == PlatformCommunityPostCategories.General && x.WorkflowTag == NeighborhoodExchange.WorkflowTag
            && (x.RoleTag == NeighborhoodExchange.Offer || x.RoleTag == NeighborhoodExchange.Need));

    private static IReadOnlyList<string> AllowedActions(생활협업Record record, string actor)
    {
        var result = new List<string>(); var party = IsParty(record, actor); var participant = record.Participants.SingleOrDefault(x => x.UserId == actor);
        if (record.StatusCode == NeighborhoodCollaborationStates.Completed)
        {
            if (party || participant?.StatusCode == "accepted") result.Add(NeighborhoodCollaborationActions.PublicHistoryConsent);
            return result;
        }
        if (NeighborhoodCollaborationStates.IsTerminal(record.StatusCode)) return result;
        if (!party)
        {
            if (participant is null && record.SourcePostId.HasValue && (record.StatusCode is NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress))
                result.Add(NeighborhoodCollaborationActions.RequestParticipation);
            if (participant?.StatusCode is "requested" or "accepted") result.Add(NeighborhoodCollaborationActions.WithdrawParticipation);
            return result;
        }
        result.Add(NeighborhoodCollaborationActions.Cancel);
        if (생활협업Policy.시작전(record.StatusCode) && (record.LinkedDeliveryRequestId is null || record.Terms.TransferMethod is not null)
            || record.StatusCode == NeighborhoodCollaborationStates.InProgress && record.Terms.TransferMethod is not null) result.Add(NeighborhoodCollaborationActions.UpdateTerms);
        if (record.Terms.TransferMethod is not null && 생활협업Policy.시작전(record.StatusCode)
            && (record.OwnerUserId == actor ? record.OwnerPrivacyNoticeVersion : record.RequesterPrivacyNoticeVersion) == NeighborhoodGoodsHandoverNotice.Version)
            result.Add(NeighborhoodCollaborationActions.WithdrawHandoverConsent);
        if (record.StatusCode == NeighborhoodCollaborationStates.Requested)
        {
            if (record.Terms.TransferMethod is not null && (record.OwnerUserId == actor ? record.OwnerPrivacyNoticeVersion : record.RequesterPrivacyNoticeVersion) != NeighborhoodGoodsHandoverNotice.Version)
                result.Add(NeighborhoodCollaborationActions.HandoverInfoConsent);
            if ((record.Terms.TransferMethod is null || HandoverVisible(record))
                && !(record.OwnerUserId == actor ? record.OwnerAgreed : record.RequesterAgreed)) result.Add(NeighborhoodCollaborationActions.Agree);
            if (record.OwnerUserId == actor) result.Add(NeighborhoodCollaborationActions.Reject);
        }
        if (record.StatusCode == NeighborhoodCollaborationStates.Agreed) result.Add(NeighborhoodCollaborationActions.Start);
        if (record.StatusCode == NeighborhoodCollaborationStates.InProgress && (record.Terms.TransferMethod is null || record.ProviderRoleCode == Role(record, actor))) result.Add(NeighborhoodCollaborationActions.ProposeCompletion);
        if (record.StatusCode == NeighborhoodCollaborationStates.CompletionProposed && record.CompletionProposerUserId != actor)
            result.Add(NeighborhoodCollaborationActions.ConfirmCompletion);
        if (record.StatusCode is NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress)
        {
            if (record.Kind != NeighborhoodCollaborationKinds.Food && record.Terms.TransferMethod is null) result.Add(NeighborhoodCollaborationActions.LinkDelivery);
            if (record.Participants.Any(x => x.StatusCode == "requested"))
            { result.Add(NeighborhoodCollaborationActions.AcceptParticipation); result.Add(NeighborhoodCollaborationActions.RejectParticipation); }
        }
        if (record.StorageReservationIntents.Any(x => x.StateCode is "pending" or "confirmed"))
        { result.Remove(NeighborhoodCollaborationActions.Cancel); result.Remove(NeighborhoodCollaborationActions.UpdateTerms); }
        return result;
    }

    private static NeighborhoodCollaborationResponse ToResponse(생활협업Record record, string actor, bool replay = false)
    {
        var party = IsParty(record, actor);
        var ownPending = party ? record.StorageReservationIntents.LastOrDefault(x => x.StateCode == "pending" && x.ActorUserId == actor) : null;
        return new()
        {
            StableId = record.StableId, Revision = record.Revision, TermsRevision = record.TermsRevision, SourcePostId = record.SourcePostId,
            SourceTitle = record.SourceTitle, PublicNeighborhoodRegionKey = record.PublicNeighborhoodRegionKey, Kind = record.Kind,
            StatusCode = record.StatusCode, MyRoleCode = Role(record, actor), IsSourceOwner = record.OwnerUserId == actor, IsRequester = record.RequesterUserId == actor,
            ProviderRoleCode = record.ProviderRoleCode,
            CanRequestDelivery = party && record.Terms.TransferMethod == NeighborhoodTransferMethods.DriverDelivery && CurrentAgreement(record)
                && record.StatusCode is NeighborhoodCollaborationStates.Agreed or NeighborhoodCollaborationStates.InProgress
                && record.LinkedDeliveryRequestId is null && record.DeliveryIntent?.StateCode is not ("pending" or "confirmed"),
            DeliveryRegistrationPending = party && record.DeliveryIntent?.StateCode == "pending",
            MyPendingDeliveryRequest = party && record.DeliveryIntent is { StateCode: "pending" } pendingDelivery && pendingDelivery.ActorUserId == actor ? pendingDelivery.Request : null,
            Terms = party ? ResponseTerms(record, actor) : null, OwnerAgreed = record.OwnerAgreed, RequesterAgreed = record.RequesterAgreed,
            OwnerPublicHistoryConsented = party && record.OwnerPublicHistoryConsented, RequesterPublicHistoryConsented = party && record.RequesterPublicHistoryConsented,
            MyPublicHistoryConsented = record.OwnerUserId == actor ? record.OwnerPublicHistoryConsented
                : record.RequesterUserId == actor ? record.RequesterPublicHistoryConsented
                : record.Participants.SingleOrDefault(x => x.UserId == actor)?.PublicHistoryConsented == true,
            PublicHistoryVisible = PublicVisible(record), CompletionProposedByMe = record.CompletionProposerUserId == actor,
            LinkedDeliveryRequestId = party ? record.LinkedDeliveryRequestId : null, CreatedAtUtc = record.CreatedAtUtc, UpdatedAtUtc = record.UpdatedAtUtc,
            StorageReservationPending = party && record.StorageReservationIntents.Any(x => x.StateCode == "pending"),
            MyPendingStorageRequestId = ownPending?.RequestId, MyPendingStorageSpaceId = ownPending?.SpaceId,
            ExpiresAtUtc = record.ExpiresAtUtc, AllowedActions = AllowedActions(record, actor), IdempotentReplay = replay,
            Participants = record.Participants.Where(x => party || x.UserId == actor).Select(x => new NeighborhoodCollaborationParticipationResponse
            { ApplicantUserId = x.UserId, StatusCode = x.StatusCode, OwnerAccepted = x.OwnerAccepted, RequesterAccepted = x.RequesterAccepted, IsMe = x.UserId == actor }).ToArray(),
            History = party ? record.History.ToArray() : []
        };
    }
    private static bool PublicVisible(생활협업Record item) => item.StatusCode == NeighborhoodCollaborationStates.Completed && item.CompletedAtUtc.HasValue
        && item.OwnerPublicHistoryConsented && item.RequesterPublicHistoryConsented
        && item.Participants.Where(x => x.OwnerAccepted && x.RequesterAccepted).All(x => x.PublicHistoryConsented);
    private static NeighborhoodCollaborationTerms ResponseTerms(생활협업Record record, string actor)
    {
        var terms = Clone(record.Terms);
        if (terms.TransferMethod is not null && !HandoverVisible(record) && record.TermsAuthorRoleCode != Role(record, actor)) terms.HandoverPlace = null;
        return terms;
    }
    private static bool HandoverVisible(생활협업Record item) => item.OwnerPrivacyNoticeVersion == NeighborhoodGoodsHandoverNotice.Version
        && item.RequesterPrivacyNoticeVersion == NeighborhoodGoodsHandoverNotice.Version;
    internal static bool CurrentAgreement(생활협업Record item) => item.OwnerAgreed && item.RequesterAgreed
        && (item.Terms.TransferMethod is null || item.OwnerPrivacyNoticeVersion == NeighborhoodGoodsHandoverNotice.Version
            && item.RequesterPrivacyNoticeVersion == NeighborhoodGoodsHandoverNotice.Version)
        && (item.Kind != NeighborhoodCollaborationKinds.Storage
            || item.OwnerPrivacyNoticeVersion == NeighborhoodCollaborationAgreementNotice.Version
            && item.RequesterPrivacyNoticeVersion == NeighborhoodCollaborationAgreementNotice.Version);
    private static bool ReservationLocked(생활협업Record item) => item.DeliveryIntent?.StateCode == "pending" || item.StorageReservationIntents.Any(x => x.StateCode is "pending" or "confirmed");
    private static bool IsParty(생활협업Record item, string actor) => item.OwnerUserId == actor || item.RequesterUserId == actor;
    private static bool CanRead(생활협업Record item, string actor) => IsParty(item, actor) || item.Participants.Any(x => x.UserId == actor);
    private static string Role(생활협업Record item, string actor) => item.OwnerUserId == actor ? "owner" : item.RequesterUserId == actor ? "requester" : "participant";
    private static NeighborhoodCollaborationTerms Clone(NeighborhoodCollaborationTerms terms) => JsonSerializer.Deserialize<NeighborhoodCollaborationTerms>(JsonSerializer.Serialize(terms, Json), Json)!;
    public static string CreateStableId(string actor, Guid requestId) => "collaboration-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actor + ":" + requestId.ToString("N")))).ToLowerInvariant();
    private static string Fingerprint<T>(T request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, Json))).ToLowerInvariant();
    private static void AddHistory(생활협업Record item, string action, string role, DateTime now) => item.History.Add(new()
    { Revision = item.Revision, Action = action, StatusCode = item.StatusCode, ActorRoleCode = role, RecordedAtUtc = now });
    private static Result<NeighborhoodCollaborationResponse> ReplayCreate(생활협업Record item, string actor, string fingerprint)
        => item.RequesterUserId != actor || item.CreateFingerprint != fingerprint ? Fail("IdempotencyConflict", "같은 신청 번호로 다른 내용을 등록할 수 없습니다.", 409)
            : Result.Ok(ToResponse(item, actor, true));
    private static Result<NeighborhoodCollaborationResponse> Fail(string code, string message, int status) => Result.Fail<NeighborhoodCollaborationResponse>(Error(code, message, status));
    private static Error Error(string code, string message, int status) => new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", status);
    private static string SourceConditionsFingerprint(PlatformCommunityPost post) => Fingerprint(new
    {
        post.Id, post.AuthorUserId, post.AppKey, post.Category, post.WorkflowTag, post.RoleTag, post.Title, post.Body,
        post.PublicNeighborhoodRegionKey, post.SharedLinkUrl, post.SalesOfferJson
    });
    private sealed record SourceSnapshot(string OwnerUserId, string Title, string? RegionKey, DateTime? PostUpdatedAtUtc,
        string? SpaceId, long? SpaceRevision, string? ConditionsFingerprint);
}
