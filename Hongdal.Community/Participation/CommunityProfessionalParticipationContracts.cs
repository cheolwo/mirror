using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public interface ICommunityProfessionalEligibilityService
{
    Task<IReadOnlyList<string>> GetVerifiedRoleCodesAsync(
        string userId,
        CancellationToken cancellationToken = default);
}

public interface ICommunityPostProfessionalParticipationService
{
    Task<JoinCommunityPostProfessionalResponse> JoinAsync(
        long postId,
        JoinCommunityPostProfessionalRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);

    Task<JoinCommunityPostPartyRoleResponse> JoinPartyRoleAsync(
        long postId,
        JoinCommunityPostPartyRoleRequest request,
        string actorUserId,
        string actorDisplayName,
        CancellationToken cancellationToken = default);
}
