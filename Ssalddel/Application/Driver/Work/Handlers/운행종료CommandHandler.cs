using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Application.Driver.Work;

using Ssalddel.Application.CommandProcessing;
using Microsoft.Extensions.Logging;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using Ssalddel.Services.Community;
using Microsoft.EntityFrameworkCore;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "권한 검증 후 SQL 근무를 종료하고 실행 인덱스를 정리한다.",
    ContractType = typeof(운행종료Command),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation,
    FlowOrder = 80,
    StepKey = "application.work-stop",
    DependsOnStepKeys = ["domain.driver-shift", "application.work-transition-lock"],
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "시작·GPS·후속과 같은 기사 잠금을 사용한다. 인덱스 정리 실패는 로그에 남기고 SQL 종료를 보존하며 기존 명시적 수신 OFF와 업무 완료를 합치지 않는다.")]
public sealed class 운행종료CommandHandler : IRequestHandler<운행종료Command, Unit>
{
    private readonly SsalddelContext _db;
    private readonly IDriverWorkQueueStore _driverWorkQueueStore;
    private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
    private readonly I음식배달권실행공간Store _음식배달권실행공간Store;
    private readonly I국내화물배달권실행공간Store _국내화물배달권실행공간Store;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ICommunityDriverAvailabilityService _communityDriverAvailabilityService;
    private readonly ILogger<운행종료CommandHandler> _logger;

    public 운행종료CommandHandler(
        SsalddelContext db,
        IDriverWorkQueueStore driverWorkQueueStore,
        I국내화물운송기사상태Service 국내화물운송기사상태Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        I국내화물배달권실행공간Store 국내화물배달권실행공간Store,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ICommunityDriverAvailabilityService communityDriverAvailabilityService,
        ILogger<운행종료CommandHandler> logger)
    {
        _db = db;
        _driverWorkQueueStore = driverWorkQueueStore;
        _국내화물운송기사상태Service = 국내화물운송기사상태Service;
        _음식배달권실행공간Store = 음식배달권실행공간Store;
        _국내화물배달권실행공간Store = 국내화물배달권실행공간Store;
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

        using var gate = await 기사운행전이Lock.진입Async(request.기사Id, cancellationToken);
        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
            await 기사운행전이Lock.행잠금Async(_db, request.기사Id, cancellationToken);
            var driver = await _db.용달기사.FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken)
                ?? throw new InvalidOperationException("용달기사 정보를 찾을 수 없습니다.");
            await _db.Entry(driver).ReloadAsync(cancellationToken);
            var now = DateTime.UtcNow;
            driver.운행상태 = 상태값.기사운행상태.대기;
            driver.UpdatedAt = now;
            var shifts = await _db.기사근무.Where(x => x.기사Id == request.기사Id && x.StoppedAtUtc == null)
                .ToArrayAsync(cancellationToken);
            foreach (var shift in shifts)
            {
                shift.ActiveDriverId = null;
                shift.StoppedAtUtc = now;
                shift.UpdatedAt = now;
            }
            await _db.SaveChangesAsync(cancellationToken);
            // 후속 시작 투영과 종료가 같은 SQL 행 잠금을 유지하는 동안 인덱스를 정리합니다.
            try
            {
            await _driverWorkQueueStore.RemoveAsync(request.기사Id, cancellationToken);
            await _국내화물운송기사상태Service.운행종료Async(request.기사Id, cancellationToken);
            await _음식배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
            await _국내화물배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
            _communityDriverAvailabilityService.Close(request.기사Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { _logger.LogWarning(ex, "운행 종료 원장은 저장하며 인덱스 정리 실패를 기록합니다. DriverId={DriverId}", request.기사Id); }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Action={Action} DriverId={DriverId} BeforeStatus={BeforeStatus} AfterStatus={AfterStatus} Result={Result} TraceId={TraceId} OccurredAt={OccurredAt}",
                "DriverWorkStopped",
                request.기사Id,
                상태값.기사운행상태.운행중,
                driver.운행상태,
                "Success",
                System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
                DateTime.UtcNow);
        });

        return Unit.Value;
    }
}
