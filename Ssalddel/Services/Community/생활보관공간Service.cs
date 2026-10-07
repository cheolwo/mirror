using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Services.Community;

/// <summary>현재 양측 합의와 비공개 인계정보 제공 동의는 협업 원장에서만 확인합니다.</summary>
public interface I생활보관협업ContextSource
{
    Task<NeighborhoodStorageCollaborationContext?> 조회Async(string collaborationId, CancellationToken cancellationToken = default);
}

public sealed class NeighborhoodStorageCollaborationContext
{
    public string CollaborationId { get; init; } = string.Empty;
    public long TermsRevision { get; init; }
    public long ExpectedStorageOfferRevision { get; init; }
    public string OwnerUserId { get; init; } = string.Empty;
    public string RequesterUserId { get; init; } = string.Empty;
    public bool AcceptedCurrentTerms { get; init; }
    public bool PrivateDisclosureConsented { get; init; }
    public bool IsClosed { get; init; }
    public string StorageSpaceId { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTimeOffset FromUtc { get; init; }
    public DateTimeOffset UntilUtc { get; init; }
}

public interface I생활보관공간Service
{
    Task<NeighborhoodStorageListResponse> 공개목록Async(string? regionKey, int page = 1, CancellationToken cancellationToken = default);
    Task<NeighborhoodStorageMapResponse> 공개지도Async(CancellationToken cancellationToken = default);
    Task<NeighborhoodStoragePublicDto?> 공개상세Async(string spaceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> 내목록Async(int page = 1, CancellationToken cancellationToken = default);
    Task<NeighborhoodStorageSpaceDto?> 비공개상세Async(string spaceId, string? collaborationId = null, CancellationToken cancellationToken = default);
    Task<NeighborhoodStorageSpaceDto?> 요청결과Async(Guid requestId, string? spaceId = null, string? collaborationId = null, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodStorageSpaceDto>> 등록Async(NeighborhoodStorageSpaceRequest request, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodStorageSpaceDto>> 수정Async(string spaceId, NeighborhoodStorageSpaceRequest request, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodStorageSpaceDto>> 상태변경Async(string spaceId, string status, NeighborhoodStorageMutationRequest request, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodStorageSpaceDto>> 예약Async(string spaceId, NeighborhoodStorageReservationRequest request, CancellationToken cancellationToken = default);
    Task<Result<NeighborhoodStorageSpaceDto>> 예약변경Async(string spaceId, string collaborationId, NeighborhoodStorageReservationActionRequest request, CancellationToken cancellationToken = default);
}

[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Ledger, SsalddelModuleKind.Application,
    "일반 회원의 작은 보관 능력 공개와 합의된 시간·수량 예약·인수·반환 기록",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.ClosedLoop,
    Boundary = "전문 창고 등록·재고·입고·결제·자동 배차 권위를 만들거나 대신하지 않습니다. 공개 위치는 동네 대표점입니다.")]
public sealed class 생활보관공간Service(I생활보관공간Store store, ICurrentUserAccessor currentUser,
    생활보관인계정보Protection protection, I생활교류공개지역Source regions,
    I생활보관협업ContextSource? collaboration = null, TimeProvider? clock = null,
    I생활협업보관예약Guard? reservationGuard = null) : I생활보관공간Service
{
    private const int PageSize = 20;
    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
    private string? Actor => string.IsNullOrWhiteSpace(currentUser.UserId) ? null : currentUser.UserId.Trim();

    public async Task<NeighborhoodStorageListResponse> 공개목록Async(string? regionKey, int page = 1,
        CancellationToken cancellationToken = default)
    {
        var directory = await regions.목록Async(cancellationToken);
        regionKey = string.IsNullOrWhiteSpace(regionKey) ? null : regionKey.Trim();
        if (regionKey is not null && !directory.Any(x => x.RegionKey == regionKey)) return new() { Page = NormalizePage(page) };
        var result = await store.목록Async(null, regionKey, true, Now, NormalizePage(page), PageSize, cancellationToken);
        return new()
        {
            Page = NormalizePage(page), TotalCount = result.Total,
            Items = result.Items.Where(x => directory.Any(r => r.RegionKey == x.PublicNeighborhoodRegionKey))
                .Select(x => Public(x, directory)).ToArray()
        };
    }

    public async Task<NeighborhoodStorageMapResponse> 공개지도Async(CancellationToken cancellationToken = default)
    {
        var directory = await regions.목록Async(cancellationToken);
        var counts = await store.공개동네집계Async(Now, cancellationToken);
        return new() { Items = directory.Where(x => counts.GetValueOrDefault(x.RegionKey) > 0)
            .Select(x => new NeighborhoodStorageMapMarkerDto { Region = x, SpaceCount = counts[x.RegionKey] }).ToArray() };
    }

    public async Task<NeighborhoodStoragePublicDto?> 공개상세Async(string spaceId, CancellationToken cancellationToken = default)
    {
        var document = await store.조회Async(spaceId, cancellationToken);
        if (document is null || document.Status != NeighborhoodStorageStatus.Published || document.AvailableUntilUtc <= Now) return null;
        var directory = await regions.목록Async(cancellationToken);
        return directory.Any(x => x.RegionKey == document.PublicNeighborhoodRegionKey) ? Public(document, directory) : null;
    }

    public async Task<IReadOnlyList<NeighborhoodStorageSpaceDto>> 내목록Async(int page = 1, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return [];
        var result = await store.목록Async(actor, null, false, Now, NormalizePage(page), PageSize, cancellationToken);
        var directory = await regions.목록Async(cancellationToken);
        return result.Items.Select(x => Private(x, actor, directory, null)).ToArray();
    }

    public async Task<NeighborhoodStorageSpaceDto?> 비공개상세Async(string spaceId, string? collaborationId = null,
        CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return null;
        var document = await store.조회Async(spaceId, cancellationToken);
        if (document is null) return null;
        if (document.OwnerUserId != actor)
        {
            if (string.IsNullOrWhiteSpace(collaborationId) || collaboration is null) return null;
            var context = await collaboration.조회Async(collaborationId, cancellationToken);
            if (context?.CollaborationId != collaborationId || !PrivateAllowed(document, context, actor)) return null;
        }
        return Private(document, actor, await regions.목록Async(cancellationToken), collaborationId);
    }

    public async Task<NeighborhoodStorageSpaceDto?> 요청결과Async(Guid requestId, string? spaceId = null, string? collaborationId = null,
        CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor || requestId == Guid.Empty) return null;
        spaceId = string.IsNullOrWhiteSpace(spaceId) ? StableId(actor, requestId) : spaceId.Trim();
        var document = await store.조회Async(spaceId, cancellationToken);
        if (document is null) return null;
        var operation = document.Operations.SingleOrDefault(x => x.ActorId == actor && x.RequestId == requestId.ToString("N"));
        if (operation is null && reservationGuard is not null && !string.IsNullOrWhiteSpace(collaborationId))
        {
            var intent = await reservationGuard.조회Async(collaborationId, requestId, actor, cancellationToken);
            if (intent is not null && intent.SpaceId == document.SpaceId && intent.StateCode == "pending")
            {
                // 아직 저장됐는지 모르는 요청은 공간 판본 fence를 남겨 지연 쓰기를 무효화한 뒤 예약권을 해제합니다.
                await AbortIntent(document.SpaceId, collaborationId, intent, "ReservationAborted", cancellationToken);
                document = await store.조회Async(spaceId, cancellationToken);
                operation = document?.Operations.SingleOrDefault(x => x.ActorId == actor && x.RequestId == requestId.ToString("N"));
            }
        }
        if (document is null || operation is null) return null;
        await ReconcileIntent(document, operation.CollaborationId, cancellationToken, operation);
        // 예약을 요청한 타인에게 소유자의 전체 원장 또는 다른 참여자의 인계 정보를 돌려주지 않습니다.
        if (document.OwnerUserId != actor)
        {
            foreach (var reservation in document.Reservations.Where(x => x.RequesterUserId == actor))
            {
                var context = collaboration is null ? null : await collaboration.조회Async(reservation.CollaborationId, cancellationToken);
                if (context is not null && context.OwnerUserId == document.OwnerUserId && context.RequesterUserId == actor
                    && context.StorageSpaceId == document.SpaceId && context.TermsRevision == reservation.TermsRevision)
                    return await MutationResponse(document, actor, reservation.CollaborationId, true, cancellationToken, operation);
            }
            if (operation.CollaborationId is { Length: > 0 } recordedCollaboration)
            {
                var context = collaboration is null ? null : await collaboration.조회Async(recordedCollaboration, cancellationToken);
                if (context is not null && context.OwnerUserId == document.OwnerUserId && context.RequesterUserId == actor
                    && context.StorageSpaceId == document.SpaceId)
                    return await MutationResponse(document, actor, recordedCollaboration, true, cancellationToken, operation);
            }
            return null;
        }
        return Private(document, actor, await regions.목록Async(cancellationToken), null, true, operation);
    }

    public async Task<Result<NeighborhoodStorageSpaceDto>> 등록Async(NeighborhoodStorageSpaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return Fail("AuthenticationRequired", "로그인 후 보관 공간을 등록해 주세요.");
        if (request.RequestId == Guid.Empty || request.ExpectedRevision != 0) return Fail("InvalidRequest", "등록 요청 번호와 최초 판본을 확인해 주세요.");
        var spaceId = StableId(actor, request.RequestId);
        var fingerprint = Fingerprint("create", request);
        var existing = await store.조회Async(spaceId, cancellationToken);
        if (existing is not null) return await Replay(existing, actor, request.RequestId, fingerprint, null, cancellationToken)
            ?? Fail("IdempotencyConflict", "같은 요청 번호로 보관 정보를 바꿀 수 없습니다.");
        var validation = await Validate(request, cancellationToken);
        if (validation is not null) return Fail("InvalidRequest", validation);
        var document = new 생활보관공간문서
        {
            SpaceId = spaceId, OwnerUserId = actor, Revision = 1, OfferRevision = 1,
            Status = request.PublishOnCreate ? NeighborhoodStorageStatus.Published : NeighborhoodStorageStatus.Draft,
            UpdatedAtUtc = Now
        };
        SetContent(document, request);
        Record(document, actor, request.RequestId, fingerprint);
        return await Persist(document, 0, actor, request.RequestId, fingerprint, null, cancellationToken);
    }

    public async Task<Result<NeighborhoodStorageSpaceDto>> 수정Async(string spaceId, NeighborhoodStorageSpaceRequest request,
        CancellationToken cancellationToken = default)
    {
        var mutation = await LoadOwner(spaceId, request.RequestId, request.ExpectedRevision, "update", request, cancellationToken);
        if (mutation.Result is not null) return mutation.Result;
        var document = mutation.Document!;
        var validation = await Validate(request, cancellationToken);
        if (validation is not null) return Fail("InvalidRequest", validation);
        var active = document.Reservations.Where(x => x.Status is not NeighborhoodStorageReservationStatus.Returned and not NeighborhoodStorageReservationStatus.Cancelled).ToArray();
        if (active.Length > 0 && (document.GoodsKind != request.GoodsKind.Trim() || document.CapacityUnit != request.CapacityUnit.Trim()
            || document.PublicNeighborhoodRegionKey != request.PublicNeighborhoodRegionKey.Trim()
            || document.ProtectedHandover.Length > 0 && protection.복원(document.ProtectedHandover)
                != new 생활보관비공개인계정보(request.PrivateAddress.Trim(), request.PrivateContact.Trim(), request.PrivateHandoverInstructions.Trim())))
            return Fail("ReservationConflict", "진행 중인 보관이 있어 물건 종류·단위·동네·인계 정보는 바꿀 수 없습니다.");
        if (active.Any(x => x.FromUtc < request.AvailableFromUtc.UtcDateTime || x.UntilUtc > request.AvailableUntilUtc.UtcDateTime)
            || 생활보관수량Policy.최대동시수량(active, request.AvailableFromUtc.UtcDateTime, request.AvailableUntilUtc.UtcDateTime) > request.CapacityQuantity)
            return Fail("ReservationConflict", "기존 보관 예약의 시간과 수량을 유지해 주세요.");
        SetContent(document, request);
        document.OfferRevision++;
        return await Commit(document, request.ExpectedRevision, mutation.Actor!, request.RequestId, mutation.Fingerprint!, null, cancellationToken);
    }

    public async Task<Result<NeighborhoodStorageSpaceDto>> 상태변경Async(string spaceId, string status,
        NeighborhoodStorageMutationRequest request, CancellationToken cancellationToken = default)
    {
        if (status is not NeighborhoodStorageStatus.Published and not NeighborhoodStorageStatus.Paused and not NeighborhoodStorageStatus.Closed)
            return Fail("InvalidStatus", "지원하는 보관 제공 상태를 선택해 주세요.");
        var mutation = await LoadOwner(spaceId, request.RequestId, request.ExpectedRevision, status, request, cancellationToken);
        if (mutation.Result is not null) return mutation.Result;
        var document = mutation.Document!;
        if (document.Status == NeighborhoodStorageStatus.Closed && status != NeighborhoodStorageStatus.Closed)
            return Fail("SpaceClosed", "종료된 보관 제공은 다시 시작할 수 없습니다. 새로 등록해 주세요.");
        if (status == NeighborhoodStorageStatus.Published && document.AvailableUntilUtc <= Now)
            return Fail("AvailabilityEnded", "보관 가능 기간을 먼저 수정해 주세요.");
        if (document.Status != status) document.OfferRevision++;
        document.Status = status;
        // pause/close는 공개와 새 예약만 중지하며 합의된 예약·인수·반환 기록을 삭제하지 않습니다.
        return await Commit(document, request.ExpectedRevision, mutation.Actor!, request.RequestId, mutation.Fingerprint!, null, cancellationToken);
    }

    public async Task<Result<NeighborhoodStorageSpaceDto>> 예약Async(string spaceId, NeighborhoodStorageReservationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return Fail("AuthenticationRequired", "로그인 후 보관을 신청해 주세요.");
        if (request.RequestId == Guid.Empty || request.ExpectedRevision < 1 || string.IsNullOrWhiteSpace(request.CollaborationId))
            return Fail("InvalidRequest", "보관 신청과 현재 판본을 확인해 주세요.");
        var document = await store.조회Async(spaceId, cancellationToken);
        var context = collaboration is null ? null : await collaboration.조회Async(request.CollaborationId, cancellationToken);
        if (document is null || context is null || context.CollaborationId != request.CollaborationId
            || context.OwnerUserId != document.OwnerUserId || context.StorageSpaceId != spaceId
            || (actor != context.OwnerUserId && actor != context.RequesterUserId)) return Fail("NotFound", "현재 참여 중인 보관 합의를 찾을 수 없습니다.");
        var fingerprint = Fingerprint("reserve", request);
        var replay = await Replay(document, actor, request.RequestId, fingerprint, request.CollaborationId, cancellationToken);
        if (replay is not null) return replay;
        if (!context.AcceptedCurrentTerms || !context.PrivateDisclosureConsented || context.IsClosed)
            return Fail("AgreementRequired", "양측이 현재 보관 조건과 인계 정보 제공에 동의해야 합니다.");
        if (document.Revision != request.ExpectedRevision) return Fail("RevisionConflict", "보관 공간이 변경됐습니다. 다시 확인해 주세요.");
        if (document.Status != NeighborhoodStorageStatus.Published || document.AvailableUntilUtc <= Now)
            return Fail("SpaceUnavailable", "새 보관 신청을 받지 않는 공간입니다.");
        if (context.ExpectedStorageOfferRevision != document.OfferRevision)
            return Fail("StorageOfferChanged", "합의한 보관 제공 조건이 바뀌었습니다. 최신 조건을 다시 확인해 주세요.");
        if (context.Quantity <= 0 || context.Quantity > document.CapacityQuantity || context.Unit != document.CapacityUnit
            || context.FromUtc == default || context.UntilUtc == default || context.FromUtc >= context.UntilUtc
            || context.FromUtc.UtcDateTime < document.AvailableFromUtc || context.UntilUtc.UtcDateTime > document.AvailableUntilUtc
            || context.UntilUtc.UtcDateTime <= Now)
            return Fail("InvalidReservation", "합의한 물건 수량·단위와 보관 가능 시간을 확인해 주세요.");
        if (document.Reservations.Any(x => x.CollaborationId == request.CollaborationId))
            return Fail("ReservationExists", "이미 기록된 보관 예약입니다. 현재 상태를 확인해 주세요.");
        if (document.Reservations.Count >= 200) return Fail("ReservationLimit", "이 공간의 예약 기록이 가득 찼습니다. 새 보관 제공을 등록해 주세요.");
        if (생활보관수량Policy.최대동시수량(document.Reservations, context.FromUtc.UtcDateTime, context.UntilUtc.UtcDateTime)
            + context.Quantity > document.CapacityQuantity)
            return Fail("CapacityExceeded", "같은 시간에 맡을 수 있는 수량을 초과합니다.");
        if (reservationGuard is null) return Fail("ReservationAuthorityUnavailable", "보관 합의의 예약권을 확인할 수 없습니다.");
        var claim = await reservationGuard.획득Async(request.CollaborationId, context.TermsRevision, spaceId,
            actor, request.RequestId, request.ExpectedRevision, fingerprint, cancellationToken);
        if (claim.IsFailed) return Result.Fail<NeighborhoodStorageSpaceDto>(claim.Errors);
        if (claim.Value.StateCode == "released") return Fail("ReservationAborted", "종료된 예약 요청입니다. 결과를 확인한 뒤 새로 신청해 주세요.");
        document.Reservations.Add(new()
        {
            CollaborationId = request.CollaborationId, TermsRevision = context.TermsRevision,
            IntentRequestId = request.RequestId, IntentActorId = actor,
            RequesterUserId = context.RequesterUserId, Quantity = context.Quantity, Unit = context.Unit,
            FromUtc = context.FromUtc.UtcDateTime, UntilUtc = context.UntilUtc.UtcDateTime,
            Status = NeighborhoodStorageReservationStatus.Reserved
        });
        document.Revision = request.ExpectedRevision + 1; document.UpdatedAtUtc = Now;
        Record(document, actor, request.RequestId, fingerprint, request.CollaborationId, context.TermsRevision);
        if (!await store.저장Async(document, request.ExpectedRevision, cancellationToken))
            return await AbortIntent(spaceId, request.CollaborationId, claim.Value, "RevisionConflict", cancellationToken);
        await ReconcileIntent(document, request.CollaborationId, cancellationToken);
        return Result.Ok(await MutationResponse(document, actor, request.CollaborationId, false, cancellationToken));
    }

    public async Task<Result<NeighborhoodStorageSpaceDto>> 예약변경Async(string spaceId, string collaborationId,
        NeighborhoodStorageReservationActionRequest request, CancellationToken cancellationToken = default)
    {
        if (Actor is not { } actor) return Fail("AuthenticationRequired", "로그인 후 인계 상태를 확인해 주세요.");
        if (request.RequestId == Guid.Empty || request.ExpectedRevision < 1
            || request.Action is not "confirm-intake" and not "confirm-return" and not "cancel")
            return Fail("InvalidRequest", "지원하는 보관 확인과 현재 판본을 선택해 주세요.");
        var document = await store.조회Async(spaceId, cancellationToken);
        var reservation = document?.Reservations.SingleOrDefault(x => x.CollaborationId == collaborationId);
        var context = collaboration is null ? null : await collaboration.조회Async(collaborationId, cancellationToken);
        if (document is null || reservation is null || context is null || context.CollaborationId != collaborationId || context.OwnerUserId != document.OwnerUserId
            || context.StorageSpaceId != spaceId || context.RequesterUserId != reservation.RequesterUserId
            || context.TermsRevision != reservation.TermsRevision || (actor != document.OwnerUserId && actor != reservation.RequesterUserId))
            return Fail("NotFound", "본인이 참여한 보관 예약을 찾을 수 없습니다.");
        var fingerprint = Fingerprint("reservation:" + collaborationId, request);
        var replay = await Replay(document, actor, request.RequestId, fingerprint, collaborationId, cancellationToken);
        if (replay is not null) return replay;
        if (document.Revision != request.ExpectedRevision) return Fail("RevisionConflict", "인계 상태가 변경됐습니다. 다시 확인해 주세요.");
        if (request.Action != "cancel" && (!context.AcceptedCurrentTerms || !context.PrivateDisclosureConsented || context.IsClosed))
            return Fail("AgreementRequired", "현재 조건에 대한 양측 동의가 필요합니다.");
        var owner = actor == document.OwnerUserId;
        switch (request.Action)
        {
            case "confirm-intake":
                if (reservation.Status != NeighborhoodStorageReservationStatus.Reserved || reservation.OwnerReturnConfirmed || reservation.RequesterReturnConfirmed)
                    return Fail("InvalidTransition", "예약 상태에서만 물건 인수를 확인할 수 있습니다.");
                if (owner) reservation.OwnerIntakeConfirmed = true; else reservation.RequesterIntakeConfirmed = true;
                if (reservation.OwnerIntakeConfirmed && reservation.RequesterIntakeConfirmed)
                    reservation.Status = NeighborhoodStorageReservationStatus.InCustody;
                break;
            case "confirm-return":
                if (reservation.Status != NeighborhoodStorageReservationStatus.InCustody
                    && !(reservation.Status == NeighborhoodStorageReservationStatus.Reserved && (reservation.OwnerIntakeConfirmed || reservation.RequesterIntakeConfirmed)))
                    return Fail("InvalidTransition", "물건을 인수한 후 반환을 확인할 수 있습니다.");
                if (owner) reservation.OwnerReturnConfirmed = true; else reservation.RequesterReturnConfirmed = true;
                if (reservation.OwnerReturnConfirmed && reservation.RequesterReturnConfirmed)
                    reservation.Status = NeighborhoodStorageReservationStatus.Returned;
                break;
            case "cancel":
                if (reservation.Status != NeighborhoodStorageReservationStatus.Reserved || reservation.OwnerIntakeConfirmed || reservation.RequesterIntakeConfirmed)
                    return Fail("InvalidTransition", "인수 확인을 시작한 물건은 취소로 지우지 않고 반환을 확인해야 합니다.");
                reservation.Status = NeighborhoodStorageReservationStatus.Cancelled;
                break;
        }
        return await Commit(document, request.ExpectedRevision, actor, request.RequestId, fingerprint, collaborationId, cancellationToken);
    }

    private async Task<(생활보관공간문서? Document, string? Actor, string? Fingerprint, Result<NeighborhoodStorageSpaceDto>? Result)> LoadOwner<T>(
        string spaceId, Guid requestId, long expectedRevision, string operation, T request, CancellationToken cancellationToken)
    {
        if (Actor is not { } actor) return (null, null, null, Fail("AuthenticationRequired", "로그인이 필요합니다."));
        if (requestId == Guid.Empty || expectedRevision < 1) return (null, null, null, Fail("InvalidRequest", "요청 번호와 현재 판본을 확인해 주세요."));
        var document = await store.조회Async(spaceId, cancellationToken);
        if (document is null || document.OwnerUserId != actor) return (null, null, null, Fail("NotFound", "본인 보관 공간을 찾을 수 없습니다."));
        var fingerprint = Fingerprint(operation, request);
        var replay = await Replay(document, actor, requestId, fingerprint, null, cancellationToken);
        if (replay is not null) return (null, actor, fingerprint, replay);
        if (document.Revision != expectedRevision) return (null, actor, fingerprint, Fail("RevisionConflict", "보관 공간이 변경됐습니다. 다시 확인해 주세요."));
        return (document, actor, fingerprint, null);
    }

    private async Task<string?> Validate(NeighborhoodStorageSpaceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PublicTitle) || request.PublicTitle.Trim().Length > 100
            || request.PublicDescription.Length > 1000 || string.IsNullOrWhiteSpace(request.GoodsKind) || request.GoodsKind.Trim().Length > 80
            || request.CapacityQuantity <= 0 || request.CapacityQuantity > 1000 || decimal.Round(request.CapacityQuantity, 3) != request.CapacityQuantity
            || string.IsNullOrWhiteSpace(request.CapacityUnit) || request.CapacityUnit.Trim().Length > 20)
            return "제목·물건 종류와 맡을 수 있는 수량·단위를 확인해 주세요.";
        if (request.AvailableFromUtc == default || request.AvailableUntilUtc == default || request.AvailableFromUtc >= request.AvailableUntilUtc
            || request.AvailableUntilUtc.UtcDateTime <= Now || (request.AvailableUntilUtc - request.AvailableFromUtc).TotalDays > 366)
            return "보관 가능 시작·종료 시간을 확인해 주세요. 한 번에 1년 이내로 등록할 수 있습니다.";
        if (request.PrivateAddress.Length > 300 || request.PrivateContact.Length > 100 || request.PrivateHandoverInstructions.Length > 1000)
            return "비공개 인계 정보를 더 짧게 입력해 주세요.";
        if (생활보관공개정보Policy.민감정보포함(request.PublicTitle + " " + request.PublicDescription + " " + request.GoodsKind + " " + request.CapacityUnit))
            return "상세 주소·연락처·출입 정보는 비공개 인계 정보에만 입력해 주세요.";
        var directory = await regions.목록Async(cancellationToken);
        if (!directory.Any(x => x.RegionKey == request.PublicNeighborhoodRegionKey.Trim())) return "검증된 공개 동네를 선택해 주세요.";
        return null;
    }

    private void SetContent(생활보관공간문서 document, NeighborhoodStorageSpaceRequest request)
    {
        document.PublicTitle = request.PublicTitle.Trim(); document.PublicDescription = request.PublicDescription.Trim();
        document.PublicNeighborhoodRegionKey = request.PublicNeighborhoodRegionKey.Trim(); document.GoodsKind = request.GoodsKind.Trim();
        document.CapacityQuantity = request.CapacityQuantity; document.CapacityUnit = request.CapacityUnit.Trim();
        document.AvailableFromUtc = request.AvailableFromUtc.UtcDateTime; document.AvailableUntilUtc = request.AvailableUntilUtc.UtcDateTime;
        document.ProtectedHandover = protection.보호(request.PrivateAddress.Trim(), request.PrivateContact.Trim(), request.PrivateHandoverInstructions.Trim());
    }

    private async Task<Result<NeighborhoodStorageSpaceDto>> Commit(생활보관공간문서 document, long expectedRevision, string actor,
        Guid requestId, string fingerprint, string? collaborationId, CancellationToken cancellationToken)
    {
        document.Revision = expectedRevision + 1; document.UpdatedAtUtc = Now;
        Record(document, actor, requestId, fingerprint, collaborationId,
            document.Reservations.FirstOrDefault(x => x.CollaborationId == collaborationId)?.TermsRevision);
        return await Persist(document, expectedRevision, actor, requestId, fingerprint, collaborationId, cancellationToken);
    }

    private async Task<Result<NeighborhoodStorageSpaceDto>> Persist(생활보관공간문서 document, long expectedRevision, string actor,
        Guid requestId, string fingerprint, string? collaborationId, CancellationToken cancellationToken)
    {
        if (await store.저장Async(document, expectedRevision, cancellationToken))
        {
            await ReconcileIntent(document, collaborationId, cancellationToken);
            return Result.Ok(await MutationResponse(document, actor, collaborationId, false, cancellationToken));
        }
        var fresh = await store.조회Async(document.SpaceId, cancellationToken);
        if (fresh is not null)
        {
            var replay = await Replay(fresh, actor, requestId, fingerprint, collaborationId, cancellationToken);
            if (replay is not null) return replay;
        }
        return Fail("RevisionConflict", "다른 요청이 먼저 변경했습니다. 현재 보관 상태를 다시 확인해 주세요.");
    }

    private async Task<Result<NeighborhoodStorageSpaceDto>?> Replay(생활보관공간문서 document, string actor, Guid requestId,
        string fingerprint, string? collaborationId, CancellationToken cancellationToken)
    {
        var operation = document.Operations.SingleOrDefault(x => x.ActorId == actor && x.RequestId == requestId.ToString("N"));
        if (operation is null) return null;
        if (operation.Fingerprint != fingerprint) return Fail("IdempotencyConflict", "같은 요청 번호로 내용을 바꿀 수 없습니다.");
        await ReconcileIntent(document, collaborationId, cancellationToken, operation);
        if (operation.Outcome == "aborted") return Fail(operation.FailureCode ?? "ReservationAborted", "이 예약 요청은 저장되지 않았습니다. 현재 상태를 확인한 뒤 새로 신청해 주세요.");
        return Result.Ok(await MutationResponse(document, actor, collaborationId, true, cancellationToken, operation));
    }

    private async Task<Result<NeighborhoodStorageSpaceDto>> AbortIntent(string spaceId, string collaborationId,
        생활협업예약IntentRecord intent, string failureCode, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var fresh = await store.조회Async(spaceId, cancellationToken);
            if (fresh is null) return Fail("ReservationRecoveryRequired", "보관 공간 원장을 확인할 수 없어 예약권을 유지합니다.");
            var existing = fresh.Operations.SingleOrDefault(x => x.ActorId == intent.ActorUserId && x.RequestId == intent.RequestId.ToString("N"));
            if (existing is not null)
                return (await Replay(fresh, intent.ActorUserId, intent.RequestId, intent.Fingerprint, collaborationId, cancellationToken))!;
            var expected = fresh.Revision;
            fresh.Revision++; fresh.UpdatedAtUtc = Now;
            fresh.Operations.Add(new()
            {
                ActorId = intent.ActorUserId, RequestId = intent.RequestId.ToString("N"), Fingerprint = intent.Fingerprint,
                CollaborationId = collaborationId, TermsRevision = intent.TermsRevision,
                Outcome = "aborted", FailureCode = failureCode
            });
            // 이 CAS가 이기면 원래 expectedSpaceRevision의 지연 쓰기는 성공할 수 없습니다.
            // 지연 쓰기가 먼저 이기면 다음 재조회가 그 committed receipt를 확인합니다.
            if (!await store.저장Async(fresh, expected, cancellationToken)) continue;
            await ReconcileIntent(fresh, collaborationId, cancellationToken, fresh.Operations[^1]);
            return Fail(failureCode, "예약을 저장하지 않았습니다. 현재 보관 상태를 확인한 뒤 새 요청으로 신청해 주세요.");
        }
        return Fail("ReservationRecoveryRequired", "예약 결과를 확정할 수 없습니다. 같은 요청 결과를 다시 확인해 주세요.");
    }

