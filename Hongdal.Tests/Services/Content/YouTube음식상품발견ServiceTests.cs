using System.Text.Json;
using Hongdal.Contracts.Common.Content;
using Hongdal.Contracts.Common.Orderer;
using Hongdal.Domain.Content;
using Hongdal.Services.Content;
using Hongdal.Services.Orderer;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTube음식상품발견ServiceTests
{
    [Fact]
    public async Task 음식채널은_한국과미국을별도집계하고필터링한다()
    {
        var graph = CreateGraph();
        graph.Channel.국가코드 = YouTube채널수집국가코드.한국;
        graph.Channel.초기동기화완료여부 = true;
        var store = new FakeStore(graph.Channel, graph.Video);
        store.Channels.Add(new YouTube감시채널
        {
            Id = 12,
            ChannelId = "UC_US_FOOD",
            채널명 = "US Food",
            UploadsPlaylistId = "UU_US_FOOD",
            음식채널여부 = true,
            활성화여부 = true,
            국가코드 = YouTube채널수집국가코드.미국,
            기본언어코드 = "en"
        });
        var sut = new YouTube음식상품발견Service(store, new RecordingGroupUseCase());

        var countries = await sut.음식채널국가집계조회Async(CancellationToken.None);
        var korean = countries[0];
        var american = countries[1];

        Assert.Equal("KR", korean.국가코드);
        Assert.Equal("한국", korean.국가표시명);
        Assert.Equal(1, korean.동기화완료채널수);
        Assert.Equal("US", american.국가코드);
        Assert.Equal("미국", american.국가표시명);

        var americanChannels = await sut.음식채널목록조회Async("us", 20, CancellationToken.None);
        Assert.Equal("UC_US_FOOD", Assert.Single(americanChannels).ChannelId);
    }

    [Fact]
    public async Task 등록한상품후보는_관리자승인전까지대기상태다()
    {
        var graph = CreateGraph();
        var store = new FakeStore(graph.Channel, graph.Video);
        var sut = new YouTube음식상품발견Service(store, new RecordingGroupUseCase());

        var result = await sut.상품후보등록Async(new YouTube상품후보등록요청Dto
        {
            VideoId = graph.Video.VideoId,
            상품키 = "youtube-product:test-sauce",
            상품명 = "테스트 소스",
            원산지국가코드 = "us",
            발견근거 = "영상 1분 20초에 포장과 브랜드가 확인됨",
            신뢰도 = 0.85m,
            허용의향유형목록 =
            [
                YouTube상품구매의향유형코드.구매관심,
                YouTube상품구매의향유형코드.수입검토
            ]
        }, CancellationToken.None);

        Assert.Equal(YouTube상품후보검수상태코드.대기, result.검수상태);
        Assert.Equal("US", result.원산지국가코드);
        Assert.Equal(1, store.SaveCount);
        Assert.Empty(await sut.공개상품후보목록조회Async(null, null, 20, CancellationToken.None));
    }

    [Fact]
    public async Task 승인된후보의수입검토는_비결제공동구매수요로만등록된다()
    {
        var graph = CreateGraph();
        var candidate = CreateApprovedCandidate(graph.Video);
        var store = new FakeStore(graph.Channel, graph.Video, candidate);
        var groupUseCase = new RecordingGroupUseCase
        {
            RegisterResult = new 공동구매자동집단응답
            {
                자동집단Id = "group-youtube-1",
                현재상태 = 공동구매자동집단상태코드.수요수집중,
                수요건수 = 4,
                총희망수량 = 12,
                수량단위 = "개"
            }
        };
        var sut = new YouTube음식상품발견Service(store, groupUseCase);

        var result = await sut.구매의향등록Async(
            candidate.Id,
            new YouTube상품구매의향등록요청Dto
            {
                의향유형 = YouTube상품구매의향유형코드.수입검토,
                배송권키 = "seoul-west",
                배송권명 = "서울 서부",
                희망수량 = 3,
                수량단위 = "개",
                메모 = "국내 정식 수입 가능 여부를 알고 싶음"
            },
            "user-sensitive-id",
            "테스트 사용자",
            CancellationToken.None);

        Assert.True(result.성공);
        Assert.Equal("group-youtube-1", result.값?.자동집단Id);
        var command = Assert.IsType<공동구매자동수요등록Command>(groupUseCase.LastCommand);
        Assert.Equal(공동구매자동수요유형코드.관심표시, command.수요유형);
        Assert.Equal(공동구매자동결제상태코드.미결제, command.결제상태);
        Assert.Null(command.예약결제금액);
        Assert.Equal("youtube-product:test-sauce", command.상품키);
        Assert.Equal("210390", command.HS코드);
        Assert.Contains("의향유형=ImportReview", command.메모, StringComparison.Ordinal);
        Assert.StartsWith("youtube-food:31:", command.수요출처키, StringComparison.Ordinal);
        Assert.DoesNotContain("user-sensitive-id", command.수요출처키, StringComparison.Ordinal);

        var json = JsonSerializer.Serialize(result.값);
        Assert.DoesNotContain("user-sensitive-id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("테스트 사용자", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 같은사용자와후보는_의향유형이달라도같은멱등키를사용한다()
    {
        var graph = CreateGraph();
        var candidate = CreateApprovedCandidate(graph.Video);
        var store = new FakeStore(graph.Channel, graph.Video, candidate);
        var groupUseCase = new RecordingGroupUseCase();
        var sut = new YouTube음식상품발견Service(store, groupUseCase);
        var request = new YouTube상품구매의향등록요청Dto
        {
            의향유형 = YouTube상품구매의향유형코드.구매관심,
            배송권키 = "seoul",
            희망수량 = 1
        };

        await sut.구매의향등록Async(candidate.Id, request, "same-user", "사용자", CancellationToken.None);
        var firstKey = groupUseCase.LastCommand!.수요출처키;
        request.의향유형 = YouTube상품구매의향유형코드.공동구매;
        await sut.구매의향등록Async(candidate.Id, request, "same-user", "사용자", CancellationToken.None);

        Assert.Equal(firstKey, groupUseCase.LastCommand!.수요출처키);
    }

    [Fact]
    public async Task 승인되지않은후보에는_구매의향을등록할수없다()
    {
        var graph = CreateGraph();
        var candidate = CreateApprovedCandidate(graph.Video);
        candidate.검수상태 = YouTube상품후보검수상태코드.대기;
        var groupUseCase = new RecordingGroupUseCase();
        var sut = new YouTube음식상품발견Service(
            new FakeStore(graph.Channel, graph.Video, candidate),
            groupUseCase);

        var result = await sut.구매의향등록Async(
            candidate.Id,
            new YouTube상품구매의향등록요청Dto
            {
                배송권키 = "seoul",
                희망수량 = 1
            },
            "user-1",
            "사용자",
            CancellationToken.None);

        Assert.False(result.성공);
        Assert.Equal(404, result.상태코드);
        Assert.Null(groupUseCase.LastCommand);
    }

    private static (YouTube감시채널 Channel, YouTube채널영상 Video) CreateGraph()
    {
        var channel = new YouTube감시채널
        {
            Id = 11,
            ChannelId = "UC_FOOD",
            채널명 = "음식 채널",
            UploadsPlaylistId = "UU_FOOD",
            음식채널여부 = true,
            활성화여부 = true,
            기본언어코드 = "ko",
            음식콘텐츠분류 = YouTube음식채널분류코드.상품리뷰
        };
        var video = new YouTube채널영상
        {
            Id = 21,
            YouTube감시채널Id = channel.Id,
            감시채널 = channel,
            VideoId = "video-food-1",
            ChannelId = channel.ChannelId,
            제목 = "해외 소스 비교",
            설명 = "원본 YouTube 설명",
            게시일시Utc = new DateTime(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc),
            공유상태 = YouTube채널영상.공개상태
        };
        channel.영상.Add(video);
        return (channel, video);
    }

    private static YouTube영상상품후보 CreateApprovedCandidate(YouTube채널영상 video)
    {
        var candidate = new YouTube영상상품후보
        {
            Id = 31,
            YouTube채널영상Id = video.Id,
            영상 = video,
            상품키 = "youtube-product:test-sauce",
            상품명 = "테스트 소스",
            원산지국가코드 = "US",
            HS코드후보 = "210390",
            온도코드 = "상온",
            물류방식 = "LCL",
            후보유형 = YouTube상품후보유형코드.포장상품,
            발견근거 = "포장과 브랜드 확인",
            추출방식 = YouTube상품후보추출방식코드.수동검수,
            신뢰도 = 0.9m,
            검수상태 = YouTube상품후보검수상태코드.승인,
            협찬표시상태 = YouTube협찬표시상태코드.표시없음,
            허용의향유형 = string.Join(',', YouTube상품구매의향유형코드.전체),
            생성일시Utc = DateTime.UtcNow,
            수정일시Utc = DateTime.UtcNow
        };
        video.상품후보.Add(candidate);
        return candidate;
    }

    private sealed class RecordingGroupUseCase : I공동구매자동집단화UseCase
    {
        public 공동구매자동수요등록Command? LastCommand { get; private set; }

        public 공동구매자동집단응답 RegisterResult { get; set; } = new()
        {
            자동집단Id = "group-default",
            현재상태 = 공동구매자동집단상태코드.수요수집중,
            수요건수 = 1,
            총희망수량 = 1,
            수량단위 = "개"
        };

        public Task<공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>> 목록조회Async(
            공동구매자동집단조회조건 조건,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                공동구매처리결과<IReadOnlyList<공동구매자동집단응답>>.성공결과([]));

        public Task<공동구매처리결과<공동구매자동집단응답>> 수요등록Async(
            공동구매자동수요등록Command command,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(공동구매처리결과<공동구매자동집단응답>.성공결과(RegisterResult));
        }
    }

    private sealed class FakeStore : IYouTube음식상품발견저장소
    {
        public FakeStore(
            YouTube감시채널 channel,
            YouTube채널영상 video,
            params YouTube영상상품후보[] candidates)
        {
            Channels.Add(channel);
            Videos.Add(video);
            Candidates.AddRange(candidates);
        }

        public List<YouTube감시채널> Channels { get; } = [];
        public List<YouTube채널영상> Videos { get; } = [];
        public List<YouTube영상상품후보> Candidates { get; } = [];
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<YouTube감시채널>> 음식채널목록조회Async(
            string? 국가코드,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube감시채널>>(
                Channels
                    .Where(channel => channel.음식채널여부 && channel.활성화여부)
                    .Where(channel => string.IsNullOrWhiteSpace(국가코드)
                        || YouTube채널수집국가코드.정규화(channel.국가코드) == 국가코드)
                    .Take(take)
                    .ToArray());

        public Task<IReadOnlyList<YouTube감시채널>> 음식채널국가집계대상조회Async(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube감시채널>>(Channels
                .Where(channel => channel.음식채널여부 && channel.활성화여부)
                .ToArray());

        public Task<YouTube채널영상?> 영상추적조회Async(
            string videoId,
            CancellationToken cancellationToken)
            => Task.FromResult(Videos.SingleOrDefault(video => video.VideoId == videoId));

        public Task<bool> 상품후보중복여부Async(
            long youtube채널영상Id,
            string 상품키,
            CancellationToken cancellationToken)
            => Task.FromResult(Candidates.Any(candidate =>
                candidate.YouTube채널영상Id == youtube채널영상Id && candidate.상품키 == 상품키));

        public Task<YouTube영상상품후보?> 상품후보추적조회Async(
            long 후보Id,
            CancellationToken cancellationToken)
            => Task.FromResult(Candidates.SingleOrDefault(candidate => candidate.Id == 후보Id));

        public Task<IReadOnlyList<YouTube영상상품후보>> 상품후보목록조회Async(
            string? 검수상태,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube영상상품후보>>(Candidates
                .Where(candidate => string.IsNullOrWhiteSpace(검수상태) || candidate.검수상태 == 검수상태)
                .Take(take)
                .ToArray());

        public Task<IReadOnlyList<YouTube영상상품후보>> 공개상품후보목록조회Async(
            string? channelId,
            string? 국가코드,
            string? 후보유형,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube영상상품후보>>(Candidates
                .Where(candidate => candidate.검수상태 == YouTube상품후보검수상태코드.승인)
                .Where(candidate => candidate.영상?.공유상태 == YouTube채널영상.공개상태)
                .Where(candidate => string.IsNullOrWhiteSpace(channelId) || candidate.영상?.ChannelId == channelId)
                .Where(candidate => string.IsNullOrWhiteSpace(국가코드)
                    || YouTube채널수집국가코드.정규화(candidate.영상?.감시채널?.국가코드) == 국가코드)
                .Where(candidate => string.IsNullOrWhiteSpace(후보유형) || candidate.후보유형 == 후보유형)
                .Take(take)
                .ToArray());

        public void 상품후보추가(YouTube영상상품후보 후보)
        {
            후보.Id = Candidates.Count == 0 ? 1 : Candidates.Max(candidate => candidate.Id) + 1;
            Candidates.Add(후보);
        }

        public Task 저장Async(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
