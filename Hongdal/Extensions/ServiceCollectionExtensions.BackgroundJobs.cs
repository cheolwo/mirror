using Quartz;
using Hongdal.Infrastructure.BackgroundJobs.AgriculturalFisheries;
using Hongdal.Infrastructure.BackgroundJobs.Community;
using Hongdal.Infrastructure.BackgroundJobs.Content;
using Hongdal.Infrastructure.BackgroundJobs.SalesOrders;
using Hongdal.Services.LogisticsProcessing.SalesOrders;
using 홍달.Infrastructure.BackgroundJobs.Customs;
using 홍달.Infrastructure.BackgroundJobs.DispatchQueue;
using 홍달.Infrastructure.BackgroundJobs.Notifications;
using 홍달.Infrastructure.BackgroundJobs.Payments;
using 홍달.Services.Options;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddHongdalBackgroundJobs(
        this IServiceCollection services,
        배차큐배치작업Options jobOptions,
        SalesChannelOrderSyncOptions salesOrderSyncOptions,
        YouTubeOptions youTubeOptions,
        HongikHakdangCardOptions hongikHakdangCardOptions,
        AgriculturalFisheriesBatchOptions agriculturalFisheriesBatchOptions,
        CommunityEditorialBatchOptions communityEditorialBatchOptions,
        HongdalExecutionOptions executionOptions)
    {
        services.AddScoped<AgriculturalFisheriesBatchRunner>();
        services.AddScoped<CommunityEditorialBatchRunner>();
        services.AddQuartz(q =>
        {
            if (executionOptions.Mode == HongdalExecutionMode.Operational)
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
                AddAgriculturalFisheriesBatchJobs(q, agriculturalFisheriesBatchOptions);
            }

            if (communityEditorialBatchOptions.Enabled)
            {
                AddCommunityEditorialBatchJobs(q, communityEditorialBatchOptions);
            }
        });

        services.AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; });
        return services;
    }

    private static void AddAgriculturalFisheriesBatchJobs(
        IServiceCollectionQuartzConfigurator quartz,
        AgriculturalFisheriesBatchOptions options)
    {
        var timeZone = AgriculturalFisheriesBatchSchedule.ResolveTimeZone(options.TimeZoneId);

        if (options.KamisDailyEnabled)
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

        if (options.KamisMonthlyEnabled)
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

        if (options.UsdaMonthlyEnabled)
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
    }

    private static void AddCommunityEditorialBatchJobs(
        IServiceCollectionQuartzConfigurator quartz,
        CommunityEditorialBatchOptions options)
    {
        var timeZone = AgriculturalFisheriesBatchSchedule.ResolveTimeZone(options.TimeZoneId);

        if (options.KamisPriceBriefEnabled)
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

        if (options.ReflectionEnabled)
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

        if (options.ActivityDigestEnabled)
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

        if (options.PrajnaPublicationEnabled)
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
