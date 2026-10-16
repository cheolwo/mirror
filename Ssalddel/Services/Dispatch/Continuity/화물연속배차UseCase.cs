using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Hubs;
using 살뜰.Data;
using 살뜰.도메인.공통;
using 살뜰.도메인.설정;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;
using 살뜰.Services.Dispatch.Common;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Dispatch.Queue;

namespace 살뜰.Services.Dispatch.Continuity;

public interface I화물연속배차UseCase
{
    Task<화물연속배차상태Dto> 조회Async(string 기사Id, CancellationToken cancellationToken = default);
    Task<화물연속배차상태Dto> 의사변경Async(string 기사Id, 화물연속배차의사변경요청 요청, CancellationToken cancellationToken = default);
    Task<화물다음콜예약Dto?> 추천탐색Async(string 기사Id, CancellationToken cancellationToken = default);
    Task<화물예약검증결과> 수락예약검증Async(string 기사Id, string 의뢰Id, string? reservationId, long? expectedRevision, CancellationToken cancellationToken = default);
    Task 수락완료Async(string 기사Id, string 의뢰Id, CancellationToken cancellationToken = default);
    Task 시간약속잠금Async(string 기사Id, 화주운송의뢰 의뢰, CancellationToken cancellationToken = default);
    Task 완료기록Async(string 기사Id, DateTime 완료시각Utc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string 기사Id, CancellationToken cancellationToken = default);
    Task<화물경로위험Dto> 현재위험조회Async(string 기사Id, CancellationToken cancellationToken = default);
}

public sealed record 화물예약검증결과(bool 유효, string? 오류Code = null, string? 오류메시지 = null);

public sealed class 화물연속배차UseCase : I화물연속배차UseCase
{
    private readonly SsalddelContext _db;
    private readonly I운영배차공통UseCase _운영배차;
    private readonly I화물연속배차ProjectionStore _projection;
    private readonly I배차추천Service _추천Service;
    private readonly I배차추천경로Service _경로Service;
    private readonly IDriverLocationStore _위치Store;
    private readonly 화물연속배차Policy _policy;
    private readonly 화물연속배차Options _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<화물연속배차UseCase> _logger;
    private readonly I화물후속픽업기준Service _후속픽업;
    private readonly 배차큐정책Options _queueOptions;

    public 화물연속배차UseCase(
        SsalddelContext db,
        I운영배차공통UseCase 운영배차,
        I화물연속배차ProjectionStore projection,
        I배차추천Service 추천Service,
        I배차추천경로Service 경로Service,
        IDriverLocationStore 위치Store,
        화물연속배차Policy policy,
        IOptions<화물연속배차Options> options,
        TimeProvider timeProvider,
        ILogger<화물연속배차UseCase> logger,
        I화물후속픽업기준Service? 후속픽업 = null,
        IOptions<배차큐정책Options>? queueOptions = null)
    {
        _db = db;
        _운영배차 = 운영배차;
        _projection = projection;
        _추천Service = 추천Service;
        _경로Service = 경로Service;
        _위치Store = 위치Store;
        _policy = policy;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _후속픽업 = 후속픽업 ?? new 화물후속픽업기준Service(db, new 기사운송일정구성Service(db), new 운송일정삽입평가Service(경로Service));
        _queueOptions = queueOptions?.Value ?? new 배차큐정책Options();
    }

