using System.Net;
using System.Text;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class ShipperWarehouseListResponseContractTests
{
    private const string Untrusted = "synthetic-private-response-not-for-display";
    private const string AccessToken = "test-only-warehouse-list-access";

    [Theory]
    [InlineData("warehouses", "null")]
    [InlineData("warehouses", "{\"items\":null}")]
    [InlineData("warehouses", "{\"items\":[null]}")]
    [InlineData("inbounds", "null")]
    [InlineData("inbounds", "{\"items\":null}")]
    [InlineData("inbounds", "{\"items\":[null]}")]
    [InlineData("inventory", "null")]
    [InlineData("inventory", "{\"items\":null}")]
    [InlineData("inventory", "{\"items\":[null]}")]
    public async Task 확인불가_창고목록본문은_빈목록이나_null항목으로_반환하지않는다(string endpoint, string body)
    {
        await using var fixture = await Fixture.CreateAsync(HttpStatusCode.OK, body);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(fixture.Service, endpoint));

        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        AssertSafe(error.ToString());
        AssertRequest(fixture, endpoint);
        Assert.True(fixture.Auth.IsLoggedIn);
    }

    [Theory]
    [InlineData("warehouses")]
    [InlineData("inbounds")]
    [InlineData("inventory")]
    public async Task 명시적인빈Items는_성공한빈목록으로_유지한다(string endpoint)
    {
        await using var fixture = await Fixture.CreateAsync(HttpStatusCode.OK, "{\"items\":[]}");

        Assert.Empty(await ReadAsync(fixture.Service, endpoint));

        AssertRequest(fixture, endpoint);
        Assert.True(fixture.Auth.IsLoggedIn);
    }

    [Theory]
    [InlineData("warehouses")]
    [InlineData("inbounds")]
    [InlineData("inventory")]
    public async Task 정상Items는_식별자와_업무내용을_그대로전달한다(string endpoint)
    {
        await using var fixture = await Fixture.CreateAsync(HttpStatusCode.OK, ValidBody(endpoint));

        var row = Assert.Single(await ReadAsync(fixture.Service, endpoint));
        switch (endpoint)
        {
            case "warehouses":
                var warehouse = Assert.IsType<창고요약응답>(row);
                Assert.Equal(701, warehouse.Id);
                Assert.Equal("합성 창고", warehouse.창고명);
                break;
            case "inbounds":
                var inbound = Assert.IsType<입고요청항목응답>(row);
                Assert.Equal(702, inbound.Id);
                Assert.Equal("입고예정", inbound.상태);
                break;
            case "inventory":
                var inventory = Assert.IsType<재고항목응답>(row);
                Assert.Equal(703, inventory.입고상품Id);
                Assert.Equal("합성 상품", inventory.상품명);
                Assert.Equal(4, inventory.가용수량);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint));
        }

        AssertRequest(fixture, endpoint);
    }

    // 세 조회는 같은 HTTP 실패 경계를 쓰므로 각 상태를 한 조회씩 대조합니다.
    [Theory]
    [InlineData("warehouses", HttpStatusCode.Unauthorized, "로그인")]
    [InlineData("inbounds", HttpStatusCode.Forbidden, "권한")]
    [InlineData("inventory", HttpStatusCode.ServiceUnavailable, "서버에서 요청을 처리하지 못했습니다")]
    public async Task HTTP거절은_안전한상태안내를_유지하고_자동재조회하지않는다(
        string endpoint, HttpStatusCode status, string expected)
    {
        await using var fixture = await Fixture.CreateAsync(status, $"{{\"detail\":\"{Untrusted}\"}}");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(fixture.Service, endpoint));

        Assert.Contains(expected, error.Message);
        Assert.Contains($"HTTP {(int)status}", error.Message);
        AssertSafe(error.ToString());
        AssertRequest(fixture, endpoint);
        Assert.True(fixture.Auth.IsLoggedIn);
    }

    [Theory]
    [InlineData("warehouses", "null")]
    [InlineData("inbounds", "{\"items\":null}")]
    [InlineData("inventory", "{\"items\":[null]}")]
    public async Task 작업ViewModel은_확인불가응답을_오류로알리고_명시적정상재조회로_복귀한다(string endpoint, string body)
    {
        await using var fixture = await Fixture.CreateAsync(HttpStatusCode.OK, body);

        switch (endpoint)
        {
            case "warehouses":
                await AssertRecoveryAsync(fixture, endpoint, fixture.Service.GetWarehousesAsync,
                    rows => Assert.Equal(701, Assert.Single(rows).Id));
                break;
            case "inbounds":
                await AssertRecoveryAsync(fixture, endpoint, fixture.Service.GetInboundsAsync,
                    rows => Assert.Equal(702, Assert.Single(rows).Id));
                break;
            case "inventory":
                await AssertRecoveryAsync(fixture, endpoint, fixture.Service.GetInventoryAsync,
                    rows => Assert.Equal(703, Assert.Single(rows).입고상품Id));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(endpoint));
        }
    }

    private static async Task AssertRecoveryAsync<T>(Fixture fixture, string endpoint,
        Func<CancellationToken, Task<IReadOnlyList<T>>> read, Action<IReadOnlyList<T>> verify)
    {
        using var model = new Api작업ViewModel<IReadOnlyList<T>>(read);

        await model.실행Async();

        Assert.True(model.오류발생);
        Assert.False(model.성공함);
        Assert.False(model.처리중);
        Assert.Null(model.결과);
        Assert.False(string.IsNullOrWhiteSpace(model.오류메시지));
        AssertSafe(model.오류메시지!);
        AssertRequest(fixture, endpoint);

        fixture.Handler.Body = ValidBody(endpoint);
        await model.실행Async();

        Assert.True(model.성공함);
        Assert.False(model.오류발생);
        Assert.False(model.처리중);
        Assert.Null(model.오류메시지);
        Assert.Null(model.오류);
        Assert.NotNull(model.결과);
        verify(model.결과);
        Assert.Equal(new[] { Path(endpoint), Path(endpoint) }, fixture.Handler.Paths);
        Assert.True(fixture.Auth.IsLoggedIn);
    }

    private static async Task<IReadOnlyList<object>> ReadAsync(
        ServerBackedShipperOperationsService service, string endpoint)
    {
        return endpoint switch
        {
            "warehouses" => await service.GetWarehousesAsync(),
            "inbounds" => await service.GetInboundsAsync(),
            "inventory" => await service.GetInventoryAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
    }

    private static string ValidBody(string endpoint) => endpoint switch
    {
        "warehouses" => """{"items":[{"id":701,"창고명":"합성 창고"}]}""",
        "inbounds" => """{"items":[{"id":702,"창고Id":701,"상태":"입고예정"}]}""",
        "inventory" => """{"items":[{"입고상품Id":703,"창고Id":701,"상품명":"합성 상품","가용수량":4}]}""",
        _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
    };

    private static string Path(string endpoint) => $"/api/v1/warehouse-operations/{endpoint}";

    private static void AssertRequest(Fixture fixture, string endpoint)
        => Assert.Equal(Path(endpoint), Assert.Single(fixture.Handler.Paths));

    private static void AssertSafe(string message)
    {
        Assert.DoesNotContain(Untrusted, message);
        Assert.DoesNotContain(AccessToken, message);
        Assert.DoesNotContain("api/v1/", message);
        Assert.DoesNotContain("synthetic-warehouse-list-owner", message);
    }

    private sealed class Fixture(HttpClient client, AuthSession auth, ResponseHandler handler,
        TransportRequestLedgerRealtimeClient realtime, ServerBackedShipperOperationsService service) : IAsyncDisposable
    {
        internal AuthSession Auth { get; } = auth;
        internal ResponseHandler Handler { get; } = handler;
        internal ServerBackedShipperOperationsService Service { get; } = service;

        internal static async Task<Fixture> CreateAsync(HttpStatusCode status, string body)
        {
            var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
            await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot("synthetic-warehouse-list-owner", AccessToken));
            var handler = new ResponseHandler { Status = status, Body = body };
            var client = new HttpClient(handler) { BaseAddress = new("https://warehouse-list.example.test/") };
            var observer = new TransportRequestLedgerObserver();
            // 구독 연결은 생략하고 모든 HTTP 조회는 아래 합성 응답기를 거칩니다.
            var realtime = new TransportRequestLedgerRealtimeClient(client.BaseAddress,
                _ => Task.FromResult<string?>(null), observer);
            var authApi = new AuthApiService(client, auth,
                new 꾸미기보유권동기화Service(client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
                new SsalddelMobilePushInstallationClient(client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
            return new(client, auth, handler, realtime, new(client, auth, authApi, observer, realtime));
        }

        public async ValueTask DisposeAsync()
        {
            await realtime.DisposeAsync();
            client.Dispose();
        }
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        internal HttpStatusCode Status { get; set; }
        internal string Body { get; set; } = string.Empty;
        internal List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal(AccessToken, request.Headers.Authorization?.Parameter);
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(Status)
            {
                Content = new StringContent(Body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
