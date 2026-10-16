using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Metadata;
using Microsoft.Extensions.Logging;
using 살뜰.도메인.배차;

namespace 살뜰.Services.Dispatch.Common;

public interface I운영배차공통UseCase
{
    Task<운영배차수신상태Dto> 수신상태조회Async(string 주체Id, CancellationToken cancellationToken = default);
    Task<운영배차수신상태Dto> 기사의사변경Async(
        string 기사Id,
        운영배차수신의사변경요청 요청,
        CancellationToken cancellationToken = default);
    Task<운영배차수신상태Dto> 서버실효상태변경Async(
        string 주체Id,
        Guid 요청Id,
        string 실효상태Code,
        string 실효사유Code,
        CancellationToken cancellationToken = default);
    Task<운영배차단기지표Dto> 단기지표조회Async(string 주체Id, CancellationToken cancellationToken = default);
    Task<bool> 활동사건기록Async(운영배차활동사건Dto 사건, CancellationToken cancellationToken = default);
}

[SsalddelCodeMetadata(
    SsalddelCodeFeatureKeys.OperationalDispatchCore,
    SsalddelCodeLayer.Application,
    "운영 배차 활동을 영속 원장에 먼저 기록하고 Redis 판정 투영을 다시 만든다.",
    Effects = SsalddelCodeEffect.PersistentRead | SsalddelCodeEffect.PersistentWrite,
    FlowOrder = 60,
    StepKey = "application.operational-dispatch-usecase",
    DependsOnStepKeys = [
        "domain.operational-dispatch-availability-policy",
        "domain.operational-dispatch-short-window",
        "application.operational-dispatch-ledger-port",
        "application.operational-dispatch-decision-projection-port"],
    ExecutionStage = SsalddelCodeExecutionStage.Confirm,
    ReadsFrom = SsalddelCodeDataScope.OperationalState,
    WritesTo = SsalddelCodeDataScope.OperationalState,
    Boundary = "영속 원장이 먼저이며 Redis 실패나 유실 뒤에도 원장에서 재구성한다. 범용 활동 사건 기록은 외부 Controller에 노출하지 않는다.")]
public sealed class 운영배차공통UseCase : I운영배차공통UseCase
{
    private static readonly DateTimeOffset LedgerEpoch = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly I운영배차활동원장Store _원장;
    private readonly I운영배차판정ProjectionStore _판정투영;
    private readonly 운영배차수신상태Policy _수신상태Policy;
    private readonly 운영배차단기지표Calculator _지표Calculator;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _운영시장시간대;
    private readonly ILogger<운영배차공통UseCase> _logger;

    public 운영배차공통UseCase(
        I운영배차활동원장Store 원장,
        I운영배차판정ProjectionStore 판정투영,
        운영배차수신상태Policy 수신상태Policy,
        운영배차단기지표Calculator 지표Calculator,
        TimeProvider timeProvider,
        ILogger<운영배차공통UseCase> logger)
    {
        _원장 = 원장;
        _판정투영 = 판정투영;
        _수신상태Policy = 수신상태Policy;
        _지표Calculator = 지표Calculator;
        _timeProvider = timeProvider;
        _logger = logger;
        _운영시장시간대 = 운영배차단기지표Calculator.대한민국시간대조회();
    }

