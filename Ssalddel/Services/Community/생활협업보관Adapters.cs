using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Services.Community;

/// <summary>서비스 간 순환 의존 없이 보관 원장의 현재 공개 조건을 읽습니다.</summary>
public sealed class 생활협업공간Source(I생활보관공간Store store, I생활교류공개지역Source regions,
    TimeProvider clock) : I생활협업공간Source
{
    public async Task<생활협업공간SourceSnapshot?> 조회Async(string spaceId, CancellationToken cancellationToken = default)
    {
        var space = await store.조회Async(spaceId, cancellationToken);
        if (space is null) return null;
        var region = (await regions.목록Async(cancellationToken)).FirstOrDefault(x => x.RegionKey == space.PublicNeighborhoodRegionKey);
        return new(space.SpaceId, space.OfferRevision, space.OwnerUserId, space.PublicTitle, region?.RegionKey,
            region is not null && space.Status == NeighborhoodStorageStatus.Published
            && space.AvailableUntilUtc > clock.GetUtcNow().UtcDateTime);
    }
}

/// <summary>현재 합의 판본의 양측 반환 확인을 완료 조건에 결속합니다.</summary>
public sealed class 생활협업보관상태Source(I생활보관공간Store store, I생활협업Store collaborations)
    : I생활협업보관상태Source
{
    public async Task<생활협업보관예약Snapshot?> 예약조회Async(string collaborationId, CancellationToken cancellationToken = default)
    {
        var context = await collaborations.조회Async(collaborationId, cancellationToken);
        if (context?.Terms.StorageSpaceId is not { Length: > 0 } spaceId) return null;
        var space = await store.조회Async(spaceId, cancellationToken);
        var reservation = space?.Reservations.SingleOrDefault(x => x.CollaborationId == collaborationId);
        return reservation is null ? null : new(reservation.TermsRevision, reservation.Status);
    }
    public async Task<bool> 반환확인Async(string collaborationId, long termsRevision, CancellationToken cancellationToken = default)
    {
        var context = await collaborations.조회Async(collaborationId, cancellationToken);
        if (context?.Terms.StorageSpaceId is not { Length: > 0 } spaceId || context.TermsRevision != termsRevision) return false;
        var space = await store.조회Async(spaceId, cancellationToken);
        return space?.Reservations.Any(x => x.CollaborationId == collaborationId && x.TermsRevision == termsRevision
            && x.Status == NeighborhoodStorageReservationStatus.Returned
            && x.OwnerReturnConfirmed && x.RequesterReturnConfirmed) == true;
    }
}

public sealed class 생활보관협업ContextSource(I생활협업연결Query collaborations) : I생활보관협업ContextSource
{
    public async Task<NeighborhoodStorageCollaborationContext?> 조회Async(string collaborationId, CancellationToken cancellationToken = default)
    {
        var context = await collaborations.조회Async(collaborationId, cancellationToken);
        if (context is null || context.Kind != NeighborhoodCollaborationKinds.Storage) return null;
        return new()
        {
            CollaborationId = context.StableId, TermsRevision = context.TermsRevision,
            ExpectedStorageOfferRevision = context.StorageOfferRevision ?? 0,
            OwnerUserId = context.OwnerUserId, RequesterUserId = context.RequesterUserId,
            AcceptedCurrentTerms = context.AcceptedCurrentTerms,
            PrivateDisclosureConsented = context.AcceptedCurrentTerms,
            IsClosed = context.StatusCode is NeighborhoodCollaborationStates.Completed or NeighborhoodCollaborationStates.Cancelled
                or NeighborhoodCollaborationStates.Rejected or NeighborhoodCollaborationStates.Expired,
            StorageSpaceId = context.Terms.StorageSpaceId ?? string.Empty,
            Quantity = context.Terms.StorageQuantity ?? 0, Unit = context.Terms.StorageUnit ?? string.Empty,
            FromUtc = Utc(context.Terms.StorageFromUtc), UntilUtc = Utc(context.Terms.StorageUntilUtc)
        };
    }
    private static DateTimeOffset Utc(DateTime? date) => date.HasValue ? new(DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)) : default;
}
