using FluentResults;
using Hongdal.ApiMetadata;
using Hongdal.Contracts.Common.Sales;
using 홍달.Services.Audit;
using 홍달.Services.Sales;

namespace Hongdal.Application.Sales;

public interface I판매페이지UseCase
{
    Task<Result<판매페이지초안목록응답>> 초안목록Async(판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매페이지초안응답>> 초안조회Async(string pageId, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매페이지초안응답>> 초안생성Async(판매페이지초안생성요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
    Task<Result<판매페이지초안응답>> 초안수정Async(string pageId, 판매페이지초안수정요청 request, 판매채널요청Context context, CancellationToken cancellationToken);
}

[HongdalUseCase(
    "판매 페이지 초안 관리",
    Summary = "일반 판매자, 농가, 제조자와 수출업자가 상품 정보를 편집하고 개별주문 또는 공동주문을 받을 판매 페이지 초안을 준비합니다.")]
[HongdalUseCaseActor(HongdalActor.Seller)]
[HongdalUseCaseActor(HongdalActor.Orderer, HongdalUseCaseActorRole.Supporting)]
[HongdalUseCaseRelation(
    HongdalUseCaseRelationKind.Extend,
    "판매채널UseCase",
    Condition = "판매 페이지 검수 후 기존 입고상품 기반 판매상품을 연결하는 경우",
    Summary = "페이지 작성과 실제 재고·판매·출품 확정을 분리합니다.")]
public sealed class 판매페이지UseCase : I판매페이지UseCase
{
    private readonly I판매페이지Service _service;
    private readonly I사용자행위로그Service _activityLogService;

    public 판매페이지UseCase(
        I판매페이지Service service,
        I사용자행위로그Service activityLogService)
    {
        _service = service;
        _activityLogService = activityLogService;
    }

    public async Task<Result<판매페이지초안목록응답>> 초안목록Async(
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _service.초안목록Async(context.UserId, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Fail<판매페이지초안목록응답>(ex.Message);
        }
    }

    public async Task<Result<판매페이지초안응답>> 초안조회Async(
        string pageId,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _service.초안조회Async(pageId, context.UserId, cancellationToken);
            return item is null
                ? Result.Fail<판매페이지초안응답>("판매 페이지 초안을 찾을 수 없습니다.")
                : Result.Ok(item);
        }
        catch (Exception ex)
        {
            return Result.Fail<판매페이지초안응답>(ex.Message);
        }
    }

    public async Task<Result<판매페이지초안응답>> 초안생성Async(
        판매페이지초안생성요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _service.초안생성Async(request, context.UserId, cancellationToken);
            await LogAsync("SalesPage", "DraftCreated", item.페이지Id, context, cancellationToken);
            return Result.Ok(item);
        }
        catch (Exception ex)
        {
            return Result.Fail<판매페이지초안응답>(ex.Message);
        }
    }

    public async Task<Result<판매페이지초안응답>> 초안수정Async(
        string pageId,
        판매페이지초안수정요청 request,
        판매채널요청Context context,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _service.초안수정Async(pageId, request, context.UserId, cancellationToken);
            await LogAsync("SalesPage", "DraftUpdated", item.페이지Id, context, cancellationToken);
            return Result.Ok(item);
        }
        catch (Exception ex)
        {
            return Result.Fail<판매페이지초안응답>(ex.Message);
        }
    }

    private Task LogAsync(
        string actionType,
        string actionName,
        string pageId,
        판매채널요청Context context,
        CancellationToken cancellationToken)
        => _activityLogService.기록Async(new 사용자행위로그기록
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
            MetadataJson = $"{{\"pageId\":\"{pageId}\"}}"
        }, cancellationToken);
}
