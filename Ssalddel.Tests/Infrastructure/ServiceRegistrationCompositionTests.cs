using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Community;
using Ssalddel.Application.Settlement;
using Ssalddel.Application.Warehouse;
using Ssalddel.Extensions;
using Ssalddel.Services.AgriculturalFisheries.Information;
using Ssalddel.Services.Auth;
using Ssalddel.Services.Community;
using Ssalddel.Services.Content;
using Ssalddel.Services.Education;
using Ssalddel.Services.Food;
using Ssalddel.Services.Notifications;
using Ssalddel.Services.Orderer;
using Ssalddel.Services.Speech;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Infrastructure;

public sealed class ServiceRegistrationCompositionTests
{
    [Fact]
    public void 커뮤니티등록은_원형의전체descriptor순서와수명을유지한다()
    {
        var services = new ServiceCollection();
        Assert.Same(services, AddCommunity(services));
        AssertDescriptors(CommunityBaseline, services);
        Assert.Equal(183, services.Count);
        Assert.Equal(8, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void 커뮤니티재등록은_일반Add를중복하고_worker와공통Options는중복등록하지않는다()
    {
        var services = new ServiceCollection();
        AddCommunity(services);
        AddCommunity(services);

        AssertDescriptors(
            [.. CommunityBaseline, .. CommunityBaseline.Where(shape => shape.ServiceType != typeof(IHostedService)
                && !CommonOptionsServiceTypes.Contains(shape.ServiceType))], services);
        Assert.Equal(353, services.Count);
        Assert.Equal(8, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    }

    [Fact]
    public void 커뮤니티별칭9개와호환Context는_scope의같은구현을공유한다()
    {
        var services = new ServiceCollection();
        AddCommunity(services);
        // 생성자에 필요한 외부 저장소만 대체하고 실제 별칭·호환 Context factory는 그대로 검증한다.
        services.Replace(ServiceDescriptor.Scoped<게시글원장선택조회Service>(_ => new(null!, null!, null!)));
        services.Replace(ServiceDescriptor.Scoped<게시글원장표시ContextService>(_ => new(null!, null!, null!)));
        services.Replace(ServiceDescriptor.Scoped<생활협업UseCase>(_ => new(null!, null!, null!, TimeProvider.System)));
        services.Replace(ServiceDescriptor.Scoped<공간공동운영UseCase>(_ => new(null!, null!, null!, null!, TimeProvider.System, null!)));
        services.Replace(ServiceDescriptor.Scoped<음식마트원장동기화OutboxService>(_ => new(null!, null!, NullLogger<음식마트원장동기화OutboxService>.Instance)));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        AssertCommunityAliases(first.ServiceProvider);
        AssertCommunityAliases(second.ServiceProvider);
        Assert.NotSame(first.ServiceProvider.GetRequiredService<게시글원장선택조회Service>(), second.ServiceProvider.GetRequiredService<게시글원장선택조회Service>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<게시글원장표시ContextService>(), second.ServiceProvider.GetRequiredService<게시글원장표시ContextService>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<게시글원장ContextService>(), second.ServiceProvider.GetRequiredService<게시글원장ContextService>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<생활협업UseCase>(), second.ServiceProvider.GetRequiredService<생활협업UseCase>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<공간공동운영UseCase>(), second.ServiceProvider.GetRequiredService<공간공동운영UseCase>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<음식마트원장동기화OutboxService>(), second.ServiceProvider.GetRequiredService<음식마트원장동기화OutboxService>());
        var query = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(I생활협업연결Query));
        Assert.Equal(typeof(생활협업연결Query), query.ImplementationType);
        Assert.Null(query.ImplementationFactory);
    }

    [Fact]
    public void 분산등록은_원형descriptor와_worker앞뒤순서를유지한다()
    {
        var configuration = new ConfigurationBuilder().Build();
        var original = new ServiceCollection();
        AddDistributedBaseline(original, configuration);
        var services = new ServiceCollection();
        Assert.Same(services, services.AddDistributedFulfillment(configuration));
        AssertDescriptors(original.Select(Shape).ToArray(), services);
        var worker = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Equal(typeof(DistributedPhotoRetentionWorker), worker.ImplementationType);
        var workerIndex = services.IndexOf(worker);
        Assert.Equal(typeof(I분산사진보존연결), services[workerIndex - 1].ServiceType);
        Assert.Equal(typeof(I분산주문원장연결Adapter), services[workerIndex + 1].ServiceType);
    }

    [Fact]
    public void 분산사진별칭은_scope내같은객체를쓰고_scope사이에는분리된다()
    {
        var services = new ServiceCollection();
        services.AddDistributedFulfillment(new ConfigurationBuilder().Build());
        services.Replace(ServiceDescriptor.Scoped<DistributedPhotoEvidenceService>(_ =>
            new(null!, null!, null!, Options.Create(new DistributedFulfillmentOptions()), TimeProvider.System)));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var firstPhoto = first.ServiceProvider.GetRequiredService<DistributedPhotoEvidenceService>();
        var secondPhoto = second.ServiceProvider.GetRequiredService<DistributedPhotoEvidenceService>();
        Assert.Same(firstPhoto, first.ServiceProvider.GetRequiredService<IDistributedPhotoEvidenceBinder>());
        Assert.Same(secondPhoto, second.ServiceProvider.GetRequiredService<IDistributedPhotoEvidenceBinder>());
        Assert.NotSame(firstPhoto, secondPhoto);
    }

    [Fact]
    public void TryAdd는_미리등록한시계와후보및보존과정산descriptor를교체하지않는다()
    {
        var services = new ServiceCollection();
        var clock = ServiceDescriptor.Singleton<TimeProvider>(new TestClock());
        var candidates = Marker<IDistributedBuildingCandidateProvider>();
        var hold = Marker<I분산사진보존연결>();
        var settlementSource = Marker<INeighborhoodSettlementSourceResolver>();
        var settlement = Marker<INeighborhoodSettlementUseCase>();
        var costReader = Marker<I주문별원가보수조회Service>();
        ServiceDescriptor[] markers = [clock, candidates, hold, settlementSource, settlement, costReader];
        foreach (var marker in markers) ((IServiceCollection)services).Add(marker);
        services.AddDistributedFulfillment(new ConfigurationBuilder().Build());

        foreach (var marker in markers)
            Assert.Same(marker, Assert.Single(services, descriptor => descriptor.ServiceType == marker.ServiceType));
    }

    [Fact]
    public void 분산재등록은_원형의Add중복과TryAdd및worker중복제외를유지한다()
    {
        var configuration = new ConfigurationBuilder().Build();
        var original = new ServiceCollection();
        AddDistributedBaseline(original, configuration);
        AddDistributedBaseline(original, configuration);
        var services = new ServiceCollection();
        services.AddDistributedFulfillment(configuration);
        services.AddDistributedFulfillment(configuration);
        AssertDescriptors(original.Select(Shape).ToArray(), services);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDistributedBuildingCandidateProvider));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(I분산사진보존연결));
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(DistributedPhotoEvidenceService)));
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IDistributedPhotoEvidenceBinder)));
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IDistributedFulfillmentUseCase)));
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<DistributedFulfillmentOptions>)));
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IConfigureOptions<NeighborhoodSettlementOptions>)));
    }

    [Fact]
    public void 빈설정의분산과정산은_기존비활성과기본값을유지한다()
    {
        var services = new ServiceCollection();
        services.AddDistributedFulfillment(new ConfigurationBuilder().Build());
        using var provider = services.BuildServiceProvider();
        var distributed = provider.GetRequiredService<IOptions<DistributedFulfillmentOptions>>().Value;
        Assert.False(distributed.Enabled);
        Assert.Equal(string.Empty, distributed.Profile);
        Assert.False(distributed.UseExistingRouteProvider);
        Assert.Equal(8, distributed.MaximumRouteStops);
        var settlement = provider.GetRequiredService<IOptions<NeighborhoodSettlementOptions>>().Value;
        Assert.False(settlement.Enabled);
        Assert.Equal(NeighborhoodSettlementOptions.RequiredProfile, settlement.Profile);
        Assert.Equal(string.Empty, settlement.PlatformPayeeUserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 명시한분산과정산설정은_기존section에서결속한다(bool enabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DistributedFulfillment:Enabled"] = enabled.ToString(),
            ["DistributedFulfillment:Profile"] = "fixture-distributed",
            ["DistributedFulfillment:UseExistingRouteProvider"] = "true",
            ["DistributedFulfillment:MaximumRouteStops"] = "17",
            ["NeighborhoodSettlement:Enabled"] = enabled.ToString(),
            ["NeighborhoodSettlement:Profile"] = "fixture-settlement",
            ["NeighborhoodSettlement:PlatformPayeeUserId"] = "fixture-payee"
        }).Build();
        var services = new ServiceCollection();
        services.AddDistributedFulfillment(configuration);
        using var provider = services.BuildServiceProvider();
        var distributed = provider.GetRequiredService<IOptions<DistributedFulfillmentOptions>>().Value;
        Assert.Equal(enabled, distributed.Enabled);
        Assert.Equal("fixture-distributed", distributed.Profile);
        Assert.True(distributed.UseExistingRouteProvider);
        Assert.Equal(17, distributed.MaximumRouteStops);
        var settlement = provider.GetRequiredService<IOptions<NeighborhoodSettlementOptions>>().Value;
        Assert.Equal(enabled, settlement.Enabled);
        Assert.Equal("fixture-settlement", settlement.Profile);
        Assert.Equal("fixture-payee", settlement.PlatformPayeeUserId);
    }

    [Fact]
    public void 커뮤니티worker옵션은_기존켜짐과꺼짐의차이를유지한다()
    {
        Assert.True(new CommunityKeywordNotificationOptions().Enabled);
        Assert.True(new CommunityLedgerProjectionOptions().Enabled);
        Assert.True(new CommunityPostPublicationOptions().Enabled);
        Assert.True(new GroupPurchaseDemandProcessManagerOptions().Enabled);
        Assert.True(new CommunityActivityBoardContentOptions().EnsureAnnouncementsAtStartup);
        Assert.False(new CommunityActivityBoardContentOptions().SeedTestActivityPosts);
        Assert.False(new 교육기관제출Options().자동전송활성화);
        Assert.False(new CommunityPostAudioOptions().Enabled);
        Assert.False(new CommunityPostEmailNotificationOptions().Enabled);
    }

    private static void AssertCommunityAliases(IServiceProvider provider)
    {
        var selection = provider.GetRequiredService<게시글원장선택조회Service>();
        var display = provider.GetRequiredService<게시글원장표시ContextService>();
        var context = provider.GetRequiredService<게시글원장ContextService>();
        var collaboration = provider.GetRequiredService<생활협업UseCase>();
        var spaceCooperation = provider.GetRequiredService<공간공동운영UseCase>();
        var outbox = provider.GetRequiredService<음식마트원장동기화OutboxService>();
        Assert.Same(selection, provider.GetRequiredService<I게시글원장선택조회Service>());
        Assert.Same(display, provider.GetRequiredService<I게시글원장표시ContextService>());
        Assert.Same(context, provider.GetRequiredService<I게시글원장ContextService>());
        Assert.Same(selection, typeof(게시글원장ContextService).GetField("_선택조회Service", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context));
        Assert.Same(display, typeof(게시글원장ContextService).GetField("_표시ContextService", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(context));
        Assert.Same(collaboration, provider.GetRequiredService<I생활협업UseCase>());
        Assert.Same(spaceCooperation, provider.GetRequiredService<I공간공동운영UseCase>());
        Assert.Same(spaceCooperation, provider.GetRequiredService<I공간공동운영연결Query>());
        Assert.Same(outbox, provider.GetRequiredService<I음식마트원장동기화OutboxService>());
        Assert.Same(outbox, provider.GetRequiredService<I음식마트원장동기화복구Service>());
        Assert.Same(outbox, provider.GetRequiredService<I음식기사후속OutboxService>());
    }

    private static IServiceCollection AddCommunity(IServiceCollection services)
        => (IServiceCollection)typeof(Ssalddel.Extensions.ServiceCollectionExtensions).GetMethod(
            "AddSsalddelCommunityDomainServices", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [services])!;

    private static ServiceDescriptor Marker<T>() where T : class
        => ServiceDescriptor.Scoped<T>(_ => throw new InvalidOperationException("사전 등록 보존 검사는 업무 서비스를 resolve하지 않습니다."));

    private static DescriptorShape Shape(ServiceDescriptor descriptor)
        => new(descriptor.ServiceType, descriptor.Lifetime, descriptor.ImplementationType,
            descriptor.ImplementationFactory is not null, descriptor.ImplementationInstance?.GetType());

    private static void AssertDescriptors(DescriptorShape[] expected, IServiceCollection actual)
        => Assert.Equal(expected, actual.Select(Shape).ToArray());

    private sealed record DescriptorShape(Type ServiceType, ServiceLifetime Lifetime, Type? ImplementationType,
        bool Factory = false, Type? InstanceType = null);

    private sealed class TestClock : TimeProvider { }

    // AddOptions의 공통 다섯 등록은 TryAdd입니다. 공고 전용 Configure/ChangeToken은 일반 Add로 유지됩니다.
    private static readonly HashSet<Type> CommonOptionsServiceTypes =
    [typeof(IOptions<>), typeof(IOptionsSnapshot<>), typeof(IOptionsMonitor<>), typeof(IOptionsFactory<>), typeof(IOptionsMonitorCache<>)];

    // facade의 기존 원형과 명시적으로 추가한 공동운영·화물 정보 공고 등록.
    // framework의 순서·수명·factory 여부도 검증하며 기존 별칭 대상은 scope 동일성 검사에서 고정한다.
    private static readonly DescriptorShape[] CommunityBaseline =
    [
        new(typeof(Mongo커뮤니티원장저장소), ServiceLifetime.Singleton, typeof(Mongo커뮤니티원장저장소)),
        new(typeof(I커뮤니티원장투영작업저장소), ServiceLifetime.Singleton, typeof(Mongo커뮤니티원장투영작업저장소)),
        new(typeof(I커뮤니티원장저장소), ServiceLifetime.Singleton, typeof(이벤트발행커뮤니티원장저장소)),
        new(typeof(IBusinessCaseStore), ServiceLifetime.Singleton, typeof(BusinessCaseStoreAdapter)),
        new(typeof(I커뮤니티원장공유정책저장소), ServiceLifetime.Singleton, typeof(Mongo커뮤니티원장공유정책저장소)),
        new(typeof(I커뮤니티원장공유Service), ServiceLifetime.Scoped, typeof(커뮤니티원장공유Service)),
        new(typeof(ICommunityLedgerRoleAccessPolicyStore), ServiceLifetime.Singleton, typeof(MongoCommunityLedgerRoleAccessPolicyStore)),
        new(typeof(ICommunityCustomsBrokerDirectory), ServiceLifetime.Scoped, typeof(EfCommunityCustomsBrokerDirectory)),
        new(typeof(ICommunityLedgerRoleAccessService), ServiceLifetime.Scoped, typeof(CommunityLedgerRoleAccessService)),
        new(typeof(ICommunityLedgerBlockAssignmentService), ServiceLifetime.Scoped, typeof(CommunityLedgerBlockAssignmentService)),
        new(typeof(I주문원장서명저장소), ServiceLifetime.Singleton, typeof(Mongo주문원장서명저장소)),
        new(typeof(I주문원장공개요청저장소), ServiceLifetime.Singleton, typeof(Mongo주문원장공개요청저장소)),
        new(typeof(I주문원장통합UseCase), ServiceLifetime.Scoped, typeof(주문원장통합UseCase)),
        new(typeof(I무역확장원장UseCase), ServiceLifetime.Scoped, typeof(무역확장원장UseCase)),
        new(typeof(I원장관행문서초안UseCase), ServiceLifetime.Scoped, typeof(원장관행문서초안UseCase)),
        new(typeof(I원장관행문서보관UseCase), ServiceLifetime.Scoped, typeof(원장관행문서보관UseCase)),
        new(typeof(I주문원장서명UseCase), ServiceLifetime.Scoped, typeof(주문원장서명UseCase)),
        new(typeof(I주문원장역할별조회Service), ServiceLifetime.Scoped, typeof(주문원장역할별조회Service)),
        new(typeof(I주문원장공개요청Service), ServiceLifetime.Scoped, typeof(주문원장공개요청Service)),
        new(typeof(I커뮤니티대화저장소), ServiceLifetime.Singleton, typeof(Mongo커뮤니티대화저장소)),
        new(typeof(I커뮤니티원장상태이벤트Service), ServiceLifetime.Scoped, typeof(커뮤니티원장상태이벤트Service)),
        new(typeof(ICommunityLedgerCompletionPostStore), ServiceLifetime.Scoped, typeof(EfCommunityLedgerCompletionPostStore)),
        new(typeof(ICommunityLedgerCompletionPostService), ServiceLifetime.Scoped, typeof(CommunityLedgerCompletionPostService)),
        new(typeof(ICommunityAutomatedPostPublisher), ServiceLifetime.Scoped, typeof(EfCommunityAutomatedPostPublisher)),
        new(typeof(I커뮤니티활동공개ProjectionRecorder), ServiceLifetime.Scoped, typeof(커뮤니티활동공개ProjectionRecorder)),
        new(typeof(ICommunityActivityPostPublisher), ServiceLifetime.Scoped, typeof(CommunityActivityPostPublisher)),
        new(typeof(ICommunityActivityBoardContentService), ServiceLifetime.Scoped, typeof(CommunityActivityBoardContentService)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(CommunityKamisPriceBriefSource)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(CommunityUsdaNassPriceBriefSource)),
        new(typeof(I주간국가농수산물비교SnapshotService), ServiceLifetime.Scoped, typeof(주간국가농수산물비교SnapshotService)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(Community주간국가농수산물비교Source)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(CommunityReflectionSource)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(CommunityActivityDigestSource)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(CommunityCultureTransportPostSource)),
        new(typeof(ICommunityAutomatedPostSource), ServiceLifetime.Scoped, typeof(CommunityPrajnaPostSource)),
        new(typeof(I같이수입원장관세사알림Service), ServiceLifetime.Scoped, typeof(같이수입원장관세사알림Service)),
        new(typeof(I공동구매원장관계자알림Service), ServiceLifetime.Scoped, typeof(공동구매원장관계자알림Service)),
        new(typeof(게시글원장선택조회Service), ServiceLifetime.Scoped, typeof(게시글원장선택조회Service)),
        new(typeof(I게시글원장선택조회Service), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(게시글원장표시ContextService), ServiceLifetime.Scoped, typeof(게시글원장표시ContextService)),
        new(typeof(I게시글원장표시ContextService), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(게시글원장ContextService), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(I게시글원장ContextService), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(IAzureTranslatorAccessTokenProvider), ServiceLifetime.Singleton, typeof(AzureTranslatorAccessTokenProvider)),
        new(typeof(I생활교류공개지역Source), ServiceLifetime.Singleton, typeof(Official생활교류공개지역Source)),
        new(typeof(I생활교류지도조회UseCase), ServiceLifetime.Scoped, typeof(생활교류지도조회UseCase)),
        new(typeof(I생활협업Store), ServiceLifetime.Scoped, typeof(Mongo생활협업Store)),
        new(typeof(I생활배송협업Guard), ServiceLifetime.Scoped, typeof(생활배송협업Guard)),
        new(typeof(I생활배송배차선택Service), ServiceLifetime.Scoped, typeof(생활배송배차선택Service)),
        new(typeof(I생활협업보관예약Guard), ServiceLifetime.Scoped, typeof(생활협업보관예약Guard)),
        new(typeof(생활협업UseCase), ServiceLifetime.Scoped, typeof(생활협업UseCase)),
        new(typeof(I생활협업UseCase), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(I생활협업연결Query), ServiceLifetime.Scoped, typeof(생활협업연결Query)),
        new(typeof(I생활협업배송Source), ServiceLifetime.Scoped, typeof(Ef생활협업배송Source)),
        new(typeof(I생활협업공간Source), ServiceLifetime.Scoped, typeof(생활협업공간Source)),
        new(typeof(I생활협업보관상태Source), ServiceLifetime.Scoped, typeof(생활협업보관상태Source)),
        new(typeof(I생활보관공간Store), ServiceLifetime.Scoped, typeof(Mongo생활보관공간Store)),
        new(typeof(생활보관인계정보Protection), ServiceLifetime.Scoped, typeof(생활보관인계정보Protection)),
        new(typeof(I생활보관공간Service), ServiceLifetime.Scoped, typeof(생활보관공간Service)),
        new(typeof(I생활보관협업ContextSource), ServiceLifetime.Scoped, typeof(생활보관협업ContextSource)),
        new(typeof(IOptions<>), ServiceLifetime.Singleton,
            typeof(IOptions<>).Assembly.GetType("Microsoft.Extensions.Options.UnnamedOptionsManager`1", throwOnError: true)!),
        new(typeof(IOptionsSnapshot<>), ServiceLifetime.Scoped, typeof(OptionsManager<>)),
        new(typeof(IOptionsMonitor<>), ServiceLifetime.Singleton, typeof(OptionsMonitor<>)),
        new(typeof(IOptionsFactory<>), ServiceLifetime.Transient, typeof(OptionsFactory<>)),
        new(typeof(IOptionsMonitorCache<>), ServiceLifetime.Singleton, typeof(OptionsCache<>)),
        new(typeof(IConfigureOptions<화물정보공고Options>), ServiceLifetime.Transient, null, Factory: true),
        new(typeof(IOptionsChangeTokenSource<화물정보공고Options>), ServiceLifetime.Singleton, null, Factory: true),
        new(typeof(I화물정보공고Store), ServiceLifetime.Scoped, typeof(Mongo화물정보공고Store)),
        new(typeof(I화물정보공고UseCase), ServiceLifetime.Scoped, typeof(화물정보공고UseCase)),
        new(typeof(I공간공동운영Store), ServiceLifetime.Scoped, typeof(공간공동운영원장Store)),
        new(typeof(I공간공동운영전문업무Bridge), ServiceLifetime.Scoped, typeof(공간공동운영전문업무Bridge)),
        new(typeof(공간공동운영UseCase), ServiceLifetime.Scoped, typeof(공간공동운영UseCase)),
        new(typeof(I공간공동운영UseCase), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(I공간공동운영연결Query), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(Ssalddel.Application.Mart.I공간공동운영주문UseCase), ServiceLifetime.Scoped, typeof(Ssalddel.Application.Mart.공간공동운영주문UseCase)),
        new(typeof(I공간공동운영통합Query), ServiceLifetime.Scoped, typeof(공간공동운영통합Query)),
        new(typeof(I공간공동운영업무연락Query), ServiceLifetime.Scoped, typeof(공간공동운영업무연락Query)),
        new(typeof(I공간공동운영예약UseCase), ServiceLifetime.Scoped, typeof(공간공동운영예약UseCase)),
        new(typeof(ICommunityPostTranslationService), ServiceLifetime.Scoped, typeof(CommunityPostTranslationService)),
        new(typeof(ICommunityPostOpportunityAnalyzer), ServiceLifetime.Singleton, typeof(CommunityPostOpportunityAnalyzer)),
        new(typeof(ICommunityDynamicTopicClassifier), ServiceLifetime.Singleton, typeof(CommunityDynamicTopicClassifier)),
        new(typeof(ICommunityPostOpportunityStore), ServiceLifetime.Scoped, typeof(EfCommunityPostOpportunityStore)),
        new(typeof(ICommunityProfessionalEligibilityService), ServiceLifetime.Scoped, typeof(CommunityProfessionalEligibilityService)),
        new(typeof(ICommunityPostProfessionalParticipationService), ServiceLifetime.Scoped, typeof(CommunityPostProfessionalParticipationService)),
        new(typeof(ICommunityActionJourneyService), ServiceLifetime.Scoped, typeof(CommunityActionJourneyService)),
        new(typeof(ICommunityDynamicDiscoveryService), ServiceLifetime.Scoped, typeof(CommunityDynamicDiscoveryService)),
        new(typeof(ICommunityPostOpportunityQueryUseCase), ServiceLifetime.Scoped, typeof(CommunityPostOpportunityQueryUseCase)),
        new(typeof(ICommunityPostMeatImportReadinessUseCase), ServiceLifetime.Scoped, typeof(CommunityPostMeatImportReadinessUseCase)),
        new(typeof(ICommunityPostParticipationUseCase), ServiceLifetime.Scoped, typeof(CommunityPostParticipationUseCase)),
        new(typeof(ICommunityPostOpportunityService), ServiceLifetime.Scoped, typeof(CommunityPostOpportunityService)),
        new(typeof(I지도신청가원장UseCase), ServiceLifetime.Scoped, typeof(지도신청가원장UseCase)),
        new(typeof(I커뮤니티세계지도원장ProjectionCache), ServiceLifetime.Singleton, typeof(커뮤니티세계지도원장ProjectionCache)),
        new(typeof(I커뮤니티세계지도원장ProjectionUseCase), ServiceLifetime.Scoped, typeof(커뮤니티세계지도원장ProjectionUseCase)),
        new(typeof(I지도신청운송취소검토AdminWorkflow), ServiceLifetime.Scoped, typeof(지도신청운송취소검토AdminWorkflow)),
        new(typeof(I원장다이어그램실시간알림Service), ServiceLifetime.Scoped, typeof(원장다이어그램SignalR알림Service)),
        new(typeof(I커뮤니티원장업무투영동기화Service), ServiceLifetime.Scoped, typeof(커뮤니티원장업무투영동기화Service)),
        new(typeof(I원장업무투영동기화Handler), ServiceLifetime.Scoped, typeof(운송원장업무투영Handler)),
        new(typeof(I원장업무투영동기화Handler), ServiceLifetime.Scoped, typeof(입출고원장업무투영Handler)),
        new(typeof(I원장업무투영동기화Handler), ServiceLifetime.Scoped, typeof(음식주문원장업무투영Handler)),
        new(typeof(I운송원장Mongo동기화Service), ServiceLifetime.Scoped, typeof(운송원장Mongo동기화Service)),
        new(typeof(I음식마트원장Mongo동기화Service), ServiceLifetime.Scoped, typeof(음식마트원장Mongo동기화Service)),
        new(typeof(음식마트원장동기화OutboxService), ServiceLifetime.Scoped, typeof(음식마트원장동기화OutboxService)),
        new(typeof(I음식마트원장동기화OutboxService), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(I음식마트원장동기화복구Service), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(I음식기사후속OutboxService), ServiceLifetime.Scoped, null, Factory: true),
        new(typeof(I음식배차요청OutboxService), ServiceLifetime.Scoped, typeof(음식배차요청OutboxService)),
        new(typeof(I교육기관제출대기열), ServiceLifetime.Singleton, typeof(Mongo교육기관제출대기열)),
        new(typeof(I현장체험활동UseCase), ServiceLifetime.Scoped, typeof(현장체험활동UseCase)),
        new(typeof(I교육과정정의Service), ServiceLifetime.Scoped, typeof(교육과정정의Service)),
        new(typeof(I교육과정참여Service), ServiceLifetime.Scoped, typeof(교육과정참여Service)),
        new(typeof(ITypecast음성카탈로그저장소), ServiceLifetime.Scoped, typeof(EfTypecast음성카탈로그저장소)),
        new(typeof(ITypecast음성카탈로그Service), ServiceLifetime.Scoped, typeof(Typecast음성카탈로그Service)),
        new(typeof(IYouTube채널감시저장소), ServiceLifetime.Scoped, typeof(EfYouTube채널감시저장소)),
        new(typeof(IYouTube채널감시Service), ServiceLifetime.Scoped, typeof(YouTube채널감시Service)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityYouTubeInformationCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityKamisInformationCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityUsdaNassInformationCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityAbsFoodPriceIndexInformationCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityFishCooperativeInformationCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityMfdsCookRecipeCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityRdaLocalFoodRecipeCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityMaffRegionalCuisineRecipeCandidateSource)),
        new(typeof(ICommunityInformationCandidateSource), ServiceLifetime.Scoped, typeof(CommunityNhsHealthierFamiliesRecipeCandidateSource)),
        new(typeof(ICommunityInformationCollectionService), ServiceLifetime.Scoped, typeof(CommunityInformationCollectionService)),
        new(typeof(ICommunityPostIngredientPriceHintService), ServiceLifetime.Scoped, typeof(CommunityPostIngredientPriceHintService)),
        new(typeof(ICommunityAuthoringAiEvidenceTool), ServiceLifetime.Scoped, typeof(CommunityInformationAuthoringAiEvidenceTool)),
        new(typeof(ICommunityAuthoringAiEvidenceTool), ServiceLifetime.Scoped, typeof(YouTubeSocialContextAuthoringAiEvidenceTool)),
        new(typeof(ICommunityAuthoringAiDraftService), ServiceLifetime.Scoped, typeof(CommunityAuthoringAiDraftService)),
        new(typeof(ICommunityAuthoringImagePromptPlanner), ServiceLifetime.Singleton, typeof(CommunityAuthoringImagePromptPlanner)),
        new(typeof(ICommunityAuthoringImageService), ServiceLifetime.Scoped, typeof(CommunityAuthoringImageService)),
        new(typeof(I지역문화이미지Prompt조회UseCase), ServiceLifetime.Scoped, typeof(지역문화이미지Prompt조회UseCase)),
        new(typeof(I지역문화공공기관Source조회UseCase), ServiceLifetime.Scoped, typeof(지역문화공공기관Source조회UseCase)),
        new(typeof(IYouTube음식상품발견저장소), ServiceLifetime.Scoped, typeof(EfYouTube음식상품발견저장소)),
        new(typeof(IYouTube음식상품발견Service), ServiceLifetime.Scoped, typeof(YouTube음식상품발견Service)),
        new(typeof(IYouTube영상재료인지Engine), ServiceLifetime.Scoped, typeof(YouTube영상재료인지Engine)),
        new(typeof(IYouTube영상재료자동인지Service), ServiceLifetime.Scoped, typeof(YouTube영상재료자동인지Service)),
        new(typeof(IHongikHakdangCardPageParser), ServiceLifetime.Singleton, typeof(HongikHakdangCardPageParser)),
        new(typeof(IHongikHakdangCardImageStore), ServiceLifetime.Singleton, typeof(HongikHakdangCardImageStore)),
        new(typeof(IHongikHakdangCardVariantRenderer), ServiceLifetime.Singleton, typeof(HongikHakdangCardVariantRenderer)),
        new(typeof(IHongikHakdangCardSelectionPolicy), ServiceLifetime.Singleton, typeof(HongikHakdangCardSelectionPolicy)),
        new(typeof(IHongikHakdangCardMediaTokenService), ServiceLifetime.Singleton, typeof(HongikHakdangCardMediaTokenService)),
        new(typeof(IHongikHakdangCardRepository), ServiceLifetime.Scoped, typeof(EfHongikHakdangCardRepository)),
        new(typeof(IHongikHakdangCardService), ServiceLifetime.Scoped, typeof(HongikHakdangCardService)),
        new(typeof(IHongikHakdangCardVariantService), ServiceLifetime.Scoped, typeof(HongikHakdangCardVariantService)),
        new(typeof(IHongikHakdangCardDeliveryService), ServiceLifetime.Scoped, typeof(HongikHakdangCardDeliveryService)),
        new(typeof(ISsalddelMobilePushInstallationService), ServiceLifetime.Scoped, typeof(SsalddelMobilePushInstallationService)),
        new(typeof(ICommunityKeywordMatcher), ServiceLifetime.Singleton, typeof(CommunityKeywordMatcher)),
        new(typeof(ICommunityDriverAvailabilityClock), ServiceLifetime.Singleton, typeof(SystemCommunityDriverAvailabilityClock)),
        new(typeof(ICommunityDriverAvailabilityService), ServiceLifetime.Singleton, typeof(CommunityDriverAvailabilityService)),
        new(typeof(ICommunityKeywordNotificationQueue), ServiceLifetime.Singleton, typeof(CommunityKeywordNotificationQueue)),
        new(typeof(ICommunityKeywordNotificationSignal), ServiceLifetime.Singleton, typeof(CommunityKeywordNotificationSignal)),
        new(typeof(ICommunityKeywordSubscriptionService), ServiceLifetime.Scoped, typeof(CommunityKeywordSubscriptionService)),
        new(typeof(ICommunityKeywordInboxService), ServiceLifetime.Scoped, typeof(CommunityKeywordInboxService)),
        new(typeof(ICommunityKeywordNotificationProcessor), ServiceLifetime.Scoped, typeof(CommunityKeywordNotificationProcessor)),
        new(typeof(ICommunityPostEmailNotificationOutboxStore), ServiceLifetime.Scoped, typeof(CommunityPostEmailNotificationOutboxStore)),
        new(typeof(ICommunityPostEmailSender), ServiceLifetime.Scoped, typeof(GmailCommunityPostEmailSender)),
        new(typeof(ICommunityPostEmailNotificationProcessor), ServiceLifetime.Scoped, typeof(CommunityPostEmailNotificationProcessor)),
        new(typeof(I커뮤니티게시글음성본문분할기), ServiceLifetime.Singleton, typeof(커뮤니티게시글음성본문분할기)),
        new(typeof(I커뮤니티게시글음성작업예약Service), ServiceLifetime.Singleton, typeof(커뮤니티게시글음성작업예약Service)),
        new(typeof(I커뮤니티게시글음성작업신호), ServiceLifetime.Singleton, typeof(커뮤니티게시글음성작업신호)),
        new(typeof(I커뮤니티게시글음성작업Processor), ServiceLifetime.Scoped, typeof(커뮤니티게시글음성작업Processor)),
        new(typeof(I커뮤니티게시글음성조회Service), ServiceLifetime.Scoped, typeof(커뮤니티게시글음성조회Service)),
        new(typeof(ICommunityScheduledPostPublicationProcessor), ServiceLifetime.Scoped, typeof(CommunityScheduledPostPublicationProcessor)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(교육기관제출Worker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(CommunityScheduledPostPublicationWorker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(커뮤니티게시글음성Worker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(CommunityKeywordNotificationWorker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(CommunityPostEmailNotificationWorker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(CommunityActivityBoardContentWorker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(커뮤니티원장투영Worker)),
        new(typeof(IHostedService), ServiceLifetime.Singleton, typeof(CommunityGroupPurchaseDemandOutboxWorker)),
        new(typeof(I인증UseCase), ServiceLifetime.Scoped, typeof(인증UseCase)),
        new(typeof(I커뮤니티대화UseCase), ServiceLifetime.Scoped, typeof(커뮤니티대화UseCase)),
        new(typeof(I공동구매자동집단화UseCase), ServiceLifetime.Scoped, typeof(공동구매자동집단화UseCase)),
        new(typeof(I공동구매커머스이행계획UseCase), ServiceLifetime.Scoped, typeof(공동구매커머스이행계획UseCase)),
        new(typeof(I공동구매해외선적통관동기화Service), ServiceLifetime.Scoped, typeof(공동구매해외선적통관동기화Service)),
        new(typeof(I공동구매수입물류정규화Service), ServiceLifetime.Singleton, typeof(공동구매수입물류정규화Service)),
        new(typeof(IVersionFeatureFlagService), ServiceLifetime.Singleton, typeof(VersionFeatureFlagService)),
        new(typeof(ISsalddelFoodOrderStore), ServiceLifetime.Scoped, typeof(EfSsalddelFoodOrderStore)),
        new(typeof(I음식점조리시간Service), ServiceLifetime.Scoped, typeof(음식점조리시간Service)),
        new(typeof(I음식주문메뉴검증Service), ServiceLifetime.Scoped, typeof(음식주문메뉴검증Service)),
        new(typeof(배차주소Store), ServiceLifetime.Singleton, typeof(배차주소Store)),
        new(typeof(I음식점주문실시간알림Service), ServiceLifetime.Scoped, typeof(음식점주문SignalR알림Service)),
    ];

    // .NET 옵션 기반 descriptor까지 비교하기 위해 추출 전 등록 순서를 그대로 보존한다.
    private static void AddDistributedBaseline(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DistributedFulfillmentOptions>(configuration.GetSection(DistributedFulfillmentOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IDistributedBuildingCandidateProvider, EmptyDistributedBuildingCandidateProvider>();
        services.AddScoped<I분산배송경로Planner, 분산배송경로Planner>(); services.AddScoped<I분산화물연결Service, 분산화물연결Service>();
        services.AddScoped<DistributedPhotoEvidenceService>();
        services.AddScoped<IDistributedPhotoEvidenceBinder>(s => s.GetRequiredService<DistributedPhotoEvidenceService>());
        services.TryAddScoped<I분산사진보존연결, DistributedPhotoHoldService>();
        services.AddHostedService<DistributedPhotoRetentionWorker>();
        services.AddScoped<I분산주문원장연결Adapter, 분산주문원장연결Adapter>();
        services.AddScoped<IDistributedFulfillmentUseCase, 분산물류UseCase>();
        services.AddNeighborhoodSettlement(configuration);
    }
}
