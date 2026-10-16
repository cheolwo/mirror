using DriverApp.Models.Driver.Samples;
using DriverApp.Models.Driver;
using Ssalddel.Contracts.Common.Dispatch;

namespace DriverApp.Services;

/// <summary>한 전경·기사 계정의 정본 조회와 마지막 확인 업무를 소유합니다. 추천 선택은 현재 수행을 바꾸지 않습니다.</summary>
public sealed class DriverNativeHomeWorkspaceState(IDriverSampleDataService samples, IAuthSession session,
    IDriverOperationalDispatchApiService? dispatchApi = null, TimeProvider? timeProvider = null)
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private CancellationTokenSource? _lifetime;
    private long _generation;
    private long _sessionRevision;
    private string? _owner;
    private bool _subscribed;
    private Task? _refreshTask;
    private Task? _sourceRead;
    private DateTimeOffset _nextRefreshAt;
    private 운영배차수신상태Dto? _availability;
    private 운영배차수신의사변경요청? _pendingIntent;
    private readonly HashSet<(string Id, int Round)> _presented = [];
    private readonly HashSet<(string Id, int Round)> _presentationAttempts = [];
    private (string Id, int Round)? _unconfirmedAcceptance;

    public long Generation => _generation;
    public CancellationToken LifetimeToken => _lifetime?.Token ?? new CancellationToken(true);
    public string? UnconfirmedAcceptanceRequestId => _unconfirmedAcceptance?.Id;
    public bool IsActive { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsChangingIntent { get; private set; }
    public bool HasStaleTransport => !IsReady && CurrentTransport is not null;
    public string ErrorMessage { get; private set; } = string.Empty;
    public 기사운송샘플항목? CurrentTransport { get; private set; }
    public DateTimeOffset? VerifiedAtUtc { get; private set; }
    // 정산·예약 조회 안내는 각각의 전용 화면에서 다룹니다. 지도는 지금 수행할 업무의 확인 실패만 표시합니다.
    public IReadOnlyList<string> MapWarnings => new[]
        {
            samples.ReadState.Work, samples.ReadState.CurrentTransport, samples.ReadState.TransportDetail,
            samples.ReadState.Recommendations, samples.ReadState.Realtime
        }
        .Where(x => x.StateCode == DriverWorkspaceReadStatus.Failed).Select(x => x.Message).ToArray();
    public bool ReceptionKnown => _availability?.수신의사Code is 운영배차수신의사Code.On or 운영배차수신의사Code.Off;
    public bool AutomaticallyPaused => ReceptionKnown && _availability!.미응답자동중지여부;
    public bool ReceivesNewDispatches => ReceptionKnown && !AutomaticallyPaused
        && _availability!.수신의사Code == 운영배차수신의사Code.On;
    public string ReceptionNotice => AutomaticallyPaused
        ? "연속 3회 미응답으로 신규 배차가 꺼졌습니다. 다시 켜면 받을 수 있습니다."
        : _pendingIntent is not null ? "수신 변경 결과를 다시 확인하고 있습니다."
        : ReceptionKnown ? string.Empty : "신규 배차 수신 상태를 확인하고 있습니다.";
    public bool CanChangeReception => IsActive && IsReady && ReceptionKnown && !IsLoading && !IsChangingIntent
        && samples.ReadState.Work.IsCurrent;
    public bool CanRespondToRecommendation => IsActive && IsReady && !IsLoading && !IsChangingIntent
        && _pendingIntent is null && ReceivesNewDispatches && samples.ReadState.Work.IsCurrent
        && _unconfirmedAcceptance is null && _availability!.수신의사변경시각Utc.HasValue
        && _availability.실효상태Code != 운영배차실효상태Code.서버일시정지
        && (!_availability.실효상태변경시각Utc.HasValue || _availability.실효상태Code == 운영배차실효상태Code.배차가능)
        && samples.근무상태.운행상태 is "운행중" or "운행 중" && samples.ReadState.Recommendations.IsCurrent;
    public event Action? Invalidated;

    public async Task ActivateAsync()
    {
        if (IsActive) { await RefreshAsync(); return; }
        DeactivateCore(preserveCurrent: SameOwner());
        var generation = _generation;
        var lifetime = new CancellationTokenSource();
        var cancellationToken = lifetime.Token;
        _lifetime = lifetime;
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            await session.RestoreAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != _generation) return;
            if (!session.IsAuthenticated)
            {
                CurrentTransport = null; VerifiedAtUtc = null;
                ErrorMessage = "로그인 후 현재 운송을 확인해 주세요.";
                return;
            }

            if (!SameOwner()) { CurrentTransport = null; VerifiedAtUtc = null; }
            _owner = session.UserId;
            _sessionRevision = session.SessionRevision;
            session.Changed += OnSessionChanged;
            _subscribed = true;
            IsActive = true;
            await RefreshAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception) when (generation != _generation)
        {
            // A departed page or old driver must not publish a response, including its failure.
        }
        catch (Exception)
        {
            ErrorMessage = "현재 운송을 불러오지 못했습니다. 연결을 확인한 뒤 다시 시도해 주세요.";
        }
        finally
        {
            if (generation == _generation) IsLoading = false;
        }
    }

    /// <summary>명시 재조회와 복귀는 즉시 확인하며, 동시에 들어온 요청은 같은 작업을 기다립니다.</summary>
    public Task RefreshAsync()
    {
        if (!IsActive || _lifetime is null) return Task.CompletedTask;
        if (_refreshTask is { IsCompleted: false }) return _refreshTask;
        _refreshTask = ReadCurrentAsync(_generation, _lifetime);
        return _refreshTask;
    }

    public async Task<bool> PollAsync()
    {
        if (!IsActive || IsChangingIntent || _clock.GetUtcNow() < _nextRefreshAt) return false;
        await RefreshAsync(); return true;
    }

    private async Task ReadCurrentAsync(long generation, CancellationTokenSource lifetime)
    {
        var token = lifetime.Token;
        IsLoading = true; IsReady = false; _nextRefreshAt = _clock.GetUtcNow() + RefreshInterval;
        try
        {
            if (_sourceRead is { IsCompleted: false })
                throw new InvalidOperationException("이전 조회 응답을 기다리고 있습니다.");
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
            bounded.CancelAfter(ReadTimeout);
            _sourceRead = samples.RefreshAsync(bounded.Token, force: true);
            await _sourceRead.WaitAsync(bounded.Token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrent(generation)) return;
            // 서버 조회 실패는 예외 대신 실패한 slice로 반환될 수도 있습니다.
            if (!samples.ReadState.CurrentTransport.IsCurrent)
                throw new InvalidOperationException("현재 운송의 최신 상태를 확인하지 못했습니다.");
            CurrentTransport = samples.현재운송조회(); VerifiedAtUtc = _clock.GetUtcNow();
            if (_unconfirmedAcceptance is { } pendingAcceptance
                && samples.운송목록.Any(x => x.의뢰Id == pendingAcceptance.Id)) _unconfirmedAcceptance = null;
            ErrorMessage = string.Empty; IsReady = true; _presentationAttempts.Clear();
            if (dispatchApi is not null)
            {
                try
                {
                    var value = await dispatchApi.GetAvailabilityAsync(bounded.Token).WaitAsync(bounded.Token);
                    token.ThrowIfCancellationRequested();
                    if (!IsCurrent(generation)) return;
                    if (!string.Equals(value.주체Id, _owner, StringComparison.Ordinal))
                        throw new InvalidOperationException("현재 기사의 배차 수신 상태를 확인하지 못했습니다.");
                    _availability = value;
                    if (_pendingIntent is { } pending && Confirms(pending, value)) _pendingIntent = null;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception) { if (IsCurrent(generation)) _availability = null; }
            }
        }
        catch (Exception) when (!IsCurrent(generation)) { }
        catch (Exception)
        {
            IsReady = false;
            ErrorMessage = CurrentTransport is null
                ? "현재 운송을 확인하지 못했습니다. 다시 확인한 뒤 업무를 진행해 주세요."
                : "현재 운송 재조회 실패 · 마지막 확인 업무입니다. 다시 확인한 뒤 업무를 진행해 주세요.";
        }
        finally { if (generation == _generation) IsLoading = false; }
    }

    public async Task ToggleReceptionAsync()
    {
        if (!CanChangeReception || dispatchApi is null || _lifetime is null) return;
        var generation = _generation; var token = _lifetime.Token;
        _pendingIntent ??= new()
        {
            클라이언트요청Id = Guid.NewGuid(),
            수신의사Code = ReceivesNewDispatches ? 운영배차수신의사Code.Off : 운영배차수신의사Code.On,
            미응답자동중지해제 = !ReceivesNewDispatches
        };
        IsChangingIntent = true;
        try
        {
            using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token);
            bounded.CancelAfter(ReadTimeout);
            await dispatchApi.ChangeIntentAsync(_pendingIntent, bounded.Token).WaitAsync(bounded.Token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrent(generation)) return;
            _pendingIntent = null; // 같은 ID의 2xx는 이전 요청을 종결합니다. 새 중지는 다음 GET을 따릅니다.
        }
        catch (Exception) when (!IsCurrent(generation)) { return; }
        catch (Exception) { /* 응답 유실은 같은 요청 ID를 보존하고 정본을 다시 확인합니다. */ }
        finally { if (generation == _generation) IsChangingIntent = false; }
        if (IsCurrent(generation)) await RefreshAsync();
    }

    public async Task AcknowledgePresentedAsync(string requestId, int round, CancellationToken cancellationToken = default)
    {
        if (!CanRespondToRecommendation || round <= 0 || dispatchApi is null || _lifetime is null) return;
        var offer = samples.추천의뢰목록.FirstOrDefault(x => x.의뢰Id == requestId && x.추천라운드 == round);
        if (offer is null || !offer.서버지정추천 || offer.추천만료시각 is not { } expiry
            || DriverRecommendationSelectionState.NormalizeServerUtc(expiry) <= _clock.GetUtcNow().UtcDateTime) return;
        var key = (requestId, round);
        if (_presented.Contains(key) || !_presentationAttempts.Add(key)) return;
        var generation = _generation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, cancellationToken);
        linked.CancelAfter(ReadTimeout);
        try
        {
            await dispatchApi.AcknowledgeOfferPresentationAsync(requestId, round, linked.Token).WaitAsync(linked.Token);
            if (IsCurrent(generation) && !linked.IsCancellationRequested) _presented.Add(key);
        }
        catch (Exception) { /* 표시 확인 실패는 현재 운송과 새 제안 표시를 막지 않습니다. */ }
    }

    public void KeepUnconfirmedAcceptance(string requestId, int round)
    {
        if (IsActive && SameOwner()) _unconfirmedAcceptance = (requestId, round);
    }
    public void ConfirmAcceptanceResponse(string requestId)
    {
        if (IsActive && SameOwner() && _unconfirmedAcceptance?.Id == requestId) _unconfirmedAcceptance = null;
    }
    public void ObserveAcceptanceFailure(string requestId, int round, Exception error)
    {
        var rejected = error is DriverApiException api && (int)api.StatusCode is >= 400 and < 500
            && (int)api.StatusCode is not (408 or 429);
        if (rejected) ConfirmAcceptanceResponse(requestId);
        else KeepUnconfirmedAcceptance(requestId, round);
    }

    public void Pause() => DeactivateCore(preserveCurrent: SameOwner());
    public void Deactivate() => DeactivateCore(preserveCurrent: false);
    private void DeactivateCore(bool preserveCurrent)
    {
        ++_generation;
        if (_subscribed)
        {
            session.Changed -= OnSessionChanged;
            _subscribed = false;
        }
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        _refreshTask = null; _sourceRead = null;
        IsActive = false; IsChangingIntent = false;
        IsLoading = false;
        IsReady = false;
        _availability = null; _presentationAttempts.Clear();
        if (!preserveCurrent)
        { CurrentTransport = null; VerifiedAtUtc = null; _pendingIntent = null; _unconfirmedAcceptance = null; _presented.Clear(); }
    }

    private bool IsCurrent(long generation)
        => generation == _generation && IsActive && SameOwner();

    private bool SameOwner() => session.IsAuthenticated && _owner is not null
        && session.SessionRevision == _sessionRevision && session.UserId == _owner;
    private static bool Confirms(운영배차수신의사변경요청 request, 운영배차수신상태Dto value)
        => request.수신의사Code == value.수신의사Code && (!request.미응답자동중지해제 || !value.미응답자동중지여부);

    private void OnSessionChanged()
    {
        if (session.IsAuthenticated && session.SessionRevision == _sessionRevision
            && string.Equals(session.UserId, _owner, StringComparison.Ordinal)) return;
        Deactivate();
        ErrorMessage = "로그인 정보가 변경되었습니다. 현재 운송을 다시 확인해 주세요.";
        Invalidated?.Invoke();
    }
}
