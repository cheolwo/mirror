using Ssalddel.Contracts.Common.Metadata;

using FluentResults;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Application.CommandProcessing;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Notification;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using Ssalddel.Services.Community;
using Ssalddel.Services.External.Naver;
using 살뜰.Services.Transport;
using Microsoft.Extensions.Options;
using Ssalddel.Application.Common.Transport;

namespace Ssalddel.Application.Driver.Work;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "현재 근무·앱 소유권을 검증해 GPS 투영과 접근·위치 후속을 갱신한다.",
    ContractType = typeof(위치갱신Command),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.ThirdPartyApiCall | SsalddelCodeEffect.MayIncurExternalCost,
    FlowOrder = 85,
    StepKey = "application.work-location",
    DependsOnStepKeys = ["application.work-transition-lock", "contract.work-status"],
    ExecutionStage = SsalddelCodeExecutionStage.Projection,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "종료와 같은 기사 잠금으로 늦은 GPS의 운행 부활을 막는다. 외부 역지오코딩은 구 단위 공개 동의가 있을 때만 호출하며 고객 경로나 배달 완료를 확정하지 않는다.")]
public sealed class 위치갱신CommandHandler : IRequestHandler<위치갱신Command, Result<기사위치갱신응답>>
{
    private readonly SsalddelContext _db;
    private readonly IDriverLocationStore _driverLocationStore;
    private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
    private readonly I음식배달권실행공간Store _음식배달권실행공간Store;
    private readonly I국내화물배달권실행공간Store _국내화물배달권실행공간Store;
    private readonly I상차접근알림Service _상차접근알림Service;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ICommunityDriverAvailabilityService _communityDriverAvailabilityService;
    private readonly INaverMapsReverseGeocodingService _reverseGeocodingService;
    private readonly ITransportRequestLedgerRealtimeService _transportLedgerRealtimeService;
    private readonly ILogger<위치갱신CommandHandler> _logger;
    private readonly IOptions<배차큐정책Options>? _위치정책;

    public 위치갱신CommandHandler(
        SsalddelContext db,
        IDriverLocationStore driverLocationStore,
        I국내화물운송기사상태Service 국내화물운송기사상태Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        I국내화물배달권실행공간Store 국내화물배달권실행공간Store,
        I상차접근알림Service 상차접근알림Service,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ICommunityDriverAvailabilityService communityDriverAvailabilityService,
        INaverMapsReverseGeocodingService reverseGeocodingService,
        ITransportRequestLedgerRealtimeService transportLedgerRealtimeService,
        ILogger<위치갱신CommandHandler> logger,
        IOptions<배차큐정책Options>? 위치정책 = null)
    {
        _db = db;
        _driverLocationStore = driverLocationStore;
        _국내화물운송기사상태Service = 국내화물운송기사상태Service;
        _음식배달권실행공간Store = 음식배달권실행공간Store;
        _국내화물배달권실행공간Store = 국내화물배달권실행공간Store;
        _상차접근알림Service = 상차접근알림Service;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _communityDriverAvailabilityService = communityDriverAvailabilityService;
        _reverseGeocodingService = reverseGeocodingService;
        _transportLedgerRealtimeService = transportLedgerRealtimeService;
        _logger = logger;
        _위치정책 = 위치정책;
    }

    public async Task<Result<기사위치갱신응답>> Handle(위치갱신Command request, CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            return Result.Fail<기사위치갱신응답>(권한오류);
        }

        if (!request.위도.HasValue || !request.경도.HasValue)
        {
            return Result.Fail<기사위치갱신응답>("위도와 경도가 필요합니다.");
        }
        var now = DateTime.UtcNow;
        if (request.위도 is < -90 or > 90 || request.경도 is < -180 or > 180
            || (request.위도 == 0 && request.경도 == 0) || request.정확도_m is < 0)
            return Result.Fail<기사위치갱신응답>("유효한 GPS 좌표와 정확도를 확인해 주세요.");
        if (request.기록시각 is { } recorded
            && (현재배정기사위치조회.Utc(recorded) > now || now - 현재배정기사위치조회.Utc(recorded) >= TimeSpan.FromMinutes(10)))
            return Result.Fail<기사위치갱신응답>("현재 GPS를 다시 확인해 주세요. 미래이거나 오래된 위치는 전송할 수 없습니다.");

