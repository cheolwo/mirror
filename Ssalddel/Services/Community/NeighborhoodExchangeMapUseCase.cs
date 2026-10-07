using FluentResults;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Domain.Community;
using 살뜰.Data;

namespace Ssalddel.Services.Community;

public interface I생활교류지도조회UseCase
{
    Task<NeighborhoodExchangeRegionListResponse> 공개지역Async(CancellationToken cancellationToken);
    Task<Result<NeighborhoodExchangeMapResponse>> 지도Async(NeighborhoodExchangeMapQuery query, CancellationToken cancellationToken);
    Task<Result<PlatformCommunityPostListResponse>> 글목록Async(NeighborhoodExchangeMapQuery query, int page,
        int pageSize, CancellationToken cancellationToken);
}

[SsalddelCommunityV0Module(SsalddelCommunityV0ModuleKeys.Content, SsalddelModuleKind.Application,
    "공개 생활 교류 글을 검증된 행정동 대표점에 집계하고 동네별 목록을 조회",
    ReleaseStage = SsalddelCommunityV0ReleaseStages.Persistence,
    Boundary = "동네 대표점과 공개 글만 읽으며 사용자 GPS·정확한 물품 위치·비공개 배송 주소를 읽거나 반환하지 않습니다.")]
public sealed class 생활교류지도조회UseCase(SsalddelContext db, I생활교류공개지역Source regions,
    ICurrentUserAccessor currentUser) : I생활교류지도조회UseCase
{
    public async Task<NeighborhoodExchangeRegionListResponse> 공개지역Async(CancellationToken cancellationToken)
        => new() { Items = await regions.목록Async(cancellationToken) };

    public async Task<Result<NeighborhoodExchangeMapResponse>> 지도Async(NeighborhoodExchangeMapQuery query,
        CancellationToken cancellationToken)
    {
        var directory = await regions.목록Async(cancellationToken);
        var validation = Validate(query, directory);
        if (validation is not null) return Invalid<NeighborhoodExchangeMapResponse>(validation);
        var counts = await Filter(PublicPosts(), query)
            .GroupBy(post => new { post.PublicNeighborhoodRegionKey, post.RoleTag })
            .Select(group => new { group.Key.PublicNeighborhoodRegionKey, group.Key.RoleTag, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var regionByKey = directory.ToDictionary(region => region.RegionKey, StringComparer.Ordinal);
        var located = counts.Where(row => row.PublicNeighborhoodRegionKey is not null
                                          && regionByKey.ContainsKey(row.PublicNeighborhoodRegionKey))
            .GroupBy(row => row.PublicNeighborhoodRegionKey!, StringComparer.Ordinal);
        return Result.Ok(new NeighborhoodExchangeMapResponse
        {
            Items = located.Select(group => new NeighborhoodExchangeMapMarkerDto
                {
                    Region = regionByKey[group.Key],
                    OfferCount = group.Where(row => row.RoleTag == NeighborhoodExchange.Offer).Sum(row => row.Count),
                    NeedCount = group.Where(row => row.RoleTag == NeighborhoodExchange.Need).Sum(row => row.Count)
                }).OrderBy(marker => marker.Region.DisplayName, StringComparer.Ordinal).ToArray(),
            UnlocatedPostCount = counts.Where(row => row.PublicNeighborhoodRegionKey is null).Sum(row => row.Count),
            UnsupportedRegionPostCount = counts.Where(row => row.PublicNeighborhoodRegionKey is not null
                                                            && !regionByKey.ContainsKey(row.PublicNeighborhoodRegionKey))
                .Sum(row => row.Count)
        });
    }

    public async Task<Result<PlatformCommunityPostListResponse>> 글목록Async(NeighborhoodExchangeMapQuery query,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var directory = await regions.목록Async(cancellationToken);
        var validation = Validate(query, directory);
        if (validation is not null) return Invalid<PlatformCommunityPostListResponse>(validation);
        page = Math.Clamp(page, 1, 1_000_000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var filtered = Filter(PublicPosts(), query);
        var count = await filtered.CountAsync(cancellationToken);
        var posts = await filtered
            .Include(post => post.Attachments).ThenInclude(attachment => attachment.Comments)
            .Include(post => post.Comments.Where(comment => !comment.IsDeleted && !comment.IsOperatorHidden))
            .AsSplitQuery()
            .OrderByDescending(post => post.PublishedAtUtc ?? post.CreatedAtUtc).ThenByDescending(post => post.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return Result.Ok(new PlatformCommunityPostListResponse
        {
            Items = posts.Select(post => CommunityPostResponseMapper.ToResponse(post,
                currentUserId: currentUser.UserId, currentUserRole: currentUser.Role)).ToArray(),
            TotalCount = count,
            Page = page,
            PageSize = pageSize
        });
    }

    private IQueryable<PlatformCommunityPost> PublicPosts()
        => db.PlatformCommunityPosts.AsNoTracking().Where(post => !post.IsDeleted && !post.IsReportBoardPost
            && post.PublicationStatusCode == PlatformCommunityPostPublicationStatusCodes.Published
            && post.Category == PlatformCommunityPostCategories.General
            && post.WorkflowTag == NeighborhoodExchange.WorkflowTag
            && (post.RoleTag == NeighborhoodExchange.Offer || post.RoleTag == NeighborhoodExchange.Need));

    private static IQueryable<PlatformCommunityPost> Filter(IQueryable<PlatformCommunityPost> posts,
        NeighborhoodExchangeMapQuery query)
    {
        var key = NeighborhoodExchangeRegionSelectionPolicy.Normalize(query.PublicNeighborhoodRegionKey);
        if (key is not null) posts = posts.Where(post => post.PublicNeighborhoodRegionKey == key);
        var intent = query.Intent?.Trim();
        if (!string.IsNullOrWhiteSpace(intent)) posts = posts.Where(post => post.RoleTag == intent);
        return posts;
    }

    private static string? Validate(NeighborhoodExchangeMapQuery query, IReadOnlyList<NeighborhoodPublicRegionDto> directory)
    {
        if (!string.IsNullOrWhiteSpace(query.Intent) && !NeighborhoodExchange.IsIntent(query.Intent.Trim()))
            return "제공해요 또는 필요해요로 구분을 선택해 주세요.";
        var key = NeighborhoodExchangeRegionSelectionPolicy.Normalize(query.PublicNeighborhoodRegionKey);
        return key is not null && !directory.Any(region => region.RegionKey == key)
            ? "지원하는 공개 동네를 선택해 주세요." : null;
    }

    private static Result<T> Invalid<T>(string message)
        => Result.Fail<T>(new Error(message).WithMetadata("StatusCode", 400));
}
