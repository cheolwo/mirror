using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Hongdal.Services.Content;
using Hongdal.Services.External.YouTube;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTube채널감시ServiceTests
{
    [Fact]
    public async Task 국가별동기화는_선택한국가채널만수집한다()
    {
        var korean = CreateChannel();
        korean.국가코드 = YouTube채널수집국가코드.한국;
        var american = new YouTube감시채널
        {
            ChannelId = "UC_US",
            채널명 = "US Food Channel",
            UploadsPlaylistId = "UU_US",
            국가코드 = YouTube채널수집국가코드.미국
        };
        var store = new FakeStore(korean, american);
        var client = new FakeClient();
        var sut = CreateService(client, store);

        var result = await sut.국가별동기화Async(
            YouTube채널수집국가코드.한국,
            CancellationToken.None);

        Assert.Equal("KR", result.국가코드);
        Assert.Equal("한국", result.국가표시명);
        Assert.Equal(1, result.동기화결과.처리채널수);
        Assert.Equal(1, client.UploadCalls);
        Assert.NotNull(korean.마지막동기화일시Utc);
        Assert.Null(american.마지막동기화일시Utc);

        var koreanChannels = await sut.채널목록조회Async("kr", CancellationToken.None);
        Assert.Equal(korean.ChannelId, Assert.Single(koreanChannels).ChannelId);
    }

    [Fact]
    public async Task 전체동기화는_설정시_음식조사카탈로그를감시DB에확보한다()
    {
        var store = new FakeStore();
        var client = new FakeClient();
        var sut = new YouTube채널감시Service(
            client,
            store,
            Options.Create(new YouTubeOptions
            {
                Enabled = true,
                ApiKey = "test-key",
                SeedFoodResearchCatalog = true,
                MaxResultsPerChannel = 20
            }));

        var result = await sut.동기화Async(null, CancellationToken.None);

        Assert.True(result.실행됨);
        Assert.Equal(YouTube음식채널조사Catalog.항목.Count, result.처리채널수);
        Assert.Equal(YouTube음식채널조사Catalog.항목.Count, store.Channels.Count);
        Assert.All(store.Channels, channel =>
        {
            Assert.True(channel.음식채널여부);
            Assert.NotEmpty(channel.음식콘텐츠분류);
            Assert.InRange(channel.구매발견점수, 0, 100);
            Assert.NotNull(channel.조사확인일시Utc);
        });
    }

    [Fact]
    public async Task 지식성찰카탈로그는_명시적으로켰을때만_Handle을해석해모듈화한다()
    {
        var store = new FakeStore();
        var client = new FakeClient();
        var sut = new YouTube채널감시Service(
            client,
            store,
            Options.Create(new YouTubeOptions
            {
                Enabled = true,
                ApiKey = "test-key",
                SeedKnowledgeReflectionCatalog = true,
                MaxResultsPerChannel = 20
            }));

        var result = await sut.동기화Async(null, CancellationToken.None);

        Assert.True(result.실행됨);
        Assert.Equal(YouTube지식성찰채널Catalog.항목.Count, store.Channels.Count);
        Assert.Equal(
            YouTube지식성찰채널Catalog.항목.Count(item => !string.IsNullOrWhiteSpace(item.Handle)),
            client.HandleCalls);
        Assert.All(store.Channels, channel =>
        {
            Assert.True(channel.지식성찰채널여부);
            Assert.NotEmpty(channel.지식성찰분류);
            Assert.NotEmpty(channel.관점표시);
            Assert.NotNull(channel.공식출처Url);
            Assert.False(channel.반야게시허용여부);
        });
    }

    [Fact]
    public async Task 최초동기화_기존영상을_기준선으로저장한다()
    {
        var channel = CreateChannel();
        var store = new FakeStore(channel);
        var client = new FakeClient
        {
            Videos = [CreateVideo("video-1", new DateTime(2026, 7, 13, 1, 0, 0, DateTimeKind.Utc))]
        };
        var sut = CreateService(client, store);

        var result = await sut.동기화Async(null, CancellationToken.None);

        Assert.True(result.실행됨);
        Assert.Equal(1, result.추가영상수);
        Assert.Equal(0, result.신규업로드수);
        var saved = Assert.Single(channel.영상);
        Assert.False(saved.신규업로드여부);
        Assert.Equal(YouTube채널영상.기준선공유상태, saved.공유상태);
        Assert.True(channel.초기동기화완료여부);
    }

    [Fact]
    public async Task 후속동기화_새로발견한영상만_공유대기로표시한다()
    {
        var channel = CreateChannel();
        channel.초기동기화완료여부 = true;
        channel.영상.Add(new YouTube채널영상
        {
            VideoId = "video-1",
            ChannelId = channel.ChannelId,
            제목 = "기존 영상",
            게시일시Utc = new DateTime(2026, 7, 13, 1, 0, 0, DateTimeKind.Utc)
        });
        var store = new FakeStore(channel);
        var client = new FakeClient
        {
            Videos =
            [
                CreateVideo("video-2", new DateTime(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc)),
                CreateVideo("video-1", new DateTime(2026, 7, 13, 1, 0, 0, DateTimeKind.Utc))
            ]
        };
        var sut = CreateService(client, store);

        var result = await sut.동기화Async(channel.ChannelId, CancellationToken.None);

        Assert.Equal(1, result.추가영상수);
        Assert.Equal(1, result.신규업로드수);
        var added = Assert.Single(channel.영상, x => x.VideoId == "video-2");
        Assert.True(added.신규업로드여부);
        Assert.Equal(YouTube채널영상.공유대기상태, added.공유상태);
        Assert.Equal("video-2", channel.마지막영상Id);
    }

    [Fact]
    public async Task 비활성설정_외부API를호출하지않는다()
    {
        var client = new FakeClient();
        var store = new FakeStore(CreateChannel());
        var sut = new YouTube채널감시Service(
            client,
            store,
            Options.Create(new YouTubeOptions { Enabled = false }));

        var result = await sut.동기화Async(null, CancellationToken.None);

        Assert.False(result.실행됨);
        Assert.Equal(0, client.UploadCalls);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task 전체동기화_설정된기본채널을_DB에자동등록한다()
    {
        var store = new FakeStore();
        var client = new FakeClient();
        var sut = new YouTube채널감시Service(
            client,
            store,
            Options.Create(new YouTubeOptions
            {
                Enabled = true,
                MaxResultsPerChannel = 20,
                DefaultChannels =
                [
                    new YouTube기본감시채널Options
                    {
                        ChannelId = "UCI8HW08rOSlvweOjJ9Gp2Ng",
                        DisplayName = "홍익학당"
                    }
                ]
            }));

        var result = await sut.동기화Async(null, CancellationToken.None);

        Assert.True(result.실행됨);
        var channel = Assert.Single(store.Channels);
        Assert.Equal("UCI8HW08rOSlvweOjJ9Gp2Ng", channel.ChannelId);
        Assert.Equal("홍익학당", channel.채널명);
        Assert.True(channel.초기동기화완료여부);
        Assert.Equal(1, client.ChannelCalls);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public async Task 전체동기화_기본채널이이미비활성화되어있으면_기존상태를유지한다()
    {
        var existing = new YouTube감시채널
        {
            ChannelId = "UCI8HW08rOSlvweOjJ9Gp2Ng",
            채널명 = "홍익학당",
            UploadsPlaylistId = "UUI8HW08rOSlvweOjJ9Gp2Ng",
            활성화여부 = false
        };
        var store = new FakeStore(existing);
        var client = new FakeClient();
        var sut = new YouTube채널감시Service(
            client,
            store,
            Options.Create(new YouTubeOptions
            {
                Enabled = true,
                DefaultChannels =
                [
                    new YouTube기본감시채널Options
                    {
                        ChannelId = existing.ChannelId,
                        DisplayName = "홍익학당"
                    }
                ]
            }));

        var result = await sut.동기화Async(null, CancellationToken.None);

        Assert.True(result.실행됨);
        Assert.Equal(0, result.처리채널수);
        Assert.False(existing.활성화여부);
        Assert.Equal(0, client.ChannelCalls);
        Assert.Equal(0, client.UploadCalls);
        Assert.Equal(0, store.SaveCount);
    }

    [Fact]
    public async Task 관리자가_영상을공개하거나숨길수있다()
    {
        var channel = CreateChannel();
        var video = new YouTube채널영상
        {
            VideoId = "video-publication",
            ChannelId = channel.ChannelId,
            감시채널 = channel,
            제목 = "공개 설정 영상",
            설명 = string.Empty,
            게시일시Utc = DateTime.UtcNow,
            공유상태 = YouTube채널영상.공유대기상태
        };
        channel.영상.Add(video);
        var store = new FakeStore(channel);
        var sut = CreateService(new FakeClient(), store);

        var published = await sut.영상공개설정Async(video.VideoId, true, CancellationToken.None);
        var hidden = await sut.영상공개설정Async(video.VideoId, false, CancellationToken.None);

        Assert.Equal(YouTube채널영상.공개상태, published.공유상태);
        Assert.Equal(YouTube채널영상.숨김상태, hidden.공유상태);
        Assert.Equal(2, store.SaveCount);
    }

    [Fact]
    public async Task 반야채널허용은_지식성찰프로필확인후에만_별도로설정한다()
    {
        var channel = CreateChannel();
        var store = new FakeStore(channel);
        var sut = CreateService(new FakeClient(), store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.반야게시채널설정Async(
            channel.ChannelId,
            true,
            CancellationToken.None));
        await sut.지식성찰채널프로필설정Async(
            channel.ChannelId,
            new YouTube지식성찰채널프로필설정요청Dto(
                true,
                "@hongdal",
                "KR",
                "ko",
                [YouTube지식성찰주제코드.철학],
                "철학 대화",
                "https://www.youtube.com/@hongdal",
                DateTime.UtcNow),
            CancellationToken.None);

        var allowed = await sut.반야게시채널설정Async(channel.ChannelId, true, CancellationToken.None);

        Assert.True(allowed.지식성찰채널여부);
        Assert.True(allowed.반야게시허용여부);
        Assert.Equal([YouTube지식성찰주제코드.철학], allowed.지식성찰분류목록);
    }

    [Fact]
    public async Task 공개영상조회는_관리자가공개한영상만반환한다()
    {
        var channel = CreateChannel();
        channel.영상.Add(CreateStoredVideo(channel, "visible", YouTube채널영상.공개상태));
        channel.영상.Add(CreateStoredVideo(channel, "pending", YouTube채널영상.공유대기상태));
        channel.영상.Add(CreateStoredVideo(channel, "hidden", YouTube채널영상.숨김상태));
        var store = new FakeStore(channel);
        var sut = CreateService(new FakeClient(), store);

        var videos = await sut.공개영상목록조회Async(null, 20, CancellationToken.None);

        var video = Assert.Single(videos);
        Assert.Equal("visible", video.VideoId);
    }

    [Fact]
    public async Task 재생목록조회는_외부목록을_클라이언트용주소와함께반환한다()
    {
        var client = new FakeClient
        {
            Playlists =
            [
                new YouTube재생목록응답(
                    "PL_TEST",
                    "UC_TEST",
                    "홍익학당 강의",
                    "강의 설명",
                    new DateTime(2026, 7, 14, 1, 0, 0, DateTimeKind.Utc),
                    8,
                    "https://img.example/playlist.jpg")
            ]
        };
        var sut = CreateService(client, new FakeStore(CreateChannel()));

        var playlists = await sut.재생목록목록조회Async(" UC_TEST ", CancellationToken.None);

        var playlist = Assert.Single(playlists);
        Assert.Equal("PL_TEST", playlist.PlaylistId);
        Assert.Equal(8, playlist.영상수);
        Assert.Equal("https://www.youtube.com/playlist?list=PL_TEST", playlist.재생목록Url);
        Assert.Equal("UC_TEST", client.LastPlaylistChannelId);
    }

    private static YouTube채널감시Service CreateService(
        IYouTubeDataApiClient client,
        IYouTube채널감시저장소 store)
        => new(client, store, Options.Create(new YouTubeOptions
        {
            Enabled = true,
            ApiKey = "test-key",
            MaxResultsPerChannel = 20
        }));

    private static YouTube감시채널 CreateChannel()
        => new()
        {
            ChannelId = "UC_TEST",
            채널명 = "홍달 채널",
            UploadsPlaylistId = "UU_TEST"
        };

    private static YouTube영상응답 CreateVideo(string videoId, DateTime publishedAtUtc)
        => new(videoId, "UC_TEST", videoId, "설명", publishedAtUtc, null);

    private static YouTube채널영상 CreateStoredVideo(
        YouTube감시채널 channel,
        string videoId,
        string sharingStatus)
        => new()
        {
            VideoId = videoId,
            ChannelId = channel.ChannelId,
            감시채널 = channel,
            제목 = videoId,
            설명 = string.Empty,
            게시일시Utc = DateTime.UtcNow,
            공유상태 = sharingStatus
        };

    private sealed class FakeClient : IYouTubeDataApiClient
    {
        public IReadOnlyList<YouTube채널검색응답> SearchResults { get; set; } = [];

        public IReadOnlyList<YouTube영상응답> Videos { get; set; } = [];

        public IReadOnlyList<YouTube재생목록응답> Playlists { get; set; } = [];

        public int UploadCalls { get; private set; }

        public int ChannelCalls { get; private set; }

        public int HandleCalls { get; private set; }

        public string? LastPlaylistChannelId { get; private set; }

        public Task<IReadOnlyList<YouTube채널검색응답>> 채널검색Async(
            string 검색어,
            int maxResults,
            string? regionCode,
            string? relevanceLanguage,
            CancellationToken cancellationToken)
            => Task.FromResult(SearchResults);

        public Task<YouTube채널응답?> 채널조회Async(
            string channelId,
            CancellationToken cancellationToken)
        {
            ChannelCalls++;
            return Task.FromResult<YouTube채널응답?>(
                new(channelId, "홍달 채널", $"UU{channelId[2..]}", null));
        }

        public Task<YouTube채널응답?> 채널Handle조회Async(
            string handle,
            CancellationToken cancellationToken)
        {
            HandleCalls++;
            var normalized = handle.Trim().TrimStart('@').Replace("-", string.Empty, StringComparison.Ordinal);
            var channelId = $"UC_{normalized}";
            return Task.FromResult<YouTube채널응답?>(
                new(channelId, handle.TrimStart('@'), $"UU_{normalized}", null));
        }

        public Task<IReadOnlyList<YouTube영상응답>> 업로드목록조회Async(
            string uploadsPlaylistId,
            int maxResults,
            CancellationToken cancellationToken)
        {
            UploadCalls++;
            return Task.FromResult(Videos);
        }

        public Task<IReadOnlyList<YouTube재생목록응답>> 재생목록목록조회Async(
            string channelId,
            CancellationToken cancellationToken)
        {
            LastPlaylistChannelId = channelId;
            return Task.FromResult(Playlists);
        }

        public Task<IReadOnlyList<YouTube영상응답>> 재생목록영상조회Async(
            string playlistId,
            int maxResults,
            CancellationToken cancellationToken)
            => Task.FromResult(Videos);
    }

    private sealed class FakeStore : IYouTube채널감시저장소
    {
        public FakeStore(params YouTube감시채널[] channels)
        {
            Channels.AddRange(channels);
        }

        public List<YouTube감시채널> Channels { get; } = [];

        public int SaveCount { get; private set; }

        public Task<YouTube감시채널?> 추적조회Async(
            string channelId,
            CancellationToken cancellationToken)
            => Task.FromResult(Channels.SingleOrDefault(x => x.ChannelId == channelId));

        public Task<List<YouTube감시채널>> 활성채널추적조회Async(
            CancellationToken cancellationToken)
            => Task.FromResult(Channels.Where(x => x.활성화여부).ToList());

        public Task<List<YouTube감시채널>> 국가별활성채널추적조회Async(
            string 국가코드,
            CancellationToken cancellationToken)
            => Task.FromResult(Channels
                .Where(channel => channel.활성화여부)
                .Where(channel => YouTube채널수집국가코드.정규화(channel.국가코드) == 국가코드)
                .ToList());

        public Task<HashSet<string>> 기존영상Id조회Async(
            string channelId,
            IReadOnlyCollection<string> 후보VideoIds,
            CancellationToken cancellationToken)
            => Task.FromResult(Channels
                .Where(x => x.ChannelId == channelId)
                .SelectMany(x => x.영상)
                .Where(x => 후보VideoIds.Contains(x.VideoId))
                .Select(x => x.VideoId)
                .ToHashSet(StringComparer.Ordinal));

        public Task<YouTube채널영상?> 영상추적조회Async(
            string videoId,
            CancellationToken cancellationToken)
            => Task.FromResult(Channels
                .SelectMany(x => x.영상)
                .SingleOrDefault(x => x.VideoId == videoId));

        public Task<IReadOnlyList<YouTube감시채널>> 채널목록조회Async(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube감시채널>>(Channels.ToArray());

        public Task<IReadOnlyList<YouTube채널영상>> 영상목록조회Async(
            string? channelId,
            bool 신규업로드만,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube채널영상>>(
                Channels.SelectMany(x => x.영상).Take(take).ToArray());

        public Task<IReadOnlyList<YouTube채널영상>> 공개영상목록조회Async(
            string? channelId,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube채널영상>>(Channels
                .SelectMany(x => x.영상)
                .Where(x => x.공유상태 == YouTube채널영상.공개상태)
                .Where(x => string.IsNullOrWhiteSpace(channelId) || x.ChannelId == channelId)
                .OrderByDescending(x => x.게시일시Utc)
                .Take(take)
                .ToArray());

        public void 채널추가(YouTube감시채널 채널)
            => Channels.Add(채널);

        public void 영상추가(YouTube채널영상 영상)
        {
            var channel = Channels.Single(x => x.ChannelId == 영상.ChannelId);
            if (!channel.영상.Contains(영상))
            {
                channel.영상.Add(영상);
            }
        }

        public Task 저장Async(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
