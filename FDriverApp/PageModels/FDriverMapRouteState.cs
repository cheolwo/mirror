using Ssalddel.Contracts.Driver.Food;

namespace FDriverApp.PageModels;

public enum FoodMapRoutePhase
{
    Pickup,
    Dropoff
}

public sealed record FDriverMapRouteIntent(
    string OfferId,
    FoodMapRoutePhase Phase,
    decimal PickupLatitude,
    decimal PickupLongitude,
    decimal DropoffLatitude,
    decimal DropoffLongitude,
    decimal TargetLatitude,
    decimal TargetLongitude,
    string TargetLabel = "");

public sealed record FDriverRouteLocation(
    decimal Latitude,
    decimal Longitude,
    decimal? AccuracyMeters,
    DateTimeOffset RecordedAtUtc);

public sealed record FDriverStoredMapRoute(
    string OfferId,
    FoodMapRoutePhase Phase,
    bool IsReference,
    string Source,
    bool IsEstimated,
    decimal OriginLatitude,
    decimal OriginLongitude,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset ReceivedAtUtc,
    decimal DistanceKm,
    int DurationMinutes,
    IReadOnlyList<FoodDeliveryDriverRoutePointDto> Points);

public sealed record FDriverMapRouteSnapshot(
    FDriverStoredMapRoute? CurrentRoute,
    IReadOnlyList<FDriverStoredMapRoute> ReferenceRoutes,
    bool IsLoading,
    string? LastError,
    DateTimeOffset? RetryAtUtc,
    long Revision);

/// <summary>업무 명령과 별도로, 선택한 배달의 경로 조회 및 다른 배달의 참고 경로를 관리한다.</summary>
public sealed class FDriverMapRouteState
{
    private static readonly TimeSpan MinimumQueryInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan LocationMaxAge = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LocationFutureTolerance = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly Func<FoodDeliveryDriverRouteRequestDto, CancellationToken, Task<FoodDeliveryDriverRouteResponseDto>> _query;
    private readonly TimeProvider _time;
    private readonly Dictionary<ReferenceKey, ReferenceEntry> _references = [];
    private IReadOnlyList<FDriverMapRouteIntent> _intents = [];
    private string _accountId = string.Empty;
    private long _lifetimeId;
    private long _ownerGeneration;
    private long _revision;
    private FDriverMapRouteIntent? _focused;
    private FDriverRouteLocation? _location;
    private DateTimeOffset? _lastLocationRecordedAtUtc;
    private FDriverStoredMapRoute? _current;
    private QueryWork? _active;
    private Task? _pumpTask;
    private bool _focusNeedsQuery;
    private int _offRouteSamples;
    private string? _lastError;
    private DateTimeOffset? _retryAtUtc;

    public FDriverMapRouteState(
        Func<FoodDeliveryDriverRouteRequestDto, CancellationToken, Task<FoodDeliveryDriverRouteResponseDto>> query,
        TimeProvider? timeProvider = null)
    {
        _query = query ?? throw new ArgumentNullException(nameof(query));
        _time = timeProvider ?? TimeProvider.System;
    }

    public event EventHandler? Changed;