    private async Task ReconcileIntent(생활보관공간문서 document, string? collaborationId,
        CancellationToken cancellationToken, 생활보관요청증적? operation = null)
    {
        if (reservationGuard is null || string.IsNullOrWhiteSpace(collaborationId)) return;
        Result<생활협업예약IntentRecord>? result = null;
        var reservation = document.Reservations.SingleOrDefault(x => x.CollaborationId == collaborationId);
        if (reservation is not null && reservation.IntentRequestId != Guid.Empty)
            result = reservation.Status is NeighborhoodStorageReservationStatus.Cancelled or NeighborhoodStorageReservationStatus.Returned
                ? await reservationGuard.해제Async(collaborationId, reservation.IntentRequestId, reservation.IntentActorId, cancellationToken)
                : await reservationGuard.확정Async(collaborationId, reservation.IntentRequestId, reservation.IntentActorId, cancellationToken);
        else if (operation?.Outcome == "aborted")
            result = await reservationGuard.해제Async(collaborationId, Guid.ParseExact(operation.RequestId, "N"), operation.ActorId, cancellationToken);
        if (result?.IsFailed == true)
            throw new InvalidOperationException("보관 원장 저장 후 합의 원장의 예약권 반영을 확인하지 못했습니다. 같은 요청 결과를 다시 조회해 주세요.");
    }

