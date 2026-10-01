using FluentResults;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed partial class 커뮤니티게시글UseCase
{
    public Task<Result<PlatformCommunityPostResponse>> 수정Async(
        long id,
        PlatformCommunityPostUpdateRequest? request,
        CancellationToken cancellationToken)
        => _publishingUseCase.수정Async(id, request, cancellationToken);

    public Task<Result<PlatformCommunityPostResponse>> 운영자고정Async(
        long id,
        PlatformCommunityPostOperatorPinRequest? request,
        CancellationToken cancellationToken)
        => _moderationUseCase.운영자고정Async(id, request, cancellationToken);

    public Task<Result<PlatformCommunityPostResponse>> 추천Async(
        long id,
        PlatformCommunityPostRecommendationRequest? request,
        string fallbackRecommenderKey,
        CancellationToken cancellationToken)
        => _participationUseCase.추천Async(
            id,
            request,
            fallbackRecommenderKey,
            cancellationToken);

    public Task<Result> 삭제Async(
        long id,
        PlatformCommunityPostPasswordRequest? request,
        CancellationToken cancellationToken)
        => _publishingUseCase.삭제Async(id, request, cancellationToken);
}
