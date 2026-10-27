using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Work;

namespace DriverApp.Services;

public sealed record DriverLocationTransmissionOptions(string AppKey, int IntervalSeconds = 30,
    decimal PickupApproachRadiusKm = 10m);

public sealed record DriverGpsObservation(double Latitude, double Longitude, double? Accuracy,
    DateTimeOffset MeasuredAtUtc, bool IsMock = false);

public sealed class DriverLocationPermissionRevokedException : Exception { }

/// <summary>
/// 명시적으로 시작한 위치 송신의 메모리 전용 권한입니다. 프로세스 종료 뒤 자동 복구하지 않습니다.
/// 각 위치 POST 전에 근무 정본과 계정·로그인 세션·서버가 같은지 다시 확인합니다.
/// </summary>
public sealed class DriverLocationTransmissionSession : IDisposable
{
    private sealed class Lease(string owner, long revision, string endpoint, long shiftId,
        DriverLocationTransmissionOptions options)
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Owner { get; } = owner;
        public long Revision { get; } = revision;
        public string Endpoint { get; } = endpoint;
        public long ShiftId { get; } = shiftId;
        public DriverLocationTransmissionOptions Options { get; } = options;
        public CancellationTokenSource Lifetime { get; } = new();
        public TaskCompletionSource<bool> Activated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Running { get; set; }
        public int IntervalSeconds { get; set; } = Math.Clamp(options.IntervalSeconds, 30, 900);
        public int Failures { get; set; }
    }

    private readonly IAuthSession _auth;
    private readonly IDriverWorkApiService _api;
    private readonly Func<Uri?> _endpoint;
    private readonly TimeProvider _clock;
    private readonly object _sync = new();
    private readonly SemaphoreSlim _startGate = new(1, 1);
    private readonly SemaphoreSlim _sampleGate = new(1, 1);
    private Lease? _current;
    private long _stopGeneration;
    private bool _disposed;
    private string? _notice;

    public DriverLocationTransmissionSession(IAuthSession auth, IDriverWorkApiService api,
        Func<Uri?> endpoint, TimeProvider? clock = null)
    {
        _auth = auth; _api = api; _endpoint = endpoint; _clock = clock ?? TimeProvider.System;
        _auth.Changed += AuthChanged;
    }

    public event Action? Changed;
    public bool IsRunning { get { lock (_sync) return _current is { Running: true } lease && IdentityMatches(lease); } }
    public string? Notice { get { lock (_sync) return _notice; } }

    public async Task<Guid> PrepareAsync(DriverLocationTransmissionOptions options, CancellationToken token = default)
    {
        await _startGate.WaitAsync(token);
        try
        {
            long stopGeneration;
            lock (_sync) stopGeneration = _stopGeneration;
            var owner = _auth.UserId;
            var revision = _auth.SessionRevision;
            var endpoint = Endpoint();
            if (_disposed || !_auth.IsAuthenticated || string.IsNullOrWhiteSpace(owner) || endpoint.Length == 0)
                throw new InvalidOperationException("로그인과 서버 연결을 확인한 뒤 운행을 시작해 주세요.");
            var canonical = await _api.운행상태조회Async(token);
            var shiftId = canonical?.ShiftId;
            token.ThrowIfCancellationRequested();
            if (_disposed || !_auth.IsAuthenticated || _auth.SessionRevision != revision
                || !string.Equals(_auth.UserId, owner, StringComparison.Ordinal) || Endpoint() != endpoint
                || !IsRunningShift(canonical, owner) || shiftId is not > 0)
                throw new InvalidOperationException("현재 계정의 운행 중인 근무가 확인되지 않아 위치 송신을 시작하지 않았습니다.");

            var lease = new Lease(owner, revision, endpoint, shiftId.GetValueOrDefault(), options);
            Lease? previous;
            lock (_sync)
            {
                // 정본 대기 중 중지한 경우에도 아직 발급 전인 시작 권한을 무효화합니다.
                if (_stopGeneration != stopGeneration || _disposed)
                    throw new OperationCanceledException("위치 송신 시작 중에 중지 요청을 확인했습니다.");
                previous = _current;
                _current = lease;
                _notice = null;
            }
            Cancel(previous);
            Changed?.Invoke();
            // 정본 조회와 시작 권한 발급 사이 로그아웃도 늦은 시작으로 이어지지 않게 합니다.
            if (!EnsureCurrent(lease)) throw new OperationCanceledException("로그인 또는 서버 연결이 변경되었습니다.");
            return lease.Id;
        }
        finally { _startGate.Release(); }
    }

    public bool TryActivate(Guid id)
    {
        var lease = FindCurrent(id);
        if (lease is null || !EnsureCurrent(lease)) return false;
        lock (_sync)
        {
            if (_current != lease || lease.Running || !IdentityMatches(lease)) return false;
            lease.Running = true;
            lease.Activated.TrySetResult(true);
        }
        Changed?.Invoke();
        return true;
    }

    public async Task WaitActivatedAsync(Guid id, CancellationToken token = default)
    {
        var lease = FindCurrent(id) ?? throw new InvalidOperationException("위치 송신 시작이 취소되었습니다.");
        var activated = await lease.Activated.Task.WaitAsync(TimeSpan.FromSeconds(8), token);
        if (!activated || !EnsureCurrent(lease) || !lease.Running)
            throw new InvalidOperationException("위치 송신 서비스가 시작되지 않았습니다.");
    }

    public bool IsCurrent(Guid id) => FindCurrent(id) is { } lease && EnsureCurrent(lease);
    public CancellationToken Lifetime(Guid id) => FindCurrent(id)?.Lifetime.Token ?? new CancellationToken(true);

    public int NextIntervalSeconds(Guid id)
    {
        lock (_sync)
        {
            if (_current is not { } lease || lease.Id != id) return 30;
            return Math.Min(900, lease.IntervalSeconds * (1 << Math.Min(lease.Failures, 3)));
        }
    }

    public void Stop(Guid? id = null, string? notice = null)
    {
        Lease? lease;
        lock (_sync)
        {
            lease = _current;
            if (id.HasValue && (lease is null || lease.Id != id.Value)) return;
            _stopGeneration++;
            _current = null;
            _notice = notice;
        }
        Cancel(lease);
        Changed?.Invoke();
    }

    public async Task DrainAsync(CancellationToken token = default)
    {
        await _sampleGate.WaitAsync(token);
        _sampleGate.Release();
    }

    /// <returns>같은 계정과 근무의 측정 위치를 서버가 확인한 경우에만 true입니다.</returns>
    public async Task<bool> SampleAndSendAsync(Guid id,
        Func<CancellationToken, Task<DriverGpsObservation?>> locate, CancellationToken token = default)
    {
        var lease = FindCurrent(id);
        if (lease is null || !lease.Running || !EnsureCurrent(lease)) return false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lease.Lifetime.Token);
        var lifetime = linked.Token;
        var entered = false;
        try
        {
            await _sampleGate.WaitAsync(lifetime); entered = true;
            if (!EnsureCurrent(lease) || !await CheckShiftAsync(lease, lifetime)) return false;
            var location = await locate(lifetime);
            lifetime.ThrowIfCancellationRequested();
            if (!EnsureCurrent(lease)) return false;
            if (!IsValid(location, _clock.GetUtcNow()))
            {
                Failed(lease, "현재 위치를 확인하지 못했습니다. 신선한 위치를 다시 확인합니다.");
                return false;
            }
            // 센서 응답을 기다리는 동안 운행이 끝났을 수 있으므로 전송 직전에 정본을 다시 읽습니다.
            if (!await CheckShiftAsync(lease, lifetime) || !EnsureCurrent(lease)) return false;
            if (!IsValid(location, _clock.GetUtcNow()))
            {
                Failed(lease, "위치가 오래되어 다시 측정합니다."); return false;
            }
            lifetime.ThrowIfCancellationRequested();
            var response = await _api.위치갱신Async(new 기사위치갱신요청
            {
                AppKey = lease.Options.AppKey,
                위도 = (decimal)location!.Latitude,
                경도 = (decimal)location.Longitude,
                정확도_m = location.Accuracy is { } accuracy ? (decimal)accuracy : null,
                상차접근허용반경Km = lease.Options.PickupApproachRadiusKm,
                운행상태 = "운행중",
                기록시각 = location.MeasuredAtUtc.UtcDateTime
            }, lifetime);
            lifetime.ThrowIfCancellationRequested();
            if (!EnsureCurrent(lease)) return false;
            if (response is null) throw new InvalidOperationException("위치 응답이 없습니다.");
            if (response.DriverId != lease.Owner || response.Status != "운행중")
            {
                Stop(id, "현재 운행 정보가 달라 위치 송신을 중지했습니다. 운행을 다시 확인해 주세요.");
                return false;
            }
            lock (_sync)
            {
                if (_current != lease) return false;
                if (response.권장위치전송간격초 > 0) lease.IntervalSeconds = Math.Clamp(response.권장위치전송간격초, 30, 900);
                lease.Failures = 0; _notice = null;
            }
            Changed?.Invoke();
            return true;
        }
        catch (DriverLocationPermissionRevokedException)
        {
            Stop(id, "위치 권한 또는 기기 위치 기능이 꺼져 송신을 중지했습니다. 권한을 확인한 뒤 운행 확인으로 다시 시작해 주세요.");
            return false;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return false; }
        catch (OperationCanceledException)
        {
            Failed(lease, "위치 확인 시간이 초과됐습니다. 잠시 후 다시 확인합니다."); return false;
        }
        catch (Exception)
        {
            // 좌표·토큰·서버 오류 본문·고객 정보는 상태 안내나 로그에 넣지 않습니다.
            Failed(lease, "위치 송신을 확인하지 못했습니다. 잠시 후 다시 시도합니다."); return false;
        }
        finally { if (entered) _sampleGate.Release(); }
    }

    public static bool IsValid(DriverGpsObservation? location, DateTimeOffset now)
        => location is not null && !location.IsMock
            && double.IsFinite(location.Latitude) && double.IsFinite(location.Longitude)
            && location.Latitude is >= -90 and <= 90 && location.Longitude is >= -180 and <= 180
            && (location.Latitude != 0 || location.Longitude != 0)
            && (location.Accuracy is null || (double.IsFinite(location.Accuracy.Value)
                && location.Accuracy.Value >= 0 && location.Accuracy.Value <= (double)decimal.MaxValue))
            && location.MeasuredAtUtc <= now && location.MeasuredAtUtc >= now.AddSeconds(-30);

    private async Task<bool> CheckShiftAsync(Lease lease, CancellationToken token)
    {
        if (!EnsureCurrent(lease)) return false;
        var current = await _api.운행상태조회Async(token);
        token.ThrowIfCancellationRequested();
        if (!EnsureCurrent(lease)) return false;
        if (current is null) throw new InvalidOperationException("현재 운행 상태가 미확인입니다.");
        if (IsRunningShift(current, lease.Owner) && current.ShiftId == lease.ShiftId) return true;
        Stop(lease.Id, "운행 종료 또는 근무 변경을 확인해 위치 송신을 중지했습니다.");
        return false;
    }

    private static bool IsRunningShift(기사운행상태응답? current, string owner)
        => current is not null && current.DriverId == owner && current.ShiftId is > 0 && current.Status == "운행중"
            && current.운송실행유형 == 운송실행유형코드.화물운송;
    private string Endpoint() => _endpoint()?.AbsoluteUri ?? string.Empty;
    private bool IdentityMatches(Lease lease) => !_disposed && _auth.IsAuthenticated
        && _auth.SessionRevision == lease.Revision && _auth.UserId == lease.Owner && Endpoint() == lease.Endpoint;
    private Lease? FindCurrent(Guid id) { lock (_sync) return _current?.Id == id ? _current : null; }
    private bool EnsureCurrent(Lease lease)
    {
        lock (_sync)
        {
            if (_current != lease || lease.Lifetime.IsCancellationRequested) return false;
            if (IdentityMatches(lease)) return true;
        }
        Stop(lease.Id, "로그인 또는 서버 연결이 변경되어 위치 송신을 중지했습니다.");
        return false;
    }
    private static void Cancel(Lease? lease)
    {
        if (lease is null) return;
        lease.Running = false; lease.Activated.TrySetResult(false); lease.Lifetime.Cancel();
    }
    private void Failed(Lease lease, string notice)
    {
        if (!EnsureCurrent(lease)) return;
        lock (_sync)
        {
            if (_current != lease) return;
            lease.Failures = Math.Min(lease.Failures + 1, 3); _notice = notice;
        }
        Changed?.Invoke();
    }
    private void AuthChanged()
    {
        Lease? lease; lock (_sync) lease = _current;
        if (lease is not null) EnsureCurrent(lease);
    }
    public void Dispose()
    {
        _disposed = true; _auth.Changed -= AuthChanged; Stop(notice: "위치 송신을 중지했습니다.");
        // 취소는 즉시 전달합니다. 센서·HTTP 콜백의 취소 등록이 남아 있을 수 있어 여기서 CTS를 폐기하지 않습니다.
    }
}
