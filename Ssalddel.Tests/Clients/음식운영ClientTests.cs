using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Ssalddel.Contracts.Admin.Restaurants;
using SsalddelAdmin.Services;

namespace Ssalddel.Tests.Clients;

public sealed class 음식운영ClientTests
{
    [Theory]
    [InlineData("pricing")]
    [InlineData("reviews")]
    [InlineData("review-policy")]
    [InlineData("save")]
    public async Task 정상HTTP의_null응답을_기본정책이나저장성공으로대체하지않는다(string operation)
    {
        var service = Create(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json") }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation switch
        {
            "pricing" => (Task)service.배달요금정책조회Async(),
            "reviews" => service.리뷰운영목록조회Async(),
            "review-policy" => service.운영정책조회Async(),
            _ => service.배달요금정책수정Async(new())
        });
    }

    [Fact]
    public async Task 수정요청은_화면밖픽업배분과기상정책을보존하고_서버저장응답을반환한다()
    {
        음식배달요금정책응답? submitted = null;
        var handler = new Handler(request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.EndsWith("/api/v1/admin/food-delivery-pricing-policy", request.RequestUri!.AbsoluteUri);
            submitted = request.Content!.ReadFromJsonAsync<음식배달요금정책응답>().GetAwaiter().GetResult();
            submitted!.BaseFee = 3900m; // 반환값이 요청 기본 객체를 대체하는지 확인한다.
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(submitted) };
        });
        var result = await Create(handler).배달요금정책수정Async(new()
        {
            BaseFee = 3000m, DriverPickupPayout = 700m,
            DriverWeatherSurchargeEnabled = true, DriverWeatherSurcharge = 1000m,
            DriverWeatherSurchargePolicyRevision = "existing-weather-r1"
        });
        Assert.Equal(700m, submitted!.DriverPickupPayout);
        Assert.True(submitted.DriverWeatherSurchargeEnabled);
        Assert.Equal("existing-weather-r1", result.DriverWeatherSurchargePolicyRevision);
        Assert.Equal(3900m, result.BaseFee);
    }

    [Fact]
    public async Task HTTP실패는_정상정책으로숨기지않는다()
    {
        var service = Create(new Handler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.배달요금정책조회Async());
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    private static 음식운영Service Create(HttpMessageHandler handler)
        => new(new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") },
            new 관리자인증세션Service(new ConfigurationBuilder().Build(), new Environment()));

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(respond(request));
    }

    private sealed class Environment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
