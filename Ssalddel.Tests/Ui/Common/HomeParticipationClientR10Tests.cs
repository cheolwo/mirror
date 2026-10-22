using System.Net;
using System.Net.Http.Json;
using Ssalddel.Contracts.Common.Home;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class HomeParticipationClientR10Tests
{
    [Fact]
    public async Task 공개집계만_GET하고_역할자격이나_개별식별자를_보내지않는다()
    {
        using var factory = new PublicClientFactory(Valid());
        var result = await new HomeParticipationSummaryClient(factory).LoadAsync();
        Assert.NotNull(result);
        Assert.Equal(SsalddelHttpClientNames.OperationalApi, factory.ClientName);
        Assert.Equal(HttpMethod.Get, factory.Method);
        Assert.Equal("https://example.test/api/v1/common/home/participation-summary", factory.Uri?.AbsoluteUri);
        Assert.Null(factory.Authorization);
        Assert.Null(result.ConfirmedPlatformProfitKrw);
        Assert.Null(result.ConfirmedSharedPayoutKrw);
        Assert.Equal(0, result.CompletedFreightTransportCount);
    }

    [Theory]
    [InlineData("period")]
    [InlineData("mode")]
    [InlineData("time")]
    [InlineData("status")]
    [InlineData("negative-completion")]
    [InlineData("negative-shipper")]
    [InlineData("negative-driver")]
    [InlineData("shipper-exceeds-completion")]
    [InlineData("driver-exceeds-completion")]
    public async Task 잘못된집계는_사용할수있는실적으로_표시하지않는다(string kind)
    {
        var response = Valid();
        switch (kind)
        {
            case "period": response.PeriodCode = "this-week"; break;
            case "mode": response.ExecutionMode = "Unknown"; break;
            case "time": response.AsOfUtc = default; break;
            case "status": response.StatusCode = "unavailable"; break;
            case "negative-completion": response.CompletedFreightTransportCount = -1; break;
            case "negative-shipper": response.FreightShipperAccountCount = -1; break;
            case "negative-driver": response.FreightDriverAccountCount = -1; break;
            case "shipper-exceeds-completion": response.FreightShipperAccountCount = 1; break;
            case "driver-exceeds-completion": response.FreightDriverAccountCount = 1; break;
        }
        using var factory = new PublicClientFactory(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => new HomeParticipationSummaryClient(factory).LoadAsync());
    }

    [Fact]
    public async Task 빈응답은_미집계성공이나_0으로_바꾸지않는다()
    {
        using var factory = new PublicClientFactory(null);
        await Assert.ThrowsAsync<InvalidDataException>(() => new HomeParticipationSummaryClient(factory).LoadAsync());
    }

    [Fact]
    public async Task 서버실패는_역할로그인이나_관리자요청으로_우회하지않는다()
    {
        using var factory = new PublicClientFactory(Valid(), HttpStatusCode.ServiceUnavailable);
        await Assert.ThrowsAsync<HttpRequestException>(() => new HomeParticipationSummaryClient(factory).LoadAsync());
        Assert.Equal(1, factory.RequestCount);
    }

    private static HomeParticipationSummaryResponse Valid() => new()
    {
        PeriodCode = "all-time", ExecutionMode = "Simulation", StatusCode = "recorded",
        AsOfUtc = new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero),
        CompletedFreightTransportCount = 0, FreightShipperAccountCount = 0, FreightDriverAccountCount = 0
    };

    private sealed class PublicClientFactory(HomeParticipationSummaryResponse? response, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler, IHttpClientFactory
    {
        private HttpClient? _client;
        public string? ClientName { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? Uri { get; private set; }
        public string? Authorization { get; private set; }
        public int RequestCount { get; private set; }
        public HttpClient CreateClient(string name)
        {
            ClientName = name;
            return _client ??= new HttpClient(this, false) { BaseAddress = new Uri("https://example.test/") };
        }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method; Uri = request.RequestUri; Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(status) { Content = JsonContent.Create(response) });
        }
        protected override void Dispose(bool disposing) { if (disposing) _client?.Dispose(); base.Dispose(disposing); }
    }
}
