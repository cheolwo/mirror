using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;
using CommonRecommendationTests = Ssalddel.Tests.UiCommon.ShipperVehicleRecommendationR74Tests;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 App/Web 전송 adapter와 모의 HTTP를 대조한다. 실제 API·DB·모바일 실행 증거는 아니다.</summary>
public sealed class ShipperVehicleRecommendationR74Tests
{
    [Fact]
    public async Task App과Web은_현재화물의같은원입력을_전체추천응답과왕복한다()
    {
        var state = CommonRecommendationTests.Cargo();
        state.화물수량 = 3; state.화물부피Cbm = .01m;
        state.화물바닥회전가능여부 = false; state.화물적층가능여부 = false;
        state.화물파손주의여부 = true; state.요청사항 = "포장 방향 유지";
        state.온도조건 = "상온"; state.팔레트개수 = 0;
        var request = state.차량추천입력생성();
        await using var f = await Fixture.CreateAsync();
        var appReply = await f.App.RecommendVehiclesAsync(request);
        var webReply = await f.Web.RecommendVehiclesAsync(request);
        Assert.Equal(2, f.Handler.Requests.Count);
        Assert.All(f.Handler.Requests, sent =>
        {
            Assert.Equal(3, sent.화물수량);
            Assert.Equal(8m, sent.화물중량Kg); // 전체 중량을 수량으로 다시 곱하지 않는다.
            Assert.Equal(.01m, sent.화물부피Cbm);
            Assert.False(sent.화물바닥회전가능여부);
            Assert.False(sent.화물적층가능여부);
            Assert.True(sent.화물파손주의여부);
            Assert.Equal(state.화물설명, sent.화물설명);
            Assert.Equal(state.전송요청사항, sent.요청사항);
            Assert.Equal(state.적재입력.입력지문,
                화물적재입력Policy.계산(화물적재입력Policy.입력생성(sent)).입력지문);
        });
        Assert.Equal(state.적재입력.입력지문, appReply.적재조건?.입력지문);
        Assert.Equal(state.적재입력.입력지문, webReply.적재조건?.입력지문);
        Assert.Equal(.072m, appReply.적재조건?.유효부피Cbm);
    }