    private async Task<NeighborhoodStorageSpaceDto> MutationResponse(생활보관공간문서 document, string actor,
        string? collaborationId, bool replay, CancellationToken cancellationToken, 생활보관요청증적? operation = null)
    {
        var directory = await regions.목록Async(cancellationToken);
        if (document.OwnerUserId == actor) return Private(document, actor, directory, null, replay, operation);
        var context = collaboration is null || collaborationId is null ? null : await collaboration.조회Async(collaborationId, cancellationToken);
        if (PrivateAllowed(document, context, actor)) return Private(document, actor, directory, collaborationId, replay, operation);
        // 동의 철회 뒤의 취소/동일 요청 결과는 기록 확인만 제공하고 인계 정보를 복원하지 않습니다.
        return new()
        {
            SpaceId = document.SpaceId, Revision = document.Revision, Status = document.Status,
            Public = Public(document, directory), IdempotentReplay = replay,
            RequestOutcome = operation?.Outcome ?? "committed", RequestFailureCode = operation?.FailureCode,
            Reservations = document.Reservations.Where(x => x.CollaborationId == collaborationId && x.RequesterUserId == actor)
                .Select(x => Reservation(x, false)).ToArray(),
            Notice = "현재 합의와 정보 제공 동의를 확인할 수 없어 인계 정보는 표시하지 않습니다."
        };
    }

