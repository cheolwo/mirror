using Hongdal.Contracts.Driver.Work;
using Hongdal.Contracts.Common.Community;
using FluentResults;
using Microsoft.Extensions.Logging;
using Hongdal.Application.CommandProcessing;
using 홍달.Services.Dispatch.Coordination;
using 홍달.Services.Dispatch.Queue;
using Hongdal.Services.Community;

namespace Hongdal.Application.Driver.Work;

public sealed class 운행시작CommandHandler : IRequestHandler<운행시작Command, Result<기사운행시작응답>>
{
    private readonly HongdalContext _db;
    private readonly I배차추천Service _dispatchRecommendationService;
    private readonly IDriverWorkQueueStore _driverWorkQueueStore;
    private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ICommunityDriverAvailabilityService _communityDriverAvailabilityService;
    private readonly ILogger<운행시작CommandHandler> _logger;

    public 운행시작CommandHandler(
        HongdalContext db,
        I배차추천Service dispatchRecommendationService,
        IDriverWorkQueueStore driverWorkQueueStore,
        I국내화물운송기사상태Service 국내화물운송기사상태Service,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ICommunityDriverAvailabilityService communityDriverAvailabilityService,
        ILogger<운행시작CommandHandler> logger)
    {
        _db = db;
        _dispatchRecommendationService = dispatchRecommendationService;
        _driverWorkQueueStore = driverWorkQueueStore;
        _국내화물운송기사상태Service = 국내화물운송기사상태Service;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _communityDriverAvailabilityService = communityDriverAvailabilityService;
        _logger = logger;
    }

    public async Task<Result<기사운행시작응답>> Handle(운행시작Command request, CancellationToken cancellationToken)
    {
        if (!_권한검사.Try검증(_currentUserAccessor.UserId, _currentUserAccessor.Role, request.참여자Id, request.실행역할, out var 권한오류))
        {
            return Result.Fail<기사운행시작응답>(권한오류);
        }

        if (string.IsNullOrWhiteSpace(request.시작모드))
        {
            return Result.Fail<기사운행시작응답>("시작모드가 필요합니다.");
        }

        if (string.IsNullOrWhiteSpace(request.시작위치))
        {
            return Result.Fail<기사운행시작응답>("시작위치가 필요합니다.");
        }

        var driver = await _db.용달기사.FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken);
        if (driver is null)
        {
            return Result.Fail<기사운행시작응답>("용달기사 정보를 찾을 수 없습니다.");
        }

        var shift = new 기사근무
        {
            기사Id = request.기사Id,
            시작모드 = request.시작모드,
            시작시각 = request.시작시각 ?? DateTime.UtcNow,
            시작위치 = request.시작위치,
            복귀지 = request.복귀지,
            오늘의복귀지주소 = ResolveTodayReturnAddress(request, driver),
            오늘의복귀지위도 = ResolveTodayReturnLatitude(request, driver),
            오늘의복귀지경도 = ResolveTodayReturnLongitude(request, driver),
            복귀지출처 = ResolveReturnSource(request, driver),
            복귀지입력일시 = ResolveTodayReturnAddress(request, driver) is null ? null : DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        driver.운행상태 = 상태값.기사운행상태.운행중;
        driver.UpdatedAt = DateTime.UtcNow;
        _db.기사근무.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);
        await _driverWorkQueueStore.UpsertAsync(new DriverWorkQueueEntry(
            request.기사Id,
            shift.Id,
            shift.CreatedAt,
            shift.시작모드,
            shift.시작위치,
            shift.오늘의복귀지주소 ?? shift.복귀지), cancellationToken);
        await _국내화물운송기사상태Service.운행시작Async(
            request.기사Id,
            shift.Id,
            shift.시작시각 ?? DateTime.UtcNow,
            shift.시작모드,
            shift.시작위치,
            shift.오늘의복귀지주소 ?? shift.복귀지,
            기사복귀선호코드.Normalize(request.복귀콜선호),
            cancellationToken);
        await tx.CommitAsync(cancellationToken);

