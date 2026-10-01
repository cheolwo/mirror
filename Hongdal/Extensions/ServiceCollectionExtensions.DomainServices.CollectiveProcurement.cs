using Hongdal.Services.CollectiveProcurement;
using Hongdal.Services.Community;
using Hongdal.Services.Orderer;
using 홍달.Services.External.PublicData;
using 홍달.Services.Sales;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    private static IServiceCollection AddHongdalCollectiveProcurementDomainServices(this IServiceCollection services)
    {
        services.AddSingleton<I주문자집단배송권조회Service, 주문자집단배송권조회Service>();
        services.AddSingleton<I주문자집단자동배정Service, 주문자집단자동배정Service>();
        services.AddSingleton<I공동구매물류워크플로우저장소, Mongo공동구매물류워크플로우저장소>();
        services.AddSingleton<I공동구매자동집단화저장소, Mongo공동구매자동집단화저장소>();
        services.AddScoped<I공동구매수령창고Service, 공동구매수령창고Service>();
        services.AddScoped<I공동구매개별주문원장Service, 공동구매개별주문원장Service>();
        services.AddSingleton<ICommunityGroupPurchaseDemandHandoff, CommunityVoteOrdererDemandHandoff>();
        services.AddSingleton<ICommunityProducerMemberDirectory, UnconnectedCommunityProducerMemberDirectory>();
        services.AddSingleton<ICommunityGroupPurchaseRepresentativeDirectory, UnconnectedCommunityGroupPurchaseRepresentativeDirectory>();
        services.AddSingleton<IDomesticProducerContactRequestDraftStore, MongoDomesticProducerContactRequestDraftStore>();
        services.AddSingleton<IDomesticProducerSupplyOfferDraftStore, MongoDomesticProducerSupplyOfferDraftStore>();
        services.AddScoped<IDomesticGroupPurchaseProducerConnectionService, DomesticGroupPurchaseProducerConnectionService>();
        services.AddSingleton<IDomesticGroupPurchaseFulfillmentOrderDraftStore, MongoDomesticGroupPurchaseFulfillmentOrderDraftStore>();
        services.AddScoped<IDomesticGroupPurchaseFulfillmentPlanService, DomesticGroupPurchaseFulfillmentPlanService>();
        services.AddScoped<IDomesticGroupPurchaseVehicleRecommendationService, DomesticGroupPurchaseVehicleRecommendationService>();
        services.AddSingleton<IDomesticGroupPurchaseNegotiationClock, SystemDomesticGroupPurchaseNegotiationClock>();
        services.AddSingleton<IDomesticGroupPurchaseNegotiationStore, MongoDomesticGroupPurchaseNegotiationStore>();
        services.AddScoped<IDomesticGroupPurchaseNegotiationService, DomesticGroupPurchaseNegotiationService>();
        services.AddSingleton<ICollectiveProcurementPlanningClock, SystemCollectiveProcurementPlanningClock>();
        services.AddSingleton<ICollectiveProcurementEconomicsEngine, CollectiveProcurementEconomicsEngine>();
        services.AddSingleton<ICollectiveProcurementPlanningStore, MongoCollectiveProcurementPlanningStore>();
        services.AddScoped<ICollectiveProcurementPlanningService, CollectiveProcurementPlanningService>();
        services.AddSingleton<I공동구매해외선적추적저장소, Mongo공동구매해외선적추적저장소>();
        services.AddSingleton<I공동구매커머스이행계획저장소, Mongo공동구매커머스이행계획저장소>();
        services.AddSingleton<I주문자집단운영주체저장소, Mongo주문자집단운영주체저장소>();
        services.AddSingleton<I판매페이지초안저장소, Mongo판매페이지초안저장소>();

        return services;
    }
}
