using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Ssalddel.Client.Infrastructure.Notifications;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using SsalddelApp.Models.Shipper;
using SsalddelApp.Services;
using SsalddelApp.ViewModels.Shipper;

namespace Ssalddel.Tests.Clients;

/// <summary>실제 작성 VM·HTTP adapter의 조건 보존 시험. 지도 좌표 해소·실제 운임 확정·APK 실행 증거는 아닙니다.</summary>
public sealed class ShipperFareEstimateConditionsTests
{
    [Fact]
    public async Task 작성화면견적은_동일좌표차량거리추가비를보내고_지급예정액을자동확정하지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var state = State();
        using var model = new ShipperRequestAuthoringPageViewModel(state, f.Service, new Navigation());

        var quote = await model.EstimateFareAsync(state.차량종류!, state.예상거리Km!.Value);

        Assert.Equal(35364m, quote);
        var request = Assert.Single(f.Handler.Estimates);
        Assert.Equal(state.차량종류, request.차량종류);
        Assert.Equal(2.5m, request.예상거리Km);
        Assert.Equal(37.588m, request.상차위도);
        Assert.Equal(127.085m, request.상차경도);
        Assert.Equal(37.589m, request.하차위도);
        Assert.Equal(127.087m, request.하차경도);
        Assert.Equal(0m, request.대기료);
        Assert.Equal(1500m, request.수작업비);
        Assert.Null(request.할증);
        Assert.Equal(12000m, state.기준운임);
        Assert.Equal(10000, state.기사지급예정운임);
        Assert.Equal(14000, state.결제예정금액);

