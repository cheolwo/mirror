using FluentResults;
using Hongdal.Contracts.Common.Community;

namespace Hongdal.Services.Community;

public sealed partial class 커뮤니티게시글UseCase
{
    public Task<Result<PlatformCommunityPostResponse>> 생성Async(
        PlatformCommunityPostCreateRequest? request,
        CancellationToken cancellationToken)
        => _publishingUseCase.생성Async(request, cancellationToken);

    public Task<Result<PlatformCommunityPostResponse>> 예약Async(
        PlatformCommunityPostScheduleCreateRequest? request,
        CancellationToken cancellationToken)
        => _schedulingUseCase.예약Async(request, cancellationToken);

    public Task<Result<IReadOnlyList<PlatformCommunityPostResponse>>> 예약목록Async(
        string? status,
        int take,
        CancellationToken cancellationToken)
        => _schedulingUseCase.예약목록Async(status, take, cancellationToken);

    public Task<Result<PlatformCommunityPostResponse>> 예약취소Async(
        long id,
        CancellationToken cancellationToken)
        => _schedulingUseCase.예약취소Async(id, cancellationToken);
}