    private NeighborhoodStorageSpaceDto Private(생활보관공간문서 document, string actor,
        IReadOnlyList<NeighborhoodPublicRegionDto> directory, string? collaborationId, bool replay = false, 생활보관요청증적? operation = null)
    {
        var handover = protection.복원(document.ProtectedHandover);
        var owner = actor == document.OwnerUserId;
        return new()
        {
            SpaceId = document.SpaceId, Revision = document.Revision, Status = document.Status, Public = Public(document, directory),
            PrivateAddress = handover.Address, PrivateContact = handover.Contact, PrivateHandoverInstructions = handover.Instructions,
            IsOwner = owner, IdempotentReplay = replay,
            RequestOutcome = operation?.Outcome ?? "committed", RequestFailureCode = operation?.FailureCode,
            Reservations = document.Reservations.Where(x => owner || x.CollaborationId == collaborationId && x.RequesterUserId == actor)
                .Select(x => Reservation(x, owner)).ToArray()
        };
    }

    private bool PrivateAllowed(생활보관공간문서 document, NeighborhoodStorageCollaborationContext? context, string actor)
        => context is not null && context.StorageSpaceId == document.SpaceId && context.OwnerUserId == document.OwnerUserId
            && context.RequesterUserId == actor && context.AcceptedCurrentTerms && context.PrivateDisclosureConsented && !context.IsClosed
            && (document.Reservations.Any(x => x.CollaborationId == context.CollaborationId && x.TermsRevision == context.TermsRevision
                    && x.RequesterUserId == actor && x.Status is NeighborhoodStorageReservationStatus.Reserved or NeighborhoodStorageReservationStatus.InCustody)
                || document.OfferRevision == context.ExpectedStorageOfferRevision && document.Status == NeighborhoodStorageStatus.Published && document.AvailableUntilUtc > Now);

