using Hongdal.Contracts.Common.Content;

namespace Hongdal.Services.Content;

public interface IYouTubeSocialContextWorkspaceService
{
    IReadOnlyList<SocialMediaResearchSourceDto> GetSources();

    Task<YouTubeSocialContextResearchResponse> ResearchAndSaveAsync(
        YouTubeSocialContextResearchRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto?> GetAsync(
        string workspaceId,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto?> GetByVideoIdAsync(
        string videoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> ListAsync(
        string? status,
        int take,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto> UpdateDraftAsync(
        string workspaceId,
        YouTubeSocialContextWorkspaceDraftUpdateRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken);

    Task<YouTubeSocialContextWorkspaceDto> LinkPublicationAsync(
        string workspaceId,
        YouTubeSocialContextPublicationLinkRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken);
}

public sealed class YouTubeSocialContextWorkspaceService : IYouTubeSocialContextWorkspaceService
{
    private readonly IYouTubeSocialContextResearchService _researchService;
    private readonly IYouTubeSocialContextWorkspaceStore _store;

    public YouTubeSocialContextWorkspaceService(
        IYouTubeSocialContextResearchService researchService,
        IYouTubeSocialContextWorkspaceStore store)
    {
        _researchService = researchService;
        _store = store;
    }

    public IReadOnlyList<SocialMediaResearchSourceDto> GetSources()
        => _researchService.GetSources();

    public async Task<YouTubeSocialContextResearchResponse> ResearchAndSaveAsync(
        YouTubeSocialContextResearchRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken)
    {
        var research = await _researchService.ResearchAsync(request, cancellationToken);
        var workspace = await _store.SaveResearchAsync(
            request,
            research,
            updatedByUserId,
            updatedByDisplayName,
            cancellationToken);
        return research with
        {
            WorkspaceId = workspace.WorkspaceId,
            WorkspaceRevision = workspace.Revision,
            WorkspaceStatus = workspace.Status
        };
    }

    public Task<YouTubeSocialContextWorkspaceDto?> GetAsync(
        string workspaceId,
        CancellationToken cancellationToken)
        => _store.GetAsync(workspaceId, cancellationToken);

    public Task<YouTubeSocialContextWorkspaceDto?> GetByVideoIdAsync(
        string videoId,
        CancellationToken cancellationToken)
        => _store.GetByVideoIdAsync(videoId, cancellationToken);

    public Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> ListAsync(
        string? status,
        int take,
        CancellationToken cancellationToken)
        => _store.ListAsync(status, take, cancellationToken);

    public Task<YouTubeSocialContextWorkspaceDto> UpdateDraftAsync(
        string workspaceId,
        YouTubeSocialContextWorkspaceDraftUpdateRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken)
        => _store.UpdateDraftAsync(
            workspaceId,
            request,
            updatedByUserId,
            updatedByDisplayName,
            cancellationToken);

    public Task<YouTubeSocialContextWorkspaceDto> LinkPublicationAsync(
        string workspaceId,
        YouTubeSocialContextPublicationLinkRequest request,
        string updatedByUserId,
        string updatedByDisplayName,
        CancellationToken cancellationToken)
        => _store.LinkPublicationAsync(
            workspaceId,
            request,
            updatedByUserId,
            updatedByDisplayName,
            cancellationToken);
}
