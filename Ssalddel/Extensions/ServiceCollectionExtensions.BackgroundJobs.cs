using Quartz;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Infrastructure.BackgroundJobs;
using Ssalddel.Infrastructure.BackgroundJobs.AgriculturalFisheries;
using Ssalddel.Infrastructure.BackgroundJobs.Community;
using Ssalddel.Infrastructure.BackgroundJobs.Content;
using Ssalddel.Infrastructure.BackgroundJobs.SalesOrders;
using Ssalddel.Services.Community;
using Ssalddel.Services.LogisticsProcessing.SalesOrders;
using Ssalddel.Services.Orderer;
using Microsoft.Extensions.DependencyInjection.Extensions;
using 살뜰.Infrastructure.BackgroundJobs.Customs;
using 살뜰.Infrastructure.BackgroundJobs.DispatchQueue;
using 살뜰.Infrastructure.BackgroundJobs.Documents;
using 살뜰.Infrastructure.BackgroundJobs.Notifications;
using 살뜰.Infrastructure.BackgroundJobs.Payments;
using 살뜰.Services.Options;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsalddelBackgroundJobs(
        this IServiceCollection services,
        배차큐배치작업Options jobOptions,
        SalesChannelOrderSyncOptions salesOrderSyncOptions,
        YouTubeOptions youTubeOptions,
        HongikHakdangCardOptions hongikHakdangCardOptions,
        AgriculturalFisheriesBatchOptions agriculturalFisheriesBatchOptions,
        CommunityEditorialBatchOptions communityEditorialBatchOptions,
        SsalddelExecutionOptions executionOptions)
    {
        var 공동구매BatchRegistrationPlan = 공동구매수요모집BatchRegistrationPlan.생성(
            agriculturalFisheriesBatchOptions);
        var communityEditorialBatchRegistrationPlan =
            CommunityEditorialBatchRegistrationPlan.Create(
                agriculturalFisheriesBatchOptions,
                communityEditorialBatchOptions);
        services.AddSingleton(공동구매BatchRegistrationPlan);
        services.AddSingleton(communityEditorialBatchRegistrationPlan);
        services.AddScoped<AgriculturalFisheriesBatchRunner>();
        services.AddScoped<OfficialFoodIngredientCompanyBatchRunner>();
        services.AddScoped<CommunityEditorialBatchRunner>();
        services.AddScoped<AgriculturalFisheriesCommunityPipelineRunner>();
        services.TryAddSingleton<
            ISsalddelBackgroundJobActivationPolicy,
            SsalddelBackgroundJobActivationPolicy>();
        services.AddQuartz(q =>
        {
            if (executionOptions.Mode == SsalddelExecutionMode.Operational)
            {
                var scanJobKey = new JobKey("DispatchQueueScan");
                q.AddJob<배차큐스캔Job>(opts => opts.WithIdentity(scanJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(scanJobKey)
                    .WithIdentity("DispatchQueueScan-trigger")
                    .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(5, jobOptions.큐스캔주기초))).RepeatForever()));

                var expireJobKey = new JobKey("DispatchRecommendationExpire");
                q.AddJob<추천만료정리Job>(opts => opts.WithIdentity(expireJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(expireJobKey)
                    .WithIdentity("DispatchRecommendationExpire-trigger")
                    .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(5, jobOptions.추천만료정리주기초))).RepeatForever()));

                var pushJobKey = new JobKey("DispatchRecommendationPush");
                q.AddJob<배차추천알림발송Job>(opts => opts.WithIdentity(pushJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(pushJobKey)
                    .WithIdentity("DispatchRecommendationPush-trigger")
                    .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(5, jobOptions.알림발송주기초))).RepeatForever()));

                var commandNotificationJobKey = new JobKey("CommandNotificationOutboxSend");
                q.AddJob<Command알림Outbox발송Job>(opts => opts.WithIdentity(commandNotificationJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(commandNotificationJobKey)
                    .WithIdentity("CommandNotificationOutboxSend-trigger")
                    .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(5, jobOptions.알림발송주기초))).RepeatForever()));
            }

            var paymentOutboxJobKey = new JobKey("PaymentApprovedOutboxPublish");
            q.AddJob<결제승인완료Outbox발행Job>(opts => opts.WithIdentity(paymentOutboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(paymentOutboxJobKey)
                .WithIdentity("PaymentApprovedOutboxPublish-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(5, jobOptions.결제승인Outbox발행주기초))).RepeatForever()));

            var documentGenerationOutboxJobKey = new JobKey("DocumentGenerationOutboxProcess");
            q.AddJob<문서생성Outbox처리Job>(opts => opts.WithIdentity(documentGenerationOutboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(documentGenerationOutboxJobKey)
                .WithIdentity("DocumentGenerationOutboxProcess-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(30, jobOptions.문서생성Outbox처리주기초))).RepeatForever()));

            var driverPayoutOutboxJobKey = new JobKey("DriverPayoutOutboxProcess");
            q.AddJob<기사지급Outbox처리Job>(opts => opts.WithIdentity(driverPayoutOutboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(driverPayoutOutboxJobKey)
                .WithIdentity("DriverPayoutOutboxProcess-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(5, jobOptions.결제승인Outbox발행주기초))).RepeatForever()));

            var foodMartLedgerOutboxJobKey = new JobKey("FoodMartLedgerSyncOutboxProcess");
            q.AddJob<음식마트원장동기화Outbox처리Job>(opts => opts.WithIdentity(foodMartLedgerOutboxJobKey));
            q.AddTrigger(opts => opts
                .ForJob(foodMartLedgerOutboxJobKey)
                .WithIdentity("FoodMartLedgerSyncOutboxProcess-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(30, jobOptions.음식마트원장동기화주기초))).RepeatForever()));

            var foodDeliveryWorldProjectionJobKey = new JobKey("FoodDeliveryCompletedWorldProjectionProcess");
            q.AddJob<음식배달완료WorldProjection처리Job>(opts => opts.WithIdentity(foodDeliveryWorldProjectionJobKey));
            q.AddTrigger(opts => opts
                .ForJob(foodDeliveryWorldProjectionJobKey)
                .WithIdentity("FoodDeliveryCompletedWorldProjectionProcess-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(30, jobOptions.음식마트원장동기화주기초))).RepeatForever()));

            var customsSyncJobKey = new JobKey("CustomsStatusSync");
            q.AddJob<통관상태동기화Job>(opts => opts.WithIdentity(customsSyncJobKey));
            q.AddTrigger(opts => opts
                .ForJob(customsSyncJobKey)
                .WithIdentity("CustomsStatusSync-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(30, jobOptions.통관상태동기화주기초))).RepeatForever()));

            var domesticSalesOrderSyncJobKey = new JobKey("DomesticSalesChannelOrderSync");
            q.AddJob<DomesticSalesChannelOrderSyncJob>(opts => opts.WithIdentity(domesticSalesOrderSyncJobKey));
            q.AddTrigger(opts => opts
                .ForJob(domesticSalesOrderSyncJobKey)
                .WithIdentity("DomesticSalesChannelOrderSync-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(60, salesOrderSyncOptions.DomesticSyncIntervalSeconds))).RepeatForever()));

            var overseasSalesOrderSyncJobKey = new JobKey("OverseasSalesChannelOrderSync");
            q.AddJob<OverseasSalesChannelOrderSyncJob>(opts => opts.WithIdentity(overseasSalesOrderSyncJobKey));
            q.AddTrigger(opts => opts
                .ForJob(overseasSalesOrderSyncJobKey)
                .WithIdentity("OverseasSalesChannelOrderSync-trigger")
                .WithSimpleSchedule(x => x.WithInterval(TimeSpan.FromSeconds(Math.Max(60, salesOrderSyncOptions.OverseasSyncIntervalSeconds))).RepeatForever()));

            if (youTubeOptions.Enabled)
            {
                var youTubeSyncJobKey = new JobKey("YouTubeChannelSync");
                q.AddJob<YouTube채널동기화Job>(opts => opts.WithIdentity(youTubeSyncJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(youTubeSyncJobKey)
                    .WithIdentity("YouTubeChannelSync-trigger")
                    .WithSimpleSchedule(x => x
                        .WithInterval(TimeSpan.FromSeconds(Math.Max(60, youTubeOptions.SyncIntervalSeconds)))
                        .RepeatForever()));
            }

            if (hongikHakdangCardOptions.Enabled)
            {
                var cardSyncJobKey = new JobKey("HongikHakdangCardSync");
                q.AddJob<HongikHakdangCardSyncJob>(opts => opts.WithIdentity(cardSyncJobKey));
                q.AddTrigger(opts => opts
                    .ForJob(cardSyncJobKey)
                    .WithIdentity("HongikHakdangCardSync-trigger")
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithInterval(TimeSpan.FromHours(Math.Max(1, hongikHakdangCardOptions.SyncIntervalHours)))
                        .RepeatForever()));

                if (hongikHakdangCardOptions.DeliveryEnabled)
                {
                    var cardDeliveryJobKey = new JobKey("HongikHakdangCardDelivery");
                    q.AddJob<HongikHakdangCardDeliveryJob>(opts => opts.WithIdentity(cardDeliveryJobKey));
                    q.AddTrigger(opts => opts
                        .ForJob(cardDeliveryJobKey)
                        .WithIdentity("HongikHakdangCardDelivery-trigger")
                        .StartNow()
                        .WithSimpleSchedule(x => x
                            .WithInterval(TimeSpan.FromSeconds(Math.Max(
                                30,
                                hongikHakdangCardOptions.DeliveryPollingSeconds)))
                            .RepeatForever()));
                }
            }

            if (agriculturalFisheriesBatchOptions.Enabled)
            {
                AddAgriculturalFisheriesBatchJobs(
                    q,
                    agriculturalFisheriesBatchOptions,
                    공동구매BatchRegistrationPlan);
            }

            if (communityEditorialBatchOptions.Enabled
                || communityEditorialBatchOptions.WeeklyCountryProductComparisonEnabled)
            {
                AddCommunityEditorialBatchJobs(
                    q,
                    communityEditorialBatchOptions,
                    communityEditorialBatchRegistrationPlan);
            }
        });

        services.AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; });
        return services;
    }

    private static void AddAgriculturalFisheriesBatchJobs(
        IServiceCollectionQuartzConfigurator quartz,
        AgriculturalFisheriesBatchOptions options,
        공동구매수요모집BatchRegistrationPlan 등록계획)
    {
        var timeZone = AgriculturalFisheriesBatchSchedule.ResolveTimeZone(options.TimeZoneId);

        if (options.DomesticAuctionDailyEnabled)
        {
            var jobKey = new JobKey("DomesticAuctionDailyPriceCollection");
            quartz.AddJob<DomesticAuctionDailyPriceCollectionJob>(job =>
                job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("DomesticAuctionDailyPriceCollection-trigger")
                .WithCronSchedule(
                    options.DomesticAuctionDailyCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (등록계획.Quartz등록여부(
                공동구매수요모집Os배치작업코드.Kamis일별가격수집))
        {
            var jobKey = new JobKey("KamisDailyPriceCollection");
            quartz.AddJob<KamisDailyPriceCollectionJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("KamisDailyPriceCollection-trigger")
                .WithCronSchedule(
                    options.KamisDailyCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (등록계획.Quartz등록여부(
                공동구매수요모집Os배치작업코드.Kamis월별가격이력수집))
        {
            var jobKey = new JobKey("KamisMonthlyPriceCollection");
            quartz.AddJob<KamisMonthlyPriceCollectionJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("KamisMonthlyPriceCollection-trigger")
                .WithCronSchedule(
                    options.KamisMonthlyCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (등록계획.Quartz등록여부(
                공동구매수요모집Os배치작업코드.UsdaNass월별가격수집))
        {
            var jobKey = new JobKey("UsdaMonthlyPriceCollection");
            quartz.AddJob<UsdaMonthlyPriceCollectionJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("UsdaMonthlyPriceCollection-trigger")
                .WithCronSchedule(
                    options.UsdaMonthlyCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (등록계획.Quartz등록여부(
                공동구매수요모집Os배치작업코드.공식재료기업근거수집))
        {
            var jobKey = new JobKey("OfficialFoodIngredientCompanyResearch");
            quartz.AddJob<OfficialFoodIngredientCompanyResearchJob>(job =>
                job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("OfficialFoodIngredientCompanyResearch-trigger")
                .WithCronSchedule(
                    options.IngredientCompanyResearchCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }
    }

    private static void AddCommunityEditorialBatchJobs(
        IServiceCollectionQuartzConfigurator quartz,
        CommunityEditorialBatchOptions options,
        CommunityEditorialBatchRegistrationPlan registrationPlan)
    {
        var timeZone = AgriculturalFisheriesBatchSchedule.ResolveTimeZone(options.TimeZoneId);

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.KamisPriceBrief))
        {
            var jobKey = new JobKey("CommunityKamisPriceBrief");
            quartz.AddJob<CommunityKamisPriceBriefJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityKamisPriceBrief-trigger")
                .WithCronSchedule(
                    options.KamisPriceBriefCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.UsdaNassPriceBrief))
        {
            var jobKey = new JobKey("CommunityUsdaNassPriceBrief");
            quartz.AddJob<CommunityUsdaNassPriceBriefJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityUsdaNassPriceBrief-trigger")
                .WithCronSchedule(
                    options.UsdaNassPriceBriefCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.WeeklyCountryProductComparison))
        {
            var jobKey = new JobKey("CommunityWeeklyCountryProductComparison");
            quartz.AddJob<CommunityWeeklyCountryProductComparisonJob>(job =>
                job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityWeeklyCountryProductComparison-trigger")
                .WithCronSchedule(
                    options.WeeklyCountryProductComparisonCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.Reflection))
        {
            var jobKey = new JobKey("CommunityReflection");
            quartz.AddJob<CommunityReflectionJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityReflection-trigger")
                .WithCronSchedule(
                    options.ReflectionCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.ActivityDigest))
        {
            var jobKey = new JobKey("CommunityActivityDigest");
            quartz.AddJob<CommunityActivityDigestJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityActivityDigest-trigger")
                .WithCronSchedule(
                    options.ActivityDigestCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.CultureTransport))
        {
            var jobKey = new JobKey("CommunityCultureTransportEditorial");
            quartz.AddJob<CommunityCultureTransportEditorialJob>(job =>
                job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityCultureTransportEditorial-trigger")
                .WithCronSchedule(
                    options.CultureTransportCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }

        if (registrationPlan.ShouldRegisterQuartz(
                CommunityAutomatedPostSourceKeys.Prajna))
        {
            var jobKey = new JobKey("CommunityPrajnaPublication");
            quartz.AddJob<CommunityPrajnaPublicationJob>(job => job.WithIdentity(jobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(jobKey)
                .WithIdentity("CommunityPrajnaPublication-trigger")
                .WithCronSchedule(
                    options.PrajnaPublicationCronExpression,
                    schedule => schedule
                        .InTimeZone(timeZone)
                        .WithMisfireHandlingInstructionDoNothing()));
        }
    }
}
