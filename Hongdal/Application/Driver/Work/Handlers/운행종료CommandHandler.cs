namespace Hongdal.Application.Driver.Work;

using Hongdal.Application.CommandProcessing;
using Microsoft.Extensions.Logging;
using 홍달.Services.Dispatch.Coordination;
using 홍달.Services.Dispatch.Queue;
using Hongdal.Services.Community;

public sealed class 운행종료CommandHandler : IRequestHandler<운행종료Command, Unit>
{
    private readonly HongdalContext _db;
    private readonly IDriverWorkQueueStore _driverWorkQueueStore;
    private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
    private readonly I배달권실행공간Store _배달권실행공간Store;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ICommunityDriverAvailabilityService _communityDriverAvailabilityService;
    private readonly ILogger<운행종료CommandHandler> _logger;

    public 운행종료CommandHandler(
        HongdalContext db,
        IDriverWorkQueueStore driverWorkQueueStore,
        I국내화물운송기사상태Service 국내화물운송기사상태Service,
        I배달권실행공간Store 배달권실행공간Store,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ICommunityDriverAvailabilityService communityDriverAvailabilityService,
        ILogger<운행종료CommandHandler> logger)
    {
        _db = db;
        _driverWorkQueueStore = driverWorkQueueStore;
        _국내화물운송기사상태Service = 국내화물운송기사상태Service;
        _배달권실행공간Store = 배달권실행공간Store;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _communityDriverAvailabilityService = communityDriverAvailabilityService;
        _logger = logger;
    }

    public async Task<Unit> Handle(운행종료Command request, CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            throw new InvalidOperationException(권한오류);
        }

        var driver = await _db.용달기사.FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken)
            ?? throw new InvalidOperationException("용달기사 정보를 찾을 수 없습니다.");

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        driver.운행상태 = 상태값.기사운행상태.대기;
        driver.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await _driverWorkQueueStore.RemoveAsync(request.기사Id, cancellationToken);
        await _국내화물운송기사상태Service.운행종료Async(request.기사Id, cancellationToken);
        await _배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        _communityDriverAvailabilityService.Close(request.기사Id);

        _logger.LogInformation(
            "Action={Action} DriverId={DriverId} BeforeStatus={BeforeStatus} AfterStatus={AfterStatus} Result={Result} TraceId={TraceId} OccurredAt={OccurredAt}",
            "DriverWorkStopped",
            request.기사Id,
            상태값.기사운행상태.운행중,
            driver.운행상태,
            "Success",
            System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
            DateTime.UtcNow);

        return Unit.Value;
    }
}
