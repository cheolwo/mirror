using FluentResults;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed partial class 커뮤니티게시글UseCase
{
    public Task<Result<PlatformCommunityPostListResponse>> 목록Async(
        string? appKey,
        string? category,
        string? boardKey,
        string? workflowTag,
        string? roleTag,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        => _readUseCase.목록Async(
            appKey,
            category,
            boardKey,
            workflowTag,
            roleTag,
            page,
            pageSize,
            cancellationToken);

    public Task<Result<IReadOnlyList<CommunityBoardSummaryResponse>>> 게시판요약목록Async(
        string? appKey,
        CancellationToken cancellationToken)
        => _readUseCase.게시판요약목록Async(appKey, cancellationToken);

    public Task<Result<PlatformCommunityPostResponse>> 상세Async(
        long id,
        CancellationToken cancellationToken)
        => _readUseCase.상세Async(id, cancellationToken);
}
