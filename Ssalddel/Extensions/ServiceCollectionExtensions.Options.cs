using Ssalddel.Application.CommandProcessing;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using Ssalddel.Security;
using Ssalddel.Services.Security;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Documents;
using 살뜰.Services.External.Customs;
using 살뜰.Services.External.PublicData;
using 살뜰.Services.HIOPSAI;
using 살뜰.Services.Notifications;
using 살뜰.Services.Options;
using 살뜰.Services.Payments;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<TossPaymentsOptions>(configuration.GetSection(TossPaymentsOptions.SectionName));
        services.Configure<GoogleCloudStorageOptions>(configuration.GetSection(GoogleCloudStorageOptions.SectionName));
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.SectionName));
        services.Configure<AzureBlobStorageOptions>(configuration.GetSection(AzureBlobStorageOptions.SectionName));
        services.Configure<CommunityPostStorageOptions>(configuration.GetSection(CommunityPostStorageOptions.SectionName));
        services.Configure<GeminiImageOptions>(
            configuration.GetSection(GeminiImageOptions.SectionName));
        services.Configure<GeminiImageBatchOptions>(
            configuration.GetSection(GeminiImageBatchOptions.SectionName));
        services.Configure<RegionalCultureImageGenerationOptions>(
            configuration.GetSection(RegionalCultureImageGenerationOptions.SectionName));
        services.Configure<HIOPSAIOptions>(configuration.GetSection(HIOPSAIOptions.SectionName));
        services.Configure<NaverCloudDirectionsOptions>(configuration.GetSection(NaverCloudDirectionsOptions.SectionName));
        services.Configure<NaverMapsOptions>(configuration.GetSection(NaverMapsOptions.SectionName));
        services.Configure<OpinetOptions>(configuration.GetSection(OpinetOptions.SectionName));
        services.Configure<NtsBusinessRegistrationOptions>(configuration.GetSection(NtsBusinessRegistrationOptions.SectionName));
        services.Configure<해외제조업소조회Options>(configuration.GetSection(해외제조업소조회Options.SectionName));
        services.Configure<수입식품제품조회Options>(configuration.GetSection(수입식품제품조회Options.SectionName));
        services.Configure<수입식품한글표시사항조회Options>(configuration.GetSection(수입식품한글표시사항조회Options.SectionName));
        var 공공데이터기본서비스키 = configuration[$"{PublicDataOptions.SectionName}:DataGoKrServiceKey"]
            ?? configuration[$"{PublicDataOptions.SectionName}:ServiceKey"];
        services.PostConfigure<NtsBusinessRegistrationOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ServiceKey))
            {
                options.ServiceKey = 공공데이터기본서비스키 ?? string.Empty;
            }
        });
        services.PostConfigure<해외제조업소조회Options>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ServiceKey))
            {
                options.ServiceKey = 공공데이터기본서비스키 ?? string.Empty;
            }
        });
        services.PostConfigure<수입식품제품조회Options>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ServiceKey))
            {
                options.ServiceKey = 공공데이터기본서비스키 ?? string.Empty;
            }
        });
        services.PostConfigure<수입식품한글표시사항조회Options>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ServiceKey))
            {
                options.ServiceKey = 공공데이터기본서비스키 ?? string.Empty;
            }
        });
        services.Configure<기사이용료정책Options>(configuration.GetSection(기사이용료정책Options.SectionName));
        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<TransientStateOptions>(configuration.GetSection(TransientStateOptions.SectionName));
        services.Configure<MongoDbOptions>(configuration.GetSection(MongoDbOptions.SectionName));
        services.Configure<DatabaseInitializationOptions>(configuration.GetSection(DatabaseInitializationOptions.SectionName));
        services.Configure<PushNotificationsOptions>(configuration.GetSection(PushNotificationsOptions.SectionName));
        services.Configure<KakaoAlimTalkOptions>(configuration.GetSection(KakaoAlimTalkOptions.SectionName));
        services.Configure<KakaoLocalOptions>(configuration.GetSection(KakaoLocalOptions.SectionName));
        services.Configure<CommandProcessingOptions>(configuration.GetSection(CommandProcessingOptions.SectionName));
        services.Configure<WorkRelationshipSnapshotOptions>(configuration.GetSection(WorkRelationshipSnapshotOptions.SectionName));
        services.Configure<CommandFileStorageOptions>(configuration.GetSection(CommandFileStorageOptions.SectionName));
        services.Configure<CustomsOptions>(configuration.GetSection(CustomsOptions.SectionName));
        services.Configure<PublicDataOptions>(configuration.GetSection(PublicDataOptions.SectionName));
        services.PostConfigure<PublicDataOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.Nongsaro.ApiKey))
            {
                options.Nongsaro.ApiKey = options.RdaLocalFood.ApiKey;
            }

            if (string.IsNullOrWhiteSpace(options.RdaLocalFood.ApiKey))
            {
                options.RdaLocalFood.ApiKey = options.Nongsaro.ApiKey;
            }
        });
        services.Configure<AgriculturalFisheriesBatchOptions>(configuration.GetSection(AgriculturalFisheriesBatchOptions.SectionName));
        services.Configure<CommunityEditorialBatchOptions>(configuration.GetSection(CommunityEditorialBatchOptions.SectionName));
        services.Configure<CommunityActivityBoardContentOptions>(
            configuration.GetSection(CommunityActivityBoardContentOptions.SectionName));
        services.Configure<CommunityContextDiscoveryOptions>(configuration.GetSection(CommunityContextDiscoveryOptions.SectionName));
        services.Configure<VersionFeatureFlagsOptions>(configuration.GetSection(VersionFeatureFlagsOptions.SectionName));
        services.Configure<RegionMobilityGraphOptions>(configuration.GetSection(RegionMobilityGraphOptions.SectionName));
        services.Configure<SsalddelExecutionOptions>(configuration.GetSection(SsalddelExecutionOptions.SectionName));
        services.Configure<RoleAdvertisingOptions>(configuration.GetSection(RoleAdvertisingOptions.SectionName));
        services.Configure<SalesChannelOrderSyncOptions>(configuration.GetSection(SalesChannelOrderSyncOptions.SectionName));
        services.Configure<배차큐정책Options>(configuration.GetSection("DispatchQueue"));
        services.Configure<국내화물배차AI정책Options>(configuration.GetSection(국내화물배차AI정책Options.SectionName));
        services.Configure<화물연속배차Options>(configuration.GetSection(화물연속배차Options.SectionName));
        services.Configure<배차큐배치작업Options>(configuration.GetSection(배차큐배치작업Options.SectionName));
        services.Configure<교육기관제출Options>(configuration.GetSection(교육기관제출Options.SectionName));
        services.Configure<TypecastOptions>(configuration.GetSection(TypecastOptions.SectionName));
        services.Configure<YouTubeOptions>(configuration.GetSection(YouTubeOptions.SectionName));
        services.Configure<HongikHakdangCardOptions>(configuration.GetSection(HongikHakdangCardOptions.SectionName));
        services.Configure<CommunityPostAudioOptions>(configuration.GetSection(CommunityPostAudioOptions.SectionName));
        services.Configure<CommunityPostPublicationOptions>(configuration.GetSection(CommunityPostPublicationOptions.SectionName));
        services.Configure<CommunityPostEmailNotificationOptions>(
            configuration.GetSection(CommunityPostEmailNotificationOptions.SectionName));
        var bootstrapAdminEmail = configuration["IdentitySeed:BootstrapAdmin:Email"];
        var gmailUserName = configuration[
            $"{CommunityPostEmailNotificationOptions.SectionName}:Gmail:UserName"];
        services.PostConfigure<CommunityPostEmailNotificationOptions>(options =>
        {
            if (!string.IsNullOrWhiteSpace(options.RecipientEmail))
            {
                return;
            }

            options.RecipientEmail = !string.IsNullOrWhiteSpace(bootstrapAdminEmail)
                ? bootstrapAdminEmail
                : gmailUserName ?? string.Empty;
        });
        services.Configure<CommunityPostTranslationOptions>(configuration.GetSection(CommunityPostTranslationOptions.SectionName));
        services.Configure<CommunityKeywordNotificationOptions>(configuration.GetSection(CommunityKeywordNotificationOptions.SectionName));
        services.Configure<CommunityLedgerProjectionOptions>(configuration.GetSection(CommunityLedgerProjectionOptions.SectionName));
        services.Configure<GroupPurchaseDemandProcessManagerOptions>(configuration.GetSection(GroupPurchaseDemandProcessManagerOptions.SectionName));
        services.Configure<GroupImportReadinessProcessManagerOptions>(
            configuration.GetSection(GroupImportReadinessProcessManagerOptions.SectionName));

        return services;
    }
}
