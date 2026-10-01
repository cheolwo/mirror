using FluentResults;
using Hongdal.Application.CommandProcessing;
using 홍달.Services.Dispatch.Coordination;
using 홍달.Services.Dispatch.Notification;
using 홍달.Services.Dispatch.Queue;
using 홍달.Services.Dispatch.Recommendation;
using Hongdal.Services.Community;
using Hongdal.Services.External.Naver;

namespace Hongdal.Application.Driver.Work;

public sealed class 위치갱신CommandHandler : IRequestHandler<위치갱신Command, Result<기사위치갱신응답>>
{
    private readonly HongdalContext _db;
    private readonly IDriverLocationStore _driverLocationStore;
    private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
    private readonly I배달권실행공간Store _배달권실행공간Store;
    private readonly I상차접근알림Service _상차접근알림Service;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ICommunityDriverAvailabilityService _communityDriverAvailabilityService;
    private readonly INaverMapsReverseGeocodingService _reverseGeocodingService;
    private readonly ILogger<위치갱신CommandHandler> _logger;

    public 위치갱신CommandHandler(
        HongdalContext db,
        IDriverLocationStore driverLocationStore,
        I국내화물운송기사상태Service 국내화물운송기사상태Service,
        I배달권실행공간Store 배달권실행공간Store,
        I상차접근알림Service 상차접근알림Service,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ICommunityDriverAvailabilityService communityDriverAvailabilityService,
        INaverMapsReverseGeocodingService reverseGeocodingService,
        ILogger<위치갱신CommandHandler> logger)
    {
        _db = db;
        _driverLocationStore = driverLocationStore;
        _국내화물운송기사상태Service = 국내화물운송기사상태Service;
        _배달권실행공간Store = 배달권실행공간Store;
        _상차접근알림Service = 상차접근알림Service;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _communityDriverAvailabilityService = communityDriverAvailabilityService;
        _reverseGeocodingService = reverseGeocodingService;
        _logger = logger;
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

        var driver = await _db.용달기사.FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken);
        if (driver is null)
        {
            return Result.Fail<기사위치갱신응답>("용달기사 정보를 찾을 수 없습니다.");
        }

        var status = string.IsNullOrWhiteSpace(request.운행상태)
            ? driver.운행상태
            : request.운행상태.Trim();
        if (!string.Equals(status, 상태값.기사운행상태.운행중, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail<기사위치갱신응답>("운행중 상태에서만 위치를 전송할 수 있습니다.");
        }

        driver.운행상태 = 상태값.기사운행상태.운행중;
        driver.UpdatedAt = DateTime.UtcNow;

        var recordedAt = request.기록시각 ?? DateTime.UtcNow;
        var receivedAt = DateTime.UtcNow;
        var snapshot = new DriverLocationSnapshot(
            request.기사Id,
            request.위도.Value,
            request.경도.Value,
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
            상차접근허용반경Km: request.상차접근허용반경Km,
            appKey: request.AppKey,
            cancellationToken: cancellationToken);
        var 배달권 = 국내화물배달권정책.판정(
            new 배차경로좌표(snapshot.Latitude, snapshot.Longitude),
            driver.주_활동지역);
        await _배달권실행공간Store.Upsert기사Async(
            배달권.배달권키,
            request.기사Id,
            국내행정구역배달권Catalog.인접배달권키조회(배달권.배달권키),
            cancellationToken);

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
            권장위치전송간격초 = 300,
            커뮤니티현재공개지역 = communityDistrictLabel
        });
    }
}
