using System.Net;
using System.Text;
using Hongdal.Ui.Common.Areas.App.Services;

namespace Hongdal.Tests.Services.Content;

public sealed class YouTube관리콘텐츠ServiceTests
{
    [Fact]
    public async Task 재생목록조회Async_관리자토큰과관리자경로를사용한다()
    {
        Uri? requestUri = null;
        string? authorization = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            requestUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    [
                      {
                        "playlistId": "PL_TEST",
                        "channelId": "UC_TEST",
                        "제목": "홍익학당 강의",
                        "설명": "강의 설명",
                        "게시일시Utc": "2026-07-14T00:00:00Z",
                        "영상수": 8,
                        "썸네일Url": "https://img.example/playlist.jpg",
                        "재생목록Url": "https://www.youtube.com/playlist?list=PL_TEST"
                      }
                    ]
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost:7117/")
        };
        var sut = new YouTube관리콘텐츠Service(httpClient, new StubAccessTokenProvider("admin-token"));

        var result = await sut.재생목록조회Async(" UC_TEST ");

        var playlist = Assert.Single(result);
        Assert.Equal("PL_TEST", playlist.PlaylistId);
        Assert.Equal(8, playlist.영상수);
        Assert.Equal(
            "/api/v1/admin/content/youtube/playlists?channelId=UC_TEST",
            requestUri!.PathAndQuery);
        Assert.Equal("Bearer admin-token", authorization);
    }

    private sealed class StubAccessTokenProvider(string accessToken) : IHongdalAccessTokenProvider
    {
        public string? AccessToken { get; } = accessToken;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
