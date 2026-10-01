using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.Text;
using System.Text.Json;

namespace Hongdal.Tests.Ui.Common;

public sealed class HongdalUiCommonServiceCollectionExtensionsTests
{
    [Fact]
    public void AddHongdalCommunityWritingServices_RegistersOnlyReusableWritingBoundary()
    {
        var services = new ServiceCollection();

        services.AddHongdalCommunityWritingServices();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostClient)
            && descriptor.ImplementationType == typeof(CommunityPlatformClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.All(
            new[]
            {
                typeof(PlatformCommunityService),
                typeof(ICommunityParticipationClient),
                typeof(ICommunityLedgerClient),
                typeof(ICommunityProcurementClient),
                typeof(ICommunityVoteClient)
            },
            contractType => Assert.DoesNotContain(services, descriptor =>
                descriptor.ServiceType == contractType));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostComposerDraftStore)
            && descriptor.ImplementationType == typeof(BrowserCommunityPostComposerDraftStore)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(CommunityPostComposerViewModel)
            && descriptor.ImplementationType == typeof(CommunityPostComposerViewModel)
            && descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(CommunityAuthoringAiDraftViewModel));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(CommunityCollectiveActionPageViewModel));
    }

    [Fact]
    public void AddHongdalUiCommonAppServices_RegistersFullCommunityClientBoundary()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ICommunityPostClient)
            && descriptor.ImplementationType == typeof(CommunityPlatformClient)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.All(
            new[]
            {
                typeof(ICommunityParticipationClient),
                typeof(ICommunityLedgerClient),
                typeof(ICommunityProcurementClient),
                typeof(ICommunityVoteClient)
            },
            contractType => Assert.Contains(services, descriptor =>
                descriptor.ServiceType == contractType
                && descriptor.ImplementationFactory is not null
                && descriptor.Lifetime == ServiceLifetime.Scoped));
    }

    [Fact]
    public void AddHongdalCommunityWritingServices_UsesHostAccessTokenProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped<TestAccessTokenProvider>();

        services.AddHongdalCommunityWritingServices<TestAccessTokenProvider>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(
            scope.ServiceProvider.GetRequiredService<TestAccessTokenProvider>(),
            scope.ServiceProvider.GetRequiredService<IHongdalAccessTokenProvider>());
    }

    [Theory]
    [InlineData(typeof(PlatformCommunityService))]
    [InlineData(typeof(PlatformHomeModeStateService))]
    [InlineData(typeof(PlatformDiagramPaletteStateService))]
    public void AddHongdalUiCommonAppServices_RegistersSharedStateAsScoped(
        Type serviceType)
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == serviceType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(serviceType, descriptor.ImplementationType);
    }

    [Fact]
    public void AddHongdalUiCommonAppServices_PreservesExistingRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<PlatformHomeModeStateService>();

        services.AddHongdalUiCommonAppServices();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(PlatformHomeModeStateService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(CommunityAuthoringPeriodStatisticsViewModel))]
    [InlineData(typeof(CommunityAuthoringAiDraftViewModel))]
    [InlineData(typeof(CommunityAuthoringImageGeneratorViewModel))]
    public void AddHongdalUiCommonAppServices_DoesNotRegisterAdminAuthoringToolsWithoutClients(
        Type serviceType)
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        Assert.DoesNotContain(services, candidate => candidate.ServiceType == serviceType);
    }

    [Fact]
    public void AddHongdalUiCommonAppServices_RegistersAgriculturalFisheriesPublicDataClient()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(I농수산공공데이터Client));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(typeof(농수산공공데이터Client), descriptor.ImplementationType);
    }

    [Fact]
    public void AddHongdalUiCommonAppServices_RegistersMvvmApiCompositionServices()
    {
        var services = new ServiceCollection();

        services.AddHongdalUiCommonAppServices();

        Assert.Contains(services, x =>
            x.ServiceType == typeof(IHongdalJsonApiClient)
            && x.ImplementationType == typeof(HongdalJsonApiClient)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공통Controller기능모음ViewModel)
            && x.ImplementationType == typeof(공통Controller기능모음ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매업무Service)
            && x.ImplementationType == typeof(PlatformCommunity공동구매업무Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매공급Service)
            && x.ImplementationType == typeof(PlatformCommunity공동구매공급Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매물류Service)
            && x.ImplementationType == typeof(PlatformCommunity공동구매물류Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매실행Service)
            && x.ImplementationType == typeof(공동구매실행Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I주문원장Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(ICommunityPostComposerDraftStore)
            && x.ImplementationType == typeof(BrowserCommunityPostComposerDraftStore)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityHomePageViewModel)
            && x.ImplementationType == typeof(PlatformCommunityHomePageViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityHomeShellViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityBoardWorkspaceViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityPostEngagementViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityLedgerPickerViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityDiagramWorkspaceViewModel)
            && x.ImplementationType == typeof(PlatformCommunityDiagramWorkspaceViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(PlatformCommunityWarehouseProxyViewModel)
            && x.ImplementationType == typeof(PlatformCommunityWarehouseProxyViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(YouTubeFoodCommunityDiscoveryService)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(YouTubeFoodCommunityDiscoveryViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(ICommunityCollectiveActionSource)
            && x.ImplementationType == typeof(PlatformCommunityCollectiveActionSource)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(CommunityActionExecutionViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(CommunityActionDeliveryViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(CommunityActionTraditionalMarketImportedMeatViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(CommunityActionMarketDayViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(CommunityCollectiveActionPageViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매창고Service)
            && x.ImplementationType == typeof(공동구매창고Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I입출고작업Service)
            && x.ImplementationType == typeof(입출고작업Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I입출고원장조회Service)
            && x.ImplementationType == typeof(PlatformCommunity입출고원장조회Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매원장절차Client)
            && x.ImplementationType == typeof(공동구매원장절차Client)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동수입원장전환Client)
            && x.ImplementationType == typeof(공동수입원장전환Client)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I판매채널Client)
            && x.ImplementationType == typeof(판매채널Client)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I판매채널계정Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I상품등록Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I채널출품Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동구매가격의사결정Service)
            && x.ImplementationType == typeof(공동구매가격의사결정Service)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(IOperatingMarketProfileClient)
            && x.ImplementationType == typeof(OperatingMarketProfileClient)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매화면상태ViewModel)
            && x.ImplementationType == typeof(공동구매화면상태ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매실행상태ViewModel)
            && x.ImplementationType == typeof(공동구매실행상태ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매창고상태ViewModel)
            && x.ImplementationType == typeof(공동구매창고상태ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(입출고화면상태ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(입출고원장상태ViewModel)
            && x.ImplementationType == typeof(입출고원장상태ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매가격의사결정ViewModel)
            && x.ImplementationType == typeof(공동구매가격의사결정ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매실행기능ViewModel)
            && x.ImplementationType == typeof(공동구매실행기능ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매주문집계ViewModel)
            && x.ImplementationType == typeof(공동구매주문집계ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매재고배분ViewModel)
            && x.ImplementationType == typeof(공동구매재고배분ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매입고원장ViewModel)
            && x.ImplementationType == typeof(공동구매입고원장ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(입고ViewModel)
            && x.ImplementationType == typeof(입고ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(출고ViewModel)
            && x.ImplementationType == typeof(출고ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(입출고화면ViewModel)
            && x.ImplementationType == typeof(입출고화면ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(입고조회ViewModel)
            && x.ImplementationType == typeof(입고조회ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(입고예정조회ViewModel)
            && x.ImplementationType == typeof(입고예정조회ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(출고포장ViewModel)
            && x.ImplementationType == typeof(출고포장ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동수입원장물류ViewModel)
            && x.ImplementationType == typeof(공동수입원장물류ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(I공동수입선적통관Client)
            && x.ImplementationType == typeof(공동수입선적통관Client)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동수입선적통관ViewModel)
            && x.ImplementationType == typeof(공동수입선적통관ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동수입통관동기화ViewModel)
            && x.ImplementationType == typeof(공동수입통관동기화ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매협상쟁점합의ViewModel)
            && x.ImplementationType == typeof(공동구매협상쟁점합의ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(국내판매ViewModel)
            && x.ImplementationType == typeof(국내판매ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(판매ViewModel)
            && x.ImplementationType == typeof(판매ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(상품등록ViewModel)
            && x.ImplementationType == typeof(상품등록ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(판매상품조회ViewModel)
            && x.ImplementationType == typeof(판매상품조회ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(주문ViewModel)
            && x.ImplementationType == typeof(주문ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(주문서명등록ViewModel)
            && x.ImplementationType == typeof(주문서명등록ViewModel)
            && x.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(해외수출ViewModel)
            && x.ImplementationType == typeof(해외수출ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매출고원장ViewModel)
            && x.ImplementationType == typeof(공동구매출고원장ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(services, x =>
            x.ServiceType == typeof(공동구매화면ViewModel)
            && x.ImplementationType == typeof(공동구매화면ViewModel)
            && x.Lifetime == ServiceLifetime.Transient);
    }

    [Fact]
    public async Task AddHongdalUiCommonAppServices_SharesFineGrainedViewModelsWithinScope()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new HttpClient
        {
            BaseAddress = new Uri("https://api.hongdal.test/")
        });
        services.AddSingleton<IJSRuntime, TestJsRuntime>();
        services.AddHongdalUiCommonAppServices();

        await using var provider = services.BuildServiceProvider(validateScopes: true);
        await using var scope = provider.CreateAsyncScope();
        var scopedProvider = scope.ServiceProvider;

        Assert.Same(
            scopedProvider.GetRequiredService<입고조회ViewModel>(),
            scopedProvider.GetRequiredService<입고ViewModel>().조회);
        Assert.Same(
            scopedProvider.GetRequiredService<출고포장ViewModel>(),
            scopedProvider.GetRequiredService<출고ViewModel>().포장);
        Assert.Same(
            scopedProvider.GetRequiredService<판매상품조회ViewModel>(),
            scopedProvider.GetRequiredService<판매ViewModel>().상품조회);
        Assert.Same(
            scopedProvider.GetRequiredService<판매상품CrudViewModel>(),
            scopedProvider.GetRequiredService<판매ViewModel>().상품Crud);
        Assert.Same(
            scopedProvider.GetRequiredService<판매상품수정ViewModel>(),
            scopedProvider.GetRequiredService<판매ViewModel>().상품Crud.수정);
        Assert.Same(
            scopedProvider.GetRequiredService<주문서명등록ViewModel>(),
            scopedProvider.GetRequiredService<주문ViewModel>().서명등록);
        Assert.Same(
            scopedProvider.GetRequiredService<주문하위원장관계CrudViewModel>(),
            scopedProvider.GetRequiredService<주문ViewModel>().하위원장관계Crud);
        Assert.Same(
            scopedProvider.GetRequiredService<주문하위원장수정ViewModel>(),
            scopedProvider.GetRequiredService<주문ViewModel>().하위원장수정);

        var warehouse = scopedProvider.GetRequiredService<입출고화면ViewModel>();
        Assert.Same(scopedProvider.GetRequiredService<창고CrudViewModel>(), warehouse.창고Crud);
        Assert.Same(scopedProvider.GetRequiredService<창고사용자CrudViewModel>(), warehouse.창고사용자Crud);
        Assert.Equal(3, warehouse.Crud업무단위목록.Count);
        Assert.Contains(
            scopedProvider.GetRequiredService<창고목록조회ViewModel>(),
            warehouse.기준정보세부업무목록);

        var groupPurchase = scopedProvider.GetRequiredService<공동구매화면ViewModel>();
        Assert.Contains(
            scopedProvider.GetRequiredService<공동구매목록조회조각ViewModel>(),
            groupPurchase.모집.세부업무목록);
        Assert.Contains(
            scopedProvider.GetRequiredService<공동구매협상쟁점합의ViewModel>(),
            groupPurchase.공급.세부업무목록);
        Assert.Contains(
            scopedProvider.GetRequiredService<공동수입통관동기화ViewModel>(),
            groupPurchase.공동수입.세부업무목록);
        Assert.Contains(
            scopedProvider.GetRequiredService<공동구매커머스문서조회ViewModel>(),
            groupPurchase.실행.세부업무목록);
    }

    [Fact]
    public void AddHongdalUiCommonAppServices_UsesRegisteredAccessTokenProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestAccessTokenProvider>();

        services.AddHongdalUiCommonAppServices<TestAccessTokenProvider>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(
            provider.GetRequiredService<TestAccessTokenProvider>(),
            scope.ServiceProvider.GetRequiredService<IHongdalAccessTokenProvider>());
    }

    [Fact]
    public async Task AddHongdalUiCommonAppServices_현재사용자를세부ViewModel과같은Scope에주입한다()
    {
        var tokenProvider = new TestAccessTokenProvider
        {
            AccessToken = CreateToken(
                "warehouse-user-17",
                "입고 담당자",
                ["창고관리자", "창고입고담당자"])
        };
        var services = new ServiceCollection();
        services.AddSingleton(tokenProvider);
        services.AddSingleton<IJSRuntime, TestJsRuntime>();
        services.AddHongdalUiCommonAppServices<TestAccessTokenProvider>();
        services.AddHongdalApiHttpClient(new Uri("https://api.hongdal.test/"));

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IHongdal현재사용자Context>();
        var warehouseState = scope.ServiceProvider.GetRequiredService<입출고화면상태ViewModel>();
        var inbound = scope.ServiceProvider.GetRequiredService<입고조회ViewModel>();
        var sales = scope.ServiceProvider.GetRequiredService<판매채널계정조회ViewModel>();
        var order = scope.ServiceProvider.GetRequiredService<주문조회ViewModel>();

        Assert.Equal("warehouse-user-17", context.현재사용자.UserId);
        Assert.Equal("입고 담당자", context.현재사용자.UserName);
        Assert.True(context.현재사용자.역할보유("창고입고담당자"));
        Assert.Same(context, warehouseState.현재사용자Context);
        Assert.Equal("warehouse-user-17", inbound.현재사용자.UserId);
        Assert.Equal("warehouse-user-17", sales.현재사용자.UserId);
        Assert.Equal("warehouse-user-17", order.현재사용자.UserId);
        Assert.True(inbound.사용자확인됨);
        Assert.True(sales.사용자확인됨);
        Assert.True(order.사용자확인됨);
    }

    [Fact]
    public void 현재사용자Context_사용자식별자가없는Token은익명으로처리한다()
    {
        var tokenProvider = new TestAccessTokenProvider
        {
            AccessToken = CreateToken(null, "이름만 있는 사용자", ["창고관리자"])
        };
        var services = new ServiceCollection();
        services.AddSingleton(tokenProvider);
        services.AddHongdalUiCommonAppServices<TestAccessTokenProvider>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var currentUser = scope.ServiceProvider
            .GetRequiredService<IHongdal현재사용자Context>()
            .현재사용자;

        Assert.False(currentUser.인증됨);
        Assert.Null(currentUser.UserId);
        Assert.Empty(currentUser.Roles);
    }

    [Fact]
    public void AddHongdalApiHttpClient_NormalizesAddressAndPreservesOptions()
    {
        var services = new ServiceCollection();

        services.AddHongdalApiHttpClient(
            new Uri("https://api.hongdal.test/v1"),
            ServiceLifetime.Singleton,
            TimeSpan.FromSeconds(20));

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(HttpClient));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<HttpClient>();
        Assert.Equal(new Uri("https://api.hongdal.test/v1/"), client.BaseAddress);
        Assert.Equal(TimeSpan.FromSeconds(20), client.Timeout);
    }

    [Fact]
    public void AddHongdalApiHttpClient_PreservesExistingRegistration()
    {
        var existingClient = new HttpClient
        {
            BaseAddress = new Uri("https://existing.hongdal.test/")
        };
        var services = new ServiceCollection();
        services.AddSingleton(existingClient);

        services.AddHongdalApiHttpClient(new Uri("https://replacement.hongdal.test/"));

        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(HttpClient));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = services.BuildServiceProvider();
        Assert.Same(existingClient, provider.GetRequiredService<HttpClient>());
    }

    [Theory]
    [InlineData("https://api.hongdal.test/v1", "https://api.hongdal.test/v1/")]
    [InlineData("http://localhost:5104/", "http://localhost:5104/")]
    public void ResolveBaseAddress_NormalizesValidAddress(string value, string expected)
    {
        var result = HongdalApiEndpoint.ResolveBaseAddress(value);

        Assert.Equal(new Uri(expected), result);
    }

    [Fact]
    public void ResolveBaseAddress_RejectsNonHttpAddress()
    {
        Assert.Throws<ArgumentException>(
            () => HongdalApiEndpoint.ResolveBaseAddress("file:///tmp/hongdal"));
    }

    private sealed class TestAccessTokenProvider : IHongdalAccessTokenProvider
    {
        public string AccessToken { get; set; } = "test-token";
    }

    private static string CreateToken(
        string? userId,
        string userName,
        IReadOnlyList<string> roles)
    {
        var payload = new Dictionary<string, object?>
        {
            ["sub"] = userId,
            ["name"] = userName,
            ["roles"] = roles
        };
        return $"{Base64Url("{\"alg\":\"none\"}")}.{Base64Url(JsonSerializer.Serialize(payload))}.";
    }

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class TestJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
