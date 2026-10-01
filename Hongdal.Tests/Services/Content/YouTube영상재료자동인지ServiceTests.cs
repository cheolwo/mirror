using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hongdal.Contracts.Common.Content;
using Hongdal.Domain.Content;
using Hongdal.Services.Content;
using Microsoft.Extensions.Options;
using SkiaSharp;
using 홍달.Services.HIOPSAI;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTube영상재료자동인지ServiceTests
{
    [Fact]
    public async Task 권한있는영상프레임은_AI재료후보로인지하고검수대기로저장한다()
    {
        var graph = CreateGraph();
        var store = new FakeStore(graph.Video);
        store.Candidates.Add(new YouTube영상상품후보
        {
            YouTube채널영상Id = graph.Video.Id,
            상품키 = BuildProductKey("마늘")
        });
        var engine = new FakeEngine
        {
            Result = new YouTube영상재료인지Engine결과(
                true,
                "vision-test-model",
                [
                    new YouTube영상재료인지Engine후보(
                        "다진 마늘",
                        "마늘",
                        12,
                        YouTube재료인지근거유형코드.영상프레임,
                        "도마 위 다진 마늘이 확인됨",
                        0.94m),
                    new YouTube영상재료인지Engine후보(
                        "후추",
                        "후추",
                        12,
                        YouTube재료인지근거유형코드.영상프레임,
                        "후추 분쇄 용기가 확인됨",
                        0.87m)
                ],
                null,
                null)
        };
        var sut = new YouTube영상재료자동인지Service(
            store,
            engine,
            Options.Create(new YouTubeOptions
            {
                AutomaticIngredientRecognitionEnabled = true,
                MinimumIngredientRecognitionConfidence = 0.55m
            }));

        var result = await sut.분석Async(
            graph.Video.VideoId,
            new YouTube영상재료자동인지요청(
                true,
                null,
                [new YouTube영상재료인지업로드프레임(12, "image/png", CreatePng())]),
            CancellationToken.None);

        Assert.True(result.실행됨);
        Assert.Equal(2, result.인지재료수);
        Assert.Equal(1, result.추가상품후보수);
        Assert.Equal(1, result.중복상품후보수);
        var added = Assert.Single(store.Candidates, candidate => candidate.상품명 == "후추");
        Assert.Equal(YouTube상품후보검수상태코드.대기, added.검수상태);
        Assert.Equal(YouTube상품후보유형코드.식재료, added.후보유형);
        Assert.Equal(YouTube상품후보추출방식코드.영상프레임자동인지, added.추출방식);
        Assert.Equal(1, store.SaveCount);
        Assert.StartsWith("data:image/jpeg;base64,", Assert.Single(engine.LastInput!.프레임목록).DataUrl);
    }

    [Fact]
    public async Task 분석권한확인없는프레임은_AI로전송하지않는다()
    {
        var graph = CreateGraph();
        var engine = new FakeEngine();
        var sut = new YouTube영상재료자동인지Service(
            new FakeStore(graph.Video),
            engine,
            Options.Create(new YouTubeOptions { AutomaticIngredientRecognitionEnabled = true }));

        await Assert.ThrowsAsync<ArgumentException>(() => sut.분석Async(
            graph.Video.VideoId,
            new YouTube영상재료자동인지요청(
                false,
                null,
                [new YouTube영상재료인지업로드프레임(0, "image/png", CreatePng())]),
            CancellationToken.None));

        Assert.Null(engine.LastInput);
    }

    [Fact]
    public async Task 재료인지Engine은_이미지와JsonSchema를_HIOPSAI에전달한다()
    {
        var ai = new FakeAiClient
        {
            Result = new HIOPSAICompletionResult(
                Success: true,
                Text: """
                      {
                        "ingredients": [
                          {
                            "displayName": "대파",
                            "normalizedName": "대파",
                            "evidenceType": "Frame",
                            "evidence": "썰어 놓은 대파가 보임",
                            "timestampSeconds": 30,
                            "confidence": 0.91
                          }
                        ],
                        "uncertaintyNote": ""
                      }
                      """,
                BlockedReason: null,
                Model: "vision-test-model",
                EstimatedCostUsd: 0.01m,
                ActualCostUsd: 0.01m,
                MonthlyUsedUsd: 0.01m,
                MonthlyBudgetUsd: 20m,
                InputTokens: 100,
                OutputTokens: 30)
        };
        var sut = new YouTube영상재료인지Engine(ai);

        var result = await sut.인지Async(
            new YouTube영상재료인지Engine입력(
                "video-1",
                "파 요리",
                "대파를 사용합니다.",
                null,
                [new YouTube영상재료인지프레임입력(30, "data:image/jpeg;base64,AQID")]),
            CancellationToken.None);

        Assert.True(result.성공);
        Assert.Equal("대파", Assert.Single(result.후보목록).표준재료명);
        Assert.NotNull(ai.LastRequest?.OutputJsonSchema);
        var userMessage = ai.LastRequest!.Messages.Single(message => message.Role == "user");
        Assert.Equal("high", Assert.Single(userMessage.Images!).Detail);
    }

    [Fact]
    public void HIOPSAI요청은_이미지입력과구조화출력형식으로직렬화된다()
    {
        using var schemaDocument = JsonDocument.Parse(
            """{"type":"object","properties":{},"required":[],"additionalProperties":false}""");
        var request = OpenAIResponsesRequest.From(
            "vision-test-model",
            new HIOPSAICompletionRequest(
                "test",
                [
                    new HIOPSAIMessage(
                        "user",
                        "재료를 찾아줘",
                        [new HIOPSAIImageInput("data:image/jpeg;base64,AQID", "high", "30초 프레임")])
                ],
                OutputJsonSchema: new HIOPSAIJsonSchema(
                    "ingredient_schema",
                    schemaDocument.RootElement.Clone())),
            300);
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var content = root.GetProperty("input")[0].GetProperty("content");

        Assert.Equal("input_image", content[2].GetProperty("type").GetString());
        Assert.Equal("data:image/jpeg;base64,AQID", content[2].GetProperty("image_url").GetString());
        Assert.Equal("json_schema", root.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.False(root.GetProperty("store").GetBoolean());
    }

    private static (YouTube감시채널 Channel, YouTube채널영상 Video) CreateGraph()
    {
        var channel = new YouTube감시채널
        {
            Id = 1,
            ChannelId = "UC_FOOD",
            채널명 = "음식 채널",
            UploadsPlaylistId = "UU_FOOD",
            음식채널여부 = true,
            활성화여부 = true,
            국가코드 = "KR"
        };
        var video = new YouTube채널영상
        {
            Id = 2,
            YouTube감시채널Id = channel.Id,
            감시채널 = channel,
            VideoId = "video-food-1",
            ChannelId = channel.ChannelId,
            제목 = "마늘과 후추 요리",
            설명 = "재료를 손질하는 영상"
        };
        return (channel, video);
    }

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Green);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string BuildProductKey(string normalizedName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedName.Trim().ToUpperInvariant()));
        return $"youtube-ingredient:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private sealed class FakeEngine : IYouTube영상재료인지Engine
    {
        public YouTube영상재료인지Engine입력? LastInput { get; private set; }

        public YouTube영상재료인지Engine결과 Result { get; set; } = new(
            true,
            "fake",
            [],
            null,
            null);

        public Task<YouTube영상재료인지Engine결과> 인지Async(
            YouTube영상재료인지Engine입력 입력,
            CancellationToken cancellationToken)
        {
            LastInput = 입력;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAiClient : IHIOPSAIClient
    {
        public HIOPSAICompletionRequest? LastRequest { get; private set; }

        public HIOPSAICompletionResult Result { get; set; } = HIOPSAICompletionResult.Blocked(
            "not configured",
            "fake",
            0m,
            0m,
            20m);

        public Task<HIOPSAICompletionResult> CompleteAsync(
            HIOPSAICompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeStore : IYouTube음식상품발견저장소
    {
        public FakeStore(YouTube채널영상 video)
        {
            Video = video;
        }

        public YouTube채널영상 Video { get; }
        public List<YouTube영상상품후보> Candidates { get; } = [];
        public int SaveCount { get; private set; }

        public Task<IReadOnlyList<YouTube감시채널>> 음식채널목록조회Async(
            string? 국가코드,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube감시채널>>([]);

        public Task<IReadOnlyList<YouTube감시채널>> 음식채널국가집계대상조회Async(
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube감시채널>>([]);

        public Task<YouTube채널영상?> 영상추적조회Async(
            string videoId,
            CancellationToken cancellationToken)
            => Task.FromResult<YouTube채널영상?>(Video.VideoId == videoId ? Video : null);

        public Task<bool> 상품후보중복여부Async(
            long youtube채널영상Id,
            string 상품키,
            CancellationToken cancellationToken)
            => Task.FromResult(Candidates.Any(candidate =>
                candidate.YouTube채널영상Id == youtube채널영상Id
                && candidate.상품키 == 상품키));

        public Task<YouTube영상상품후보?> 상품후보추적조회Async(
            long 후보Id,
            CancellationToken cancellationToken)
            => Task.FromResult(Candidates.SingleOrDefault(candidate => candidate.Id == 후보Id));

        public Task<IReadOnlyList<YouTube영상상품후보>> 상품후보목록조회Async(
            string? 검수상태,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube영상상품후보>>(Candidates.Take(take).ToArray());

        public Task<IReadOnlyList<YouTube영상상품후보>> 공개상품후보목록조회Async(
            string? channelId,
            string? 국가코드,
            string? 후보유형,
            int take,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<YouTube영상상품후보>>([]);

        public void 상품후보추가(YouTube영상상품후보 후보)
        {
            후보.Id = Candidates.Count + 1;
            Candidates.Add(후보);
        }

        public Task 저장Async(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
