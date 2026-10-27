using DriverApp.Models.Driver;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Common.Notifications;
using Ssalddel.Contracts.Common.Transport;
using DriverWorkKinds = Ssalddel.Contracts.Driver.Settings.DriverWorkKinds;
using Ssalddel.Contracts.Driver.Notification;

namespace DriverApp.Services;

public sealed record DriverCargoRecommendationAlert(string OwnerId, long SessionRevision, string RequestId,
    int RecommendationRound, DateTimeOffset ExpiresAtUtc, string WorkKind, NotificationAlertPlan Plan);

public sealed record DriverCargoPublishedAlert(string RequestId, int Round, DateTimeOffset ExpiresAtUtc);

public static class DriverCargoRecommendationPublicationGate
{
    public static bool AllowsCargoAlert(기사알림설정응답? settings)
        => settings is { 배차추천알림사용: true, 화물운송추천알림사용: true };

    public static bool IsDuplicate(IEnumerable<DriverCargoPublishedAlert> alerts, string requestId, int round, DateTimeOffset now)
        => alerts.Any(x => x.RequestId == requestId && x.Round == round && x.ExpiresAtUtc > now);

    public static IReadOnlyList<DriverCargoPublishedAlert> Record(IEnumerable<DriverCargoPublishedAlert> alerts,
        string requestId, int round, DateTimeOffset expiresAtUtc, DateTimeOffset now)
        => alerts.Where(x => x.ExpiresAtUtc > now && (x.RequestId != requestId || x.Round != round))
            .Append(new(requestId, round, expiresAtUtc)).TakeLast(100).ToArray();
}

public interface IDriverCargoRecommendationAlertPublisher
{
    bool Publish(DriverCargoRecommendationAlert alert, Func<bool> canPublish, CancellationToken cancellationToken);
    void Cancel();
}

public interface IDriverCargoRecommendationAlertService
{
    Task<bool> TryPublishAsync(string ownerId, long sessionRevision, DriverRequestItem request,
        CancellationToken cancellationToken = default);
    void Cancel();
}

/// <summary>정본 복구 뒤 본인 설정과 수신 의사를 읽어 알림만 게시합니다. 배차·수락 명령을 보내지 않습니다.</summary>
public sealed class DriverCargoRecommendationAlertService : IDriverCargoRecommendationAlertService, IDisposable
{
    private readonly IAuthSession _auth;
    private readonly IDriverSampleDataService _samples;
    private readonly IDriverNotificationApiService _settings;
    private readonly IDriverOperationalDispatchApiService _dispatch;
    private readonly IDriverCargoRecommendationAlertPublisher _publisher;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private long _generation;
    private string? _owner;
    private long _revision;

    public DriverCargoRecommendationAlertService(IAuthSession auth, IDriverSampleDataService samples,
        IDriverNotificationApiService settings, IDriverOperationalDispatchApiService dispatch,
        IDriverCargoRecommendationAlertPublisher publisher, TimeProvider? timeProvider = null)
    {
        _auth = auth; _samples = samples; _settings = settings; _dispatch = dispatch; _publisher = publisher;
        _time = timeProvider ?? TimeProvider.System;
        _owner = auth.UserId; _revision = auth.SessionRevision;
        _auth.Changed += OnSessionChanged;
    }

