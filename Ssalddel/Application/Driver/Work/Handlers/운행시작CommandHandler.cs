using Ssalddel.Contracts.Common.Metadata;

using Ssalddel.Contracts.Driver.Work;
using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Transport;
using FluentResults;
using Microsoft.Extensions.Logging;
using Ssalddel.Application.CommandProcessing;
using 살뜰.Services.Dispatch.Coordination;
using 살뜰.Services.Dispatch.Queue;
using Ssalddel.Services.Community;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ssalddel.Services.Outbox;

namespace Ssalddel.Application.Driver.Work;

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.FoodDriverWork,
    SsalddelCodeLayer.Application,
    "권한과 요청 소유권을 검증해 근무와 음식 운행 시작 후속 의도를 함께 확정한다.",
    ContractType = typeof(운행시작Command),
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite | SsalddelCodeEffect.NetworkCall | SsalddelCodeEffect.StateMutation | SsalddelCodeEffect.ThirdPartyApiCall | SsalddelCodeEffect.MayIncurExternalCost,
    FlowOrder = 70,
    StepKey = "application.work-start",
    DependsOnStepKeys = ["contract.work-start-request", "contract.work-start-response", "domain.work-start-followup", "application.work-transition-lock"],
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "동일 요청·동일 입력은 같은 SQL 근무를 반환한다. 음식 시작의 SQL commit과 인덱스 후속 성공은 별도다. 같은 Handler의 화물 분기는 기존 즉시 후속·추천을 통해 외부 경로 API와 푸시를 호출할 수 있으며 음식 시작에는 그 추천 호출을 적용하지 않는다.")]
public sealed class 운행시작CommandHandler : IRequestHandler<운행시작Command, Result<기사운행시작응답>>
{
    private readonly SsalddelContext _db;
    private readonly I배차추천Service _dispatchRecommendationService;
    private readonly IDriverWorkQueueStore _driverWorkQueueStore;
    private readonly I국내화물운송기사상태Service _국내화물운송기사상태Service;
    private readonly I음식배달권실행공간Store _음식배달권실행공간Store;
    private readonly I국내화물배달권실행공간Store _국내화물배달권실행공간Store;
    private readonly ICurrentUserAccessor _currentUserAccessor;
    private readonly I참여자실행권한검사 _권한검사;
    private readonly ICommunityDriverAvailabilityService _communityDriverAvailabilityService;
    private readonly ILogger<운행시작CommandHandler> _logger;
    private readonly I기사운행시작후속Service _후속;

