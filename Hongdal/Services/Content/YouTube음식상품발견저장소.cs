using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Content;

public interface IYouTube음식상품발견저장소
{
    Task<IReadOnlyList<YouTube감시채널>> 음식채널목록조회Async(
        string? 국가코드,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube감시채널>> 음식채널국가집계대상조회Async(
        CancellationToken cancellationToken);

    Task<YouTube채널영상?> 영상추적조회Async(
        string videoId,
        CancellationToken cancellationToken);

    Task<bool> 상품후보중복여부Async(
        long youtube채널영상Id,
        string 상품키,
        CancellationToken cancellationToken);

    Task<YouTube영상상품후보?> 상품후보추적조회Async(
        long 후보Id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube영상상품후보>> 상품후보목록조회Async(
        string? 검수상태,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube영상상품후보>> 공개상품후보목록조회Async(
        string? channelId,
        string? 국가코드,
        string? 후보유형,
        int take,
        CancellationToken cancellationToken);

    void 상품후보추가(YouTube영상상품후보 후보);

    Task 저장Async(CancellationToken cancellationToken);
}

public sealed class EfYouTube음식상품발견저장소 : IYouTube음식상품발견저장소
{
    private readonly HongdalContext _db;

    public EfYouTube음식상품발견저장소(HongdalContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<YouTube감시채널>> 음식채널목록조회Async(
        string? 국가코드,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube감시채널
            .AsNoTracking()
            .Where(channel => channel.음식채널여부 && channel.활성화여부);
        if (!string.IsNullOrWhiteSpace(국가코드))
        {
            query = 국가코드 == Contracts.Common.Content.YouTube채널수집국가코드.미분류
                ? query.Where(channel => channel.국가코드 == null || channel.국가코드 == string.Empty || channel.국가코드 == 국가코드)
                : query.Where(channel => channel.국가코드 == 국가코드);
        }

        return await query
            .OrderByDescending(channel => channel.구매발견점수)
            .ThenByDescending(channel => channel.수입발견점수)
            .ThenBy(channel => channel.채널명)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<YouTube감시채널>> 음식채널국가집계대상조회Async(
        CancellationToken cancellationToken)
        => await _db.YouTube감시채널
            .AsNoTracking()
            .Where(channel => channel.음식채널여부 && channel.활성화여부)
            .ToListAsync(cancellationToken);

    public Task<YouTube채널영상?> 영상추적조회Async(
        string videoId,
        CancellationToken cancellationToken)
        => _db.YouTube채널영상
            .Include(video => video.감시채널)
            .SingleOrDefaultAsync(video => video.VideoId == videoId, cancellationToken);

    public Task<bool> 상품후보중복여부Async(
        long youtube채널영상Id,
        string 상품키,
        CancellationToken cancellationToken)
        => _db.YouTube영상상품후보
            .AnyAsync(
                candidate => candidate.YouTube채널영상Id == youtube채널영상Id
                    && candidate.상품키 == 상품키,
                cancellationToken);

    public Task<YouTube영상상품후보?> 상품후보추적조회Async(
        long 후보Id,
        CancellationToken cancellationToken)
        => _db.YouTube영상상품후보
            .Include(candidate => candidate.영상)
            .ThenInclude(video => video!.감시채널)
            .SingleOrDefaultAsync(candidate => candidate.Id == 후보Id, cancellationToken);

    public async Task<IReadOnlyList<YouTube영상상품후보>> 상품후보목록조회Async(
        string? 검수상태,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube영상상품후보
            .AsNoTracking()
            .Include(candidate => candidate.영상)
            .ThenInclude(video => video!.감시채널)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(검수상태))
        {
            query = query.Where(candidate => candidate.검수상태 == 검수상태);
        }

        return await query
            .OrderByDescending(candidate => candidate.수정일시Utc)
            .ThenByDescending(candidate => candidate.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<YouTube영상상품후보>> 공개상품후보목록조회Async(
        string? channelId,
        string? 국가코드,
        string? 후보유형,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube영상상품후보
            .AsNoTracking()
            .Include(candidate => candidate.영상)
            .ThenInclude(video => video!.감시채널)
            .Where(candidate => candidate.검수상태 == Contracts.Common.Content.YouTube상품후보검수상태코드.승인
                && candidate.영상 != null
                && candidate.영상.공유상태 == YouTube채널영상.공개상태
                && candidate.영상.감시채널 != null
                && candidate.영상.감시채널.음식채널여부
                && candidate.영상.감시채널.활성화여부);

        if (!string.IsNullOrWhiteSpace(channelId))
        {
            query = query.Where(candidate => candidate.영상!.ChannelId == channelId);
        }

        if (!string.IsNullOrWhiteSpace(국가코드))
        {
            query = 국가코드 == Contracts.Common.Content.YouTube채널수집국가코드.미분류
                ? query.Where(candidate => candidate.영상!.감시채널!.국가코드 == null
                    || candidate.영상.감시채널.국가코드 == string.Empty
                    || candidate.영상.감시채널.국가코드 == 국가코드)
                : query.Where(candidate => candidate.영상!.감시채널!.국가코드 == 국가코드);
        }

        if (!string.IsNullOrWhiteSpace(후보유형))
        {
            query = query.Where(candidate => candidate.후보유형 == 후보유형);
        }

        return await query
            .OrderByDescending(candidate => candidate.영상!.게시일시Utc)
            .ThenByDescending(candidate => candidate.신뢰도)
            .ThenByDescending(candidate => candidate.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public void 상품후보추가(YouTube영상상품후보 후보)
        => _db.YouTube영상상품후보.Add(후보);

    public Task 저장Async(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);
}
