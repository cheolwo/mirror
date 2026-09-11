using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.Simulation.Persistence;

namespace Ssalddel.Simulation.Hosting;

public static class SsalddelSimulationHostingServiceCollectionExtensions
{
    public const string ConnectionStringMissingErrorCode =
        "SimulationSharedPublicDataConnectionStringMissing";
    public const string WorldDerivationConnectionStringMissingErrorCode =
        "SimulationWorldDerivationConnectionStringMissing";
    public const string SessionConnectionStringMissingErrorCode =
        "SimulationSessionConnectionStringMissing";

    public static IServiceCollection AddSsalddelSimulationModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null)
    {
        var moduleOptions = configuration
            .GetSection(SsalddelSimulationModuleOptions.SectionName)
            .Get<SsalddelSimulationModuleOptions>()
            ?? new SsalddelSimulationModuleOptions();
        var allowUnauthenticatedTesting = environment?.IsEnvironment("Testing") == true
                                          && moduleOptions.AllowUnauthenticatedTesting;
        services.AddOptions<SimulationSharedPublicDataOptions>()
            .Bind(configuration.GetSection(SimulationSharedPublicDataOptions.SectionName))
            .Validate(options => !options.Enabled
                || !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "공유 공공데이터 연결 문자열 이름이 필요합니다.")
            .Validate(options => options.MaxItems is >= 1 and <= 200,
                "공유 공공데이터 최대 조회 건수는 1~200이어야 합니다.")
            .ValidateOnStart();
        services.AddOptions<SimulationWorldDerivationDatabaseOptions>()
            .Bind(configuration.GetSection(SimulationWorldDerivationDatabaseOptions.SectionName))
            .Validate(options => !options.Enabled
                || !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "Simulation World 파생 DB 연결 문자열 이름이 필요합니다.")
            .ValidateOnStart();
        services.AddOptions<SimulationSessionDatabaseOptions>()
            .Bind(configuration.GetSection(
                SimulationSessionDatabaseOptions.SectionName))
            .Validate(options => !options.Enabled
                || !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "Simulation Session DB 연결 문자열 이름이 필요합니다.")
            .ValidateOnStart();
        services.AddControllers(options =>
            options.Conventions.Add(new SsalddelSimulationControllerConvention()))
            .AddApplicationPart(typeof(SimulationApiExceptionFilter).Assembly);
        if (allowUnauthenticatedTesting)
        {
            services.AddAuthentication()
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                    SsalddelSimulationTestingAuthenticationHandler>(
                    SsalddelSimulationAuthorizationPolicies.TestingScheme,
                    _ => { });
        }
        services.AddAuthorization(options => options.AddPolicy(
            SsalddelSimulationAuthorizationPolicies.Participant,
            policy =>
            {
                if (allowUnauthenticatedTesting)
                {
                    policy.AddAuthenticationSchemes(
                        SsalddelSimulationAuthorizationPolicies.TestingScheme);
                }
                policy.RequireAuthenticatedUser();
            }));
        services.AddSignalR();
        services.AddHttpContextAccessor();

        var sharedOptions = configuration
            .GetSection(SimulationSharedPublicDataOptions.SectionName)
            .Get<SimulationSharedPublicDataOptions>() ?? new SimulationSharedPublicDataOptions();

        if (sharedOptions.Enabled)
        {
            var connectionString = ResolveConnectionString(configuration, sharedOptions);
            services.AddSimulationSharedPublicDataPersistence(
                connectionString,
                sharedOptions.MaxItems);
        }
        else
        {
            services.AddSingleton<ISimulation공유공공데이터조회Port,
                DisabledSimulation공유공공데이터Reader>();
        }

        var derivationOptions = configuration
            .GetSection(SimulationWorldDerivationDatabaseOptions.SectionName)
            .Get<SimulationWorldDerivationDatabaseOptions>()
            ?? new SimulationWorldDerivationDatabaseOptions();
        services.AddSingleton<ISimulationWorld지역ProjectionReader,
            DisabledSimulationWorld지역ProjectionReader>();
        services.AddSingleton<ISimulationWorld지역표현요약Reader,
            DisabledSimulationWorld지역표현요약Reader>();
        services.AddSingleton<ISimulationWorldTileArtifactReader,
            DisabledSimulationWorldTileArtifactReader>();
        services.AddSingleton<ISimulationWorldLandscapeGrammarCatalogReader,
            DisabledSimulationWorldLandscapeGrammarCatalogReader>();
        services.AddSingleton<DisabledSimulationWorldLandscapeCompositionStore>();
        services.AddSingleton<ISimulationWorldLandscapeCompositionStore>(provider =>
            provider.GetRequiredService<DisabledSimulationWorldLandscapeCompositionStore>());
        services.AddSingleton<ISimulationWorldLandscapeCompositionReader>(provider =>
            provider.GetRequiredService<DisabledSimulationWorldLandscapeCompositionStore>());
        services.AddSingleton<ISimulationWorldAreaSetDefinitionReader,
            DisabledSimulationWorldAreaSetDefinitionReader>();
        services.AddSingleton<ISimulationWorldAreaSetGraphStore,
            DisabledSimulationWorldAreaSetGraphStore>();
        services.AddSingleton<ISimulationWorld상호작용GraphReadinessStore,
            DisabledSimulationWorld상호작용GraphReadinessStore>();
        services.AddSingleton<ISimulationFarmRealityOperationalReader,
            DisabledSimulationFarmRealityOperationalReader>();
        services.AddSingleton<ISimulationFarmRealityEvidenceStore,
            DisabledSimulationFarmRealityEvidenceStore>();
        services.AddScoped<SimulationFarmRealityEvidenceService>();
        if (derivationOptions.Enabled)
        {
            var connectionString = configuration.GetConnectionString(
                derivationOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    WorldDerivationConnectionStringMissingErrorCode);
            services.AddSimulationWorldDerivationPersistence(
                connectionString,
                derivationOptions.LandscapeGrammarManifestPath,
                derivationOptions.AreaSetDefinitionPath);
            if (!sharedOptions.Enabled)
                services.AddSingleton<ISimulationFarmRealityOperationalReader,
                    DisabledSimulationFarmRealityOperationalReader>();
            if (sharedOptions.Enabled)
                services.Add평창군공간파생Pipeline();
        }

        services.AddSingleton<I경영SimulationSessionStore, InMemory경영SimulationSessionStore>();
        var sessionDatabaseOptions = configuration
            .GetSection(SimulationSessionDatabaseOptions.SectionName)
            .Get<SimulationSessionDatabaseOptions>()
            ?? new SimulationSessionDatabaseOptions();
        if (sessionDatabaseOptions.Enabled)
        {
            var connectionString = configuration.GetConnectionString(
                sessionDatabaseOptions.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    SessionConnectionStringMissingErrorCode);
            services.AddSimulationSessionPersistence(connectionString);
            services.AddSingleton<ISimulationSessionAccessLedger,
                EfSimulationSessionAccessLedger>();
        }
        else
        {
            services.AddSingleton<ISimulationSessionSaveStore,
                InMemorySimulationSessionSaveStore>();
            services.AddSingleton<ISimulationOnlineWorldCheckpointStore,
                InMemorySimulationOnlineWorldCheckpointStore>();
            services.AddSingleton<ISimulationSessionAccessLedger,
                InMemorySimulationSessionAccessLedger>();
        }
        services.AddScoped<SimulationSessionAccessActionFilter>();
        services.AddScoped<SimulationApiExceptionFilter>();
        services.AddSingleton<SimulationOnlineWorldService>();
        services.AddSingleton<SimulationOnlineMeditationBridgeService>();
        services.AddSingleton<SimulationOnlineNatureSessionProvisioningService>();
        services.AddSingleton<SimulationOnlineCooperativeLoggingService>();
        services.AddSingleton<경영SimulationSessionService>();
        services.AddSingleton<경영SimulationSessionAccessor>();
        services.AddSingleton<ISimulationHexagramCampaignAttemptStore,
            InMemorySimulationHexagramCampaignAttemptStore>();
        services.AddSingleton<경영SimulationSession생명주기Service>();
        services.AddSingleton<SimulationHexagramCampaignService>();
        services.AddSingleton<경영SimulationWorldGameplayService>();
        services.AddSingleton<ISimulationRealityContextClock,
            SystemSimulationRealityContextClock>();
        var fileRealityContextReader = new FileSimulationRealityContextCatalogReader(
            derivationOptions.RealityContextCatalogPath);
        services.AddSingleton<ISimulationRealityContextCatalogReader>(provider =>
            new SimulationFarmRealityContextCatalogReader(
                provider.GetRequiredService<IServiceScopeFactory>(),
                fileRealityContextReader));
        services.AddSingleton<SimulationRealityContextService>();
        services.AddSingleton<경영Simulation통합생활세계Service>();
        services.AddSingleton<ISimulationBattleRuntimeProjectionProvider,
            SimulationBattleRuntimeProjectionProvider>();
        services.AddSingleton<경영Simulation턴결정Service>();
        services.AddSingleton<경영Simulation수확수출Service>();
        services.AddSingleton<경영Simulation물류창고Service>();
        services.AddSingleton<경영Simulation주문소비Service>();
        services.AddSingleton<SimulationWorldUIProjectionService>();
        services.AddSingleton<Simulation타로화물운송PreviewService>();
        services.AddSingleton<Simulation타로객체반응PreviewService>();
        services.AddSingleton<SimulationFreight렌더링의도Projector>();
        services.AddSingleton<Simulation렌더링의도합성Policy>();
        services.AddSingleton<Simulation기본Urp표현Catalog>();
        services.AddSingleton<SimulationRuntimeWorldPresentationService>();
        services.AddScoped<SimulationWorldStreamingService>();
        services.AddScoped<ISimulationLhWindowPlanner, SimulationLhWindowPlanner>();
        services.AddScoped<ISimulationAreaSetHandoverPlanner,
            SimulationAreaSetHandoverPlanner>();
        services.AddScoped<SimulationLhWorldService>();
        services.AddScoped<SimulationWorldLandscapeCompositionService>();
        services.AddScoped<SimulationWorldLandscapeCompositionJobShell>();
        services.AddScoped<SimulationWorldAreaSetLandscapeGraphJobShell>();
        services.AddScoped<SimulationWorldAreaSetLandscapeGraphService>();
        services.AddSingleton<ISimulationWorldActualE5SpatialCatalogReader>(
            new FileSimulationWorldActualE5SpatialCatalogReader(
                derivationOptions.ActualE5SpatialCatalogPath));
        services.AddScoped<SimulationWorldActualE5SpatialService>();
        services.AddSingleton<ISimulationWorldLayoutCatalogReader>(
            new FileSimulationWorldLayoutCatalogReader(
                derivationOptions.WorldLayoutCatalogPath));
        services.AddScoped<SimulationWorldLayoutService>();
        services.AddSingleton<ISimulationAreaSetImmersionCatalogReader>(
            new FileSimulationAreaSetImmersionCatalogReader(
                derivationOptions.AreaSetImmersionReadinessCatalogPath));
        services.AddScoped<SimulationAreaSetImmersionService>();
        services.AddScoped<ISimulationLhCellContentSource,
            H5AuthoritativeSimulationLhCellContentSource>();
        services.AddScoped<SimulationWorld상호작용NetworkService>();
        services.AddScoped<SimulationActualE5SessionCreationService>();
        services.AddSingleton<ISimulationWorld상호작용GraphCatalogReader>(
            new FileSimulationWorld상호작용GraphCatalogReader(
                derivationOptions.InteractionGraphBindingCatalogPath));
        services.AddScoped<SimulationWorld상호작용GraphService>();
        services.AddScoped<SimulationWorld상호작용GraphJobShell>();
        services.AddScoped<ISimulationWorldLandscapeSkeletonSource,
            PyeongchangFirstLandscapeSkeletonSource>();
        services.AddSingleton<SimulationWorldLandscapeGraphAssembler>();
        services.AddScoped<SimulationWorldTileArtifactContentService>();
        services.AddScoped<SimulationWorld지역표현요약Service>();
        services.AddScoped<SimulationWorldExplorationService>();
        services.AddSingleton<SimulationWorldSurvivalInventoryService>();
        services.AddSingleton<SimulationActorEquipmentService>();
        services.AddSingleton<InMemorySimulation플레이어지식Store>();
        services.AddSingleton<ISimulation플레이어지식Store>(provider =>
            provider.GetRequiredService<InMemorySimulation플레이어지식Store>());
        services.AddSingleton<Simulation플레이어지식Service>();
        services.AddSingleton<ISimulation공동체방문자체류Store,
            InMemorySimulation공동체방문자체류Store>();
        services.AddSingleton<Simulation공동체방문자체류Service>();
        services.AddSingleton<I세계상호작용실행Pipeline,
            세계상호작용실행Pipeline>();
        services.AddSingleton<SimulationNatureSurvivalService>();
        services.AddSingleton<SimulationSurvivalTarotService>();
        services.AddSingleton<SimulationWorldEventProjectionService>();
        services.AddSingleton<SimulationRegionalIncidentService>();
        services.AddSingleton<SimulationFarmSurvivalService>();
        services.AddSingleton<InMemorySimulationTeamObservationPolicyStore>();
        services.AddSingleton<ISimulationTeamObservationPolicyStore>(provider =>
            provider.GetRequiredService<InMemorySimulationTeamObservationPolicyStore>());
        services.AddSingleton<InMemorySimulationTeamMemberPoseStore>();
        services.AddSingleton<ISimulationTeamMemberPoseStore>(provider =>
            provider.GetRequiredService<InMemorySimulationTeamMemberPoseStore>());
        services.AddSingleton<InMemorySimulationTeamObservationSessionStore>();
        services.AddSingleton<ISimulationTeamObservationSessionStore>(provider =>
            provider.GetRequiredService<InMemorySimulationTeamObservationSessionStore>());
        services.AddSingleton<SimulationTeamObservationService>();
        services.AddSingleton<SimulationTeamRoleCardService>();
        services.AddSingleton<SimulationPlayerLearningFocusService>();
        services.AddSingleton<SimulationPlayerIdeaMapService>();
        services.AddSingleton<InMemorySimulationBattleInstanceStore>();
        services.AddSingleton<ISimulationBattleInstanceStore>(provider =>
            provider.GetRequiredService<InMemorySimulationBattleInstanceStore>());
        services.AddSingleton<ISimulationBattleResourceLockReader>(provider =>
            provider.GetRequiredService<InMemorySimulationBattleInstanceStore>());
        services.AddSingleton<ISimulationBattleReservationReader>(provider =>
            provider.GetRequiredService<InMemorySimulationBattleInstanceStore>());
        services.AddSingleton<ISimulationBattlefieldDerivationService,
            SimulationBattlefieldDerivationService>();
        services.AddSingleton<SimulationBattleInstanceService>();
        services.AddSingleton<ISimulationBattleWorldReconciler>(provider =>
            provider.GetRequiredService<SimulationBattleInstanceService>());
        services.AddSingleton<SimulationCollectibleCardRewardService>();
        services.AddHealthChecks()
            .AddCheck<SsalddelSimulationReadinessHealthCheck>(
                "simulation-persistence",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(15));
        return services;
    }

    private static string ResolveConnectionString(
        IConfiguration configuration,
        SimulationSharedPublicDataOptions options)
    {
        var connectionString = configuration.GetConnectionString(options.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString)
            && !string.IsNullOrWhiteSpace(options.FallbackConnectionStringName))
        {
            connectionString = configuration.GetConnectionString(
                options.FallbackConnectionStringName);
        }

        return !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException(ConnectionStringMissingErrorCode);
    }
}
