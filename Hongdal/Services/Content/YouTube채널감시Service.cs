using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Hongdal.Services.External.YouTube;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Services.Content;

public interface IYouTube채널감시Service
{
    Task<IReadOnlyList<YouTube채널검색Dto>> 채널검색Async(
        string 검색어,
        int take,
        string? regionCode,
        string? languageCode,
        CancellationToken cancellationToken);

    Task<YouTube감시채널Dto> 채널등록Async(
        YouTube감시채널등록요청Dto 요청,
        CancellationToken cancellationToken);

    Task<YouTube감시채널Dto> 음식채널프로필설정Async(
        string channelId,
        YouTube음식채널프로필설정요청Dto 요청,
        CancellationToken cancellationToken);

    Task<YouTube감시채널Dto> 지식성찰채널프로필설정Async(
        string channelId,
        YouTube지식성찰채널프로필설정요청Dto 요청,
        CancellationToken cancellationToken);

    Task<YouTube감시채널Dto> 반야게시채널설정Async(
        string channelId,
        bool 허용여부,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube감시채널Dto>> 채널목록조회Async(CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube감시채널Dto>> 채널목록조회Async(
        string? 국가코드,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube채널영상Dto>> 영상목록조회Async(
        string? channelId,
        bool 신규업로드만,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube채널영상Dto>> 공개영상목록조회Async(
        string? channelId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube재생목록Dto>> 재생목록목록조회Async(
        string channelId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<YouTube재생목록영상Dto>> 재생목록영상목록조회Async(
        string playlistId,
        int take,
        CancellationToken cancellationToken);

    Task<YouTube채널영상Dto> 영상공개설정Async(
        string videoId,
        bool 공개여부,
        CancellationToken cancellationToken);

    Task<YouTube채널동기화결과Dto> 동기화Async(
        string? channelId,
        CancellationToken cancellationToken);

    Task<YouTube국가별채널동기화결과Dto> 국가별동기화Async(
        string 국가코드,
        CancellationToken cancellationToken);
}

public sealed class YouTube채널감시Service : IYouTube채널감시Service
{
    private readonly IYouTubeDataApiClient _client;
    private readonly IYouTube채널감시저장소 _저장소;
    private readonly YouTubeOptions _options;

    public YouTube채널감시Service(
        IYouTubeDataApiClient client,
        IYouTube채널감시저장소 저장소,
        IOptions<YouTubeOptions> options)
    {
        _client = client;
        _저장소 = 저장소;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<YouTube채널검색Dto>> 채널검색Async(
        string 검색어,
        int take,
        string? regionCode,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var collectionCountryCode = YouTube채널수집국가코드.정규화(regionCode);
        return (await _client.채널검색Async(
                검색어,
                Math.Clamp(take, 1, 25),
                collectionCountryCode == YouTube채널수집국가코드.미분류
                    ? null
                    : collectionCountryCode,
                languageCode,
                cancellationToken))
            .Select(item => new YouTube채널검색Dto(
                item.ChannelId,
                item.채널명,
                item.설명,
                item.게시일시Utc,
                item.썸네일Url,
                $"https://www.youtube.com/channel/{Uri.EscapeDataString(item.ChannelId)}",
                collectionCountryCode))
            .ToArray();
    }

    public async Task<YouTube감시채널Dto> 채널등록Async(
        YouTube감시채널등록요청Dto 요청,
        CancellationToken cancellationToken)
    {
        var channelId = NormalizeId(요청.ChannelId);
        if (await _저장소.추적조회Async(channelId, cancellationToken) is not null)
        {
            throw new InvalidOperationException("이미 감시 중인 YouTube 채널입니다.");
        }

        var 채널 = await 원격채널생성Async(channelId, 요청.표시이름, cancellationToken);
        채널.국가코드 = YouTube채널수집국가코드.정규화(요청.국가코드);
        채널.음식채널여부 = 요청.음식채널여부;
        var 조사 = YouTube음식채널조사Catalog.찾기(channelId);
        if (조사 is not null)
        {
            조사프로필적용(채널, 조사);
        }
        var 지식성찰항목 = YouTube지식성찰채널Catalog.찾기(channelId);
        if (지식성찰항목 is not null)
        {
            지식성찰Catalog프로필적용(채널, 지식성찰항목);
        }

        _저장소.채널추가(채널);
        await _저장소.저장Async(cancellationToken);
        return ToChannelDto(채널);
    }

    public async Task<YouTube감시채널Dto> 음식채널프로필설정Async(
        string channelId,
        YouTube음식채널프로필설정요청Dto 요청,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var 채널 = await _저장소.추적조회Async(NormalizeId(channelId), cancellationToken)
            ?? throw new InvalidOperationException("음식 프로필을 설정할 YouTube 채널을 찾지 못했습니다.");
        var categories = 음식분류정규화(요청.분류코드목록);

        채널.음식채널여부 = 요청.음식채널여부;
        채널.Handle = NormalizeOptional(요청.Handle, 100);
        채널.국가코드 = NormalizeCountryCode(요청.국가코드);
        채널.기본언어코드 = NormalizeRequired(요청.기본언어코드, "ko", 10);
        채널.음식콘텐츠분류 = string.Join(',', categories);
        채널.구매발견점수 = NormalizeScore(요청.구매발견점수, nameof(요청.구매발견점수));
        채널.수입발견점수 = NormalizeScore(요청.수입발견점수, nameof(요청.수입발견점수));
        채널.조사근거Url = NormalizeHttpsUrl(요청.조사근거Url, nameof(요청.조사근거Url));
        채널.조사메모 = NormalizeOptional(요청.조사메모, 1000);
        채널.조사확인일시Utc = 요청.조사확인일시Utc?.ToUniversalTime() ?? DateTime.UtcNow;
        채널.수정일시Utc = DateTime.UtcNow;

        await _저장소.저장Async(cancellationToken);
        return ToChannelDto(채널);
    }

    public async Task<YouTube감시채널Dto> 지식성찰채널프로필설정Async(
        string channelId,
        YouTube지식성찰채널프로필설정요청Dto 요청,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var 채널 = await _저장소.추적조회Async(NormalizeId(channelId), cancellationToken)
            ?? throw new InvalidOperationException("지식·성찰 프로필을 설정할 YouTube 채널을 찾지 못했습니다.");
        var topics = 지식성찰분류정규화(요청.주제코드목록);

        채널.지식성찰채널여부 = 요청.지식성찰채널여부;
        채널.Handle = NormalizeOptional(요청.Handle, 100);
        채널.국가코드 = NormalizeCountryCode(요청.국가코드);
        채널.기본언어코드 = NormalizeRequired(요청.기본언어코드, "ko", 10);
        채널.지식성찰분류 = string.Join(',', topics);
        채널.관점표시 = NormalizeRequired(요청.관점표시, "지식·성찰", 200);
        채널.공식출처Url = NormalizeHttpsUrl(요청.공식출처Url, nameof(요청.공식출처Url));
        채널.자료확인일시Utc = 요청.자료확인일시Utc?.ToUniversalTime() ?? DateTime.UtcNow;
        if (!채널.지식성찰채널여부)
        {
            채널.반야게시허용여부 = false;
        }

        채널.수정일시Utc = DateTime.UtcNow;
        await _저장소.저장Async(cancellationToken);
        return ToChannelDto(채널);
    }

    public async Task<YouTube감시채널Dto> 반야게시채널설정Async(
        string channelId,
        bool 허용여부,
        CancellationToken cancellationToken)
    {
        var 채널 = await _저장소.추적조회Async(NormalizeId(channelId), cancellationToken)
            ?? throw new InvalidOperationException("반야 게시 여부를 설정할 YouTube 채널을 찾지 못했습니다.");
        if (허용여부 && !채널.지식성찰채널여부)
        {
            throw new InvalidOperationException("지식·성찰 채널 프로필을 먼저 확인한 뒤 반야 게시를 허용할 수 있습니다.");
        }

        채널.반야게시허용여부 = 허용여부;
        채널.수정일시Utc = DateTime.UtcNow;
        await _저장소.저장Async(cancellationToken);
        return ToChannelDto(채널);
    }

    public async Task<IReadOnlyList<YouTube감시채널Dto>> 채널목록조회Async(
        CancellationToken cancellationToken)
        => await 채널목록조회Async(null, cancellationToken);

    public async Task<IReadOnlyList<YouTube감시채널Dto>> 채널목록조회Async(
        string? 국가코드,
        CancellationToken cancellationToken)
    {
        var normalizedCountryCode = string.IsNullOrWhiteSpace(국가코드)
            ? null
            : YouTube채널수집국가코드.정규화(국가코드);
        return (await _저장소.채널목록조회Async(cancellationToken))
            .Where(channel => normalizedCountryCode is null
                || YouTube채널수집국가코드.정규화(channel.국가코드) == normalizedCountryCode)
            .Select(ToChannelDto)
            .ToArray();
    }

    public async Task<IReadOnlyList<YouTube채널영상Dto>> 영상목록조회Async(
        string? channelId,
        bool 신규업로드만,
        int take,
        CancellationToken cancellationToken)
        => (await _저장소.영상목록조회Async(channelId?.Trim(), 신규업로드만, take, cancellationToken))
            .Select(ToVideoDto)
            .ToArray();

    public async Task<IReadOnlyList<YouTube채널영상Dto>> 공개영상목록조회Async(
        string? channelId,
        int take,
        CancellationToken cancellationToken)
        => (await _저장소.공개영상목록조회Async(channelId?.Trim(), take, cancellationToken))
            .Select(ToVideoDto)
            .ToArray();

    public async Task<IReadOnlyList<YouTube재생목록Dto>> 재생목록목록조회Async(
        string channelId,
        CancellationToken cancellationToken)
        => (await _client.재생목록목록조회Async(NormalizeId(channelId), cancellationToken))
            .Select(ToPlaylistDto)
            .ToArray();

    public async Task<IReadOnlyList<YouTube재생목록영상Dto>> 재생목록영상목록조회Async(
        string playlistId,
        int take,
        CancellationToken cancellationToken)
        => (await _client.재생목록영상조회Async(
                NormalizePlaylistId(playlistId),
                Math.Clamp(take, 1, 200),
                cancellationToken))
            .Select(ToPlaylistVideoDto)
            .ToArray();

    public async Task<YouTube채널영상Dto> 영상공개설정Async(
        string videoId,
        bool 공개여부,
        CancellationToken cancellationToken)
    {
        var normalizedVideoId = NormalizeVideoId(videoId);
        var video = await _저장소.영상추적조회Async(normalizedVideoId, cancellationToken)
            ?? throw new InvalidOperationException("설정할 YouTube 영상을 찾지 못했습니다.");

        video.공유상태 = 공개여부
            ? YouTube채널영상.공개상태
            : YouTube채널영상.숨김상태;
        await _저장소.저장Async(cancellationToken);
        return ToVideoDto(video);
    }

    public async Task<YouTube채널동기화결과Dto> 동기화Async(
        string? channelId,
        CancellationToken cancellationToken)
        => await 동기화내부Async(channelId, null, cancellationToken);

    public async Task<YouTube국가별채널동기화결과Dto> 국가별동기화Async(
        string 국가코드,
        CancellationToken cancellationToken)
    {
        var normalizedCountryCode = YouTube채널수집국가코드.정규화(국가코드);
        var result = await 동기화내부Async(null, normalizedCountryCode, cancellationToken);
        return new YouTube국가별채널동기화결과Dto(
            normalizedCountryCode,
            YouTube채널수집국가코드.표시명(normalizedCountryCode),
            result);
    }

    private async Task<YouTube채널동기화결과Dto> 동기화내부Async(
        string? channelId,
        string? 국가코드,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return new YouTube채널동기화결과Dto(
                false, 0, 0, 0, 0, null, "YouTube Data API가 비활성화되어 있습니다.");
        }

        if (string.IsNullOrWhiteSpace(channelId))
        {
            await 기본감시채널확보Async(국가코드, cancellationToken);
        }

        IReadOnlyList<YouTube감시채널> channels;
        if (string.IsNullOrWhiteSpace(channelId))
        {
            channels = 국가코드 is null
                ? await _저장소.활성채널추적조회Async(cancellationToken)
                : await _저장소.국가별활성채널추적조회Async(국가코드, cancellationToken);
        }
        else
        {
            var channel = await _저장소.추적조회Async(NormalizeId(channelId), cancellationToken)
                ?? throw new InvalidOperationException("감시 중인 YouTube 채널을 찾지 못했습니다.");
            channels = [channel];
        }

        var now = DateTime.UtcNow;
        var receivedCount = 0;
        var addedCount = 0;
        var newUploadCount = 0;

        foreach (var channel in channels)
        {
            var remoteVideos = await _client.업로드목록조회Async(
                channel.UploadsPlaylistId,
                _options.MaxResultsPerChannel,
                cancellationToken);
            receivedCount += remoteVideos.Count;

            var candidateIds = remoteVideos.Select(x => x.VideoId).ToArray();
            var existingIds = await _저장소.기존영상Id조회Async(
                channel.ChannelId,
                candidateIds,
                cancellationToken);
            var isInitialSync = !channel.초기동기화완료여부;

            foreach (var remote in remoteVideos.OrderBy(x => x.게시일시Utc))
            {
                if (!existingIds.Add(remote.VideoId))
                {
                    continue;
                }

                var isNewUpload = !isInitialSync;
                var video = new YouTube채널영상
                {
                    감시채널 = channel,
                    VideoId = remote.VideoId,
                    ChannelId = channel.ChannelId,
                    제목 = remote.제목,
                    설명 = remote.설명,
                    게시일시Utc = remote.게시일시Utc,
                    썸네일Url = remote.썸네일Url,
                    신규업로드여부 = isNewUpload,
                    공유상태 = isNewUpload
                        ? YouTube채널영상.공유대기상태
                        : YouTube채널영상.기준선공유상태,
                    최초감지일시Utc = now
                };
                _저장소.영상추가(video);
                addedCount++;
                if (isNewUpload)
                {
                    newUploadCount++;
                }
            }

            var latest = remoteVideos.MaxBy(x => x.게시일시Utc);
            if (latest is not null)
            {
                channel.마지막영상Id = latest.VideoId;
                channel.마지막영상게시일시Utc = latest.게시일시Utc;
            }

            channel.초기동기화완료여부 = true;
            channel.마지막동기화일시Utc = now;
            channel.수정일시Utc = now;
        }

        if (channels.Count > 0)
        {
            await _저장소.저장Async(cancellationToken);
        }

        return new YouTube채널동기화결과Dto(
            true,
            channels.Count,
            receivedCount,
            addedCount,
            newUploadCount,
            now,
            newUploadCount > 0
                ? $"{국가표시접두어(국가코드)}새 YouTube 영상 {newUploadCount}건을 감지했습니다."
                : $"{국가표시접두어(국가코드)}YouTube 채널 동기화를 완료했습니다.");
    }

    private async Task 기본감시채널확보Async(
        string? 국가코드,
        CancellationToken cancellationToken)
    {
        if (_options.SeedKnowledgeReflectionCatalog)
        {
            await 지식성찰Catalog확보Async(국가코드, cancellationToken);
        }

        var 설정목록 = new List<(string ChannelId, string? DisplayName, string CountryCode)>();
        설정목록.AddRange((_options.DefaultChannels ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.ChannelId))
            .Select(item => (
                item.ChannelId,
                item.DisplayName,
                YouTube채널수집국가코드.정규화(item.CountryCode))));

        if (_options.SeedFoodResearchCatalog)
        {
            설정목록.AddRange(YouTube음식채널조사Catalog.항목
                .Select(item => (item.ChannelId, (string?)item.채널명, item.국가코드)));
        }

        if (국가코드 is not null)
        {
            설정목록 = 설정목록
                .Where(item => item.CountryCode == 국가코드)
                .ToList();
        }

        if (설정목록.Count == 0)
        {
            return;
        }

        var 설정채널Ids = new HashSet<string>(StringComparer.Ordinal);
        var 추가됨 = false;

        foreach (var 기본채널 in 설정목록)
        {
            if (string.IsNullOrWhiteSpace(기본채널.ChannelId))
            {
                continue;
            }

            var channelId = NormalizeId(기본채널.ChannelId);
            if (!설정채널Ids.Add(channelId))
            {
                continue;
            }

            var 채널 = await _저장소.추적조회Async(channelId, cancellationToken);
            if (채널 is null)
            {
                채널 = await 원격채널생성Async(
                    channelId,
                    기본채널.DisplayName,
                    cancellationToken);
                _저장소.채널추가(채널);
                추가됨 = true;
            }

            // 관리자가 비활성화한 기존 채널은 기본 설정이나 카탈로그 동기화가
            // 메타데이터까지 암묵적으로 되살리거나 변경하지 않도록 그대로 둔다.
            if (!채널.활성화여부)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(채널.국가코드))
            {
                채널.국가코드 = 기본채널.CountryCode;
                채널.수정일시Utc = DateTime.UtcNow;
                추가됨 = true;
            }

            var 조사 = YouTube음식채널조사Catalog.찾기(channelId);
            if (조사 is not null && 조사프로필적용(채널, 조사))
            {
                추가됨 = true;
            }

            var 지식성찰항목 = YouTube지식성찰채널Catalog.찾기(channelId);
            if (지식성찰항목 is not null && 지식성찰Catalog프로필적용(채널, 지식성찰항목))
            {
                추가됨 = true;
            }
        }

        if (추가됨)
        {
            await _저장소.저장Async(cancellationToken);
        }
    }

    private async Task 지식성찰Catalog확보Async(
        string? 국가코드,
        CancellationToken cancellationToken)
    {
        var items = YouTube지식성찰채널Catalog.항목
            .Where(item => 국가코드 is null || item.국가코드 == 국가코드)
            .ToArray();
        var changed = false;
        foreach (var item in items)
        {
            YouTube채널응답? remote = null;
            var channelId = item.ChannelId;
            if (string.IsNullOrWhiteSpace(channelId))
            {
                if (string.IsNullOrWhiteSpace(item.Handle))
                {
                    continue;
                }

                remote = await _client.채널Handle조회Async(item.Handle, cancellationToken)
                    ?? throw new InvalidOperationException($"YouTube handle을 채널로 해석하지 못했습니다: {item.Handle}");
                channelId = remote.ChannelId;
            }

            var 채널 = await _저장소.추적조회Async(NormalizeId(channelId), cancellationToken);
            if (채널 is null)
            {
                remote ??= await _client.채널조회Async(channelId, cancellationToken)
                    ?? throw new InvalidOperationException($"YouTube 채널을 찾지 못했습니다: {channelId}");
                채널 = 원격채널생성(remote, item.표시이름);
                _저장소.채널추가(채널);
                changed = true;
            }

            if (지식성찰Catalog프로필적용(채널, item))
            {
                changed = true;
            }
        }

        if (changed)
        {
            await _저장소.저장Async(cancellationToken);
        }
    }

    private async Task<YouTube감시채널> 원격채널생성Async(
        string channelId,
        string? 표시이름,
        CancellationToken cancellationToken)
    {
        var remote = await _client.채널조회Async(channelId, cancellationToken)
            ?? throw new InvalidOperationException("YouTube 채널을 찾지 못했거나 업로드 목록을 확인할 수 없습니다.");

        return 원격채널생성(remote, 표시이름);
    }

    private static YouTube감시채널 원격채널생성(
        YouTube채널응답 remote,
        string? 표시이름)
    {
        var now = DateTime.UtcNow;
        return new YouTube감시채널
        {
            ChannelId = remote.ChannelId,
            채널명 = string.IsNullOrWhiteSpace(표시이름) ? remote.채널명 : 표시이름.Trim(),
            UploadsPlaylistId = remote.UploadsPlaylistId,
            썸네일Url = remote.썸네일Url,
            생성일시Utc = now,
            수정일시Utc = now
        };
    }

    private static string NormalizeId(string channelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        var normalized = channelId.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(channelId), "YouTube 채널 ID가 너무 깁니다.");
        }

        return normalized;
    }

    private static string NormalizeVideoId(string videoId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoId);
        var normalized = videoId.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(videoId), "YouTube 영상 ID가 너무 깁니다.");
        }