    [Fact]
    public async Task App과Web접수는_설명취급조건과원입력부피를추천과같게보낸다()
    {
        var state = CommonRecommendationTests.Cargo();
        state.화물적재형태 = "컨테이너 화물(LCL/FCL)";
        state.요청사항 = "입구에서 먼저 연락";
        state.화물수량 = 2; state.화물부피Cbm = null;
        state.화물적층가능여부 = false;
        state.차량종류 = "검증용 차량"; state.결제수단 = "카드";
        var item = new ShipperRequestItem
        {
            화물종류 = state.화물종류, 화물적재형태 = state.화물적재형태,
            화물원본 = new()
            {
                화물종류 = state.화물종류, 설명 = state.화물설명, 수량 = state.화물수량,
                길이Mm = state.화물길이Mm, 폭Mm = state.화물폭Mm, 높이Mm = state.화물높이Mm,
                중량Kg = state.화물중량Kg, 부피Cbm = state.화물부피Cbm,
                바닥회전가능여부 = state.화물바닥회전가능여부, 적층가능여부 = state.화물적층가능여부
            },
            운송방식 = state.운송방식, 서비스레벨 = state.서비스레벨,
            요청사항 = state.요청사항, 결제수단 = "카드", 차량종류 = state.차량종류
        };
        await using var f = await Fixture.CreateAsync();
        var app = (화주운송의뢰생성요청)typeof(ServerBackedShipperOperationsService)
            .GetMethod("ToCreateRequest", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(f.App, [item])!;
        var web = (화주운송의뢰생성요청)typeof(화주운송의뢰등록Service)
            .GetMethod("ToCreateRequest", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [state, "shipper-a", null, ""])!;
        foreach (var payload in new[] { app, web })
        {
            var snapshot = 화물적재입력Policy.계산(화물적재입력Policy.입력생성(payload.화물!, payload.운송방식,
                payload.요금옵션?.서비스레벨, payload.요금옵션?.요청사항));
            Assert.Equal(state.적재입력.입력지문, snapshot.입력지문);
            Assert.Null(payload.화물?.부피Cbm);
            Assert.Equal(state.화물설명, payload.화물?.설명);
            Assert.Contains(state.화물적재형태, payload.요금옵션?.요청사항);
            Assert.DoesNotContain("추천차량", payload.요금옵션?.요청사항);
        }
        Assert.Equal("입구에서 먼저 연락", state.요청사항);
    }

    [Fact]
    public async Task 초기화는_화물없는간이추천을호출하지않는다()
    {
        // 이 구판 대역의 GetVehicleTypesAsync는 호출하면 예외다.
        using var model = new ShipperRequestAuthoringPageViewModel(CommonRecommendationTests.Cargo(),
            new ShipperRecoveryOperations(), new Navigation());
        await model.InitializeAsync();
        Assert.True(model.RegistrationEnabled);
        Assert.Empty(model.VehicleTypes);
        Assert.Empty(model.State.추천차량종류목록);
    }

    [Fact]
    public async Task Web조회도중계정이바뀌면_이전계정추천을반환하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Handler.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = f.Web.RecommendVehiclesAsync(CommonRecommendationTests.Cargo().차량추천입력생성());
        await f.Handler.Started.Task;
        f.Js.Snapshot = ShipperRecoveryFixture.Snapshot("shipper-b", "access-b");
        await f.WebAuth.RestoreAsync();
        f.Handler.Pending.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Handler Handler { get; } = new();
        public AuthJs Js { get; } = new();
        public WebAuthSessionService WebAuth { get; }
        public ServerBackedShipperOperationsService App { get; }
        public 화주운송의뢰등록Service Web { get; }
        private readonly HttpClient client;
        private readonly TransportRequestLedgerRealtimeClient realtime;
        private Fixture(AuthSession auth)
        {
            client = new(Handler) { BaseAddress = new("https://test.invalid/") };
            var observer = new TransportRequestLedgerObserver();
            realtime = new(client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
            var authApi = new AuthApiService(client, auth,
                new 꾸미기보유권동기화Service(client, auth, new UnusedDecorationStore(), new PlatformCommunityDecorationStateService()),
                new SsalddelMobilePushInstallationClient(client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
            App = new(client, auth, authApi, observer, realtime);
            WebAuth = new(client, Js, new ClientSessionGuard());
            Web = new(client, WebAuth, observer, Js);
        }
        public static async Task<Fixture> CreateAsync()
        {
            var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
            await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
            return new(auth);
        }
        public async ValueTask DisposeAsync() { await realtime.DisposeAsync(); client.Dispose(); }
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<차량추천요청> Requests { get; } = [];
        public TaskCompletionSource? Pending;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
        {
            if (message.RequestUri!.AbsolutePath != "/api/v1/shipper/requests/recommend-vehicle")
                return new(HttpStatusCode.ServiceUnavailable); // 실시간 연결은 별도이며 외부로 연결하지 않는다.
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal("access-a", message.Headers.Authorization?.Parameter);
            var body = (await message.Content!.ReadFromJsonAsync<차량추천요청>(ct))!;
            Requests.Add(body); Started.TrySetResult();
            if (Pending is not null) await Pending.Task;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(CommonRecommendationTests.Response(body)) };
        }
    }

    private sealed class AuthJs : IJSRuntime
    {
        public ClientAuthTokenSnapshot Snapshot = ShipperRecoveryFixture.Snapshot();
        public ValueTask<T> InvokeAsync<T>(string name, object?[]? args) => InvokeAsync<T>(name, default, args);
        public ValueTask<T> InvokeAsync<T>(string name, CancellationToken ct, object?[]? args)
        {
            Assert.Equal("localStorage.getItem", name);
            return ValueTask.FromResult((T)(object)JsonSerializer.Serialize(Snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://test.invalid/", "https://test.invalid/shipper/request");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }
    private sealed class UnusedDecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
