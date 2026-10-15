using System.Net;
using System.Net.Http.Json;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperReceiptApiR70Tests
{
    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Conflict)]
    public async Task 등록은_기존인증된보호API를한번만호출하고_거부를성공으로바꾸지않는다(HttpStatusCode status)
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
        using var handler = new Handler(status);
        using var client = new HttpClient(handler) { BaseAddress = new("https://test.invalid/") };
        var observer = new TransportRequestLedgerObserver();
        await using var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
        var authApi = new AuthApiService(client, auth,
            new 꾸미기보유권동기화Service(client, auth, new DecorationStore(), new PlatformCommunityDecorationStateService()),
            new SsalddelMobilePushInstallationClient(client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
        var service = new ServerBackedShipperOperationsService(client, auth, authApi, observer, realtime);
        var registering = service.RegisterReceiptAsync(" request/a 01 ", new() { 인수증번호 = "receipt-a", 등록메모 = "검수 완료" });
        if (status == HttpStatusCode.OK) await registering;
        else Assert.Contains("409", (await Assert.ThrowsAsync<InvalidOperationException>(() => registering)).Message);
        Assert.Single(handler.Posts);
        Assert.Equal("/api/v1/shipper/requests/request%2Fa%2001/settlement/receipt", handler.Posts[0].Path);
        Assert.Equal("Bearer access-a", handler.Posts[0].Auth);
        Assert.Equal("receipt-a", handler.Posts[0].Payload.인수증번호);
        Assert.Equal("검수 완료", handler.Posts[0].Payload.등록메모);
        var result = await service.GetRequestForReceiptAsync("request/a 01");
        Assert.Equal("request/a 01", result?.의뢰Id);
        Assert.Equal("/api/v1/shipper/requests/request%2Fa%2001", handler.GetPath);
    }

    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public List<(string Path, string Auth, 화주운송의뢰인수증등록요청 Payload)> Posts { get; } = [];
        public string? GetPath;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post)
            {
                Posts.Add((request.RequestUri!.AbsolutePath, request.Headers.Authorization!.ToString(),
                    (await request.Content!.ReadFromJsonAsync<화주운송의뢰인수증등록요청>(ct))!));
                return status == HttpStatusCode.OK
                    ? new(status) { Content = JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = "request/a 01" }) }
                    : new(status) { Content = new StringContent("CargoCompletionRequired") };
            }
            GetPath = request.RequestUri!.AbsolutePath;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = "request/a 01" }) };
        }
    }

    private sealed class DecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response value, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