        return normalized;
    }

    private static string NormalizePlaylistId(string playlistId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(playlistId);
        var normalized = playlistId.Trim();
        if (normalized.Length > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(playlistId), "YouTube 재생목록 ID가 너무 깁니다.");
        }

        return normalized;
    }

    private static YouTube감시채널Dto ToChannelDto(YouTube감시채널 channel)
        => new(
            channel.ChannelId,
            channel.채널명,
            channel.썸네일Url,
            channel.활성화여부,
            channel.초기동기화완료여부,
            channel.마지막동기화일시Utc,
            channel.마지막영상Id,
            channel.마지막영상게시일시Utc,
            channel.음식채널여부,
            channel.Handle,
            YouTube채널수집국가코드.정규화(channel.국가코드),
            channel.기본언어코드,
            분류목록(channel.음식콘텐츠분류),
            channel.구매발견점수,
            channel.수입발견점수,
            channel.조사근거Url,
            channel.조사메모,
            channel.조사확인일시Utc,
            channel.지식성찰채널여부,
            분류목록(channel.지식성찰분류),
            channel.관점표시,
            channel.공식출처Url,
            channel.자료확인일시Utc,
            channel.반야게시허용여부);

    private static YouTube채널영상Dto ToVideoDto(YouTube채널영상 video)
        => new(
            video.VideoId,
            video.ChannelId,
            video.감시채널?.채널명 ?? string.Empty,
            YouTube채널수집국가코드.정규화(video.감시채널?.국가코드),
            video.제목,
            video.설명,
            video.게시일시Utc,
            video.썸네일Url,
            $"https://www.youtube.com/watch?v={Uri.EscapeDataString(video.VideoId)}",
            video.신규업로드여부,
            video.공유상태,
            video.최초감지일시Utc);

    private static YouTube재생목록Dto ToPlaylistDto(YouTube재생목록응답 playlist)
        => new(
            playlist.PlaylistId,
            playlist.ChannelId,
            playlist.제목,
            playlist.설명,
            playlist.게시일시Utc,
            playlist.영상수,
            playlist.썸네일Url,
            $"https://www.youtube.com/playlist?list={Uri.EscapeDataString(playlist.PlaylistId)}");

    private static YouTube재생목록영상Dto ToPlaylistVideoDto(YouTube영상응답 video)
        => new(
            video.VideoId,
            video.ChannelId,
            video.제목,
            video.설명,
            video.게시일시Utc,
            video.썸네일Url,
            $"https://www.youtube.com/watch?v={Uri.EscapeDataString(video.VideoId)}");

    private static bool 조사프로필적용(
        YouTube감시채널 채널,
        YouTube음식채널조사항목 조사)
    {
        var 분류 = string.Join(',', 음식분류정규화(조사.분류코드목록));
        var changed = !채널.음식채널여부
            || !string.Equals(채널.Handle, 조사.Handle, StringComparison.Ordinal)
            || !string.Equals(채널.국가코드, 조사.국가코드, StringComparison.Ordinal)
            || !string.Equals(채널.기본언어코드, 조사.기본언어코드, StringComparison.Ordinal)
            || !string.Equals(채널.음식콘텐츠분류, 분류, StringComparison.Ordinal)
            || 채널.구매발견점수 != 조사.구매발견점수
            || 채널.수입발견점수 != 조사.수입발견점수
            || !string.Equals(채널.조사근거Url, 조사.공식채널Url, StringComparison.Ordinal)
            || !string.Equals(채널.조사메모, 조사.조사메모, StringComparison.Ordinal)
            || 채널.조사확인일시Utc != 조사.조사확인일시Utc;
        if (!changed)
        {
            return false;
        }

        채널.음식채널여부 = true;
        채널.Handle = 조사.Handle;
        채널.국가코드 = YouTube채널수집국가코드.정규화(조사.국가코드);
        채널.기본언어코드 = 조사.기본언어코드;
        채널.음식콘텐츠분류 = 분류;
        채널.구매발견점수 = 조사.구매발견점수;
        채널.수입발견점수 = 조사.수입발견점수;
        채널.조사근거Url = 조사.공식채널Url;
        채널.조사메모 = 조사.조사메모;
        채널.조사확인일시Utc = 조사.조사확인일시Utc;
        채널.수정일시Utc = DateTime.UtcNow;
        return true;
    }

    private static bool 지식성찰Catalog프로필적용(
        YouTube감시채널 채널,
        YouTube지식성찰채널Catalog항목 item)
    {
        var categories = string.Join(',', 지식성찰분류정규화(item.주제코드목록));
        var changed = !채널.지식성찰채널여부
                      || !string.Equals(채널.Handle, item.Handle, StringComparison.OrdinalIgnoreCase)
                      || !string.Equals(채널.국가코드, item.국가코드, StringComparison.Ordinal)
                      || !string.Equals(채널.기본언어코드, item.기본언어코드, StringComparison.Ordinal)
                      || !string.Equals(채널.지식성찰분류, categories, StringComparison.Ordinal)
                      || !string.Equals(채널.관점표시, item.관점표시, StringComparison.Ordinal)
                      || !string.Equals(채널.공식출처Url, item.공식출처Url, StringComparison.Ordinal)
                      || 채널.자료확인일시Utc != item.자료확인일시Utc;
        if (!changed)
        {
            return false;
        }

        채널.지식성찰채널여부 = true;
        채널.Handle = item.Handle;
        채널.국가코드 = YouTube채널수집국가코드.정규화(item.국가코드);
        채널.기본언어코드 = item.기본언어코드;
        채널.지식성찰분류 = categories;
        채널.관점표시 = item.관점표시;
        채널.공식출처Url = item.공식출처Url;
        채널.자료확인일시Utc = item.자료확인일시Utc;
        채널.수정일시Utc = DateTime.UtcNow;
        return true;
    }

    private static IReadOnlyList<string> 음식분류정규화(IEnumerable<string>? categories)
    {
        var normalized = (categories ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknown = normalized.FirstOrDefault(item => !YouTube음식채널분류코드.전체.Contains(item));
        if (unknown is not null)
        {
            throw new ArgumentException($"지원하지 않는 YouTube 음식 채널 분류입니다: {unknown}");
        }

        return normalized;
    }

    private static IReadOnlyList<string> 지식성찰분류정규화(IEnumerable<string>? categories)
    {
        var normalized = (categories ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknown = normalized.FirstOrDefault(item => !YouTube지식성찰주제코드.전체.Contains(item));
        if (unknown is not null)
        {
            throw new ArgumentException($"지원하지 않는 YouTube 지식·성찰 채널 분류입니다: {unknown}");
        }

        return normalized;
    }

    private static IReadOnlyList<string> 분류목록(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    private static int NormalizeScore(int value, string parameterName)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(parameterName, "발견 점수는 0부터 100까지 입력해야 합니다.");
        }

        return value;
    }

    private static string NormalizeCountryCode(string? value)
        => YouTube채널수집국가코드.정규화(value);

    private static string 국가표시접두어(string? 국가코드)
        => 국가코드 is null
            ? string.Empty
            : $"[{YouTube채널수집국가코드.표시명(국가코드)}] ";

    private static string NormalizeRequired(string? value, string fallback, int maxLength)
        => NormalizeOptional(value, maxLength) ?? fallback;

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"입력값은 {maxLength}자 이하여야 합니다.");
        }

        return normalized;
    }

    private static string? NormalizeHttpsUrl(string? value, string parameterName)
    {
        var normalized = NormalizeOptional(value, 1000);
        if (normalized is not null
            && (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException("외부 URL은 HTTPS 절대 주소여야 합니다.", parameterName);
        }

        return normalized;
    }
}