        var applied = await ApplyCurrentWorkLocationAsync(request, cancellationToken);
        if (applied.IsFailed)
        {
            return Result.Fail<기사위치갱신응답>(applied.Errors);
        }

        var (snapshot, osState) = applied.Value;
        await PublishActiveTransportLocationAsync(request.기사Id, cancellationToken);

        try
        {
            await _상차접근알림Service.상차지접근알림검사Async(snapshot, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "상차지 접근 알림 검사 중 예외가 발생했습니다. DriverId={DriverId}", request.기사Id);
        }

        string? communityDistrictLabel = null;
        if (_communityDriverAvailabilityService.HasDistrictLocationConsent(request.기사Id))
        {
            try
            {
                var region = await _reverseGeocodingService.ResolveDistrictAsync(
                    snapshot.Latitude,
                    snapshot.Longitude,
                    cancellationToken);
                if (region is not null
                    && !string.IsNullOrWhiteSpace(region.SidoName)
                    && !string.IsNullOrWhiteSpace(region.SigunguName))
                {
                    var publicPost = _communityDriverAvailabilityService.UpdateDistrictLocation(
                        request.기사Id,
                        region.SidoName,
                        region.SigunguName);
                    communityDistrictLabel = publicPost?.CurrentDistrictLabel;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "기사 커뮤니티 구 단위 위치 변환 실패. DriverId={DriverId}", request.기사Id);
            }
        }

