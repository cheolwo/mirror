using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Content;
using Hongdal.Services.Content;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTubeSocialContextWorkspaceServiceTests
{
    [Fact]
    public async Task ResearchAndSaveAsync_YouTube를루트로_SNS조사와초안을저장한다()
    {
        var research = CreateResearch();
        var workspace = CreateWorkspace(research);
        var researchService = new StubResearchService(research);
        var store = new RecordingWorkspaceStore(workspace);
        var sut = new YouTubeSocialContextWorkspaceService(researchService, store);
        var request = new YouTubeSocialContextResearchRequest
        {
            VideoId = research.Video.VideoId,
            SourceKeys = ["reddit-public-posts"],
            SearchTerms = ["공동구매"],
            SourceTargets =
            [
                new SocialMediaResearchTargetDto(
                    "reddit-public-posts",
                    ["https://www.reddit.com/r/localfood/"])
            ],
            TakePerSource = 8
        };

        var result = await sut.ResearchAndSaveAsync(
            request,
            "admin-1",
            "홍달 운영자",
            CancellationToken.None);

        Assert.Same(request, store.SavedRequest);
        Assert.Same(research, store.SavedResearch);
        Assert.Equal("admin-1", store.UpdatedByUserId);
        Assert.Equal(workspace.WorkspaceId, result.WorkspaceId);
        Assert.Equal(workspace.Revision, result.WorkspaceRevision);
        Assert.Equal(YouTubeSocialContextWorkspaceStatusCodes.ResearchReady, result.WorkspaceStatus);
    }

    private static YouTubeSocialContextResearchResponse CreateResearch()
    {
        var video = new YouTubeSocialContextVideoDto(
            "video-1",
            "Food channel",
            "Local food video",
            "Summary",
            "https://www.youtube.com/watch?v=video-1",
            null,
            new DateTime(2026, 7, 18, 1, 0, 0, DateTimeKind.Utc),
            "US",
            "en");
        var source = new SocialMediaResearchSourceDto(
            "reddit-public-posts",
            "Reddit",
            "Reddit 공개 글",
            "https://developers.reddit.com/",
            true,
            true,
            false);
        var item = new CommunityInformationCandidateDto(
            "reddit:1",
            source.SourceKey,
            CommunityInformationSourceTypes.SocialMedia,
            source.Provider,
            "Local food discussion",
            "Summary",
            "https://www.reddit.com/r/localfood/comments/1",
            null,
            new DateTime(2026, 7, 18, 1, 10, 0, DateTimeKind.Utc),
            null,
            new DateTime(2026, 7, 18, 1, 20, 0, DateTimeKind.Utc),
            "US",
            "en",
            null,
            null,
            CommunityInformationReviewStates.PendingReview,
            ["지역 식재료"],
            "Public post",
            "Editorial review required");
        return new YouTubeSocialContextResearchResponse(
            new DateTime(2026, 7, 18, 2, 0, 0, DateTimeKind.Utc),
            video,
            ["공동구매"],
            ["지역 식재료"],
            [source],
            [item],
            [],
            new YouTubeSocialContextPostDraftDto(
                "[together] Local food video",
                "Draft body",
                new YouTubeSocialContextCollectiveActionDraftDto(
                    "공동구매",
                    CommunityCollectiveIntentTypeCodes.GroupPurchase,
                    [CommunityCollectiveIntentTypeCodes.GroupPurchase],
                    "prompt",
                    "non-binding",
                    "/api/v1/community/posts/{postId}/opportunities")));
    }

    private static YouTubeSocialContextWorkspaceDto CreateWorkspace(
        YouTubeSocialContextResearchResponse research)
    {
        var now = research.GeneratedAtUtc;
        return new YouTubeSocialContextWorkspaceDto(
            "youtube-video-1",
            1,
            YouTubeSocialContextWorkspaceStatusCodes.ResearchReady,
            research.Video,
            research.SearchTerms,
            research.AdjacentTopics,
            [new SocialMediaResearchTargetDto("reddit-public-posts", ["https://www.reddit.com/r/localfood/"])],
            8,
            [new YouTubeSocialContextSourceGroupDto(research.Sources[0], research.Items)],
            research.Failures,
            new YouTubeSocialContextWorkspaceDraftDto(
                string.Empty,
                string.Empty,
                research.Draft.CollectiveAction.WorkflowTag,
                string.Empty,
                research.Draft.Title,
                research.Draft.Body,
                research.Video.OriginalUrl,
                research.Draft.CollectiveAction,
                false,
                now),
            null,
            [],
            now,
            now,
            now,
            "홍달 운영자");
    }

    private sealed class StubResearchService(YouTubeSocialContextResearchResponse response)
        : IYouTubeSocialContextResearchService
    {
        public IReadOnlyList<SocialMediaResearchSourceDto> GetSources() => response.Sources;

        public Task<YouTubeSocialContextResearchResponse> ResearchAsync(
            YouTubeSocialContextResearchRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    private sealed class RecordingWorkspaceStore(YouTubeSocialContextWorkspaceDto workspace)
        : IYouTubeSocialContextWorkspaceStore
    {
        public YouTubeSocialContextResearchRequest? SavedRequest { get; private set; }
        public YouTubeSocialContextResearchResponse? SavedResearch { get; private set; }
        public string? UpdatedByUserId { get; private set; }

        public Task<YouTubeSocialContextWorkspaceDto> SaveResearchAsync(
            YouTubeSocialContextResearchRequest request,
            YouTubeSocialContextResearchResponse research,
            string updatedByUserId,
            string updatedByDisplayName,
            CancellationToken cancellationToken)
        {
            SavedRequest = request;
            SavedResearch = research;
            UpdatedByUserId = updatedByUserId;
            return Task.FromResult(workspace);
        }

        public Task<YouTubeSocialContextWorkspaceDto?> GetAsync(
            string workspaceId,
            CancellationToken cancellationToken)
            => Task.FromResult<YouTubeSocialContextWorkspaceDto?>(workspace);

        public Task<YouTubeSocialContextWorkspaceDto?> GetByVideoIdAsync(
            string videoId,
            CancellationToken cancellationToken)
            => Task.FromResult<YouTubeSocialContextWorkspaceDto?>(workspace);

        public Task<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>> ListAsync(
            string? status,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTubeSocialContextWorkspaceSummaryDto>>([]);

        public Task<YouTubeSocialContextWorkspaceDto> UpdateDraftAsync(
            string workspaceId,
            YouTubeSocialContextWorkspaceDraftUpdateRequest request,
            string updatedByUserId,
            string updatedByDisplayName,
            CancellationToken cancellationToken)
            => Task.FromResult(workspace);

        public Task<YouTubeSocialContextWorkspaceDto> LinkPublicationAsync(
            string workspaceId,
            YouTubeSocialContextPublicationLinkRequest request,
            string updatedByUserId,
            string updatedByDisplayName,
            CancellationToken cancellationToken)
            => Task.FromResult(workspace);
    }
}
