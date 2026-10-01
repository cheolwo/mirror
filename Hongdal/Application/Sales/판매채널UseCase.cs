using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Sales;
using 홍달.Services.Audit;
using 홍달.Services.Sales;

namespace Hongdal.Application.Sales;

public interface I판매채널UseCase
{
    Task<Result<판매채널계정목록응답>> 계정목록Async(CancellationToken cancellationToken);
    Task<Result<판매채널계정항목응답>> 계정생성Async(판매채널계정저장요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매채널계정항목응답>> 계정수정Async(long accountId, 판매채널계정저장요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result> 계정삭제Async(long accountId, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매상품목록응답>> 상품목록Async(CancellationToken cancellationToken);
    Task<Result<판매상품항목응답>> 상품생성Async(판매상품저장요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매상품항목응답>> 상품수정Async(long productId, 판매상품저장요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result> 상품삭제Async(long productId, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매상품목록응답>> 샘플상품시드Async(판매상품샘플시드요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<채널출품목록응답>> 출품목록Async(CancellationToken cancellationToken);
    Task<Result<채널출품항목응답>> 출품생성Async(채널출품저장요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<채널출품항목응답>> 출품수정Async(long listingId, 채널출품저장요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result> 출품삭제Async(long listingId, 판매채널요청Context context, CancellationToken cancellationToken);
}

[HongdalApiWorkflow(HongdalWorkflow.SalesChannelFulfillment)]
[HongdalUseCase("판매채널 출품 관리", Summary = "판매자가 판매채널 계정, 판매상품, 채널 출품을 만들고 출고 이행의 시작점을 준비합니다.")]
[HongdalUseCaseActor(HongdalActor.Seller)]
[HongdalUseCaseActor(HongdalActor.WarehouseManager, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "샘플이미지작업UseCase",
    Condition = "상품 상세 페이지, 판매 이미지, 광고 소재 생성을 보조하는 경우",
    Summary = "판매채널 출품을 샘플 이미지 생성과 상세 이미지 작업 흐름으로 확장합니다.")]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "창고작업UseCase",
    Condition = "판매채널 주문을 창고 재고 확인과 출고 배치로 연결하는 경우",
    Summary = "판매채널 주문 이행을 창고 출고 작업으로 확장합니다.")]
public sealed class 판매채널UseCase : I판매채널UseCase
{
    private readonly ISalesChannelService _salesChannelService;
    private readonly I사용자행위로그Service _activityLogService;

    public 판매채널UseCase(
        ISalesChannelService salesChannelService,
        I사용자행위로그Service activityLogService)
    {
        _salesChannelService = salesChannelService;
        _activityLogService = activityLogService;
    }

    public async Task<Result<판매채널계정목록응답>> 계정목록Async(CancellationToken cancellationToken)
        => await _salesChannelService.GetAccountsAsync(cancellationToken);

    public async Task<Result<판매채널계정항목응답>> 계정생성Async(
        판매채널계정저장요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.CreateAccountAsync(request, cancellationToken);
        await 로그기록Async("SalesChannel", "AccountCreated", $"{{\"accountId\":{result.Id},\"channelType\":\"{result.채널종류}\"}}", context, cancellationToken);
        return result;
    }

    public async Task<Result<판매채널계정항목응답>> 계정수정Async(
        long accountId,
        판매채널계정저장요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.UpdateAccountAsync(accountId, request, cancellationToken);
        await 로그기록Async("SalesChannel", "AccountUpdated", $"{{\"accountId\":{result.Id}}}", context, cancellationToken);
        return result;
    }

    public async Task<Result> 계정삭제Async(
        long accountId,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        await _salesChannelService.DeleteAccountAsync(accountId, cancellationToken);
        await 로그기록Async("SalesChannel", "AccountDeleted", $"{{\"accountId\":{accountId}}}", context, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<판매상품목록응답>> 상품목록Async(CancellationToken cancellationToken)
        => await _salesChannelService.GetProductsAsync(cancellationToken);

    public async Task<Result<판매상품항목응답>> 상품생성Async(
        판매상품저장요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.CreateProductAsync(request, cancellationToken);
        await 로그기록Async("SalesProduct", "ProductCreated", $"{{\"productId\":{result.Id},\"inboundItemId\":{result.입고상품Id}}}", context, cancellationToken);
        return result;
    }

    public async Task<Result<판매상품항목응답>> 상품수정Async(
        long productId,
        판매상품저장요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.UpdateProductAsync(productId, request, cancellationToken);
        await 로그기록Async("SalesProduct", "ProductUpdated", $"{{\"productId\":{result.Id}}}", context, cancellationToken);
        return result;
    }

    public async Task<Result> 상품삭제Async(
        long productId,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        await _salesChannelService.DeleteProductAsync(productId, cancellationToken);
        await 로그기록Async("SalesProduct", "ProductDeleted", $"{{\"productId\":{productId}}}", context, cancellationToken);
        return Result.Ok();
    }

    public async Task<Result<판매상품목록응답>> 샘플상품시드Async(
        판매상품샘플시드요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.SeedSampleProductsAsync(request, cancellationToken);
        await 로그기록Async("SalesProduct", "SampleProductsSeeded", $"{{\"count\":{result.Items.Count}}}", context, cancellationToken);
        return result;
    }

    public async Task<Result<채널출품목록응답>> 출품목록Async(CancellationToken cancellationToken)
        => await _salesChannelService.GetListingsAsync(cancellationToken);

    public async Task<Result<채널출품항목응답>> 출품생성Async(
        채널출품저장요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.CreateListingAsync(request, cancellationToken);
        await 로그기록Async("Listing", "ListingCreated", $"{{\"listingId\":{result.Id},\"salesProductId\":{result.판매상품Id},\"accountId\":{result.판매채널계정Id}}}", context, cancellationToken);
        return result;
    }

    public async Task<Result<채널출품항목응답>> 출품수정Async(
        long listingId,
        채널출품저장요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        var result = await _salesChannelService.UpdateListingAsync(listingId, request, cancellationToken);
        await 로그기록Async("Listing", "ListingUpdated", $"{{\"listingId\":{result.Id}}}", context, cancellationToken);
        return result;
    }

    public async Task<Result> 출품삭제Async(
        long listingId,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        await _salesChannelService.DeleteListingAsync(listingId, cancellationToken);
        await 로그기록Async("Listing", "ListingDeleted", $"{{\"listingId\":{listingId}}}", context, cancellationToken);
        return Result.Ok();
    }

    private async Task 로그기록Async(
        string actionType,
        string actionName,
        string metadataJson,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        await _activityLogService.기록Async(new 사용자행위로그기록
        {
            AppKey = context.AppKey,
            UserId = context.UserId,
            UserName = context.UserName,
            RoleName = context.RoleName,
            ActionType = actionType,
            ActionName = actionName,
            Route = context.Route,
            TraceId = context.TraceId,
            IsSuccess = true,
            ClientIp = context.ClientIp,
            UserAgent = context.UserAgent,
            OccurredAtUtc = DateTime.UtcNow,
            MetadataJson = metadataJson
        }, cancellationToken);
    }
}

public sealed record 판매채널요청Context(
    string AppKey,
    string UserId,
    string UserName,
    string RoleName,
    string Route,
    string TraceId,
    string ClientIp,
    string UserAgent);