    public FDriverMapRouteSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                var references = _intents
                    .Where(intent => intent.OfferId != _focused?.OfferId)
                    .Select(intent => _references.GetValueOrDefault(ReferenceKey.From(intent))?.Route)
                    .OfType<FDriverStoredMapRoute>()
                    .ToArray();
                return new(_current, references,
                    _active is { IsReference: false } && _active.OwnerGeneration == _ownerGeneration &&
                        !_active.Cancellation.IsCancellationRequested,
                    _lastError, _retryAtUtc, _revision);
            }
        }
    }

    public void UpdateContext(string accountId, long lifetimeId,
        IReadOnlyList<FDriverMapRouteIntent> intents, string? focusedOfferId)
    {
        ArgumentNullException.ThrowIfNull(intents);
        CancellationTokenSource? cancel = null;
        var changed = false;
        lock (_gate)
        {
            if (_accountId != accountId || _lifetimeId != lifetimeId)
            {
                ResetOwnerLocked(accountId, lifetimeId);
                cancel = _active?.Cancellation;
                changed = true;
            }

            FDriverMapRouteIntent[] next = string.IsNullOrWhiteSpace(accountId)
                ? []
                : intents.Where(IsValidIntent).DistinctBy(intent => intent.OfferId).ToArray();
            var focused = next.FirstOrDefault(intent => intent.OfferId == focusedOfferId);
            if (FocusKey.From(_focused) != FocusKey.From(focused))
            {
                _revision++;
                _current = null;
                _lastError = null;
                _retryAtUtc = null;
                _offRouteSamples = 0;
                _focusNeedsQuery = focused is not null;
                cancel = _active?.Cancellation;
                changed = true;
            }

            _focused = focused;
            _intents = next;
            var validReferences = next.Select(ReferenceKey.From).ToHashSet();
            foreach (var key in _references.Keys.Where(key => !validReferences.Contains(key)).ToArray())
            {
                _references.Remove(key);
                changed = true;
            }
            if (_active is { IsReference: true } reference &&
                !validReferences.Contains(ReferenceKey.From(reference.Intent)))
                cancel = reference.Cancellation;
        }
        CancelQuery(cancel);
        if (changed)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ObserveLocation(FDriverRouteLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        CancellationTokenSource? cancel = null;
        lock (_gate)
        {
            if (!IsUsableLocation(location, _time.GetUtcNow()) ||
                (_lastLocationRecordedAtUtc is not null && location.RecordedAtUtc <= _lastLocationRecordedAtUtc))
                return;
            _location = location;
            _lastLocationRecordedAtUtc = location.RecordedAtUtc;
            if (_current is { IsEstimated: false } route && location.AccuracyMeters is >= 0 and <= 50)
            {
                _offRouteSamples = DistanceFromPolylineMeters(location.Latitude, location.Longitude, route.Points) > 50
                    ? Math.Min(2, _offRouteSamples + 1)
                    : 0;
            }
            if (_active is { IsReference: true } && IsFocusedQueryDueLocked(_time.GetUtcNow()))
                cancel = _active.Cancellation;
        }
        CancelQuery(cancel);
    }

    /// <summary>위치가 끊기면 현재 건의 경로를 지우고, 새 GPS를 받을 때 즉시 다시 조회한다.</summary>
    public void ForgetLocation()
    {
        CancellationTokenSource? cancel = null;
        lock (_gate)
        {
            if (_location is null && _current is null && _lastError is null && _retryAtUtc is null)
                return;
            _location = null;
            _current = null;
            _lastError = null;
            _retryAtUtc = null;
            _offRouteSamples = 0;
            _focusNeedsQuery = _focused is not null;
            if (_active is { IsReference: false })
                cancel = _active.Cancellation;
        }
        CancelQuery(cancel);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>명시적인 화면 새로고침. 이미 같은 현재 건을 조회 중이면 해당 요청을 재사용한다.</summary>
    public Task RefreshAsync()
    {
        CancellationTokenSource? cancel = null;
        lock (_gate)
        {
            if (_focused is not null &&
                !(_active is { IsReference: false } active && IsCurrentWorkLocked(active)))
            {
                _focusNeedsQuery = true;
                _retryAtUtc = null;
                cancel = _active?.Cancellation;
            }
        }
        CancelQuery(cancel);
        return TickAsync();
    }

    /// <summary>매초 호출해도 조회는 하나씩 실행하며, 현재 건 다음에 참고 경로를 처리한다.</summary>
    public Task TickAsync()
    {
        TaskCompletionSource? completion = null;
        CancellationTokenSource? cancel = null;
        Task task;
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(_accountId))
                return Task.CompletedTask;
            if (_active is { IsReference: true } && IsFocusedQueryDueLocked(_time.GetUtcNow()))
                cancel = _active.Cancellation;
            if (_pumpTask is null)
            {
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _pumpTask = completion.Task;
            }
            task = _pumpTask;
        }
        CancelQuery(cancel);
        if (completion is not null)
            _ = PumpAsync(completion);
        return task;
    }

    public void Clear()
    {
        CancellationTokenSource? cancel;
        lock (_gate)
        {
            ResetOwnerLocked(string.Empty, 0);
            _revision++;
            cancel = _active?.Cancellation;
        }
        CancelQuery(cancel);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task PumpAsync(TaskCompletionSource completion)
    {
        try
        {
            while (true)
            {
                QueryWork? work;
                lock (_gate)
                {
                    work = NextWorkLocked(_time.GetUtcNow());
                    if (work is null)
                    {
                        _pumpTask = null;
                        break;
                    }
                    _active = work;
                }
                Changed?.Invoke(this, EventArgs.Empty);
                FoodDeliveryDriverRouteResponseDto? response = null;
                Exception? error = null;
                try
                {
                    response = await _query(work.Request, work.Cancellation.Token).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    error = exception;
                }

                var changed = false;
                lock (_gate)
                {
                    _active = null;
                    if (work.OwnerGeneration == _ownerGeneration)
                    {
                        if (work.IsReference)
                        {
                            var key = ReferenceKey.From(work.Intent);
                            if (_intents.Any(intent => ReferenceKey.From(intent) == key) &&
                                (response is not null || !work.Cancellation.IsCancellationRequested))
                            {
                                var stored = response is null ? null : Store(work, response, _time.GetUtcNow());
                                _references[key] = new(stored,
                                    stored is { IsEstimated: false } ? null : _time.GetUtcNow() + RetryInterval);
                                changed = true;
                            }
                        }
                        else if (IsCurrentWorkLocked(work) && !work.Cancellation.IsCancellationRequested)
                        {
                            if (response is not null)
                                _current = Store(work, response, _time.GetUtcNow());
                            var failed = response is null || error is not null || _current is null || _current.IsEstimated;
                            _retryAtUtc = failed ? _time.GetUtcNow() + RetryInterval : null;
                            _lastError = failed ? "도로 경로를 확인하지 못했습니다. 잠시 후 다시 확인합니다." : null;
                            _offRouteSamples = 0;
                            changed = true;
                        }
                    }
                }
                work.Cancellation.Dispose();
                if (changed)
                    Changed?.Invoke(this, EventArgs.Empty);
            }
            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _active = null;
                _pumpTask = null;
            }
            completion.TrySetException(exception);
        }
    }

    private QueryWork? NextWorkLocked(DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(_accountId))
            return null;
        if (IsFocusedQueryDueLocked(now))
        {
            _focusNeedsQuery = false;
            return CreateWork(_focused!, false, _location!.Latitude, _location.Longitude,
                _focused!.TargetLatitude, _focused.TargetLongitude, _focused.TargetLabel, now);
        }
        // 좌표가 같은 픽업→전달 참고 경로는 GPS 이동이나 단계 변경으로 재조회하지 않는다.
        foreach (var intent in _intents.Where(intent => intent.OfferId != _focused?.OfferId))
        {
            if (!IsValidCoordinate(intent.PickupLatitude, intent.PickupLongitude) ||
                !IsValidCoordinate(intent.DropoffLatitude, intent.DropoffLongitude))
                continue;
            var entry = _references.GetValueOrDefault(ReferenceKey.From(intent));
            if (entry is null || (entry.RetryAtUtc is not null && now >= entry.RetryAtUtc))
                return CreateWork(intent, true, intent.PickupLatitude, intent.PickupLongitude,
                    intent.DropoffLatitude, intent.DropoffLongitude, "전달지", now);
        }
        return null;
    }

    private bool IsFocusedQueryDueLocked(DateTimeOffset now)
    {
        if (_focused is null || _location is null || !IsUsableLocation(_location, now))
            return false;
        if (_focusNeedsQuery)
            return true;
        if (_retryAtUtc is not null)
            return now >= _retryAtUtc;
        return _current is { IsEstimated: false } route && now - route.ReceivedAtUtc >= MinimumQueryInterval &&
            (DistanceMeters(route.OriginLatitude, route.OriginLongitude, _location.Latitude, _location.Longitude) >= 100 ||
             _offRouteSamples >= 2);
    }

    private bool IsCurrentWorkLocked(QueryWork work) =>
        work.OwnerGeneration == _ownerGeneration && work.Revision == _revision &&
        FocusKey.From(work.Intent) == FocusKey.From(_focused);

    private QueryWork CreateWork(FDriverMapRouteIntent intent, bool reference,
        decimal originLatitude, decimal originLongitude, decimal targetLatitude, decimal targetLongitude,
        string label, DateTimeOffset now) => new(_ownerGeneration, _revision, intent, reference,
            new FoodDeliveryDriverRouteRequestDto
            {
                StartLatitude = originLatitude,
                StartLongitude = originLongitude,
                Stops = [new() { Latitude = targetLatitude, Longitude = targetLongitude, Label = label }]
            }, now, new CancellationTokenSource());

    private void ResetOwnerLocked(string accountId, long lifetimeId)
    {
        _ownerGeneration++;
        _accountId = accountId ?? string.Empty;
        _lifetimeId = lifetimeId;
        _references.Clear();
        _intents = [];
        _focused = null;
        _location = null;
        _lastLocationRecordedAtUtc = null;
        _current = null;
        _focusNeedsQuery = false;
        _offRouteSamples = 0;
        _lastError = null;
        _retryAtUtc = null;
    }

    private static FDriverStoredMapRoute Store(QueryWork work, FoodDeliveryDriverRouteResponseDto response, DateTimeOffset now)
    {
        var sourcePoints = response.Points ?? [];
        var invalidPoints = sourcePoints.Any(point => point is null || !IsValidCoordinate(point.Latitude, point.Longitude));
        var points = sourcePoints.Where(point => point is not null).Select(point => new FoodDeliveryDriverRoutePointDto
        {
            Latitude = point.Latitude,
            Longitude = point.Longitude
        }).ToArray();
        var estimated = response.IsEstimated || string.IsNullOrWhiteSpace(response.Source) || points.Length < 2 || invalidPoints;
        return new(work.Intent.OfferId, work.Intent.Phase, work.IsReference, response.Source, estimated,
            work.Request.StartLatitude, work.Request.StartLongitude, work.RequestedAtUtc, now,
            response.DistanceKm, response.DurationMinutes, points);
    }

    private static bool IsValidIntent(FDriverMapRouteIntent intent) =>
        !string.IsNullOrWhiteSpace(intent.OfferId) && Enum.IsDefined(intent.Phase) &&
        IsValidCoordinate(intent.TargetLatitude, intent.TargetLongitude);

    private static bool IsUsableLocation(FDriverRouteLocation location, DateTimeOffset now) =>
        IsValidCoordinate(location.Latitude, location.Longitude) &&
        (location.AccuracyMeters is null || location.AccuracyMeters is >= 0 and <= 50) &&
        now - location.RecordedAtUtc <= LocationMaxAge && location.RecordedAtUtc - now <= LocationFutureTolerance;

    public static bool IsValidCoordinate(decimal latitude, decimal longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && latitude != 0 && longitude != 0;

    public static double DistanceMeters(decimal latitude1, decimal longitude1, decimal latitude2, decimal longitude2)
    {
        const double radius = 6_371_000;
        var lat1 = (double)latitude1 * Math.PI / 180;
        var lat2 = (double)latitude2 * Math.PI / 180;
        var dLat = lat2 - lat1;
        var dLng = ((double)longitude2 - (double)longitude1) * Math.PI / 180;
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dLng / 2), 2);
        return radius * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    public static double DistanceFromPolylineMeters(decimal latitude, decimal longitude,
        IReadOnlyList<FoodDeliveryDriverRoutePointDto> points)
    {
        const double metersPerRadian = 6_371_000;
        var cosine = Math.Cos((double)latitude * Math.PI / 180);
        var minimum = double.PositiveInfinity;
        for (var index = 1; index < points.Count; index++)
        {
            var first = points[index - 1];
            var second = points[index];
            if (!IsValidCoordinate(first.Latitude, first.Longitude) || !IsValidCoordinate(second.Latitude, second.Longitude))
                continue;
            var x1 = LongitudeDelta((double)first.Longitude - (double)longitude) * Math.PI / 180 * metersPerRadian * cosine;
            var y1 = ((double)first.Latitude - (double)latitude) * Math.PI / 180 * metersPerRadian;
            var x2 = LongitudeDelta((double)second.Longitude - (double)longitude) * Math.PI / 180 * metersPerRadian * cosine;
            var y2 = ((double)second.Latitude - (double)latitude) * Math.PI / 180 * metersPerRadian;
            var dx = x2 - x1;
            var dy = y2 - y1;
            var lengthSquared = dx * dx + dy * dy;
            var fraction = lengthSquared == 0 ? 0 : Math.Clamp(-(x1 * dx + y1 * dy) / lengthSquared, 0, 1);
            minimum = Math.Min(minimum, Math.Sqrt(Math.Pow(x1 + fraction * dx, 2) + Math.Pow(y1 + fraction * dy, 2)));
        }
        return minimum;
    }

    private static double LongitudeDelta(double value) => (value + 540) % 360 - 180;

    private static void CancelQuery(CancellationTokenSource? cancellation)
    {
        try
        {
            cancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 완료와 취소가 경합하면 이미 종료된 조회에는 취소를 다시 전달할 필요가 없다.
        }
    }

    private sealed record FocusKey(string OfferId, FoodMapRoutePhase Phase, decimal Latitude, decimal Longitude)
    {
        public static FocusKey? From(FDriverMapRouteIntent? intent) => intent is null ? null :
            new(intent.OfferId, intent.Phase, intent.TargetLatitude, intent.TargetLongitude);
    }

    private sealed record ReferenceKey(string OfferId, decimal PickupLatitude, decimal PickupLongitude,
        decimal DropoffLatitude, decimal DropoffLongitude)
    {
        public static ReferenceKey From(FDriverMapRouteIntent intent) => new(intent.OfferId,
            intent.PickupLatitude, intent.PickupLongitude, intent.DropoffLatitude, intent.DropoffLongitude);
    }

    private sealed record ReferenceEntry(FDriverStoredMapRoute? Route, DateTimeOffset? RetryAtUtc);

    private sealed record QueryWork(long OwnerGeneration, long Revision, FDriverMapRouteIntent Intent,
        bool IsReference, FoodDeliveryDriverRouteRequestDto Request, DateTimeOffset RequestedAtUtc,
        CancellationTokenSource Cancellation);
}
