using FluentValidation;
using Hongdal.Application.Behaviors;
using Hongdal.Application.CommandProcessing;
using Hongdal.Application.Community;
using Hongdal.Middleware;
using Hongdal.Application.HumanResources;
using Hongdal.Application.Sales;
using Hongdal.Application.ViewSettings;
using Hongdal.Application.Warehouse;
using Hongdal.Application.Customs;
using Hongdal.Application.Audit;
using Hongdal.Application.CommonContents;
using Hongdal.Application.Images;
using Hongdal.Application.Driver.Food;
using Hongdal.Application.Food;
using Hongdal.Application.PublicData;
using Hongdal.Application.Evidence;
using Hongdal.Application.Files;
using Hongdal.Application.Orderer;
using Hongdal.Application.Settlement;
using Hongdal.Application.Versioning;
using Hongdal.Application.Security;
using Hongdal.Application.Operations;
using Hongdal.Services.HumanResources;
using Hongdal.Services.Community;
using Hongdal.Services.LogisticsProcessing.SalesOrders;
using 홍달.Infrastructure;
using 홍달.Services.Audit;
using 홍달.Services.Sales;
using 홍달.Services.ViewSettings;
using Hongdal.Services.LogisticsProcessing.Warehouse;
using Hongdal.Services.LogisticsProcessing.VehicleLoading;
using Hongdal.Application.Driver.Transport;

namespace Hongdal.Extensions;

public static partial class ServiceCollectionExtensions
{
    public static IServiceCollection AddHongdalApplicationCore(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddValidatorsFromAssembly(typeof(Program).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Command후처리Behavior<,>));

        services.AddHongdalInfrastructure();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        services.AddScoped<I참여자실행권한검사, 참여자실행권한검사>();
        services.AddScoped<I사회보험신고UseCase, 사회보험신고UseCase>();
        services.AddScoped<IHR참여운영UseCase, HR참여운영UseCase>();
        services.AddScoped<I인연스냅샷조회UseCase, 인연스냅샷조회UseCase>();
        services.AddScoped<I기사운송상태변경CommandExecutor, 기사운송상태변경CommandExecutor>();
        services.AddScoped<I운송증빙첨부JsonWriter, 운송증빙첨부JsonWriter>();
        services.AddScoped<I운송완료입금요청Service, 운송완료입금요청Service>();
        services.AddScoped<ICommand기능설정Resolver, Command기능설정Resolver>();
        services.AddScoped<ICommand기능CatalogResolver, Command기능CatalogResolver>();
        services.AddScoped<IWorkRelationshipSnapshotCollector, WorkRelationshipSnapshotCollector>();
        services.AddScoped<ICommand후처리Processor, Command감사로그Processor>();
        services.AddScoped<ICommand후처리Processor, Command알림의도Processor>();
        services.AddScoped<ICommand후처리Processor, Command인연스냅샷Processor>();

