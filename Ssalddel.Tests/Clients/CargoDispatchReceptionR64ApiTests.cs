using System.Net;
using System.Net.Http.Json;
using DriverApp.Services;
using Ssalddel.Contracts.Common.Dispatch;

namespace Ssalddel.Tests.Clients;

public sealed class CargoDispatchReceptionR64ApiTests
{
    [Fact]
    public async Task 실제기사인증으로수신조회와명시재개를같은API에전달한다()
    {
        var requestId = Guid.NewGuid(); var calls = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            calls++;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-only-access", request.Headers.Authorization?.Parameter);
            if (request.Method == HttpMethod.Get)
                Assert.Equal("/api/v1/driver/operational-dispatch/availability", request.RequestUri!.AbsolutePath);
            else
            {
                Assert.Equal(HttpMethod.Put, request.Method);
                Assert.Equal("/api/v1/driver/operational-dispatch/availability/intent", request.RequestUri!.AbsolutePath);
                var body = await request.Content!.ReadFromJsonAsync<운영배차수신의사변경요청>();
                Assert.Equal(requestId, body!.클라이언트요청Id);
                Assert.Equal(운영배차수신의사Code.On, body.수신의사Code);
                Assert.True(body.미응답자동중지해제);
            }
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 운영배차수신상태Dto
            {
                주체Id = "driver-r75", 수신의사Code = 운영배차수신의사Code.On,
                실효상태Code = 운영배차실효상태Code.서버일시정지,
                실효사유Code = "OtherRestriction", 미응답자동중지여부 = request.Method == HttpMethod.Get
            }) };
        })) { BaseAddress = new("https://test.invalid/") };
        var api = Api(http);
        Assert.True((await api.GetAvailabilityAsync()).미응답자동중지여부);
        var resumed = await api.ChangeIntentAsync(new()
        {
            클라이언트요청Id = requestId, 수신의사Code = 운영배차수신의사Code.On, 미응답자동중지해제 = true
        });
        Assert.False(resumed.미응답자동중지여부);
        Assert.Equal("OtherRestriction", resumed.실효사유Code); Assert.Equal(2, calls);
    }

    [Fact]
    public async Task 표시확인은추천차수와인증을보내고204를본문으로해석하지않는다()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            calls++;
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("/api/v1/driver/operational-dispatch/offers/offer-r64/receipt", request.RequestUri!.AbsolutePath);
            Assert.Equal("test-only-access", request.Headers.Authorization?.Parameter);
            Assert.Equal(8, (await request.Content!.ReadFromJsonAsync<운영배차제안표시확인요청>())!.추천라운드);
            return new(HttpStatusCode.NoContent);
        })) { BaseAddress = new("https://test.invalid/") };
        await Api(http).AcknowledgeOfferPresentationAsync("offer-r64", 8);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task 기능미활성404를수신켜짐이나성공으로대체하지않는다()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))))
            { BaseAddress = new("https://test.invalid/") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Api(http).GetAvailabilityAsync());
    }

    private static DriverOperationalDispatchApiService Api(HttpClient http)
    {
        var session = new CargoWorkRecoveryR75Tests.RecoveryAuth();
        return new(new DriverApiClient(http, session, new AuthApiService(http, session), new DriverOperatingProfileService()));
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handle(request);
    }
}