    private static NeighborhoodStoragePublicDto Public(생활보관공간문서 document, IReadOnlyList<NeighborhoodPublicRegionDto> directory)
        => new()
        {
            SpaceId = document.SpaceId, Revision = document.Revision, OfferRevision = document.OfferRevision, PublicTitle = document.PublicTitle,
            PublicDescription = document.PublicDescription, Region = directory.SingleOrDefault(x => x.RegionKey == document.PublicNeighborhoodRegionKey)
                ?? new() { RegionKey = document.PublicNeighborhoodRegionKey, DisplayName = "지원되지 않는 동네" },
            GoodsKind = document.GoodsKind, CapacityQuantity = document.CapacityQuantity, CapacityUnit = document.CapacityUnit,
            AvailableFromUtc = Utc(document.AvailableFromUtc), AvailableUntilUtc = Utc(document.AvailableUntilUtc)
        };

    private static NeighborhoodStorageReservationDto Reservation(생활보관예약문서 reservation, bool owner)
        => new()
        {
            CollaborationId = reservation.CollaborationId, TermsRevision = reservation.TermsRevision,
            Quantity = reservation.Quantity, Unit = reservation.Unit, FromUtc = Utc(reservation.FromUtc), UntilUtc = Utc(reservation.UntilUtc),
            Status = reservation.Status, OwnerIntakeConfirmed = reservation.OwnerIntakeConfirmed, RequesterIntakeConfirmed = reservation.RequesterIntakeConfirmed,
            OwnerReturnConfirmed = reservation.OwnerReturnConfirmed, RequesterReturnConfirmed = reservation.RequesterReturnConfirmed,
            AllowedActions = reservation.Status switch
            {
                NeighborhoodStorageReservationStatus.Reserved => new[]
                {
                    (owner ? reservation.OwnerIntakeConfirmed : reservation.RequesterIntakeConfirmed)
                        || reservation.OwnerReturnConfirmed || reservation.RequesterReturnConfirmed ? null : "confirm-intake",
                    !reservation.OwnerIntakeConfirmed && !reservation.RequesterIntakeConfirmed ? "cancel" : null,
                    (reservation.OwnerIntakeConfirmed || reservation.RequesterIntakeConfirmed)
                        && !(owner ? reservation.OwnerReturnConfirmed : reservation.RequesterReturnConfirmed) ? "confirm-return" : null
                }.OfType<string>().ToArray(),
                NeighborhoodStorageReservationStatus.InCustody => (owner ? reservation.OwnerReturnConfirmed : reservation.RequesterReturnConfirmed)
                    ? [] : ["confirm-return"],
                _ => []
            }
        };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static int NormalizePage(int page) => Math.Clamp(page, 1, 10000);
    public static string StableId(string owner, Guid requestId) => "storage-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner + ":" + requestId.ToString("N")))).ToLowerInvariant();
    private static string Fingerprint<T>(string operation, T request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operation + ":" + JsonSerializer.Serialize(request))));
    private static void Record(생활보관공간문서 document, string actor, Guid requestId, string fingerprint,
        string? collaborationId = null, long? termsRevision = null)
        => document.Operations.Add(new() { ActorId = actor, RequestId = requestId.ToString("N"), Fingerprint = fingerprint,
            CollaborationId = collaborationId, TermsRevision = termsRevision });
    private static Result<NeighborhoodStorageSpaceDto> Fail(string code, string message)
        => Result.Fail<NeighborhoodStorageSpaceDto>(new Error(message).WithMetadata("ErrorCode", code).WithMetadata("StatusCode", code switch
        { "AuthenticationRequired" => 401, "NotFound" => 404, "RevisionConflict" or "IdempotencyConflict" or "CapacityExceeded" or "ReservationConflict" or "ReservationExists" => 409, _ => 400 }));
}

