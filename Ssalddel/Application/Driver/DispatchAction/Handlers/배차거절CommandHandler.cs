using Ssalddel.Application.CommandProcessing;
using Microsoft.EntityFrameworkCore;
using 살뜰.도메인.공통;
using 살뜰.Services.Dispatch.Queue;

namespace Ssalddel.Application.Driver.DispatchAction;

public sealed class 배차거절CommandHandler : IRequestHandler<배차거절Command, FluentResults.Result<배차거절결과>>
{
    private readonly SsalddelContext _db;
    private readonly IDriverRejectedRequestStore _rejectedRequestStore;
    private readonly I배차대기원장전환Service _원장전환Service;
    private readonly IPublisher _publisher;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ILogger<배차거절CommandHandler> _logger;

    public 배차거절CommandHandler(
        SsalddelContext db,
        IDriverRejectedRequestStore rejectedRequestStore,
        I배차대기원장전환Service 원장전환Service,
        IPublisher publisher,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ILogger<배차거절CommandHandler> logger)
    {
        _db = db;
        _rejectedRequestStore = rejectedRequestStore;
        _원장전환Service = 원장전환Service;
        _publisher = publisher;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _logger = logger;
    }

    public async Task<FluentResults.Result<배차거절결과>> Handle(배차거절Command request, CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            return FluentResults.Result.Fail<배차거절결과>(권한오류);
        }

        if (string.IsNullOrWhiteSpace(request.기사Id))
        {
            return FluentResults.Result.Fail<배차거절결과>("기사 인증 정보가 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(request.RequestId))
        {
            return FluentResults.Result.Fail<배차거절결과>("의뢰Id는 필수입니다.");
        }

        var now = DateTime.UtcNow;
        var queue = await _db.운송원장
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.의뢰Id == request.RequestId, cancellationToken);
        if (queue is null)
        {
            return FluentResults.Result.Fail<배차거절결과>("배차대기 데이터를 찾을 수 없습니다.");
        }

        var canRejectRecommendation = 배차응답가능정책.추천거절가능(queue, request.기사Id, now);
        if (!canRejectRecommendation)
        {
            return FluentResults.Result.Fail<배차거절결과>("거절 가능한 추천 배차가 아닙니다.");
        }

        var reasonCode = 배차거절사유Policy.분류(request.사유);

        try
        {
            await _rejectedRequestStore.RejectAsync(request.기사Id, request.RequestId, cancellationToken);
            var 전환결과 = await _원장전환Service.추천거절처리Async(request.RequestId, request.기사Id, reasonCode, cancellationToken);
            if (!전환결과.전환여부)
            {
                _logger.LogWarning(
                    "배차거절 원장 전환이 적용되지 않았습니다. RequestId={RequestId} DriverId={DriverId} ResultCode={ResultCode} Message={Message}",
                    request.RequestId,
                    request.기사Id,
                    전환결과.결과코드,
                    전환결과.메시지);
                return FluentResults.Result.Fail<배차거절결과>("거절 가능한 추천 배차가 아닙니다.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차거절 필수 처리 중 예외가 발생했습니다. RequestId={RequestId} DriverId={DriverId}", request.RequestId, request.기사Id);
            return FluentResults.Result.Fail<배차거절결과>("거절 처리 중 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.");
        }

        try
        {
            await _publisher.Publish(
                new 배차거절됨Event(
                    request.기사Id,
                    request.RequestId,
                    reasonCode,
                    now,
                    System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "배차거절 사후처리 이벤트 발행 중 예외가 발생했습니다. RequestId={RequestId}", request.RequestId);
        }

        return FluentResults.Result.Ok(new 배차거절결과(request.RequestId, "거절되었습니다."));
    }
}
