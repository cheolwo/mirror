using System.Net;
using System.Reflection;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using WarehouseManagerApp.Services;
using WarehouseManagerApp.ViewModels.Warehouse;

namespace Ssalddel.Tests.Clients;

public sealed class WarehouseHomeAccessGateTests
{
    [Fact]
    public async Task AnonymousHomeDoesNotReadProtectedWarehouseData()
    {
        using var fixture = new Fixture();

        Assert.False(await fixture.Home.초기화Async());

        Assert.True(fixture.Home.초기화됨);
        Assert.True(fixture.Home.기능사용가능);
        Assert.False(fixture.Home.인증.창고업무접근가능);
        Assert.False(fixture.Home.업무사용가능);
        Assert.Null(fixture.Home.페이지오류메시지);
        Assert.Empty(fixture.Warehouse.Calls);
        Assert.Equal(0, fixture.AuthHttp.Calls);
    }

    [Fact]
    public async Task DisabledEnvironmentDoesNotRestoreAuthenticationOrReadWarehouseData()
    {
        using var fixture = new Fixture("창고관리자");
        fixture.Metadata.Response = () => Task.FromResult(Metadata(enabled: false));

        Assert.False(await fixture.Home.초기화Async());

        Assert.True(fixture.Home.초기화됨);
        Assert.False(fixture.Home.기능사용가능);
        Assert.False(fixture.Home.업무사용가능);
        Assert.Equal(0, fixture.Store.LoadCalls);
        Assert.Empty(fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task AccountWithoutWarehouseRoleDoesNotReadProtectedData()
    {
        using var fixture = new Fixture("Driver");

        Assert.False(await fixture.Home.초기화Async());

        Assert.True(fixture.Home.인증.로그인됨);
        Assert.False(fixture.Home.인증.창고업무접근가능);
        Assert.False(fixture.Home.업무사용가능);
        Assert.Empty(fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task DirectReloadBeforeFeatureAndAuthenticationCheckDoesNothing()
    {
        using var fixture = new Fixture("창고관리자");

        Assert.False(await fixture.Home.인증후조회Async());

        Assert.False(fixture.Home.초기화됨);
        Assert.Equal(0, fixture.Metadata.Calls);
        Assert.Empty(fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task CheckingFeatureHidesBusinessAndBlocksOverlappingInitialization()
    {
        using var fixture = new Fixture("창고관리자");
        var pending = new TaskCompletionSource<VersionFeatureFlagsResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Metadata.Response = () => pending.Task;

        var initializing = fixture.Home.초기화Async();

        Assert.True(fixture.Home.초기화중);
        Assert.False(fixture.Home.초기화됨);
        Assert.False(fixture.Home.업무사용가능);
        Assert.Empty(fixture.Warehouse.Calls);
        Assert.False(await fixture.Home.초기화Async());
        Assert.Equal(1, fixture.Metadata.Calls);

        pending.SetResult(Metadata(enabled: true));
        Assert.True(await initializing);
        Assert.True(fixture.Home.업무사용가능);
    }

    [Fact]
    public async Task MetadataFailureRequiresExplicitRetryBeforeProtectedRead()
    {
        using var fixture = new Fixture("창고관리자");
        fixture.Metadata.Response = () => Task.FromException<VersionFeatureFlagsResponse>(new HttpRequestException("offline"));

        Assert.False(await fixture.Home.초기화Async());
        Assert.NotNull(fixture.Home.페이지오류메시지);
        Assert.False(fixture.Home.업무사용가능);
        Assert.Empty(fixture.Warehouse.Calls);
        Assert.False(await fixture.Home.인증후조회Async());
        Assert.Equal(1, fixture.Metadata.Calls);

        fixture.Metadata.Response = () => Task.FromResult(Metadata(enabled: true));
        Assert.True(await fixture.Home.초기화Async());
        Assert.Null(fixture.Home.페이지오류메시지);
        Assert.True(fixture.Home.업무사용가능);
        Assert.Equal(2, fixture.Metadata.Calls);
        Assert.Equal(new[] { nameof(I입출고작업Service.창고목록조회Async) }, fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task ProtectedReadFailureDoesNotOpenWorkAndExplicitRetryRefreshesGate()
    {
        using var fixture = new Fixture("창고관리자");
        fixture.Warehouse.FailWarehouseList = true;

        Assert.False(await fixture.Home.초기화Async());
        Assert.NotNull(fixture.Home.페이지오류메시지);
        Assert.False(fixture.Home.업무사용가능);
        Assert.Single(fixture.Warehouse.Calls);

        fixture.Warehouse.FailWarehouseList = false;
        Assert.True(await fixture.Home.초기화Async());
        Assert.Null(fixture.Home.페이지오류메시지);
        Assert.True(fixture.Home.업무사용가능);
        Assert.Equal(2, fixture.Metadata.Calls);
        Assert.Equal(2, fixture.Warehouse.Calls.Count);
        Assert.All(fixture.Warehouse.Calls, name => Assert.Equal(nameof(I입출고작업Service.창고목록조회Async), name));
        Assert.Equal(0, fixture.AuthHttp.Calls);
    }

    [Fact]
    public async Task WarehouseAdministratorReadsExistingWarehouseAndSelectedWorkDataWithoutCommands()
    {
        using var fixture = new Fixture("창고관리자");
        fixture.Warehouse.Warehouses = [new 창고요약응답 { Id = 12, 창고명 = "시험 창고", 주소 = "시험 주소" }];

        Assert.True(await fixture.Home.초기화Async());

        Assert.True(fixture.Home.업무사용가능);
        Assert.Equal(12L, fixture.Home.세션.선택된창고?.Id);
        Assert.Equal(new[]
        {
            nameof(I입출고작업Service.창고목록조회Async),
            nameof(I입출고작업Service.입고목록조회Async),
            nameof(I입출고작업Service.재고목록조회Async)
        }, fixture.Warehouse.Calls);
        Assert.Equal(0, fixture.AuthHttp.Calls);
    }

    [Fact]
    public async Task LogoutClosesBusinessAndCannotReadProtectedDataAgain()
    {
        using var fixture = new Fixture("서버관리자");
        Assert.True(await fixture.Home.초기화Async());

        await fixture.Home.인증.로그아웃Async();
        fixture.Home.인증해제적용();

        Assert.False(fixture.Home.업무사용가능);
        Assert.False(await fixture.Home.인증후조회Async());
        Assert.Single(fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task ExplicitHomeCapabilityOverridesEnabledFallbackFlag()
    {
        using var fixture = new Fixture("창고관리자");
        fixture.Metadata.Response = () => Task.FromResult(new VersionFeatureFlagsResponse
        {
            Flags = new Dictionary<string, bool> { ["WarehouseFulfillmentWorkflow"] = true },
            PageCapabilities = [new PageCapabilityDto
            {
                AppCode = SsalddelPageAppCodes.Warehouse,
                RoutePattern = WarehouseManagerRoutes.Warehouse,
                IsFeatureEnabled = false,
                Notice = "이 환경에서는 창고 업무를 제공하지 않습니다."
            }]
        });

        Assert.False(await fixture.Home.초기화Async());

        Assert.False(fixture.Home.기능사용가능);
        Assert.Equal("이 환경에서는 창고 업무를 제공하지 않습니다.", fixture.Home.기능안내);
        Assert.Empty(fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task LateReadAfterLogoutDoesNotReopenWorkOrReadSelectedWarehouseData()
    {
        using var fixture = new Fixture("창고관리자");
        var pending = new TaskCompletionSource<IReadOnlyList<창고요약응답>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Warehouse.ListResponse = () => pending.Task;
        var initializing = fixture.Home.초기화Async();
        Assert.Single(fixture.Warehouse.Calls);

        await fixture.Home.인증.로그아웃Async();
        fixture.Home.인증해제적용();
        pending.SetResult([new 창고요약응답 { Id = 12, 창고명 = "이전 조회 창고" }]);

        Assert.False(await initializing);
        Assert.False(fixture.Home.업무사용가능);
        Assert.False(fixture.Home.인증.창고업무접근가능);
        Assert.Null(fixture.Home.페이지오류메시지);
        Assert.Single(fixture.Warehouse.Calls);
    }

    [Fact]
    public async Task LateReadAfterAccountChangeRequiresFreshExplicitCheck()
    {
        using var fixture = new Fixture("창고관리자");
        var pending = new TaskCompletionSource<IReadOnlyList<창고요약응답>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Warehouse.ListResponse = () => pending.Task;
        var initializing = fixture.Home.초기화Async();

        await fixture.Session.ApplyAsync(Snapshot("second-warehouse-owner"));
        pending.SetResult([new 창고요약응답 { Id = 12, 창고명 = "이전 조회 창고" }]);

        Assert.False(await initializing);
        Assert.False(fixture.Home.업무사용가능);
        Assert.NotNull(fixture.Home.페이지오류메시지);
        Assert.Single(fixture.Warehouse.Calls);

        fixture.Warehouse.ListResponse = null;
        Assert.True(await fixture.Home.초기화Async());
        Assert.True(fixture.Home.업무사용가능);
        Assert.Equal("second-warehouse-owner", fixture.Home.인증.현재사용자Id);
        Assert.Equal(2, fixture.Warehouse.Calls.Count);
    }

    [Fact]
    public async Task LogoutAndLoginWithSameOwnerDoesNotAcceptPreviousGeneration()
    {
        using var fixture = new Fixture("창고관리자");
        var pending = new TaskCompletionSource<IReadOnlyList<창고요약응답>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Warehouse.ListResponse = () => pending.Task;
        var initializing = fixture.Home.초기화Async();

        await fixture.Home.인증.로그아웃Async();
        fixture.Home.인증해제적용();
        await fixture.Session.ApplyAsync(Snapshot("warehouse-test-owner"));
        pending.SetResult([]);

        Assert.False(await initializing);
        Assert.False(fixture.Home.업무사용가능);
        Assert.NotNull(fixture.Home.페이지오류메시지);
        Assert.Single(fixture.Warehouse.Calls);
    }

    private static ClientAuthTokenSnapshot Snapshot(string owner) => new(
        "test-only-access", DateTime.UtcNow.AddHours(1), "test-only-refresh", DateTime.UtcNow.AddDays(1),
        owner, "시험 계정", ["창고관리자"]);

    private static VersionFeatureFlagsResponse Metadata(bool enabled) => new()
    {
        Flags = new Dictionary<string, bool> { ["WarehouseFulfillmentWorkflow"] = enabled }
    };

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient http;
        private readonly 창고기준정보ViewModel warehouseData;
        public TokenStore Store { get; }
        public AuthenticationHttpHandler AuthHttp { get; } = new();
        public MetadataProxy Metadata { get; }
        public WarehouseProxy Warehouse { get; }
        public 창고홈PageViewModel Home { get; }
        public ClientAuthSession Session { get; }

        public Fixture(string? role = null)
        {
            Store = new TokenStore(role is null ? null : new ClientAuthTokenSnapshot(
                "test-only-access", DateTime.UtcNow.AddHours(1), "test-only-refresh", DateTime.UtcNow.AddDays(1),
                "warehouse-test-owner", "시험 계정", [role]));
            var session = Session = new ClientAuthSession(Store, new ClientSessionGuard());
            http = new HttpClient(AuthHttp) { BaseAddress = new Uri("https://warehouse.test/") };
            var auth = new 창고로그인ViewModel(session, new WarehouseAuthApiService(http, session), new WarehouseAccessPolicyService());
            var client = DispatchProxy.Create<ICommunityProcurementClient, MetadataProxy>();
            Metadata = (MetadataProxy)(object)client;
            var warehouseService = DispatchProxy.Create<I입출고작업Service, WarehouseProxy>();
            Warehouse = (WarehouseProxy)(object)warehouseService;
            var state = new 입출고화면상태ViewModel();
            warehouseData = new 창고기준정보ViewModel(warehouseService, state);
            Home = new 창고홈PageViewModel(new 창고작업세션상태ViewModel(state),
                new 창고작업구성Resolver([
                    new 일반입출고작업구성Provider(), new 보세수입작업구성Provider(),
                    new 도심생활물류센터작업구성Provider(), new 마트도심작업구성Provider(),
                    new 공동주택물류작업구성Provider()]), new 창고목록조회ViewModel(warehouseData),
                new 입고조회ViewModel(warehouseService, state, new 입고원장ViewModel(new 입출고원장상태ViewModel())),
                new 출고재고조회ViewModel(warehouseService, state), auth, new WarehousePageAvailabilityService(client));
        }

        public void Dispose()
        {
            Home.Dispose();
            Home.세션.Dispose();
            Home.입고조회.Dispose();
            Home.재고조회.Dispose();
            warehouseData.Dispose();
            http.Dispose();
        }
    }

    public class MetadataProxy : DispatchProxy
    {
        public int Calls { get; private set; }
        public Func<Task<VersionFeatureFlagsResponse>> Response { get; set; } = () => Task.FromResult(Metadata(enabled: true));
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ICommunityProcurementClient.GetVersionWorkflowMetadataAsync))
                throw new InvalidOperationException("창고 홈은 기능 상태 조회 외 공동구매 업무를 실행하지 않습니다.");
            Calls++;
            return Response();
        }
    }

    public class WarehouseProxy : DispatchProxy
    {
        public List<string> Calls { get; } = [];
        public bool FailWarehouseList { get; set; }
        public Func<Task<IReadOnlyList<창고요약응답>>>? ListResponse { get; set; }
        public IReadOnlyList<창고요약응답> Warehouses { get; set; } = [];
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var name = targetMethod!.Name;
            Calls.Add(name);
            return name switch
            {
                nameof(I입출고작업Service.창고목록조회Async) => ListResponse?.Invoke() ?? (FailWarehouseList
                    ? Task.FromException<IReadOnlyList<창고요약응답>>(new HttpRequestException("offline"))
                    : Task.FromResult(Warehouses)),
                nameof(I입출고작업Service.입고목록조회Async) => Task.FromResult<IReadOnlyList<입고요청항목응답>>([]),
                nameof(I입출고작업Service.재고목록조회Async) => Task.FromResult<IReadOnlyList<재고항목응답>>([]),
                _ => throw new InvalidOperationException("창고 홈에서 업무 명령을 실행하면 안 됩니다.")
            };
        }
    }

    private sealed class AuthenticationHttpHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }
    }

    private sealed class TokenStore(ClientAuthTokenSnapshot? snapshot) : IClientSecureTokenStore
    {
        public int LoadCalls { get; private set; }
        public Task<ClientAuthTokenSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        { LoadCalls++; return Task.FromResult(snapshot); }
        public Task SaveAsync(ClientAuthTokenSnapshot value, CancellationToken cancellationToken = default)
        { snapshot = value; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default)
        { snapshot = null; return Task.CompletedTask; }
    }
}
