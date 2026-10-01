using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface ICommunityInformationReviewClient : ICommunityAuthoringImageClient
{
    Task<IReadOnlyList<CommunityInformationSourceDto>> GetSourcesAsync(
        CancellationToken cancellationToken = default);

    Task<CommunityInformationCollectionResponse> GetCandidatesAsync(
        CommunityInformationCollectionQuery query,
        CancellationToken cancellationToken = default);

    Task<CommunityAuthoringAiDraftResponse> GenerateAiDraftAsync(
        CommunityAuthoringAiDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialMediaResearchSourceDto>> GetSocialMediaSourcesAsync(
        CancellationToken cancellationToken = default);

    Task<YouTubeSocialContextResearchResponse> ResearchYouTubeSocialContextAsync(
        YouTubeSocialContextResearchRequest request,
        CancellationToken cancellationToken = default);

    Task<YouTubeSocialContextWorkspaceDto?> GetYouTubeSocialContextWorkspaceByVideoAsync(
        string videoId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> GetYouTubeSocialContextWorkspacesAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<YouTubeSocialContextWorkspaceDto> SaveYouTubeSocialContextWorkspaceDraftAsync(
        string workspaceId,
        YouTubeSocialContextWorkspaceDraftUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<YouTubeSocialContextWorkspaceDto> LinkYouTubeSocialContextPublicationAsync(
        string workspaceId,
        YouTubeSocialContextPublicationLinkRequest request,
        CancellationToken cancellationToken = default);
}