public static class 생활보관수량Policy
{
    /// <summary>기간은 [시작, 종료)이며, 인수 확인을 시작한 물건은 양측 반환 전까지 용량을 계속 차지합니다.</summary>
    public static decimal 최대동시수량(IEnumerable<생활보관예약문서> reservations, DateTime fromUtc, DateTime untilUtc)
    {
        var events = new List<(DateTime At, decimal Delta)>();
        foreach (var reservation in reservations)
        {
            if (reservation.Status is NeighborhoodStorageReservationStatus.Cancelled or NeighborhoodStorageReservationStatus.Returned) continue;
            var end = reservation.Status == NeighborhoodStorageReservationStatus.InCustody || reservation.OwnerIntakeConfirmed || reservation.RequesterIntakeConfirmed
                ? DateTime.MaxValue : reservation.UntilUtc;
            if (reservation.FromUtc >= untilUtc || end <= fromUtc) continue;
            events.Add((reservation.FromUtc < fromUtc ? fromUtc : reservation.FromUtc, reservation.Quantity));
            events.Add((end > untilUtc ? untilUtc : end, -reservation.Quantity));
        }
        decimal current = 0, maximum = 0;
        foreach (var point in events.OrderBy(x => x.At).ThenBy(x => x.Delta))
        { current += point.Delta; maximum = Math.Max(maximum, current); }
        return maximum;
    }
}

public static partial class 생활보관공개정보Policy
{
    // 자유 문장의 모든 개인정보를 알아낼 수는 없습니다. 흔한 상세 주소·연락처·출입 코드 입력을 막고 별도 비공개 필드를 안내합니다.
    public static bool 민감정보포함(string value) => ContactOrAddress().IsMatch(value);
    [GeneratedRegex(@"(?:(?<!\d)0\d{1,2}[\s-]?\d{3,4}[\s-]?\d{4}(?!\d)|[가-힣]+(?:로|길)\s*\d+(?:[-\s]\d+)?|\d+\s*동\s*\d+\s*호|\d+\s*호\b|(?:비밀번호|출입번호|공동현관)\s*[:：]?\s*\d+|\b[^\s@]+@[^\s@]+\.[^\s@]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ContactOrAddress();
}