    public async Task<bool> TryPublishAsync(string ownerId, long sessionRevision, DriverRequestItem request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var generation = Interlocked.Read(ref _generation);
        var workKind = CargoWorkKind(request.WorkKind ?? request.운송의뢰유형코드);
        if (workKind is null || !CanPublish(ownerId, sessionRevision, generation, request)) return false;

        // Both canonical push recovery and the foreground map use these gates, never the OS publisher.
        기사알림설정응답? settings;
        try { settings = await _settings.설정조회Async(cancellationToken); }
        catch { CancelIfCurrent(ownerId, sessionRevision, generation, cancellationToken); throw; }
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrent(ownerId, sessionRevision, generation)) return false;
        if (settings is null || settings.DriverId != ownerId || !DriverCargoRecommendationPublicationGate.AllowsCargoAlert(settings))
        { CancelIfCurrent(ownerId, sessionRevision, generation, cancellationToken); return false; }
        운영배차수신상태Dto availability;
        try { availability = await _dispatch.GetAvailabilityAsync(cancellationToken); }
        catch { CancelIfCurrent(ownerId, sessionRevision, generation, cancellationToken); throw; }
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsCurrent(ownerId, sessionRevision, generation)) return false;
        if (availability.주체Id != ownerId || availability.수신의사Code != 운영배차수신의사Code.On
            || availability.미응답자동중지여부 || !availability.수신의사변경시각Utc.HasValue
            || availability.실효상태Code != 운영배차실효상태Code.배차가능)
        { CancelIfCurrent(ownerId, sessionRevision, generation, cancellationToken); return false; }
        var plan = NotificationAlertPolicy.Evaluate(new(true, settings.소리사용, settings.진동사용,
            settings.야간알림제한), _time.GetUtcNow());
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CanPublish(ownerId, sessionRevision, generation, request)) return false;
            var alert = new DriverCargoRecommendationAlert(ownerId, sessionRevision, request.의뢰Id,
                request.추천라운드, Expiry(request), workKind, plan);
            return _publisher.Publish(alert,
                () => !cancellationToken.IsCancellationRequested && CanPublish(ownerId, sessionRevision, generation, request),
                cancellationToken);
        }
    }

    public void Cancel()
    {
        lock (_gate) { Interlocked.Increment(ref _generation); _publisher.Cancel(); }
    }

    private void CancelIfCurrent(string ownerId, long revision, long generation, CancellationToken token)
    {
        // Serialize the final cancellation check with Publish. A departed foreground read must not
        // pass its check, wait behind a newer Push publication, and then remove that newer alert.
        lock (_gate)
        {
            if (!IsCurrent(ownerId, revision, generation) || token.IsCancellationRequested) return;
            Interlocked.Increment(ref _generation);
            _publisher.Cancel();
        }
    }

    private bool CanPublish(string ownerId, long revision, long generation, DriverRequestItem request)
    {
        if (!IsCurrent(ownerId, revision, generation) || !DriverRecommendationPushHintParser.ValidId(request.의뢰Id)
            || !DriverRecommendationSelectionState.IsLive(request, _time.GetUtcNow().UtcDateTime)
            || !_samples.ReadState.Recommendations.IsCurrent || !_samples.ReadState.Work.IsCurrent
            || _samples.근무상태?.운행상태 is not ("운행중" or "운행 중")) return false;
        var canonical = _samples.추천의뢰조회(request.의뢰Id);
        return canonical is not null && DriverRecommendationSelectionState.IsLive(canonical, _time.GetUtcNow().UtcDateTime)
            && canonical.추천라운드 == request.추천라운드
            && Expiry(canonical) == Expiry(request) && canonical.추천시작시각 == request.추천시작시각
            && CargoWorkKind(canonical.WorkKind ?? canonical.운송의뢰유형코드)
                == CargoWorkKind(request.WorkKind ?? request.운송의뢰유형코드);
    }

    private bool IsCurrent(string ownerId, long revision, long generation) => _auth.IsAuthenticated
        && _auth.UserId == ownerId && _auth.SessionRevision == revision && Interlocked.Read(ref _generation) == generation
        && _auth.Roles.Any(x => x == "기사" || string.Equals(x, "Driver", StringComparison.OrdinalIgnoreCase));

    private static DateTimeOffset Expiry(DriverRequestItem request)
        => new(DriverRecommendationSelectionState.NormalizeServerUtc(request.추천만료시각!.Value));

    private static string? CargoWorkKind(string? kind) => kind switch
    {
        DriverWorkKinds.QuickDelivery or 운송실행상세유형코드.마트라스트마일배송 => DriverWorkKinds.QuickDelivery,
        DriverWorkKinds.CargoTransport or 운송실행상세유형코드.일반화물운송 or 운송실행상세유형코드.창고출고화물운송
            or 운송실행상세유형코드.같이주문국내배송 or 운송실행상세유형코드.같이수입국내인계 => DriverWorkKinds.CargoTransport,
        _ => null
    };

    private void OnSessionChanged()
    {
        if (_auth.IsAuthenticated && _owner == _auth.UserId && _revision == _auth.SessionRevision) return;
        Cancel(); _owner = _auth.UserId; _revision = _auth.SessionRevision;
    }

    public void Dispose() { _auth.Changed -= OnSessionChanged; Cancel(); }
}

public sealed class NoopDriverCargoRecommendationAlertPublisher : IDriverCargoRecommendationAlertPublisher
{
    public bool Publish(DriverCargoRecommendationAlert alert, Func<bool> canPublish, CancellationToken cancellationToken) => false;
    public void Cancel() { }
}
