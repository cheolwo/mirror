using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface INeighborhoodExchangeClient
{
    Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct);
    Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct);
    Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct);
    Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct);
    Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct);
    Task DeleteAsync(long id, string? password, CancellationToken ct);
    Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct);
    Task ReportCommentAsync(long commentId, CancellationToken ct);
}

/// <summary>기존 공개 게시글 API를 사용하며 결제·재고·배차를 생성하지 않습니다.</summary>
public sealed class NeighborhoodExchangeClient(ICommunityPostClient posts) : INeighborhoodExchangeClient
{
    public Task<PlatformCommunityPostListResponse> ListAsync(string? intent, int page, CancellationToken ct)
        => posts.GetBoardPostsAsync("platform", category: PlatformCommunityPostCategories.General,
            workflowTag: NeighborhoodExchange.WorkflowTag,
            roleTag: NeighborhoodExchange.IsIntent(intent) ? intent : null,
            page: page, pageSize: NeighborhoodExchange.PageSize, cancellationToken: ct);
    public Task<PlatformCommunityPostResponse?> ReadAsync(long id, CancellationToken ct) => posts.GetPostAsync(id, ct);
    public Task<PlatformCommunityPostResponse?> PublishAsync(PlatformCommunityPostCreateRequest request, CancellationToken ct)
    {
        if (!NeighborhoodExchange.IsIntent(request.RoleTag))
            throw new ArgumentException("제공해요 또는 필요해요를 선택해 주세요.", nameof(request));
        request.AppKey = "platform";
        request.Category = PlatformCommunityPostCategories.General;
        request.WorkflowTag = NeighborhoodExchange.WorkflowTag;
        request.SalesOffer = null;
        request.IsInterestGatheringEnabled = false;
        request.커뮤니티원장Id = null;
        request.IsReportBoardPost = false;
        return posts.CreatePostAsync(request, ct);
    }
    public Task<IReadOnlyList<PlatformCommunityPostCommentResponse>> CommentsAsync(long id, CancellationToken ct) => posts.GetCommentsAsync(id, ct);
    public Task<PlatformCommunityPostCommentResponse?> CommentAsync(long id, PlatformCommunityPostCommentCreateRequest request, CancellationToken ct) => posts.CreateCommentAsync(id, request, ct);
    public Task DeleteAsync(long id, string? password, CancellationToken ct) => posts.DeletePostAsync(id, password, ct);
    public Task DeleteCommentAsync(long id, long commentId, string password, CancellationToken ct) => posts.DeleteCommentAsync(id, commentId, password, ct);
    public Task ReportCommentAsync(long commentId, CancellationToken ct) => posts.ReportCommentAsync(commentId, ct);
}
