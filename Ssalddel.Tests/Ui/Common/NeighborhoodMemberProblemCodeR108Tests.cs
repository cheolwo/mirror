using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class NeighborhoodMemberProblemCodeR108Tests
{
    [Theory]
    [InlineData("{\"errorCode\":\"Canonical\"}", "Canonical")]
    [InlineData("{\"code\":\"MemberWorkNotFound\"}", "MemberWorkNotFound")]
    [InlineData("{\"errorCode\":\"Canonical\",\"code\":\"Legacy\"}", "Canonical")]
    [InlineData("{\"errorCode\":null,\"code\":\"Legacy\"}", "Legacy")]
    [InlineData("{\"errorCode\":\"\",\"code\":\"Legacy\"}", "Legacy")]
    [InlineData("{\"errorCode\":\"  \",\"code\":\"Legacy\"}", "Legacy")]
    [InlineData("{\"errorCode\":42,\"code\":\"Legacy\"}", "Legacy")]
    [InlineData("{\"ERRORCODE\":\"Canonical\",\"Code\":\"Legacy\"}", "Canonical")]
    [InlineData("{\"Code\":\"MemberWorkNotFound\"}", "MemberWorkNotFound")]
    [InlineData("{\"errorCode\":\"Canonical\",\"ERRORCODE\":\"Other\",\"CODE\":\"Legacy\"}", "Canonical")]
    [InlineData("{\"errorCode\":null,\"CODE\":\"Legacy\"}", "Legacy")]
    [InlineData("{\"errorCode\":\" MixedCase \",\"code\":\"Legacy\"}", " MixedCase ")]
    [InlineData("{\"errorCode\":null,\"code\":null}", null)]
    [InlineData("{\"errorCode\":\"  \",\"code\":\"\"}", null)]
    [InlineData("{\"code\":42}", null)]
    [InlineData("{}", null)]
    public void 안정코드는_기존필드를우선하고_ProblemDetails별칭을읽는다(string response, string? expected)
        => Assert.Equal(expected, SsalddelApiProblemParser.Parse(response).ErrorCode);

    [Theory]
    [InlineData("MemberWorkNotFound")]
    [InlineData("MemberDeliveryDisabled")]
    public async Task 사진영수증조회에서_업무없음과기능차단404를_사진미전송으로바꾸지않는다(string code)
    {
        using var handler = new ProblemHandler(JsonSerializer.Serialize(new { code, title = "현재 업무를 확인해 주세요." }));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5378/") };
        await using var encryption = new SsalddelIsmsPClientEncryptionService(new NoJs());
        var client = new NeighborhoodMemberDeliveryClient(new SsalddelJsonApiClient(new SsalddelProtectedApiClient(http, encryption, new NoToken())));
        var error = await Assert.ThrowsAsync<SsalddelApiException>(() => client.EvidenceReceiptAsync("synthetic-work", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(code, error.ErrorCode);
        Assert.Equal(404, error.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task 실제사진영수증이없는404만_미전송결과로유지한다()
    {
        using var handler = new ProblemHandler("{\"code\":\"MemberPhotoReceiptNotFound\",\"title\":\"사진 저장 결과가 없습니다.\"}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5378/") };
        await using var encryption = new SsalddelIsmsPClientEncryptionService(new NoJs());
        var client = new NeighborhoodMemberDeliveryClient(new SsalddelJsonApiClient(new SsalddelProtectedApiClient(http, encryption, new NoToken())));
        Assert.Null(await client.EvidenceReceiptAsync("synthetic-work", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    private sealed class ProblemHandler(string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Contains("/evidence-receipts/", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            { Content = new StringContent(body, Encoding.UTF8, "application/problem+json") });
        }
    }
    private sealed class NoToken : ISsalddelAccessTokenProvider { public string? AccessToken => null; }
    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => throw new NotSupportedException();
    }
}