        CommunityDriverAvailabilityPostResponse? communityPost = null;
        if (request.커뮤니티운행공개)
        {
            try
            {
                communityPost = _communityDriverAvailabilityService.Publish(
                    new CommunityDriverAvailabilityPublishRequest(
                        request.기사Id,
                        shift.Id,
                        driver.기사명,
                        driver.차량,
                        driver.주_활동지역,
                        new DateTimeOffset(DateTime.SpecifyKind(shift.시작시각 ?? DateTime.UtcNow, DateTimeKind.Utc)),
                        request.커뮤니티구단위위치공개동의));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "기사 운행 시작 후 커뮤니티 운행 공개 글 생성 실패. DriverId={DriverId}", request.기사Id);
            }
        }
        else
        {
            _communityDriverAvailabilityService.Close(request.기사Id);
        }

        await _dispatchRecommendationService.SendToDriverAsync(request.기사Id);

        _logger.LogInformation(
            "Action={Action} DriverId={DriverId} BeforeStatus={BeforeStatus} AfterStatus={AfterStatus} Result={Result} TraceId={TraceId} OccurredAt={OccurredAt}",
            "DriverWorkStarted",
            request.기사Id,
            상태값.기사운행상태.대기,
            driver.운행상태,
            "Success",
            System.Diagnostics.Activity.Current?.TraceId.ToString() ?? string.Empty,
            DateTime.UtcNow);

        return Result.Ok(new 기사운행시작응답
        {
            DriverId = request.기사Id,
            Status = driver.운행상태,
            ShiftId = shift.Id,
            StartedAt = shift.시작시각,
            적용복귀지 = shift.오늘의복귀지주소 ?? shift.복귀지,
            복귀지출처 = shift.복귀지출처,
            복귀콜선호 = 기사복귀선호코드.Normalize(request.복귀콜선호),
            커뮤니티운행공개됨 = communityPost is not null,
            커뮤니티운행공개글Id = communityPost?.PostId,
            커뮤니티구단위위치공개동의됨 = communityPost?.DistrictLocationConsentGranted == true,
            커뮤니티공개안내 = communityPost is not null
                ? communityPost.DistrictLocationConsentGranted
                    ? "연락처와 좌표를 제외한 운행 중 글이 공개됐습니다. 현재 위치는 시·도와 시·군·구까지만 표시됩니다."
                    : "정확한 위치와 연락처를 제외한 운행 중 글이 커뮤니티에 공개됐습니다."
                : request.커뮤니티운행공개
                    ? "운행은 시작됐지만 커뮤니티 공개 글은 생성하지 못했습니다."
                    : "커뮤니티 운행 공개를 사용하지 않았습니다."
        });
    }

    private static string? ResolveTodayReturnAddress(운행시작Command request, 용달기사 driver)
    {
        if (!string.IsNullOrWhiteSpace(request.오늘의복귀지주소))
        {
            return request.오늘의복귀지주소.Trim();
        }

        if (request.기본복귀지사용 && !string.IsNullOrWhiteSpace(driver.기본복귀지주소))
        {
            return driver.기본복귀지주소;
        }

        return request.복귀지;
    }

    private static decimal? ResolveTodayReturnLatitude(운행시작Command request, 용달기사 driver)
    {
        if (request.오늘의복귀지위도.HasValue)
        {
            return request.오늘의복귀지위도.Value;
        }

        return request.기본복귀지사용 ? driver.기본복귀지위도 : null;
    }

    private static decimal? ResolveTodayReturnLongitude(운행시작Command request, 용달기사 driver)
    {
        if (request.오늘의복귀지경도.HasValue)
        {
            return request.오늘의복귀지경도.Value;
        }

        return request.기본복귀지사용 ? driver.기본복귀지경도 : null;
    }

    private static string ResolveReturnSource(운행시작Command request, 용달기사 driver)
    {
        if (!string.IsNullOrWhiteSpace(request.복귀지출처))
        {
            return request.복귀지출처;
        }

        if (!string.IsNullOrWhiteSpace(request.오늘의복귀지주소))
        {
            return "오늘입력";
        }

        if (request.기본복귀지사용 && !string.IsNullOrWhiteSpace(driver.기본복귀지주소))
        {
            return "기본복귀지";
        }

        return string.IsNullOrWhiteSpace(request.복귀지) ? string.Empty : "직접입력";
    }
}