        services.AddScoped<I사용자행위로그Service, 사용자행위로그Service>();
        services.AddScoped<ISalesChannelService, SalesChannelService>();
        services.AddScoped<I판매채널UseCase, 판매채널UseCase>();
        services.AddScoped<I판매페이지Service, 판매페이지Service>();
        services.AddScoped<I판매페이지UseCase, 판매페이지UseCase>();
        services.AddScoped<IView가시성Service, View가시성Service>();
        services.AddScoped<IView설정UseCase, View설정UseCase>();
        services.AddScoped<I관리자View정책UseCase, 관리자View정책UseCase>();
        services.AddScoped<I보조기능설정UseCase, 보조기능설정UseCase>();
        services.AddScoped<IHS코드운영UseCase, HS코드운영UseCase>();
        services.AddScoped<I공동수입HS코드조회UseCase, 공동수입HS코드조회UseCase>();
        services.AddScoped<I사용자행위로그조회UseCase, 사용자행위로그조회UseCase>();
        services.AddScoped<I공통콘텐츠관리UseCase, 공통콘텐츠관리UseCase>();
        services.AddScoped<IKieAi콜백UseCase, KieAi콜백UseCase>();
        services.AddScoped<I샘플이미지작업UseCase, 샘플이미지작업UseCase>();
        services.AddScoped<I배달기사월정산UseCase, 배달기사월정산UseCase>();
        services.AddScoped<IFoodDeliveryDriverWorkspaceUseCase, FoodDeliveryDriverWorkspaceUseCase>();
        services.AddScoped<IFoodDeliveryDriverRouteService, FoodDeliveryDriverRouteService>();
        services.AddScoped<I음식주문접수UseCase, 음식주문접수UseCase>();
        services.AddScoped<I공공데이터조회UseCase, 공공데이터조회UseCase>();
        services.AddScoped<I파일POD관리UseCase, 파일POD관리UseCase>();
        services.AddScoped<I문서관리UseCase, 문서관리UseCase>();
        services.AddScoped<I파일업로드UseCase, 파일업로드UseCase>();
        services.AddScoped<I공동구매해외선적추적UseCase, 공동구매해외선적추적UseCase>();
        services.AddScoped<I플랫폼수익환급UseCase, 플랫폼수익환급UseCase>();
        services.AddScoped<I버전워크플로우UseCase, 버전워크플로우UseCase>();
        services.AddScoped<IISMSP전송보호UseCase, ISMSP전송보호UseCase>();
        services.AddScoped<I커뮤니티게시판UseCase, 커뮤니티게시판UseCase>();
        services.AddScoped<ICommunityBoardWritePolicy, CommunityBoardWritePolicy>();
        services.AddScoped<커뮤니티게시글생성Service>();
        services.AddScoped<커뮤니티게시글UseCase>();
        services.AddScoped<I커뮤니티게시글UseCase>(provider =>
            provider.GetRequiredService<커뮤니티게시글UseCase>());
        services.AddScoped<I커뮤니티게시글조회UseCase, 커뮤니티게시글조회UseCase>();
        services.AddScoped<I커뮤니티게시글발행UseCase, 커뮤니티게시글발행UseCase>();
        services.AddScoped<I커뮤니티게시글예약발행UseCase, 커뮤니티게시글예약발행UseCase>();
        services.AddScoped<I커뮤니티게시글첨부UseCase, 커뮤니티게시글첨부UseCase>();
        services.AddScoped<I커뮤니티게시글참여UseCase, 커뮤니티게시글참여UseCase>();
        services.AddScoped<I커뮤니티게시글운영UseCase, 커뮤니티게시글운영UseCase>();
        services.AddScoped<I커뮤니티투표UseCase, 커뮤니티투표UseCase>();
        services.AddScoped<I커뮤니티활동신호UseCase, 커뮤니티활동신호UseCase>();
        services.AddScoped<I노드스티커상점UseCase, 노드스티커상점UseCase>();
        services.AddScoped<ICommunityExperienceAwardService, CommunityExperienceAwardService>();
        services.AddScoped<ICommunityExperienceEventRecorder, CommunityExperienceEventRecorder>();
        services.AddScoped<IWarehouseOperationService, WarehouseOperationService>();
        services.AddScoped<IWarehousePerspectiveReadService, WarehousePerspectiveReadService>();
        services.AddScoped<ILoadingPerspectiveReadService, LoadingPerspectiveReadService>();
        services.AddScoped<IUnloadingPerspectiveReadService, UnloadingPerspectiveReadService>();
        services.AddScoped<IIndividualOrderPerspectiveReadService, IndividualOrderPerspectiveReadService>();
        services.AddScoped<IGroupOrderPerspectiveReadService, GroupOrderPerspectiveReadService>();
        services.AddScoped<I창고작업UseCase, 창고작업UseCase>();
        services.AddScoped<IWarehouseServiceAreaPolicy, WarehouseServiceAreaPolicy>();
        services.AddScoped<IWarehouseDistanceCostEstimator, WarehouseDistanceCostEstimator>();
        services.AddScoped<IOutboundBatchEngine, OutboundBatchEngine>();
        services.AddScoped<I피킹배치Engine, 피킹배치Engine>();
        services.AddScoped<IWorkRelationshipSnapshotService, WorkRelationshipSnapshotService>();
        services.AddScoped<ICommunityActivitySignalService, CommunityActivitySignalService>();
        services.AddSingleton<ICommunityVoteStore, MongoCommunityVoteStore>();
        services.AddSingleton<I공동구매원장캠페인Store, CommunityVote공동구매원장캠페인Store>();
        services.AddScoped<I공동구매원장절차Service, 공동구매원장절차Service>();
        services.AddScoped<I공동수입원장전환Service, 공동수입원장전환Service>();
        services.AddScoped<ICommunityGroupPurchaseDemandOutboxProcessor, CommunityGroupPurchaseDemandOutboxProcessor>();
        services.AddScoped<ICommunityVoteService>(serviceProvider =>
            new CommunityVoteService(
                serviceProvider.GetRequiredService<ICommunityVoteStore>(),
                serviceProvider.GetRequiredService<ICommunityGroupPurchaseDemandOutboxProcessor>(),
                serviceProvider.GetRequiredService<I공동구매원장절차Service>(),
                serviceProvider.GetRequiredService<IOperatingMarketContextAccessor>()
                    .Current.Profile.CountryCode));
        services.AddScoped<ISalesChannelOrderSyncService, SalesChannelOrderSyncService>();
        services.AddSingleton<ISalesChannelOrderFeedClient, EmptySalesChannelOrderFeedClient>();
        services.AddScoped<IHrRoleAssignmentStore, EfCoreHrRoleAssignmentStore>();
        services.AddScoped<IHrEmploymentContractService, HrEmploymentContractService>();
        services.AddSingleton<IHrParticipationBenefitRecordService, InMemoryHrParticipationBenefitRecordService>();
        services.AddSingleton<ISocialInsuranceFilingService, InMemorySocialInsuranceFilingService>();
        services.AddScoped<HrRoleAccessMiddleware>();
        services.AddScoped<사용자행위로그Middleware>();

        return services;
    }
}
