using Ssalddel.Application.Driver.Notification;
using Ssalddel.Application.Driver.Profile;
using Ssalddel.Application.Driver.Settlement;
using Ssalddel.Application.Driver.Transport;
using Ssalddel.Application.Admin.Settlement;
using Ssalddel.Services.LogisticsProcessing.VehicleLoading;
using Ssalddel.Services.LogisticsProcessing.Warehouse;
using Ssalddel.Services.Content;
using Ssalddel.Services.Operations;
using Ssalddel.Services.Storage.Azure;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Continuity;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.DeliveryZones;
using 살뜰.Services.Documents;
using 살뜰.Services.HIOPSAI;
using 살뜰.Services.Images;
using 살뜰.Services.Payments;
using 살뜰.Services.Sales;
using 살뜰.Services.Operations;

namespace Ssalddel.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddSsalddelOperationsDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<AzureBlobStorageService>();
        services.AddSingleton<GoogleCloudStorageService>();
        services.AddSingleton<DevelopmentLocalStorageService>();
        services.AddSingleton<IObjectStorageService>(serviceProvider =>
        {
            var provider = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<ObjectStorageOptions>>()
                .Value.Provider?
                .Trim() ?? string.Empty;
            return provider switch
            {
                ObjectStorageProviderNames.AzureBlob => serviceProvider.GetRequiredService<AzureBlobStorageService>(),
                ObjectStorageProviderNames.GoogleCloud => serviceProvider.GetRequiredService<GoogleCloudStorageService>(),
                ObjectStorageProviderNames.Local => serviceProvider.GetRequiredService<DevelopmentLocalStorageService>(),
                _ => throw new InvalidOperationException($"Unsupported ObjectStorage provider: {provider}")
            };
        });
        services.AddScoped<I앱문맥이미지자산조회UseCase, 앱문맥이미지자산조회UseCase>();
        services.AddScoped<I국내화물운송기사상태Service, 국내화물운송기사상태Service>();
        services.AddSingleton<ICommandFileStoragePathResolver, CommandFileStoragePathResolver>();
        services.AddSingleton<IDispatchRecommendationLogStore, DispatchRecommendationLogStore>();
        services.AddSingleton<IDispatchAcceptanceLogStore, DispatchAcceptanceLogStore>();
        services.AddScoped<I배차대기원장전환Service, 배차대기원장전환Service>();
        services.AddScoped<I음식배달기사제안요금Service, 음식배달기사제안요금Service>();
        services.AddScoped<I배차실행인덱스예열Service, 배차실행인덱스예열Service>();
        services.AddScoped<I피킹포장작업투영Service, 피킹포장작업투영Service>();
        services.AddSingleton<살뜰.도메인.운영.살뜰마트라스트마일배차Policy>();
        services.AddScoped<I살뜰마트라스트마일배차인계Service, 살뜰마트라스트마일배차인계Service>();
        services.AddScoped<I알뜰살뜰마트배차대기Service, 알뜰살뜰마트배차대기Service>();
        services.AddScoped<I배차추천알림Service, 배차추천알림Service>();
        services.AddScoped<I상차접근알림Service, 상차접근알림Service>();
        services.AddScoped<ICommand알림Outbox발송Service, Command알림Outbox발송Service>();
        services.AddSingleton<I탐색캠페인이벤트저장소, 탐색캠페인이벤트저장소>();
        services.AddSingleton<IAdminFilePodStore, AdminFilePodStore>();
        services.AddSingleton<I문서관리Store, 문서관리Store>();
        services.AddSingleton<I문서관리Service, 문서관리Service>();
        services.AddSingleton<I문서생성OutboxService, 문서생성OutboxService>();
        services.AddSingleton<IHIOPSAIUsageBudgetStore, FileHIOPSAIUsageBudgetStore>();
        services.AddSingleton<이미지프롬프트생성기Resolver, 기본이미지프롬프트생성기Resolver>();
        services.AddScoped<I샘플이미지대상ResolverResolver, 샘플이미지대상ResolverResolver>();
        services.AddSingleton<I이미지프롬프트생성기, 화주상품사진프롬프트생성기>();
        services.AddSingleton<I이미지프롬프트생성기, 기사상차인증사진프롬프트생성기>();
        services.AddSingleton<I이미지프롬프트생성기, 기사배차완료인증사진프롬프트생성기>();
        services.AddSingleton<I이미지프롬프트생성기, 음식상품썸네일프롬프트생성기>();
        services.AddSingleton<I이미지프롬프트생성기, 주문후기사진프롬프트생성기>();
        services.AddSingleton<I이미지프롬프트생성기, 상품상세페이지이미지프롬프트생성기>();
        services.AddSingleton<I이미지프롬프트생성기, 커뮤니티글쓰기이미지프롬프트생성기>();
        services.AddScoped<I샘플이미지대상Resolver, 판매상품샘플이미지대상Resolver>();
        services.AddScoped<I샘플이미지대상Resolver, 상품상세이미지생성작업대상Resolver>();
        services.AddScoped<I샘플이미지생성Service, 샘플이미지생성Service>();
        services.AddScoped<I배차추천경로Service, 배차추천경로Service>();
        services.AddScoped<I용달기사프로필UseCase, 용달기사프로필UseCase>();
        services.AddScoped<I기사알림UseCase, 기사알림UseCase>();
        services.AddScoped<I기사정산계좌UseCase, 기사정산계좌UseCase>();
        services.AddScoped<I기사지급준비UseCase, 기사지급준비UseCase>();
        services.AddScoped<I기사지급승인UseCase, 기사지급승인UseCase>();
        services.AddScoped<I기사지급Gateway, 준비전용기사지급Gateway>();
        services.AddScoped<I기사지급OutboxService, 기사지급OutboxService>();
        services.AddScoped<I기사운송일정구성Service, 기사운송일정구성Service>();
        services.AddScoped<I운송일정삽입평가Service, 운송일정삽입평가Service>();
        services.AddScoped<I화물배차수락적격성Service, 화물배차수락적격성Service>();
        services.AddSingleton<살뜰.도메인.운송.화물연속배차Policy>();
        services.AddScoped<I화물연속배차UseCase, 화물연속배차UseCase>();
        services.AddScoped<I운영체제업무인계Coordinator, 운영체제업무인계Coordinator>();
        services.AddScoped<I화주운송의뢰화물운송인계Service, 화주운송의뢰화물운송인계Service>();
        services.AddScoped<I화물운송완료화주인수인계Service, 화물운송완료화주인수인계Service>();
        services.AddScoped<I비정상운송사건Service, 비정상운송사건Service>();
        services.AddScoped<I출고화물운송운영체제인계Service, 출고화물운송운영체제인계Service>();
        services.AddScoped<I생활권물류거점Service, 생활권물류거점Service>();
        services.AddScoped<I픽업하차경로최적화Service, 픽업하차경로최적화Service>();
        services.AddScoped<I음식멀티배차조합AIService, 규칙기반음식멀티배차조합AIService>();
        services.AddScoped<I음식멀티배차조합Service, 음식멀티배차조합Service>();
        services.AddScoped<I배차추천판정Service, 배차추천판정Service>();
        services.AddScoped<I배차추천평가Service, 배차추천평가Service>();
        services.AddScoped<I원장배달권투영Service, 원장배달권투영Service>();
        services.AddScoped<I운송원장배달권연결Service, 운송원장배달권연결Service>();
        services.AddScoped<I행정동배달운영권역Source, Official행정동배달운영권역Source>();
        services.AddScoped<I배달운영권역관리Service, 배달운영권역관리Service>();
        services.AddSingleton<I음식배달권실행공간Store, InMemory음식배달권실행공간Store>();
        services.AddSingleton<I국내화물배달권실행공간Store, InMemory국내화물배달권실행공간Store>();
        services.AddScoped<I배달권기반배차조율계획Service, 배달권기반배차조율계획Service>();
        services.AddScoped<I배달권기반배차조율실행Service, 배달권기반배차조율실행Service>();
        services.AddScoped<I국내화물배차조율입력Factory, 국내화물배차조율입력Factory>();
        services.AddSingleton<File배차AI판단사례LedgerStore>();
        services.AddSingleton<I배차AI판단사례LedgerStore>(sp => sp.GetRequiredService<File배차AI판단사례LedgerStore>());
        services.AddSingleton<I배차AI판단근거Source>(sp => sp.GetRequiredService<File배차AI판단사례LedgerStore>());
        services.AddScoped<I배차AI판단근거조회Service, 규칙기반배차AI판단근거조회Service>();
        services.AddScoped<I운송의뢰수익묶음AIService, 규칙기반운송의뢰수익묶음AIService>();
        services.AddScoped<I운송의뢰수익묶음Service, 운송의뢰수익묶음Service>();
        services.AddScoped<I국내화물기사배정AIService, 규칙기반국내화물기사배정AIService>();
        services.AddScoped<I국내화물배차조율Service, 국내화물배차조율Service>();
        services.AddScoped<I국내화물배차조율적용Service, 국내화물배차조율적용Service>();
        services.AddScoped<I국내화물배차조율실행Service, 국내화물배차조율실행Service>();
        services.AddScoped<IDomesticCargoDispatchAIReviewService, DomesticCargoDispatchAIReviewService>();
        services.AddScoped<IFoodDeliveryDispatchAIReviewService, FoodDeliveryDispatchAIReviewService>();
        services.AddScoped<I배차업무정책, 용달운송배차업무정책>();
        services.AddScoped<I배차업무정책, 음식배달배차업무정책>();
        services.AddScoped<I운송의뢰배차원천분류Service, 운송의뢰배차원천분류Service>();
        services.AddScoped<I운송의뢰배차대기Service, 운송의뢰배차대기Service>();
        services.AddScoped<I화물용달배차흐름Resolver, 화물용달배차흐름Resolver>();
        services.AddScoped<I음식배달배차흐름Resolver, 음식배달배차흐름Resolver>();
        services.AddScoped<화물용달배차엔진>();
        services.AddScoped<음식배달배차엔진>();
        services.AddScoped<I운송의뢰배차엔진>(sp => sp.GetRequiredService<화물용달배차엔진>());
        services.AddScoped<I운송의뢰배차엔진>(sp => sp.GetRequiredService<음식배달배차엔진>());
        services.AddScoped<I운영체제배차EngineCatalog, 운영체제배차EngineCatalog>();
        services.AddScoped<I배차추천후보선정Service, 배차추천후보선정Service>();
        services.AddScoped<I공개배차Service, 공개배차Service>();
        services.AddSingleton<차량적재추천Engine>();
        services.AddSingleton<차량혼적순서Engine>();
        services.AddScoped<I차량적재추천Service, 차량적재추천Service>();
        services.AddScoped<Ssalddel.Application.Driver.Recommendation.I기사배차추천UseCase, Ssalddel.Application.Driver.Recommendation.기사배차추천UseCase>();
        services.AddScoped<살뜰.Services.Dispatch.Recommendation.I차량화물적합성Service, 살뜰.Services.Dispatch.Recommendation.차량화물적합성Service>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I차량추천Service, Ssalddel.Application.Shipper.Request.차량추천Service>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송의뢰추천Service, Ssalddel.Application.Shipper.Request.화주운송의뢰추천Service>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송의뢰일괄등록파서Service, Ssalddel.Application.Shipper.Request.화주운송의뢰일괄등록파서Service>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송기준운임Service, Ssalddel.Application.Shipper.Request.화주운송기준운임Service>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송요금정책검토Service, Ssalddel.Application.Shipper.Request.화주운송요금정책검토Service>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송업무담당자UseCase, Ssalddel.Application.Shipper.Request.화주운송업무담당자UseCase>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송업무망조회UseCase, Ssalddel.Application.Shipper.Request.화주운송업무망조회UseCase>();
        services.AddScoped<Ssalddel.Application.Shipper.Request.I화주운송의뢰UseCase, Ssalddel.Application.Shipper.Request.화주운송의뢰UseCase>();
        services.AddScoped<I판매상품샘플시드Service, 판매상품샘플시드Service>();
        services.AddScoped<I배차추천Service, 화물배차추천Service>();
        services.AddScoped<I음식배차추천Service, 음식배차추천Service>();
        services.AddScoped<I음식배달기사업무Service, 음식배달기사업무Service>();
        services.AddScoped<I운행중배차추천Service, 운행중배차추천Service>();
        services.AddScoped<I비운행중배차추천Service, 비운행중배차추천Service>();
        services.AddScoped<I기사운송상태전이Service, 기사운송상태전이Service>();
        services.AddScoped<I탐색대상추천Service, 탐색대상추천Service>();
        services.AddScoped<I탐색캠페인상태전이Service, 탐색캠페인상태전이Service>();
        services.AddScoped<INationalDispatchRequestService, NationalDispatchRequestService>();
        services.AddScoped<I기사월정산Service, 기사월정산Service>();
        services.AddScoped<IPlatformProfitReturnService, PlatformProfitReturnService>();
        services.AddHostedService<배차실행인덱스예열HostedService>();
        services.AddHostedService<화물연속배차BackgroundService>();

        return services;
    }
}
