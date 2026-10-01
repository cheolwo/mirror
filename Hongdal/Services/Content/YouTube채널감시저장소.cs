using Hongdal.Domain.Content;
using Microsoft.EntityFrameworkCore;
using 홍달.Data;

namespace Hongdal.Services.Content;

public interface IYouTube채널감시저장소
{
    Task<YouTube감시채널?> 추적조회Async(
        string channelId,
        CancellationToken cancellationToken);

    Task<List<YouTube감시채널>> 활성채널추적조회Async(CancellationToken cancellationToken);

    Task<List<YouTube감시채널>> 국가별활성채널추적조회Async(
        string 국가코드,
        CancellationToken cancellationToken);

    Task<HashSet<string>> 기존영상Id조회Async(
        string channelId,
        IReadOnlyCollection<string> 후보VideoIds,
        CancellationToken cancellationToken);

    Task<YouTube채널영상?> 영상추적조회Async(
        string videoId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube감시채널>> 채널목록조회Async(CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube채널영상>> 영상목록조회Async(
        string? channelId,
        bool 신규업로드만,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube채널영상>> 공개영상목록조회Async(
        string? channelId,
        int take,
        CancellationToken cancellationToken);

    void 채널추가(YouTube감시채널 채널);

    void 영상추가(YouTube채널영상 영상);

    Task 저장Async(CancellationToken cancellationToken);
}

public sealed class EfYouTube채널감시저장소 : IYouTube채널감시저장소
{
    private readonly HongdalContext _db;

    public EfYouTube채널감시저장소(HongdalContext db)
    {
        _db = db;
    }

    public Task<YouTube감시채널?> 추적조회Async(
        string channelId,
        CancellationToken cancellationToken)
        => _db.YouTube감시채널
            .SingleOrDefaultAsync(x => x.ChannelId == channelId, cancellationToken);

    public Task<List<YouTube감시채널>> 활성채널추적조회Async(CancellationToken cancellationToken)
        => _db.YouTube감시채널
            .Where(x => x.활성화여부)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<List<YouTube감시채널>> 국가별활성채널추적조회Async(
        string 국가코드,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube감시채널.Where(channel => channel.활성화여부);
        query = 국가코드 == Contracts.Common.Content.YouTube채널수집국가코드.미분류
            ? query.Where(channel => channel.국가코드 == null || channel.국가코드 == string.Empty || channel.국가코드 == 국가코드)
            : query.Where(channel => channel.국가코드 == 국가코드);
        return query.OrderBy(channel => channel.Id).ToListAsync(cancellationToken);
    }

    public async Task<HashSet<string>> 기존영상Id조회Async(
        string channelId,
        IReadOnlyCollection<string> 후보VideoIds,
        CancellationToken cancellationToken)
    {
        if (후보VideoIds.Count == 0)
        {
            return [];
        }

        var ids = await _db.YouTube채널영상
            .AsNoTracking()
            .Where(x => x.ChannelId == channelId && 후보VideoIds.Contains(x.VideoId))
            .Select(x => x.VideoId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    public Task<YouTube채널영상?> 영상추적조회Async(
        string videoId,
        CancellationToken cancellationToken)
        => _db.YouTube채널영상
            .Include(x => x.감시채널)
            .SingleOrDefaultAsync(x => x.VideoId == videoId, cancellationToken);

    public async Task<IReadOnlyList<YouTube감시채널>> 채널목록조회Async(
        CancellationToken cancellationToken)
        => await _db.YouTube감시채널
            .AsNoTracking()
            .OrderBy(x => x.채널명)
            .ThenBy(x => x.ChannelId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<YouTube채널영상>> 영상목록조회Async(
        string? channelId,
        bool 신규업로드만,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube채널영상
            .AsNoTracking()
            .Include(x => x.감시채널)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(channelId))
        {
            query = query.Where(x => x.ChannelId == channelId);
        }

        if (신규업로드만)
        {
            query = query.Where(x => x.신규업로드여부);
        }

        return await query
            .OrderByDescending(x => x.게시일시Utc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<YouTube채널영상>> 공개영상목록조회Async(
        string? channelId,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.YouTube채널영상
            .AsNoTracking()
            .Include(x => x.감시채널)
            .Where(x => x.공유상태 == YouTube채널영상.공개상태);

        if (!string.IsNullOrWhiteSpace(channelId))
        {
            query = query.Where(x => x.ChannelId == channelId);
        }

        return await query
            .OrderByDescending(x => x.게시일시Utc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public void 채널추가(YouTube감시채널 채널)
        => _db.YouTube감시채널.Add(채널);

    public void 영상추가(YouTube채널영상 영상)
        => _db.YouTube채널영상.Add(영상);

    public Task 저장Async(CancellationToken cancellationToken)
        => _db.SaveChangesAsync(cancellationToken);
}
