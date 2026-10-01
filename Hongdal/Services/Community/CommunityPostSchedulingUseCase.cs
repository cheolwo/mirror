using FluentResults;
using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Metadata;
using Hongdal.Domain.Community;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Community;

[HongdalCommunityV0Module(
    HongdalCommunityV0ModuleKeys.Authoring,
    HongdalModuleKind.Application,
    "게시글 예약 생성과 예약 상태 조회·취소를 처리",
    ReleaseStage = HongdalCommunityV0ReleaseStages.Persistence,
    Boundary = "운영자가 명시한 예약 시각과 취소만 기록하며 예약 게시글을 직접 공개 상태로 전환하지 않습니다.")]
public sealed class 커뮤니티게시글예약발행UseCase : I커뮤니티게시글예약발행UseCase
{
    private readonly 커뮤니티게시글생성Service _creationService;
    private readonly HongdalContext _db;

    public 커뮤니티게시글예약발행UseCase(
        커뮤니티게시글생성Service creationService,
        HongdalContext db)
    {
        _creationService = creationService;
        _db = db;
    }

    public Task<Result<PlatformCommunityPostResponse>> 예약Async(
        PlatformCommunityPostScheduleCreateRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Task.FromResult(BadRequest<PlatformCommunityPostResponse>(
                "request body is required"));
        }

        var scheduledPublishAtUtc = CommunityPostWritePolicy.EnsureUtc(
            request.ScheduledPublishAtUtc);
        var now = DateTime.UtcNow;
        if (scheduledPublishAtUtc < now.Add(PlatformCommunityPostSchedulePolicy.MinimumLeadTime)
            || scheduledPublishAtUtc > now.Add(PlatformCommunityPostSchedulePolicy.MaximumLeadTime))
        {
            return Task.FromResult(BadRequest<PlatformCommunityPostResponse>(
                "예약 발행 시각은 현재부터 1분 이후, 365일 이내여야 합니다."));
        }

        return _creationService.CreateAsync(request.Post, scheduledPublishAtUtc, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<PlatformCommunityPostResponse>>> 예약목록Async(
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status)
            ? null
            : status.Trim().ToLowerInvariant();
        if (normalizedStatus is not null
            && !PlatformCommunityPostPublicationStatuses.IsSupported(normalizedStatus))
        {
            return BadRequest<IReadOnlyList<PlatformCommunityPostResponse>>(
                "지원하지 않는 예약 발행 상태입니다.");
        }

        var query = _db.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(post => !post.IsDeleted);
        query = normalizedStatus is null
            ? query.Where(post =>
                post.PublicationStatusCode != PlatformCommunityPostPublicationStatusCodes.Published)
            : query.Where(post => post.PublicationStatusCode == normalizedStatus);
        var items = await query
            .OrderBy(post => post.ScheduledPublishAtUtc ?? DateTime.MaxValue)
            .ThenByDescending(post => post.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
        return Result.Ok<IReadOnlyList<PlatformCommunityPostResponse>>(
            items.Select(post => CommunityPostResponseMapper.ToResponse(post)).ToArray());
    }

    public async Task<Result<PlatformCommunityPostResponse>> 예약취소Async(
        long id,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var cancelled = await _db.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .Where(post => post.Id == id
                           && !post.IsDeleted
                           && post.PublicationStatusCode
                           == PlatformCommunityPostPublicationStatusCodes.Scheduled)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(
                        post => post.PublicationStatusCode,
                        PlatformCommunityPostPublicationStatusCodes.Cancelled)
                    .SetProperty(post => post.PublicationNextAttemptAtUtc, (DateTime?)null)
                    .SetProperty(post => post.PublicationClaimedAtUtc, (DateTime?)null)
                    .SetProperty(post => post.UpdatedAtUtc, now),
                cancellationToken);
        if (cancelled == 0)
        {
            var exists = await _db.PlatformCommunityPosts
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(post => post.Id == id && !post.IsDeleted, cancellationToken);
            return exists
                ? BadRequest<PlatformCommunityPostResponse>(
                    "발행 대기 중인 예약 게시글만 취소할 수 있습니다.")
                : NotFound("예약 게시글을 찾을 수 없습니다.");
        }

        var post = await _db.PlatformCommunityPosts
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(item => item.Id == id, cancellationToken);
        return Result.Ok(CommunityPostResponseMapper.ToResponse(post));
    }

    private static Result<T> BadRequest<T>(string message) => Result.Fail<T>(message);

    private static Result<PlatformCommunityPostResponse> NotFound(string message)
        => Result.Fail<PlatformCommunityPostResponse>(
            new Error(message).WithMetadata("StatusCode", StatusCodes.Status404NotFound));
}