    public async Task<운영배차수신상태Dto> 수신상태조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default)
    {
        var subjectId = 주체확인(주체Id);
        // 만료/수락은 업무 트랜잭션에서 직접 사건을 추가한다. 과거 캐시로 신규 수신을 허용하지 않는다.
        return await 수신상태재구성Async(subjectId, cancellationToken);
    }

    public async Task<운영배차수신상태Dto> 기사의사변경Async(
        string 기사Id,
        운영배차수신의사변경요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        var subjectId = 주체확인(기사Id);
        요청Id확인(요청.클라이언트요청Id);
        if (요청.미응답자동중지해제 && 요청.수신의사Code != 운영배차수신의사Code.On)
            throw new ArgumentException("미응답 자동 중지는 본인의 명시 ON 요청으로만 해제할 수 있습니다.", nameof(요청));
        if (요청.최초On조건부적용 && (요청.수신의사Code != 운영배차수신의사Code.On || 요청.미응답자동중지해제))
            throw new ArgumentException("조건부 최초 ON은 자동 중지를 해제할 수 없습니다.", nameof(요청));
        if (요청.예상수신의사판본Token?.Length > 180)
            throw new ArgumentException("수신 의사 판본을 다시 확인해 주세요.", nameof(요청));
        var stableId = $"dispatch-intent:{요청.클라이언트요청Id:N}";
        var existing = await _원장.사건조회Async(stableId, cancellationToken);
        if (existing is not null)
        {
            멱등의사요청확인(existing, subjectId, 요청);
            return await 수신상태재구성Async(subjectId, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow();
        var current = await 수신상태조회Async(subjectId, cancellationToken);
        _수신상태Policy.기사의사변경(current, subjectId, 요청.수신의사Code, now);
        await _원장.수신의사추가Async(new 운영배차활동사건Dto
        {
            사건StableId = stableId,
            발생시각Utc = now,
            주체Id = subjectId,
            주체역할Code = 운영배차주체역할Code.기사,
            사건유형Code = 운영배차사건유형Code.수신의사변경,
            제안유효성Code = 운영배차제안유효성Code.해당없음,
            책임Code = 운영배차책임Code.해당없음,
            사유Code = 요청.최초On조건부적용 ? 운영배차수신의사판본.최초On사유
                : 요청.미응답자동중지해제 ? 운영배차미응답Code.기사명시재개 : "DriverExplicitIntent",
            업무시도Id = 운영배차수신의사판본.요청근거(요청.최초On조건부적용, 요청.예상수신의사판본Token),
            상태값Code = 요청.수신의사Code
        }, 요청.최초On조건부적용, 요청.예상수신의사판본Token, cancellationToken);

        return await 수신상태재구성Async(subjectId, cancellationToken);
    }

    public async Task<운영배차수신상태Dto> 서버실효상태변경Async(
        string 주체Id,
        Guid 요청Id,
        string 실효상태Code,
        string 실효사유Code,
        CancellationToken cancellationToken = default)
    {
        var subjectId = 주체확인(주체Id);
        요청Id확인(요청Id);
        var stableId = $"dispatch-effective:{요청Id:N}";
        var existing = await _원장.사건조회Async(stableId, cancellationToken);
        if (existing is not null)
        {
            멱등실효요청확인(existing, subjectId, 실효상태Code, 실효사유Code);
            return await 수신상태재구성Async(subjectId, cancellationToken);
        }

        var now = _timeProvider.GetUtcNow();
        var current = await 수신상태조회Async(subjectId, cancellationToken);
        _수신상태Policy.서버실효상태변경(current, 실효상태Code, 실효사유Code, now);
        await _원장.사건추가Async(new 운영배차활동사건Dto
        {
            사건StableId = stableId,
            발생시각Utc = now,
            주체Id = subjectId,
            주체역할Code = 운영배차주체역할Code.시스템,
            사건유형Code = 운영배차사건유형Code.실효상태변경,
            제안유효성Code = 운영배차제안유효성Code.해당없음,
            책임Code = 운영배차책임Code.시스템,
            사유Code = 실효사유Code?.Trim() ?? string.Empty,
            상태값Code = 실효상태Code
        }, cancellationToken);

        return await 수신상태재구성Async(subjectId, cancellationToken);
    }

    public async Task<운영배차단기지표Dto> 단기지표조회Async(
        string 주체Id,
        CancellationToken cancellationToken = default)
    {
        var subjectId = 주체확인(주체Id);
        var now = _timeProvider.GetUtcNow();
        return await 단기지표재구성Async(subjectId, now, cancellationToken);
    }

    public async Task<bool> 활동사건기록Async(
        운영배차활동사건Dto 사건,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(사건);
        if (사건.사건유형Code is 운영배차사건유형Code.수신의사변경
            or 운영배차사건유형Code.실효상태변경)
        {
            throw new InvalidOperationException("수신 의사와 실효 상태는 전용 변경 메서드로 기록해야 합니다.");
        }

        주체확인(사건.주체Id);
        var added = await _원장.사건추가Async(사건, cancellationToken);
        await 단기지표재구성Async(사건.주체Id, _timeProvider.GetUtcNow(), cancellationToken);
        return added;
    }

    private async Task<운영배차수신상태Dto> 수신상태재구성Async(
        string 주체Id,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var events = await _원장.기간조회Async(
            주체Id,
            LedgerEpoch,
            now.AddTicks(1),
            cancellationToken);
        운영배차수신상태Dto? state = null;
        foreach (var item in events
                     .Where(x => x.사건유형Code is 운영배차사건유형Code.수신의사변경
                         or 운영배차사건유형Code.실효상태변경)
                     .OrderBy(x => x.사건유형Code == 운영배차사건유형Code.수신의사변경
                         ? 운영배차수신의사판본.기록순서(x) : x.발생시각Utc)
                     .ThenBy(x => x.사건StableId, StringComparer.Ordinal))
        {
            state = item.사건유형Code == 운영배차사건유형Code.수신의사변경
                ? _수신상태Policy.기사의사변경(state, 주체Id, item.상태값Code, item.발생시각Utc)
                : _수신상태Policy.서버실효상태변경(
                    state ?? 초기상태(주체Id, item.발생시각Utc),
                    item.상태값Code,
                    item.사유Code,
                    item.발생시각Utc);
        }

        state ??= 초기상태(주체Id, now);
        state.수신의사판본Token = 운영배차수신의사판본.계산(events);
        운영배차미응답Policy.반영(state, events);
        state.서버관측시각Utc = now;
        await 수신상태투영저장Async(state, cancellationToken);
        return state;
    }

    private async Task<운영배차단기지표Dto> 단기지표재구성Async(
        string 주체Id,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var startDate = 운영날짜(now).AddDays(-2);
        var localStart = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, _운영시장시간대));
        var events = await _원장.기간조회Async(주체Id, startUtc, now.AddTicks(1), cancellationToken);
        var metrics = _지표Calculator.계산(events, now, _운영시장시간대);
        await 단기지표투영저장Async(주체Id, metrics, cancellationToken);
        return metrics;
    }

    private async Task<운영배차수신상태Dto?> 수신상태투영조회Async(
        string 주체Id,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _판정투영.수신상태조회Async(주체Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "운영 배차 수신 상태 투영 조회에 실패하여 영속 원장에서 재구성합니다. SubjectId={SubjectId}", 주체Id);
            return null;
        }
    }

    private async Task<운영배차단기지표Dto?> 단기지표투영조회Async(
        string 주체Id,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _판정투영.단기지표조회Async(주체Id, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "운영 배차 단기 지표 투영 조회에 실패하여 영속 원장에서 재구성합니다. SubjectId={SubjectId}", 주체Id);
            return null;
        }
    }

    private async Task 수신상태투영저장Async(
        운영배차수신상태Dto 상태,
        CancellationToken cancellationToken)
    {
        try
        {
            await _판정투영.수신상태저장Async(상태, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "운영 배차 수신 상태 Redis 투영 갱신에 실패했습니다. 영속 원장은 유지됩니다. SubjectId={SubjectId}", 상태.주체Id);
        }
    }

    private async Task 단기지표투영저장Async(
        string 주체Id,
        운영배차단기지표Dto 지표,
        CancellationToken cancellationToken)
    {
        try
        {
            await _판정투영.단기지표저장Async(주체Id, 지표, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "운영 배차 단기 지표 Redis 투영 갱신에 실패했습니다. 영속 원장은 유지됩니다. SubjectId={SubjectId}", 주체Id);
        }
    }

    private DateOnly 운영날짜(DateTimeOffset utc)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, _운영시장시간대).DateTime);

    private static 운영배차수신상태Dto 초기상태(string 주체Id, DateTimeOffset now)
        => new()
        {
            주체Id = 주체Id,
            수신의사Code = 운영배차수신의사Code.Off,
            실효상태Code = 운영배차실효상태Code.조건부적합,
            실효사유Code = "NoEffectiveEligibilityRecorded",
            서버관측시각Utc = now.ToUniversalTime()
        };

    private static void 멱등의사요청확인(
        운영배차활동사건Dto existing,
        string 주체Id,
        운영배차수신의사변경요청 요청)
    {
        if (existing.주체Id != 주체Id
            || existing.사건유형Code != 운영배차사건유형Code.수신의사변경
            || existing.상태값Code != 요청.수신의사Code
            || existing.사유Code != (요청.최초On조건부적용 ? 운영배차수신의사판본.최초On사유
                : 요청.미응답자동중지해제 ? 운영배차미응답Code.기사명시재개 : "DriverExplicitIntent")
            || existing.업무시도Id != 운영배차수신의사판본.요청근거(요청.최초On조건부적용, 요청.예상수신의사판본Token))
        {
            throw new 운영배차수신요청충돌Exception();
        }
    }

    private static void 멱등실효요청확인(
        운영배차활동사건Dto existing,
        string 주체Id,
        string 실효상태Code,
        string 실효사유Code)
    {
        if (existing.주체Id != 주체Id
            || existing.사건유형Code != 운영배차사건유형Code.실효상태변경
            || existing.상태값Code != 실효상태Code
            || existing.사유Code != (실효사유Code?.Trim() ?? string.Empty))
        {
            throw new InvalidOperationException("같은 요청 ID에 다른 실효 배차 상태 요청이 있습니다.");
        }
    }

    private static string 주체확인(string 주체Id)
    {
        var clean = 주체Id?.Trim();
        return string.IsNullOrWhiteSpace(clean)
            ? throw new ArgumentException("운영 배차 주체 ID가 필요합니다.", nameof(주체Id))
            : clean;
    }

    private static void 요청Id확인(Guid 요청Id)
    {
        if (요청Id == Guid.Empty)
        {
            throw new ArgumentException("멱등 처리를 위한 요청 ID가 필요합니다.", nameof(요청Id));
        }
    }
}
