using Ssalddel.Contracts.Common.Metadata;

namespace Ssalddel.Application.Driver.Work;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "SQL 운행·최신 근무 소유권과 위치 인덱스를 함께 재조회한다.",
    ContractType = typeof(운행상태조회Query),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.NetworkCall,
    FlowOrder = 75,
    StepKey = "application.work-status",
    DependsOnStepKeys = ["contract.work-status", "domain.driver-shift"],
    ExecutionStage = SsalddelCodeExecutionStage.Query,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.None,
    Boundary = "운행 상태·시작 요청 ID·실행유형의 권위는 SQL이며 위치·Aging은 Redis 또는 설정된 인덱스 사본이다. 인덱스로 SQL 운행을 복원하지 않는다.")]
public sealed class 운행상태조회QueryHandler : IRequestHandler<운행상태조회Query, 기사운행상태응답>
{
    private readonly SsalddelContext _db;
    private readonly I국내화물운송기사상태Store _국내화물운송기사상태Store;

    public 운행상태조회QueryHandler(
        SsalddelContext db,
        I국내화물운송기사상태Store 국내화물운송기사상태Store)
    {
        _db = db;
        _국내화물운송기사상태Store = 국내화물운송기사상태Store;
    }

    public async Task<기사운행상태응답> Handle(운행상태조회Query request, CancellationToken cancellationToken)
    {
        var driver = await _db.용달기사.AsNoTracking().FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken)
            ?? throw new InvalidOperationException("용달기사 정보를 찾을 수 없습니다.");
        var shift = await _db.기사근무.AsNoTracking().Where(x => x.기사Id == request.기사Id)
            .OrderByDescending(x => x.Id)
            .Select(x => new { x.Id, x.WorkStartClientRequestId, x.운송실행유형 })
            .FirstOrDefaultAsync(cancellationToken);
        var osState = await _국내화물운송기사상태Store.GetAsync(request.기사Id, cancellationToken);

        return new 기사운행상태응답
        {
            DriverId = request.기사Id,
            Status = driver.운행상태,
            ShiftId = shift?.Id,
            WorkStartClientRequestId = shift?.WorkStartClientRequestId,
            운송실행유형 = shift?.운송실행유형,
            UpdatedAt = driver.UpdatedAt,
            현재위도 = osState?.Latitude,
            현재경도 = osState?.Longitude,
            최근위치수신시각 = osState?.위치수신시각Utc,
            Aging점수 = osState?.Aging점수,
            Aging기준시각 = osState?.Aging기준시각Utc,
            복귀콜선호 = osState?.복귀콜선호
        };
    }
}