        // 견적과 등록은 별개 요청이지만 같은 입력 위치·추가비를 사용합니다.
        var draft = state.ToDraft();
        await f.Service.AddRequestAsync(new ShipperRequestItem
        {
            의뢰Id = "synthetic-fare-request", 화물종류 = "합성 상자", 차량종류 = state.차량종류!, 결제수단 = "카드",
            예상거리Km = draft.예상거리Km, 기준운임 = draft.기준운임,
            기사지급예정운임 = draft.기사지급예정운임, 결제예정금액 = draft.결제예정금액,
            대기료 = draft.대기료, 수작업비 = draft.수작업비, 할증 = draft.할증,
            픽업지 = draft.픽업도로명주소, 하차지 = draft.하차도로명주소,
            픽업정보 = ShipperRequestHandoffMapper.CreatePickup(draft), 하차정보 = ShipperRequestHandoffMapper.CreateDropoff(draft)
        });
        var created = Assert.IsType<화주운송의뢰생성요청>(f.Handler.Created);
        Assert.Equal(request.상차위도, created.픽업?.주소.위도);
        Assert.Equal(request.상차경도, created.픽업?.주소.경도);
        Assert.Equal(request.하차위도, created.하차?.주소.위도);
        Assert.Equal(request.하차경도, created.하차?.주소.경도);
        Assert.Equal(request.예상거리Km, created.요금옵션?.예상거리Km);
        Assert.Equal(request.대기료, created.요금옵션?.대기료);
        Assert.Equal(request.수작업비, created.요금옵션?.수작업비);
        Assert.Null(created.요금옵션?.할증);
        Assert.Equal(12000m, created.요금옵션?.최종운임);
    }

    [Fact]
    public async Task 주소만있거나편측좌표만있으면_없는좌표나추가비를채우지않는다()
    {
        await using var f = await Fixture.CreateAsync();
        var state = new 운송의뢰작성ViewModel { 상차도로명주소 = "합성 상차", 하차도로명주소 = "합성 하차", 상차위도 = 0m };
        using var model = new ShipperRequestAuthoringPageViewModel(state, f.Service, new Navigation());
        await model.EstimateFareAsync("오토바이", 3m);
        var request = Assert.Single(f.Handler.Estimates);
        Assert.Equal(0m, request.상차위도);
        Assert.Null(request.상차경도);
        Assert.Null(request.하차위도);
        Assert.Null(request.하차경도);
        Assert.Null(request.대기료);
        Assert.Null(request.수작업비);
        Assert.Null(request.할증);
        var pickup = ShipperRequestHandoffMapper.CreatePickup(state.ToDraft());
        Assert.Equal(0m, pickup.주소.위도);
        Assert.Null(pickup.주소.경도);
    }

    [Fact]
    public async Task 구판거리견적호출은_기존차량거리만직렬화한다()
    {
        await using var f = await Fixture.CreateAsync();
        await f.Service.EstimateFareAsync("1톤 카고", 5m);
        var request = Assert.Single(f.Handler.Estimates);
        Assert.Equal("1톤 카고", request.차량종류);
        Assert.Equal(5m, request.예상거리Km);
        Assert.Null(request.상차위도);
        Assert.Null(request.하차경도);
        Assert.Null(request.수작업비);
    }

    [Fact]
    public async Task 견적실패는_기존운임지급예정액위치를보존한다()
    {
        await using var f = await Fixture.CreateAsync();
        f.Handler.FailEstimate = true;
        var state = State();
        using var model = new ShipperRequestAuthoringPageViewModel(state, f.Service, new Navigation());
        await Assert.ThrowsAsync<InvalidOperationException>(() => model.EstimateFareAsync(state.차량종류!, 2.5m));
        Assert.Equal(12000m, state.기준운임);
        Assert.Equal(10000, state.기사지급예정운임);
        Assert.Equal(14000, state.결제예정금액);
        Assert.Equal(37.588m, state.상차위도);
        Assert.Equal(1500m, state.수작업비);
        Assert.Null(f.Handler.Created);
    }

    [Fact]
    public void 초안왕복은좌표를보존하되_주소변경은그쪽기존좌표만해제한다()
    {
        var original = State();
        var restored = new 운송의뢰작성ViewModel();
        restored.ApplyDraft(original.ToDraft());
        Assert.Equal(original.상차위도, restored.상차위도);
        Assert.Equal(original.상차경도, restored.상차경도);
        Assert.Equal(original.하차위도, restored.하차위도);
        Assert.Equal(original.하차경도, restored.하차경도);
        restored.상차도로명주소 = original.상차도로명주소;
        Assert.Equal(original.상차위도, restored.상차위도);
        restored.상차도로명주소 = "변경된 합성 상차";
        Assert.Null(restored.상차위도);
        Assert.Null(restored.상차경도);
        Assert.Equal(original.하차위도, restored.하차위도);
        restored.하차도로명주소 = "변경된 합성 하차";
        Assert.Null(restored.하차위도);
        Assert.Null(restored.하차경도);
        restored.ApplyDraft(original.ToDraft());
        restored.Reset();
        Assert.Null(restored.상차위도);
        Assert.Null(restored.상차경도);
        Assert.Null(restored.하차위도);
        Assert.Null(restored.하차경도);
    }

    [Fact]
    public async Task 구판서비스는_추가조건없는호출만호환하고_좌표추가비를조용히버리지않는다()
    {
        IShipperOperationsService legacy = new LegacyOperations();
        Assert.Equal(8000m, await legacy.EstimateFareAsync(new 화주운송기준운임견적요청 { 차량종류 = "오토바이", 예상거리Km = 2m }));
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.EstimateFareAsync(new 화주운송기준운임견적요청 { 상차위도 = 0m }));
        await Assert.ThrowsAsync<NotSupportedException>(() => legacy.EstimateFareAsync(new 화주운송기준운임견적요청 { 수작업비 = 100m }));
    }

    private static 운송의뢰작성ViewModel State() => new()
    {
        상차도로명주소 = "합성 상차 주소", 하차도로명주소 = "합성 하차 주소",
        상차위도 = 37.588m, 상차경도 = 127.085m, 하차위도 = 37.589m, 하차경도 = 127.087m,
        차량종류 = "1톤 카고", 예상거리Km = 2.5m, 대기료 = 0m, 수작업비 = 1500m,
        기준운임 = 12000m, 기사지급예정운임 = 10000, 결제예정금액 = 14000
    };

    private sealed class Fixture : IAsyncDisposable
    {
        public Handler Handler { get; } = new();
        public ServerBackedShipperOperationsService Service { get; }
        private readonly HttpClient _client;
        private readonly TransportRequestLedgerRealtimeClient _realtime;
        private Fixture(AuthSession auth)
        {
            _client = new(Handler) { BaseAddress = new("https://test.invalid/") };
            var observer = new TransportRequestLedgerObserver();
            _realtime = new(_client.BaseAddress, _ => Task.FromResult<string?>(null), observer);
            var authApi = new AuthApiService(_client, auth,
                new 꾸미기보유권동기화Service(_client, auth, new DecorationStore(), new PlatformCommunityDecorationStateService()),
                new SsalddelMobilePushInstallationClient(_client, new NullSsalddelMobilePushTokenProvider(), () => auth.AccessToken));
            Service = new(_client, auth, authApi, observer, _realtime);
        }
        public static async Task<Fixture> CreateAsync()
        {
            var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
            await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot());
            return new(auth);
        }
        public async ValueTask DisposeAsync() { await _realtime.DisposeAsync(); _client.Dispose(); }
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<화주운송기준운임견적요청> Estimates { get; } = [];
        public 화주운송의뢰생성요청? Created { get; private set; }
        public bool FailEstimate { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("access-a", request.Headers.Authorization?.Parameter);
            if (request.RequestUri!.AbsolutePath.EndsWith("/fare-estimate", StringComparison.Ordinal))
            {
                Estimates.Add((await request.Content!.ReadFromJsonAsync<화주운송기준운임견적요청>(ct))!);
                return FailEstimate ? new(HttpStatusCode.Conflict) { Content = new StringContent("견적 조건 재확인") }
                    : new(HttpStatusCode.OK) { Content = JsonContent.Create(new 화주운송기준운임견적응답 { 최종운임 = 35364m }) };
            }
            Assert.Equal("/api/v1/shipper/requests", request.RequestUri.AbsolutePath);
            Created = await request.Content!.ReadFromJsonAsync<화주운송의뢰생성요청>(ct);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new 화주운송의뢰응답 { 의뢰Id = "synthetic-fare-request" }) };
        }
    }

    private sealed class DecorationStore : I꾸미기보유권LocalStore
    {
        public Task<노드스티커보유권동기화Response?> LoadAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveAsync(노드스티커보유권동기화Response snapshot, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class Navigation : NavigationManager
    {
        public Navigation() => Initialize("https://test.invalid/", "https://test.invalid/shipper/request/procedure");
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new NotSupportedException();
    }

    private sealed class LegacyOperations : IShipperOperationsService
    {
        public Task<decimal> EstimateFareAsync(string vehicle, decimal distance, CancellationToken ct = default) => Task.FromResult(8000m);
        public Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem?> GetRequestAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ssalddel.Contracts.Common.Warehouse.창고요약응답>> GetWarehousesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ssalddel.Contracts.Common.Inbound.입고요청항목응답>> GetInboundsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Ssalddel.Contracts.Common.Inventory.재고항목응답>> GetInventoryAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ShipperRequestItem> UpdateRequestAsync(ShipperRequestItem r, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteRequestAsync(string id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