    public async Task<화물연속배차상태Dto> 조회Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        기사확인(기사Id);
        await 만료정리Async(기사Id, cancellationToken);
        var state = await _db.화물연속배차상태.AsNoTracking().SingleOrDefaultAsync(x => x.기사Id == 기사Id, cancellationToken);
        var reservation = await _db.화물다음콜예약.AsNoTracking()
            .Where(x => x.기사Id == 기사Id && x.상태Code == 화물연속배차예약상태Code.보유)
            .OrderByDescending(x => x.보유시각Utc)
            .FirstOrDefaultAsync(cancellationToken);
        var dto = new 화물연속배차상태Dto
        {
            기사Id = 기사Id,
            Enabled = state?.활성여부 ?? false,
            Revision = state?.Revision ?? 0,
            변경시각Utc = state?.변경시각Utc,
            보유중다음콜 = reservation is null ? null : 예약Dto(reservation),
            지표 = 지표(state)
        };
        await 투영저장Async(dto, cancellationToken);
        return dto;
    }

    public async Task<화물연속배차상태Dto> 의사변경Async(
        string 기사Id,
        화물연속배차의사변경요청 요청,
        CancellationToken cancellationToken = default)
    {
        기사확인(기사Id);
        ArgumentNullException.ThrowIfNull(요청);
        if (요청.ClientRequestId == Guid.Empty)
        {
            throw new ArgumentException("ClientRequestId가 필요합니다.", nameof(요청));
        }

        var requestId = 요청.ClientRequestId.ToString("N");
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var state = await _db.화물연속배차상태.SingleOrDefaultAsync(x => x.기사Id == 기사Id, cancellationToken);
        if (state is not null && string.Equals(state.마지막ClientRequestId, requestId, StringComparison.Ordinal))
        {
            return await 조회Async(기사Id, cancellationToken);
        }

        if (state is null)
        {
            state = new 화물연속배차상태
            {
                기사Id = 기사Id,
                CreatedAt = now
            };
            _db.화물연속배차상태.Add(state);
        }

        state.활성여부 = 요청.Enabled;
        state.Revision++;
        state.마지막ClientRequestId = requestId;
        state.변경시각Utc = now;
        state.UpdatedAt = now;

        if (!요청.Enabled)
        {
            var held = await _db.화물다음콜예약
                .Where(x => x.기사Id == 기사Id && x.상태Code == 화물연속배차예약상태Code.보유)
                .ToListAsync(cancellationToken);
            foreach (var item in held)
            {
                item.상태Code = 화물연속배차예약상태Code.반환;
                item.활성예약기사Key = null;
                item.반환사유Code = "DriverContinuityOff";
                item.Revision++;
                item.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await _운영배차.기사의사변경Async(기사Id, new 운영배차수신의사변경요청
        {
            클라이언트요청Id = 요청.ClientRequestId,
            수신의사Code = 요청.Enabled ? 운영배차수신의사Code.On : 운영배차수신의사Code.Off
        }, cancellationToken);
        return await 조회Async(기사Id, cancellationToken);
    }

    public async Task<화물다음콜예약Dto?> 추천탐색Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        기사확인(기사Id);
        if (!_options.Enabled || _options.ShadowMode || !_options.운임정책활성)
        {
            return null;
        }

        var availability = await _운영배차.수신상태조회Async(기사Id, cancellationToken);
        if (!살뜰.도메인.배차.운영배차수신상태Policy.신규배차수신허용(availability)
            || !await _db.화물연속배차상태.AsNoTracking().AnyAsync(x => x.기사Id == 기사Id && x.활성여부, cancellationToken))
        {
            return null;
        }

        await 만료정리Async(기사Id, cancellationToken);
        var existing = await _db.화물다음콜예약.AsNoTracking()
            .Where(x => x.기사Id == 기사Id && x.상태Code == 화물연속배차예약상태Code.보유)
            .OrderByDescending(x => x.보유시각Utc)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return 예약Dto(existing);
        }

        var current = await _db.운송원장.AsNoTracking()
            .Where(x => x.기사_운송자 == 기사Id
                        && x.배차업무유형 == 상태값.배차업무유형.용달운송
                        && x.배차큐단계 == 상태값.배차큐단계.확정
                        && x.상태 != "인수완료")
            .OrderBy(x => x.도착 ?? DateTime.MaxValue)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
        {
            return null;
        }

        var basisNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (!_위치Store.TryGetLatest(기사Id, out var anchorLocation)
            || !기사위치신선도정책.유효한가(anchorLocation.ReceivedAtUtc, basisNow, _queueOptions.기사위치유효시간분)) return null;
        var basis = await _후속픽업.조회Async(기사Id, new 배차경로좌표(anchorLocation.Latitude, anchorLocation.Longitude), basisNow, cancellationToken);
        if (!basis.확인됨 || !basis.활성업무있음 || basis.기준좌표 is null) return null;

        var recommendations = await _추천Service.GetDrivingRecommendationsAsync(기사Id);
        var candidate = recommendations
            .Where(추천가능)
            .OrderByDescending(x => x.추천점수 ?? decimal.MinValue)
            .ThenBy(x => x.픽업거리Km ?? decimal.MaxValue)
            .FirstOrDefault();
        if (candidate is null)
        {
            return null;
        }

        var request = await _db.화주운송의뢰.AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == candidate.의뢰Id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        var floor = 최소지급판정(candidate, request, 승인프로모션보전액: 0m);
        if (!floor.추천가능)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var moving = _위치Store.TryGetLatest(기사Id, out var location)
                     && string.Equals(location.DrivingStatus, 상태값.기사운행상태.운행중, StringComparison.Ordinal);
        var nearDropoff = false;
        if (location is not null)
        {
            nearDropoff = (_경로Service.CalculateDistanceKm(
                new 배차경로좌표(location.Latitude, location.Longitude),
                basis.기준좌표) ?? decimal.MaxValue)
                <= _options.도착임박거리Km;
        }

        var seconds = nearDropoff ? _options.도착임박예약초 : moving ? _options.이동중예약초 : _options.일반예약초;
        var queueExpiry = candidate.추천만료시각;
        var requestedExpiry = now.AddSeconds(seconds);
        var expiresAt = queueExpiry.HasValue && queueExpiry.Value < requestedExpiry
            ? queueExpiry.Value
            : requestedExpiry;
        if (expiresAt <= now)
        {
            return null;
        }

        var reservation = new 화물다음콜예약
        {
            ReservationId = $"freight-hold:{Guid.NewGuid():N}",
            기사Id = 기사Id,
            활성예약기사Key = 기사Id,
            의뢰Id = candidate.의뢰Id,
            현재운송의뢰Id = current.의뢰Id,
            상태Code = 화물연속배차예약상태Code.보유,
            Revision = 1,
            RecommendationRound = candidate.추천라운드,
            보유시각Utc = now,
            만료시각Utc = expiresAt,
            음성알림예정시각Utc = now.AddTicks((expiresAt - now).Ticks / 2),
            추천점수 = candidate.추천점수,
            기사최소지급액 = floor.기사최소지급액,
            운임부족금액 = floor.운임부족금액,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.화물다음콜예약.Add(reservation);
        var finalNow = _timeProvider.GetUtcNow().UtcDateTime;
        if (reservation.만료시각Utc <= finalNow
            || !await _후속픽업.현재근거확인Async(기사Id, basis, cancellationToken)
            || !await _db.화물연속배차상태.AsNoTracking().AnyAsync(x => x.기사Id == 기사Id && x.활성여부, cancellationToken)
            || !await _db.운송원장.AsNoTracking().AnyAsync(x => x.의뢰Id == candidate.의뢰Id
                && x.현재추천대상기사Id == 기사Id && x.추천라운드 == candidate.추천라운드
                && x.상태 == 상태값.배차대기상태.대기 && x.배차큐단계 == 상태값.배차큐단계.배차추천
                && x.배차노출상태 == 상태값.배차노출상태.추천중 && x.추천만료시각 > finalNow, cancellationToken)
            || !await 운영배차신규수신Guard.허용Async(_운영배차, 기사Id, cancellationToken))
        { _db.Entry(reservation).State = EntityState.Detached; return null; }
        _db.배차추천알림Outbox.Add(new 배차추천알림Outbox
        {
            의뢰Id = candidate.의뢰Id,
            기사Id = 기사Id,
            추천라운드 = candidate.추천라운드,
            제목 = "다음 화물 운송 제안",
            본문 = "운행 중 조작을 줄이기 위해 다음 운송 한 건을 잠시 보유했습니다.",
            DataJson = JsonSerializer.Serialize(new
            {
                reservation.ReservationId,
                reservation.Revision,
                reservation.만료시각Utc,
                음성우선 = true
            }),
            CreatedAt = now,
            UpdatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        await 조회Async(기사Id, cancellationToken);
        return 예약Dto(reservation);
    }

    public async Task<화물예약검증결과> 수락예약검증Async(
        string 기사Id,
        string 의뢰Id,
        string? reservationId,
        long? expectedRevision,
        CancellationToken cancellationToken = default)
    {
        if (!await 운영배차신규수신Guard.허용Async(_운영배차, 기사Id, cancellationToken))
            return new(false, "FreightDispatchReceivingOff", "신규 배차 수신 상태를 확인해 주세요.");
        if (string.IsNullOrWhiteSpace(reservationId))
        {
            return new 화물예약검증결과(true);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var reservation = await _db.화물다음콜예약.SingleOrDefaultAsync(
            x => x.ReservationId == reservationId && x.기사Id == 기사Id && x.의뢰Id == 의뢰Id,
            cancellationToken);
        if (reservation is null)
        {
            return new 화물예약검증결과(false, "FreightReservationNotFound", "다음 콜 예약을 찾을 수 없습니다.");
        }

        if (reservation.상태Code != 화물연속배차예약상태Code.보유 || reservation.만료시각Utc <= now)
        {
            return new 화물예약검증결과(false, "FreightReservationExpired", "다음 콜 예약이 만료되었거나 반환되었습니다.");
        }

        if (expectedRevision.HasValue && expectedRevision.Value != reservation.Revision)
        {
            return new 화물예약검증결과(false, "FreightReservationRevisionMismatch", "다음 콜 예약 판본이 변경되었습니다.");
        }

        return new 화물예약검증결과(true);
    }

    public async Task 수락완료Async(string 기사Id, string 의뢰Id, CancellationToken cancellationToken = default)
    {
        var reservation = await _db.화물다음콜예약
            .Where(x => x.기사Id == 기사Id && x.의뢰Id == 의뢰Id && x.상태Code == 화물연속배차예약상태Code.보유)
            .OrderByDescending(x => x.보유시각Utc)
            .FirstOrDefaultAsync(cancellationToken);
        if (reservation is null)
        {
            return;
        }

        reservation.상태Code = 화물연속배차예약상태Code.수락;
        reservation.활성예약기사Key = null;
        reservation.Revision++;
        reservation.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task 시간약속잠금Async(string 기사Id, 화주운송의뢰 의뢰, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (await _db.화물운송시간약속.AnyAsync(x => x.의뢰Id == 의뢰.의뢰Id, cancellationToken))
        {
            return;
        }

        var pickup = 좌표(의뢰.픽업_위도, 의뢰.픽업_경도);
        var dropoff = 좌표(의뢰.하차_위도, 의뢰.하차_경도);
        var route = await _경로Service.EstimateRouteAsync(pickup, dropoff);
        if (route?.Duration is null)
        {
            return;
        }

        var routeMinutes = Math.Max(1, (int)Math.Ceiling(route.Duration.Value.TotalMinutes));
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var baseTime = 의뢰.픽업_시간창_시작일시 > now ? 의뢰.픽업_시간창_시작일시 : now;
        var promise = _policy.시간약속계산(
            baseTime,
            _options.기본픽업서비스분,
            routeMinutes,
            _options.기본하차서비스분,
            _options.불확실성완충비율,
            _options.최소완충분,
            의뢰.하차_시간창_종료일시);
        _db.화물운송시간약속.Add(new 화물운송시간약속
        {
            의뢰Id = 의뢰.의뢰Id,
            기사Id = 기사Id,
            Revision = 1,
            목표도착시각Utc = promise.목표도착시각Utc,
            최종도착한계시각Utc = promise.최종도착한계시각Utc,
            화주최종요청시각Utc = 의뢰.하차_시간창_종료일시,
            잠금완충분 = promise.잠금완충분,
            픽업서비스분 = _options.기본픽업서비스분,
            하차서비스분 = _options.기본하차서비스분,
            기준경로분 = routeMinutes,
            보수경로분 = routeMinutes + promise.잠금완충분,
            시간약속Revision = _options.시간약속Revision,
            잠금시각Utc = now,
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task 완료기록Async(string 기사Id, DateTime 완료시각Utc, CancellationToken cancellationToken = default)
    {
        var state = await _db.화물연속배차상태.SingleOrDefaultAsync(x => x.기사Id == 기사Id, cancellationToken);
        if (state is null)
        {
            return;
        }

        state.마지막유상운송완료시각Utc = 완료시각Utc;
        state.다음유상픽업시각Utc = null;
        state.Revision++;
        state.UpdatedAt = 완료시각Utc;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<화물운송시간약속Dto>> 시간약속목록Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var items = await _db.화물운송시간약속.AsNoTracking()
            .Where(x => x.기사Id == 기사Id)
            .OrderBy(x => x.최종도착한계시각Utc)
            .ToListAsync(cancellationToken);
        return items.Select(x => 약속Dto(x, now)).ToArray();
    }

    public async Task<화물경로위험Dto> 현재위험조회Async(string 기사Id, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var latest = await _db.화물운송시간약속.AsNoTracking()
            .Where(x => x.기사Id == 기사Id)
            .OrderBy(x => x.최종도착한계시각Utc)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
        {
            return new 화물경로위험Dto { 사유Code = "TimeCommitmentMissing" };
        }

        if (!_위치Store.TryGetLatest(기사Id, out var location))
        {
            return new 화물경로위험Dto
            {
                의뢰Id = latest.의뢰Id,
                사유Code = "CurrentLocationMissing"
            };
        }

        var transport = await _db.운송원장.AsNoTracking()
            .SingleOrDefaultAsync(x => x.의뢰Id == latest.의뢰Id, cancellationToken);
        var destination = transport is null ? null : 좌표(transport.하차_위도, transport.하차_경도);
        if (destination is null)
        {
            return new 화물경로위험Dto
            {
                의뢰Id = latest.의뢰Id,
                사유Code = "DropoffLocationMissing"
            };
        }

        var route = await _경로Service.EstimateRouteAsync(
            new 배차경로좌표(location.Latitude, location.Longitude),
            destination);
        if (route?.Duration is null)
        {
            return new 화물경로위험Dto
            {
                의뢰Id = latest.의뢰Id,
                사유Code = "CurrentRouteEvidenceMissing"
            };
        }

        var remainingRouteMinutes = Math.Max(1, (int)Math.Ceiling(route.Duration.Value.TotalMinutes));
        var conservativeRouteMinutes = remainingRouteMinutes + latest.잠금완충분;
        var eta = now.AddMinutes(remainingRouteMinutes);
        var conservativeEta = now.AddMinutes(conservativeRouteMinutes);

        var risk = _policy.위험판정(
            latest.목표도착시각Utc,
            latest.최종도착한계시각Utc,
            now,
            conservativeEta,
            _options.주의남은완충비율);
        return new 화물경로위험Dto
        {
            위험Code = risk,
            의뢰Id = latest.의뢰Id,
            예상도착시각Utc = eta,
            보수예상도착시각Utc = conservativeEta,
            남은완충분 = (int)Math.Floor((latest.최종도착한계시각Utc - conservativeEta).TotalMinutes),
            보유콜자동반환필요 = risk == 화물시간위험Code.위험,
            사유Code = risk == 화물시간위험Code.위험 ? "ConservativeEtaBeyondFinalDeadline" : string.Empty
        };
    }

    private bool 추천가능(DispatchRecommendationDto item)
        => item.차량적합여부
           && item.일정삽입가능여부
           && item.전체일정완수가능여부
           && (item.픽업거리Km ?? decimal.MaxValue) <= _options.연속픽업최대거리Km
           && item.일정위반사유.Length == 0;

    private 화물최소지급계산결과 최소지급판정(DispatchRecommendationDto item, 화주운송의뢰 request, decimal 승인프로모션보전액)
        => _policy.최소지급액계산(
            _options.차량기본액,
            item.공차거리Km ?? 0m,
            _options.공차Km단가,
            item.운송거리Km ?? 0m,
            _options.적재Km단가,
            item.추가예상시간분 ?? 0m,
            _options.시간당단가,
            item.예상톨비 ?? 0m,
            request.수작업비 ?? 0m,
            request.대기료 ?? 0m,
            request.할증 ?? 0m,
            request.최종운임 ?? 0m,
            승인프로모션보전액);

    private async Task 만료정리Async(string 기사Id, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var expired = await _db.화물다음콜예약
            .Where(x => x.기사Id == 기사Id
                        && x.상태Code == 화물연속배차예약상태Code.보유
                        && x.만료시각Utc <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var item in expired)
        {
            item.상태Code = 화물연속배차예약상태Code.만료;
            item.활성예약기사Key = null;
            item.반환사유Code = "NoResponseWithoutPenalty";
            item.Revision++;
            item.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task 투영저장Async(화물연속배차상태Dto dto, CancellationToken cancellationToken)
    {
        try
        {
            await _projection.저장Async(dto, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "화물 연속 배차 Redis 투영 갱신에 실패했습니다. MySQL 원장에서 재구성할 수 있습니다. DriverId={DriverId}", dto.기사Id);
        }
    }

    private 화물운송시간약속Dto 약속Dto(화물운송시간약속 x, DateTime now)
    {
        var conservative = now.AddMinutes(x.보수경로분);
        return new 화물운송시간약속Dto
        {
            의뢰Id = x.의뢰Id,
            Revision = x.Revision,
            목표도착시각Utc = x.목표도착시각Utc,
            최종도착한계시각Utc = x.최종도착한계시각Utc,
            잠금완충분 = x.잠금완충분,
            픽업서비스분 = x.픽업서비스분,
            하차서비스분 = x.하차서비스분,
            기준경로분 = x.기준경로분,
            보수경로분 = x.보수경로분,
            위험Code = _policy.위험판정(x.목표도착시각Utc, x.최종도착한계시각Utc, now, conservative, _options.주의남은완충비율),
            남은완충분 = (int)Math.Floor((x.최종도착한계시각Utc - conservative).TotalMinutes),
            시간약속Revision = x.시간약속Revision
        };
    }

    private static 화물다음콜예약Dto 예약Dto(화물다음콜예약 x)
        => new()
        {
            ReservationId = x.ReservationId,
            의뢰Id = x.의뢰Id,
            상태Code = x.상태Code,
            Revision = x.Revision,
            RecommendationRound = x.RecommendationRound,
            보유시각Utc = x.보유시각Utc,
            만료시각Utc = x.만료시각Utc,
            음성알림예정시각Utc = x.음성알림예정시각Utc,
            보유초 = Math.Max(0, (int)Math.Round((x.만료시각Utc - x.보유시각Utc).TotalSeconds)),
            추천점수 = x.추천점수,
            기사최소지급액 = x.기사최소지급액,
            운임부족금액 = x.운임부족금액,
            반환사유Code = x.반환사유Code
        };

    private static 화물연속배차지표Dto 지표(화물연속배차상태? state)
    {
        var gap = state?.마지막유상운송완료시각Utc is not null && state.다음유상픽업시각Utc is not null
            ? Math.Max(0, (int)Math.Round((state.다음유상픽업시각Utc.Value - state.마지막유상운송완료시각Utc.Value).TotalMinutes))
            : (int?)null;
        return new 화물연속배차지표Dto { 무급공백분 = gap };
    }

    private static 배차경로좌표? 좌표(decimal? latitude, decimal? longitude)
        => latitude.HasValue && longitude.HasValue ? new 배차경로좌표(latitude.Value, longitude.Value) : null;

    private static void 기사확인(string 기사Id)
    {
        if (string.IsNullOrWhiteSpace(기사Id))
        {
            throw new ArgumentException("기사 ID가 필요합니다.", nameof(기사Id));
        }
    }
}