        return Result.Ok(new 기사위치갱신응답
        {
            DriverId = request.기사Id,
            Status = 상태값.기사운행상태.운행중,
            현재위도 = osState.Latitude,
            현재경도 = osState.Longitude,
            최근위치수신시각 = osState.위치수신시각Utc,
            Aging점수 = osState.Aging점수,
            Aging기준시각 = osState.Aging기준시각Utc,
            상차접근허용반경Km = osState.상차접근허용반경Km,
            권장위치전송간격초 = Math.Max(1, _위치정책?.Value.기사위치전송간격초 ?? 30),
            커뮤니티현재공개지역 = communityDistrictLabel
        });
    }

    private async Task<Result<LocationProjection>> ApplyCurrentWorkLocationAsync(
        위치갱신Command request, CancellationToken cancellationToken)
    {
        // 시작·종료·후속 투영과 같은 잠금을 사용해, 늦은 GPS가 종료된 인덱스를 되살리지 않게 합니다.
        using var gate = await 기사운행전이Lock.진입Async(request.기사Id, cancellationToken);
        return await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
            await 기사운행전이Lock.행잠금Async(_db, request.기사Id, cancellationToken);
            var driver = await _db.용달기사.FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken);
            if (driver is null)
            {
                return Result.Fail<LocationProjection>("용달기사 정보를 찾을 수 없습니다.");
            }
            await _db.Entry(driver).ReloadAsync(cancellationToken);

            var status = string.IsNullOrWhiteSpace(request.운행상태)
                ? driver.운행상태
                : request.운행상태.Trim();
            if (!string.Equals(driver.운행상태, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(status, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail<LocationProjection>("운행중 상태에서만 위치를 전송할 수 있습니다.");
            }

            var currentShift = await _db.기사근무
                .AsNoTracking()
                .Where(x => x.기사Id == request.기사Id)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var shiftAppKey = ResolveShiftAppKey(currentShift?.운송실행유형);
            if (!string.IsNullOrWhiteSpace(shiftAppKey)
                && !string.IsNullOrWhiteSpace(request.AppKey)
                && !string.Equals(shiftAppKey, request.AppKey, StringComparison.Ordinal))
            {
                return Result.Fail<LocationProjection>(
                    "현재 근무의 운송 실행유형과 위치를 전송한 기사 앱이 일치하지 않습니다.");
            }

            var recordedAt = request.기록시각.HasValue ? 현재배정기사위치조회.Utc(request.기록시각.Value) : DateTime.UtcNow;
            var receivedAt = DateTime.UtcNow;
            if (currentShift?.StoppedAtUtc is not null
                || (currentShift?.시작시각 is { } startedAt && recordedAt < 현재배정기사위치조회.Utc(startedAt)))
                return Result.Fail<LocationProjection>("현재 근무를 시작한 뒤의 GPS를 다시 확인해 주세요.");
            var latest = await _db.기사위치기록.AsNoTracking().Where(x => x.기사Id == request.기사Id)
                .OrderByDescending(x => x.기록시각).ThenByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
            if (recordedAt > receivedAt || receivedAt - recordedAt >= TimeSpan.FromMinutes(10)
                || (latest is not null && recordedAt <= 현재배정기사위치조회.Utc(latest.기록시각)))
                return Result.Fail<LocationProjection>("새 GPS를 다시 확인해 주세요. 이전 위치를 최신 위치로 덮어쓰지 않습니다.");
            // 위치 보고는 운행 시작 명령이 아니다. 동시 종료나 검증 실패로 운행을 되살리지 않는다.
            driver.UpdatedAt = receivedAt;
            var snapshot = new DriverLocationSnapshot(
                request.기사Id,
                request.위도!.Value,
                request.경도!.Value,
                request.정확도_m,
                상태값.기사운행상태.운행중,
                recordedAt,
                receivedAt);

            _driverLocationStore.Upsert(snapshot);
            _db.기사위치기록.Add(new 기사위치기록
            {
                기사Id = snapshot.DriverId,
                위도 = snapshot.Latitude,
                경도 = snapshot.Longitude,
                정확도_m = snapshot.AccuracyM,
                기록시각 = snapshot.RecordedAtUtc,
                CreatedAt = snapshot.ReceivedAtUtc,
                UpdatedAt = snapshot.ReceivedAtUtc
            });

            await _db.SaveChangesAsync(cancellationToken);
            var osState = await _국내화물운송기사상태Service.위치갱신Async(
                snapshot,
                shiftId: currentShift?.Id,
                상차접근허용반경Km: request.상차접근허용반경Km,
                appKey: shiftAppKey ?? request.AppKey,
                cancellationToken: cancellationToken);
            var 음식배달기사 = string.Equals(
                osState.AppKey,
                기사앱식별자.FoodDeliveryDriverApp,
                StringComparison.Ordinal);
            var 현재좌표 = new 배차경로좌표(snapshot.Latitude, snapshot.Longitude);
            var 배달권 = 음식배달기사
                ? 음식배달권정책.판정(현재좌표, null)
                : 국내화물배달권정책.판정(현재좌표, driver.주_활동지역);
            if (음식배달기사)
            {
                await _국내화물배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
                await _음식배달권실행공간Store.Upsert기사Async(
                    배달권.배달권키,
                    request.기사Id,
                    음식배달권정책.인접배달권키조회(배달권.배달권키),
                    cancellationToken);
            }
            else
            {
                await _음식배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
                await _국내화물배달권실행공간Store.Upsert기사Async(
                    배달권.배달권키,
                    request.기사Id,
                    국내행정구역배달권Catalog.인접배달권키조회(배달권.배달권키),
                    cancellationToken);
            }

            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result.Ok(new LocationProjection(snapshot, osState));
        });
    }

    private sealed record LocationProjection(
        DriverLocationSnapshot Snapshot, 국내화물운송기사상태Snapshot State);

    private static string? ResolveShiftAppKey(string? 운송실행유형)
        => 운송실행유형 switch
        {
            운송실행유형코드.음식배달 => 기사앱식별자.FoodDeliveryDriverApp,
            운송실행유형코드.화물운송 => 기사앱식별자.CargoYongdalDriverApp,
            _ => null
        };

    private async Task PublishActiveTransportLocationAsync(
        string driverId,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestIds = await _db.운송원장
                .AsNoTracking()
                .Where(x => x.확정기사Id == driverId
                            && x.상태 != 상태값.배차상태.인수완료
                            && x.상태 != 상태값.배차상태.하차완료
                            && x.상태 != 상태값.배차상태.취소)
                .Select(x => x.의뢰Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var requestId in requestIds)
            {
                await _transportLedgerRealtimeService.PublishAsync(
                    requestId,
                    "DriverLocationUpdated",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "기사 위치 기반 화주 실시간 갱신 알림에 실패했습니다. DriverId={DriverId}",
                driverId);
        }
    }
}