    public 운행시작CommandHandler(
        SsalddelContext db,
        I배차추천Service dispatchRecommendationService,
        IDriverWorkQueueStore driverWorkQueueStore,
        I국내화물운송기사상태Service 국내화물운송기사상태Service,
        I음식배달권실행공간Store 음식배달권실행공간Store,
        I국내화물배달권실행공간Store 국내화물배달권실행공간Store,
        ICurrentUserAccessor currentUserAccessor,
        I참여자실행권한검사 권한검사,
        ICommunityDriverAvailabilityService communityDriverAvailabilityService,
        ILogger<운행시작CommandHandler> logger,
        I기사운행시작후속Service? 후속 = null)
    {
        _db = db;
        _dispatchRecommendationService = dispatchRecommendationService;
        _driverWorkQueueStore = driverWorkQueueStore;
        _국내화물운송기사상태Service = 국내화물운송기사상태Service;
        _음식배달권실행공간Store = 음식배달권실행공간Store;
        _국내화물배달권실행공간Store = 국내화물배달권실행공간Store;
        _currentUserAccessor = currentUserAccessor;
        _권한검사 = 권한검사;
        _communityDriverAvailabilityService = communityDriverAvailabilityService;
        _logger = logger;
        _후속 = 후속 ?? new 기사운행시작후속Service(db, driverWorkQueueStore, 국내화물운송기사상태Service,
            음식배달권실행공간Store, 국내화물배달권실행공간Store,
            NullLogger<기사운행시작후속Service>.Instance);
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
        if (request.ClientRequestId == Guid.Empty)
        {
            return 실패("운행 시작 요청 식별자를 확인해 주세요.", "DriverWork.InvalidRequestId", 400);
        }
        var fingerprint = 입력Fingerprint(request);
        var requestKey = request.ClientRequestId.HasValue
            ? Hash($"{request.기사Id}|{request.ClientRequestId.Value:N}") : null;
        Result<기사근무> saved;
        var newlyCreated = false;
        용달기사? canonicalDriver = null;
        // Start·Stop·후속 인덱스 재구성이 같은 기사 SQL 행을 잠급니다.
        using (await 기사운행전이Lock.진입Async(request.기사Id, cancellationToken))
        {
            saved = await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                foreach (var entry in _db.ChangeTracker.Entries<기사근무>().Where(x => x.Entity.기사Id == request.기사Id).ToArray())
                    entry.State = EntityState.Detached;
                foreach (var entry in _db.ChangeTracker.Entries<기사운행시작후속>().Where(x => x.Entity.DriverId == request.기사Id).ToArray())
                    entry.State = EntityState.Detached;
                await using var transaction = _db.Database.IsRelational()
                    ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
                await 기사운행전이Lock.행잠금Async(_db, request.기사Id, cancellationToken);
                var lockedDriver = await _db.용달기사.FirstOrDefaultAsync(x => x.기사Id == request.기사Id, cancellationToken);
                if (lockedDriver is null) return Result.Fail<기사근무>("용달기사 정보를 찾을 수 없습니다.");
                await _db.Entry(lockedDriver).ReloadAsync(cancellationToken);
                canonicalDriver = lockedDriver;
                if (requestKey is not null)
                {
                    var previous = await _db.기사근무.AsNoTracking().SingleOrDefaultAsync(
                        x => x.WorkStartRequestKey == requestKey, cancellationToken);
                    if (previous is not null)
                        return previous.WorkStartInputFingerprint == fingerprint
                            ? Result.Ok(previous)
                            : Result.Fail<기사근무>(new Error("동일 요청 식별자로 다른 운행 조건을 제출할 수 없습니다.")
                                .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "DriverWork.RequestConflict"));
                }
                if (lockedDriver.운행상태 == 상태값.기사운행상태.운행중)
                {
                    var current = await _db.기사근무.AsNoTracking().Where(x => x.기사Id == request.기사Id)
                        .OrderByDescending(x => x.Id).FirstOrDefaultAsync(cancellationToken);
                    if (request.ClientRequestId is null && current is not null && current.운송실행유형 == request.운송실행유형)
                        return Result.Ok(current); // 구형 호출도 중복 근무를 만들지 않습니다.
                    return Result.Fail<기사근무>(new Error("이미 운행 중입니다. 현재 근무를 확인해 주세요.")
                        .WithMetadata("StatusCode", 409).WithMetadata("ErrorCode", "DriverWork.AlreadyStarted"));
                }
                var now = DateTime.UtcNow;
                var staleSlots = await _db.기사근무.Where(x => x.ActiveDriverId == request.기사Id).ToArrayAsync(cancellationToken);
                foreach (var stale in staleSlots) { stale.ActiveDriverId = null; stale.StoppedAtUtc ??= now; }
                if (staleSlots.Length > 0) await _db.SaveChangesAsync(cancellationToken);
                var newShift = new 기사근무
                {
                    WorkStartClientRequestId = request.ClientRequestId,
                    WorkStartRequestKey = requestKey,
                    WorkStartInputFingerprint = fingerprint,
                    ActiveDriverId = request.기사Id,
                    StartReturnPreference = 기사복귀선호코드.Normalize(request.복귀콜선호),
                    기사Id = request.기사Id,
                    시작모드 = request.시작모드,
                    시작시각 = request.시작시각 ?? DateTime.UtcNow,
                    시작위치 = request.시작위치,
                    운송실행유형 = request.운송실행유형,
                    복귀지 = request.복귀지,
                    오늘의복귀지주소 = ResolveTodayReturnAddress(request, lockedDriver),
                    오늘의복귀지위도 = ResolveTodayReturnLatitude(request, lockedDriver),
                    오늘의복귀지경도 = ResolveTodayReturnLongitude(request, lockedDriver),
                    복귀지출처 = ResolveReturnSource(request, lockedDriver),
                    복귀지입력일시 = ResolveTodayReturnAddress(request, lockedDriver) is null ? null : DateTime.UtcNow,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                lockedDriver.운행상태 = 상태값.기사운행상태.운행중;
                lockedDriver.UpdatedAt = now;
                _db.기사근무.Add(newShift);
                await _db.SaveChangesAsync(cancellationToken);
                if (request.운송실행유형 == 운송실행유형코드.음식배달)
                {
                    _db.Set<기사운행시작후속>().Add(new 기사운행시작후속
                    { ShiftId = newShift.Id, DriverId = request.기사Id, CreatedAtUtc = now, Status = OutboxProcessingStatuses.Pending });
                    await _db.SaveChangesAsync(cancellationToken);
                }
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                newlyCreated = true;
                return Result.Ok(newShift);
            });
        }
        if (saved.IsFailed) return Result.Fail<기사운행시작응답>(saved.Errors);
        var shift = saved.Value;
        var driver = canonicalDriver!;
        var food = request.운송실행유형 == 운송실행유형코드.음식배달;
        var followupPending = false;
        if (food)
        {
            // 근무 저장 이후의 실패는 시작 실패로 반환하지 않습니다. 예약은 같은 SQL transaction에 있습니다.
            try { followupPending = !await _후속.처리Async(shift.Id, cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                followupPending = true;
                _logger.LogWarning(ex, "확정한 근무의 인덱스 확인은 후속 재처리로 이어집니다. ShiftId={ShiftId}", shift.Id);
            }
        }
        else if (newlyCreated && driver.운행상태 == 상태값.기사운행상태.운행중 && !shift.StoppedAtUtc.HasValue)
        {
            await _driverWorkQueueStore.UpsertAsync(new DriverWorkQueueEntry(
                request.기사Id,
                shift.Id,
                shift.CreatedAt,
                shift.시작모드,
                shift.시작위치,
                shift.오늘의복귀지주소 ?? shift.복귀지), cancellationToken);
            var appKey = string.Equals(
                request.운송실행유형,
                운송실행유형코드.음식배달,
                StringComparison.Ordinal)
                ? 기사앱식별자.FoodDeliveryDriverApp
                : 기사앱식별자.CargoYongdalDriverApp;
            await _국내화물운송기사상태Service.운행시작Async(
                request.기사Id,
                shift.Id,
                shift.시작시각 ?? DateTime.UtcNow,
                shift.시작모드,
                shift.시작위치,
                shift.오늘의복귀지주소 ?? shift.복귀지,
                기사복귀선호코드.Normalize(request.복귀콜선호),
                appKey,
                cancellationToken);
            await _음식배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
            await _국내화물배달권실행공간Store.Remove기사Async(request.기사Id, cancellationToken);
        }

        CommunityDriverAvailabilityPostResponse? communityPost = null;
        if (newlyCreated && !food && request.커뮤니티운행공개 && driver.운행상태 == 상태값.기사운행상태.운행중)
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
        else if (newlyCreated && !food)
        {
            _communityDriverAvailabilityService.Close(request.기사Id);
        }

        if (newlyCreated && driver.운행상태 == 상태값.기사운행상태.운행중 && !shift.StoppedAtUtc.HasValue && !string.Equals(
                request.운송실행유형,
                운송실행유형코드.음식배달,
                StringComparison.Ordinal))
        {
            await _dispatchRecommendationService.SendToDriverAsync(request.기사Id);
        }

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
            FollowupPending = followupPending,
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

    private static Result<기사운행시작응답> 실패(string message, string code, int status)
        => Result.Fail<기사운행시작응답>(new Error(message).WithMetadata("StatusCode", status).WithMetadata("ErrorCode", code));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string 입력Fingerprint(운행시작Command request) => Hash(JsonSerializer.Serialize(new
    {
        request.기사Id, 시작모드 = request.시작모드.Trim(), request.시작시각,
        시작위치 = request.시작위치.Trim(), request.복귀지, request.오늘의복귀지주소,
        request.오늘의복귀지위도, request.오늘의복귀지경도, request.기본복귀지사용,
        request.복귀지출처, 복귀콜선호 = 기사복귀선호코드.Normalize(request.복귀콜선호),
        request.커뮤니티운행공개, request.커뮤니티구단위위치공개동의, request.운송실행유형
    }));

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
