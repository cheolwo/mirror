using System.Net;
using System.Text;
using Hongdal.Services.External.YouTube;
using Microsoft.Extensions.Options;
using 홍달.Services.Options;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTubeDataApiClientTests
{
    [Fact]
    public async Task 채널검색Async_음식주제와지역언어조건을사용한다()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse(
                """
                {
                  "items": [
                    {
                      "id": { "channelId": "UC_FOOD" },
                      "snippet": {
                        "publishedAt": "2020-01-02T03:04:05Z",
                        "title": "음식 발견 채널",
                        "description": "식재료와 상품 리뷰",
                        "thumbnails": { "high": { "url": "https://img.example/food.jpg" } }
                      }
                    }
                  ]
                }
                """);
        });
        var sut = CreateClient(handler);

        var result = await sut.채널검색Async(
            "한국 식재료",
            10,
            "kr",
            "ko",
            CancellationToken.None);

        var channel = Assert.Single(result);
        Assert.Equal("UC_FOOD", channel.ChannelId);
        Assert.Equal("음식 발견 채널", channel.채널명);
        Assert.Contains("type=channel", requestUri!.Query);
        Assert.Contains("topicId=%2Fm%2F02wbm", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("q=%ED%95%9C%EA%B5%AD%20%EC%8B%9D%EC%9E%AC%EB%A3%8C", requestUri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("regionCode=KR", requestUri.Query);
        Assert.Contains("relevanceLanguage=ko", requestUri.Query);
    }

    [Fact]
    public async Task 채널조회Async_업로드재생목록과채널정보를_반환한다()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse(
                """
                {
                  "items": [
                    {
                      "id": "UC_TEST",
                      "snippet": {
                        "title": "홍달 생활 물류",
                        "thumbnails": { "high": { "url": "https://img.example/channel.jpg" } }
                      },
                      "contentDetails": {
                        "relatedPlaylists": { "uploads": "UU_TEST" }
                      }
                    }
                  ]
                }
                """);
        });
        var sut = CreateClient(handler);

        var result = await sut.채널조회Async("UC_TEST", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("UC_TEST", result.ChannelId);
        Assert.Equal("UU_TEST", result.UploadsPlaylistId);
        Assert.Equal("홍달 생활 물류", result.채널명);
        Assert.Equal("https://img.example/channel.jpg", result.썸네일Url);
        Assert.Contains("part=snippet,contentDetails", requestUri!.Query);
        Assert.Contains("id=UC_TEST", requestUri.Query);
        Assert.Contains("key=test-key", requestUri.Query);
    }

    [Fact]
    public async Task 채널Handle조회Async_공식Handle을채널ID와업로드목록으로해석한다()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            return JsonResponse(
                """
                {
                  "items": [
                    {
                      "id": "UC_TED",
                      "snippet": { "title": "TED" },
                      "contentDetails": {
                        "relatedPlaylists": { "uploads": "UU_TED" }
                      }
                    }
                  ]
                }
                """);
        });
        var sut = CreateClient(handler);

        var result = await sut.채널Handle조회Async("@TED", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("UC_TED", result.ChannelId);
        Assert.Equal("UU_TED", result.UploadsPlaylistId);
        Assert.Contains("forHandle=TED", requestUri!.Query);
        Assert.DoesNotContain("%40", requestUri.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task 업로드목록조회Async_영상정보를_게시일역순으로반환한다()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(
            """
            {
              "items": [
                {
                  "snippet": {
                    "publishedAt": "2026-07-13T01:00:00Z",
                    "channelId": "UC_TEST",
                    "title": "첫 영상",
                    "description": "설명",
                    "resourceId": { "videoId": "video-1" },
                    "thumbnails": { "medium": { "url": "https://img.example/1.jpg" } }
                  },
                  "contentDetails": {
                    "videoId": "video-1",
                    "videoPublishedAt": "2026-07-13T01:00:00Z"
                  }
                },
                {
                  "snippet": {
                    "publishedAt": "2026-07-14T01:00:00Z",
                    "channelId": "UC_TEST",
                    "title": "새 영상",
                    "description": "새 설명",
                    "resourceId": { "videoId": "video-2" },
                    "thumbnails": { "high": { "url": "https://img.example/2.jpg" } }
                  },
                  "contentDetails": {
                    "videoId": "video-2",
                    "videoPublishedAt": "2026-07-14T01:00:00Z"
                  }
                }
              ]
            }
            """));
        var sut = CreateClient(handler);

        var result = await sut.업로드목록조회Async("UU_TEST", 20, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("video-2", result[0].VideoId);
        Assert.Equal("새 영상", result[0].제목);
        Assert.Equal(DateTimeKind.Utc, result[0].게시일시Utc.Kind);
    }

    [Fact]
    public async Task 재생목록목록조회Async_모든페이지를조회하고_최신순으로반환한다()
    {
        var requestUris = new List<Uri>();
        var callCount = 0;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUris.Add(request.RequestUri!);
            callCount++;
            return callCount == 1
                ? JsonResponse(
                    """
                    {
                      "nextPageToken": "NEXT_PAGE",
                      "items": [
                        {
                          "id": "PL_OLD",
                          "snippet": {
                            "publishedAt": "2025-01-01T00:00:00Z",
                            "channelId": "UC_TEST",
                            "title": "이전 재생목록",
                            "description": "이전 설명",
                            "thumbnails": { "medium": { "url": "https://img.example/old.jpg" } }
                          },
                          "contentDetails": { "itemCount": 3 }
                        }
                      ]
                    }
                    """)
                : JsonResponse(
                    """
                    {
                      "items": [
                        {
                          "id": "PL_NEW",
                          "snippet": {
                            "publishedAt": "2026-07-14T00:00:00Z",
                            "channelId": "UC_TEST",
                            "title": "새 재생목록",
                            "description": "새 설명",
                            "thumbnails": { "high": { "url": "https://img.example/new.jpg" } }
                          },
                          "contentDetails": { "itemCount": 7 }
                        }
                      ]
                    }
                    """);
        });
        var sut = CreateClient(handler);

        var result = await sut.재생목록목록조회Async("UC_TEST", CancellationToken.None);

        Assert.Equal(2, requestUris.Count);
        Assert.Contains("channelId=UC_TEST", requestUris[0].Query);
        Assert.Contains("maxResults=50", requestUris[0].Query);
        Assert.Contains("pageToken=NEXT_PAGE", requestUris[1].Query);
        Assert.Equal("PL_NEW", result[0].PlaylistId);
        Assert.Equal("새 재생목록", result[0].제목);
        Assert.Equal(7, result[0].영상수);
        Assert.Equal("https://img.example/new.jpg", result[0].썸네일Url);
        Assert.Equal(DateTimeKind.Utc, result[0].게시일시Utc.Kind);
    }

    private static YouTubeDataApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/")
        };
        return new YouTubeDataApiClient(httpClient, Options.Create(new YouTubeOptions
        {
            Enabled = true,
            ApiKey = "test-key"
        }));
    }

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(_responseFactory(request));
    }
}
