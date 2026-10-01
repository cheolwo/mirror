using Hongdal.Services.Auth;
using Hongdal.Services.Community;
using Hongdal.Services.Content;
using Hongdal.Services.Education;
using Hongdal.Services.Food;
using Hongdal.Services.Notifications;
using Hongdal.Services.Orderer;
using Hongdal.Services.Speech;
using 홍달.Services.Versioning;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddHongdalCommunityDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<Mongo커뮤니티원장저장소>();
        services.AddSingleton<I커뮤니티원장투영작업저장소, Mongo커뮤니티원장투영작업저장소>();
        services.AddSingleton<I커뮤니티원장저장소, 이벤트발행커뮤니티원장저장소>();
        services.AddSingleton<I커뮤니티원장공유정책저장소, Mongo커뮤니티원장공유정책저장소>();
        services.AddScoped<I커뮤니티원장공유Service, 커뮤니티원장공유Service>();
        services.AddSingleton<ICommunityLedgerRoleAccessPolicyStore, MongoCommunityLedgerRoleAccessPolicyStore>();
        services.AddScoped<ICommunityCustomsBrokerDirectory, EfCommunityCustomsBrokerDirectory>();
        services.AddScoped<ICommunityLedgerRoleAccessService, CommunityLedgerRoleAccessService>();
        services.AddScoped<ICommunityLedgerBlockAssignmentService, CommunityLedgerBlockAssignmentService>();
        services.AddSingleton<I주문원장서명저장소, Mongo주문원장서명저장소>();
        services.AddSingleton<I주문원장공개요청저장소, Mongo주문원장공개요청저장소>();
        services.AddScoped<I주문원장통합UseCase, 주문원장통합UseCase>();
        services.AddScoped<I주문원장서명UseCase, 주문원장서명UseCase>();
        services.AddScoped<I주문원장역할별조회Service, 주문원장역할별조회Service>();
        services.AddScoped<I주문원장공개요청Service, 주문원장공개요청Service>();
        services.AddSingleton<I커뮤니티대화저장소, Mongo커뮤니티대화저장소>();
        services.AddScoped<I커뮤니티원장상태이벤트Service, 커뮤니티원장상태이벤트Service>();
        services.AddScoped<ICommunityLedgerCompletionPostStore, EfCommunityLedgerCompletionPostStore>();
        services.AddScoped<ICommunityLedgerCompletionPostService, CommunityLedgerCompletionPostService>();
        services.AddScoped<ICommunityAutomatedPostPublisher, EfCommunityAutomatedPostPublisher>();
        services.AddScoped<ICommunityAutomatedPostSource, CommunityKamisPriceBriefSource>();
        services.AddScoped<ICommunityAutomatedPostSource, CommunityReflectionSource>();
        services.AddScoped<ICommunityAutomatedPostSource, CommunityActivityDigestSource>();
        services.AddScoped<ICommunityAutomatedPostSource, CommunityPrajnaPostSource>();
        services.AddScoped<I공동수입원장관세사알림Service, 공동수입원장관세사알림Service>();
        services.AddScoped<I공동구매원장관계자알림Service, 공동구매원장관계자알림Service>();
        services.AddScoped<게시글원장선택조회Service>();
        services.AddScoped<I게시글원장선택조회Service>(provider =>
            provider.GetRequiredService<게시글원장선택조회Service>());
        services.AddScoped<게시글원장표시ContextService>();
        services.AddScoped<I게시글원장표시ContextService>(provider =>
            provider.GetRequiredService<게시글원장표시ContextService>());
        services.AddScoped<게시글원장ContextService>(provider => new 게시글원장ContextService(
            provider.GetRequiredService<I게시글원장선택조회Service>(),
            provider.GetRequiredService<I게시글원장표시ContextService>()));
        services.AddScoped<I게시글원장ContextService>(provider =>
            provider.GetRequiredService<게시글원장ContextService>());
        services.AddScoped<ICommunityPostTranslationService, CommunityPostTranslationService>();
        services.AddSingleton<ICommunityPostOpportunityAnalyzer, CommunityPostOpportunityAnalyzer>();
        services.AddSingleton<ICommunityDynamicTopicClassifier, CommunityDynamicTopicClassifier>();
        services.AddScoped<ICommunityPostOpportunityStore, EfCommunityPostOpportunityStore>();
        services.AddScoped<ICommunityProfessionalEligibilityService, CommunityProfessionalEligibilityService>();
        services.AddScoped<ICommunityPostProfessionalParticipationService, CommunityPostProfessionalParticipationService>();
        services.AddScoped<ICommunityActionJourneyService, CommunityActionJourneyService>();
        services.AddScoped<ICommunityDynamicDiscoveryService, CommunityDynamicDiscoveryService>();
        services.AddScoped<ICommunityPostOpportunityQueryUseCase, CommunityPostOpportunityQueryUseCase>();
        services.AddScoped<ICommunityPostMeatImportReadinessUseCase, CommunityPostMeatImportReadinessUseCase>();
        services.AddScoped<ICommunityPostParticipationUseCase, CommunityPostParticipationUseCase>();
        services.AddScoped<ICommunityPostOpportunityService, CommunityPostOpportunityService>();
        services.AddScoped<I원장다이어그램실시간알림Service, 원장다이어그램SignalR알림Service>();
        services.AddScoped<I커뮤니티원장업무투영동기화Service, 커뮤니티원장업무투영동기화Service>();
        services.AddScoped<I원장업무투영동기화Handler, 운송원장업무투영Handler>();
        services.AddScoped<I원장업무투영동기화Handler, 입출고원장업무투영Handler>();
        services.AddScoped<I원장업무투영동기화Handler, 음식주문원장업무투영Handler>();
        services.AddScoped<I운송원장Mongo동기화Service, 운송원장Mongo동기화Service>();
        services.AddScoped<I음식마트원장Mongo동기화Service, 음식마트원장Mongo동기화Service>();
        services.AddSingleton<I교육기관제출대기열, Mongo교육기관제출대기열>();
        services.AddScoped<I현장체험활동UseCase, 현장체험활동UseCase>();
        services.AddScoped<I교육과정정의Service, 교육과정정의Service>();
        services.AddScoped<I교육과정참여Service, 교육과정참여Service>();
        services.AddScoped<ITypecast음성카탈로그저장소, EfTypecast음성카탈로그저장소>();
        services.AddScoped<ITypecast음성카탈로그Service, Typecast음성카탈로그Service>();
        services.AddScoped<IYouTube채널감시저장소, EfYouTube채널감시저장소>();
        services.AddScoped<IYouTube채널감시Service, YouTube채널감시Service>();
        services.AddScoped<ICommunityInformationCandidateSource, CommunityYouTubeInformationCandidateSource>();
        services.AddScoped<ICommunityInformationCandidateSource, CommunityKamisInformationCandidateSource>();
        services.AddScoped<ICommunityInformationCandidateSource, CommunityUsdaNassInformationCandidateSource>();
        services.AddScoped<ICommunityInformationCandidateSource, CommunityAbsFoodPriceIndexInformationCandidateSource>();
        services.AddScoped<ICommunityInformationCandidateSource, CommunityFishCooperativeInformationCandidateSource>();
        services.AddScoped<ICommunityInformationCollectionService, CommunityInformationCollectionService>();
        services.AddScoped<ICommunityAuthoringAiEvidenceTool, CommunityInformationAuthoringAiEvidenceTool>();
        services.AddScoped<ICommunityAuthoringAiEvidenceTool, YouTubeSocialContextAuthoringAiEvidenceTool>();
        services.AddScoped<ICommunityAuthoringAiDraftService, CommunityAuthoringAiDraftService>();
        services.AddSingleton<ICommunityAuthoringImagePromptPlanner, CommunityAuthoringImagePromptPlanner>();
        services.AddScoped<ICommunityAuthoringImageService, CommunityAuthoringImageService>();
        services.AddScoped<IYouTube음식상품발견저장소, EfYouTube음식상품발견저장소>();
        services.AddScoped<IYouTube음식상품발견Service, YouTube음식상품발견Service>();
        services.AddScoped<IYouTube영상재료인지Engine, YouTube영상재료인지Engine>();
        services.AddScoped<IYouTube영상재료자동인지Service, YouTube영상재료자동인지Service>();
        services.AddSingleton<IHongikHakdangCardPageParser, HongikHakdangCardPageParser>();
        services.AddSingleton<IHongikHakdangCardImageStore, HongikHakdangCardImageStore>();
        services.AddSingleton<IHongikHakdangCardVariantRenderer, HongikHakdangCardVariantRenderer>();
        services.AddSingleton<IHongikHakdangCardSelectionPolicy, HongikHakdangCardSelectionPolicy>();
        services.AddSingleton<IHongikHakdangCardMediaTokenService, HongikHakdangCardMediaTokenService>();
        services.AddScoped<IHongikHakdangCardRepository, EfHongikHakdangCardRepository>();
        services.AddScoped<IHongikHakdangCardService, HongikHakdangCardService>();
        services.AddScoped<IHongikHakdangCardVariantService, HongikHakdangCardVariantService>();
        services.AddScoped<IHongikHakdangCardDeliveryService, HongikHakdangCardDeliveryService>();
        services.AddScoped<IHongdalMobilePushInstallationService, HongdalMobilePushInstallationService>();
        services.AddSingleton<ICommunityKeywordMatcher, CommunityKeywordMatcher>();
        services.AddSingleton<ICommunityDriverAvailabilityClock, SystemCommunityDriverAvailabilityClock>();
        services.AddSingleton<ICommunityDriverAvailabilityService, CommunityDriverAvailabilityService>();
        services.AddSingleton<ICommunityKeywordNotificationQueue, CommunityKeywordNotificationQueue>();
        services.AddSingleton<ICommunityKeywordNotificationSignal, CommunityKeywordNotificationSignal>();
        services.AddScoped<ICommunityKeywordSubscriptionService, CommunityKeywordSubscriptionService>();
        services.AddScoped<ICommunityKeywordInboxService, CommunityKeywordInboxService>();
        services.AddScoped<ICommunityKeywordNotificationProcessor, CommunityKeywordNotificationProcessor>();
        services.AddSingleton<I커뮤니티게시글음성본문분할기, 커뮤니티게시글음성본문분할기>();
        services.AddSingleton<I커뮤니티게시글음성작업예약Service, 커뮤니티게시글음성작업예약Service>();
        services.AddSingleton<I커뮤니티게시글음성작업신호, 커뮤니티게시글음성작업신호>();
        services.AddScoped<I커뮤니티게시글음성작업Processor, 커뮤니티게시글음성작업Processor>();
        services.AddScoped<I커뮤니티게시글음성조회Service, 커뮤니티게시글음성조회Service>();
        services.AddScoped<ICommunityScheduledPostPublicationProcessor, CommunityScheduledPostPublicationProcessor>();
        services.AddHostedService<교육기관제출Worker>();
        services.AddHostedService<CommunityScheduledPostPublicationWorker>();
        services.AddHostedService<커뮤니티게시글음성Worker>();
        services.AddHostedService<CommunityKeywordNotificationWorker>();
        services.AddHostedService<커뮤니티원장투영Worker>();
        services.AddHostedService<CommunityGroupPurchaseDemandOutboxWorker>();
        services.AddScoped<I인증UseCase, 인증UseCase>();
        services.AddScoped<I커뮤니티대화UseCase, 커뮤니티대화UseCase>();
        services.AddScoped<I공동구매자동집단화UseCase, 공동구매자동집단화UseCase>();
        services.AddScoped<I공동구매커머스이행계획UseCase, 공동구매커머스이행계획UseCase>();
        services.AddScoped<I공동구매해외선적통관동기화Service, 공동구매해외선적통관동기화Service>();
        services.AddSingleton<I공동구매수입물류정규화Service, 공동구매수입물류정규화Service>();
        services.AddSingleton<IVersionFeatureFlagService, VersionFeatureFlagService>();
        services.AddScoped<IHongdalFoodOrderStore, EfHongdalFoodOrderStore>();
        services.AddSingleton<배차주소Store>();
        services.AddScoped<I음식점주문실시간알림Service, 음식점주문SignalR알림Service>();

        return services;
    }
}
