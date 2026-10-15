using Ssalddel.Contracts.Common.Transport;
using Ssalddel.Contracts.Driver.Work;

namespace DriverApp.Services;

public sealed record DriverWorkStartRecoveryResult(bool ServerConfirmed, bool LocationStarted, string Message);

/// <summary>
/// One explicit start POST is followed by a canonical read. Unknown outcomes are read again, never silently reposted.
/// Account/session/page cancellation invalidates late replies before local location transmission can begin.
/// </summary>
public sealed class DriverWorkStartRecoveryCoordinator : IDisposable
{
    private readonly IDriverWorkApiService _api;
    private readonly IAuthSession _session;
    private readonly IDriverWorkStartRecoveryStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _active;
    private string? _activeOwner;
    private long _activeRevision;
    private bool _disposed;
    public bool IsBusy { get; private set; }
    public bool IsServerConfirmed { get; private set; }
    public string ResolutionState { get; private set; } = "Unknown";
    public DriverWorkStartRecoveryRecord? Pending { get; private set; }
    public event Action? Changed;

    public DriverWorkStartRecoveryCoordinator(IDriverWorkApiService api, IAuthSession session, IDriverWorkStartRecoveryStore store)
    {
        _api = api; _session = session; _store = store;
        _session.Changed += SessionChanged;
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        var owner = Owner(); var revision = _session.SessionRevision;
        _activeOwner = owner; _activeRevision = revision;
        var pending = await _store.LoadAsync(owner, cancellationToken);
        EnsureCurrent(owner, revision, cancellationToken);
        Pending = pending;
        var current = await ReadCanonicalAsync(owner, revision, cancellationToken);
        IsServerConfirmed = current is not null && IsCargoRunning(current, owner);
        ResolutionState = pending is not null ? "PendingCanonicalCheck" : IsServerConfirmed ? "Confirmed" : "Unknown";
        Changed?.Invoke();
    }

    public async Task<DriverWorkStartRecoveryResult> StartOrRecoverAsync(
        Func<CancellationToken, Task<기사운행시작요청?>> createRequest,
        Func<string, CancellationToken, Task> startLocation,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var owner = Owner(); var revision = _session.SessionRevision;
            using var active = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _active = active; _activeOwner = owner; _activeRevision = revision; var token = active.Token;
            IsBusy = true; Changed?.Invoke();
            Pending = await _store.LoadAsync(owner, token);
            EnsureCurrent(owner, revision, token);
            IsServerConfirmed = false; ResolutionState = "CheckingCanonicalState";

            if (Pending is null)
            {
                // A return to the page must not create another shift while one is already active.
                var existing = await ReadCanonicalAsync(owner, revision, token);
                if (existing is null)
                    return Unknown("현재 운행 상태를 확인하지 못했습니다. 새 운행 요청을 보내지 않았습니다.");
                if (IsCargoRunning(existing, owner))
                    return await ContinueLocationAsync(owner, revision, existing, startLocation, token,
                        "서버의 기존 운행을 확인하고 위치 송신을 이어갑니다. 공개 설정은 새로 변경하지 않았습니다.");
                if (existing.Status != "대기")
                    return Unknown("현재 운행 상태가 새 시작을 허용하는지 확인되지 않았습니다. 새 요청을 보내지 않았습니다.");

                var request = await createRequest(token);
                EnsureCurrent(owner, revision, token);
                if (request is null) return Unknown("운행 시작에 필요한 위치 또는 권한을 확인해 주세요.");
                request.ClientRequestId = Guid.NewGuid();
                Pending = new(owner, request.ClientRequestId.Value, DateTime.UtcNow,
                    request.커뮤니티운행공개, request.커뮤니티운행공개 && request.커뮤니티구단위위치공개동의);
                // Do not send a POST unless recovery identity is safely saved first.
                await _store.SaveAsync(Pending, token);
                EnsureCurrent(owner, revision, token); Changed?.Invoke();
                try { await _api.운행시작Async(request, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (DriverApiException error) when (error.RejectedWorkStartRequestId == Pending.ClientRequestId
                    && (int)error.StatusCode is >= 400 and < 500 and not 408 and not 429)
                {
                    EnsureCurrent(owner, revision, token);
                    Pending = Pending with { RejectedClientRequestId = Pending.ClientRequestId, RejectionStatusCode = (int)error.StatusCode };
                    await _store.SaveAsync(Pending, token);
                    ResolutionState = "RejectedAwaitingCanonicalCheck";
                }
                catch (Exception)
                {
                    // 표식 없는 4xx·5xx·timeout은 원문 문자열로 거절을 추정하지 않는다.
                    ResolutionState = "StartResponseUnconfirmed";
                }
                EnsureCurrent(owner, revision, token);
            }

            var canonical = await ReadCanonicalAsync(owner, revision, token);
            if (canonical is null)
                return Unknown("운행 시작 결과가 미확인입니다. ‘현재 운행 확인’으로 같은 요청의 상태를 확인해 주세요. 새 시작은 보내지 않습니다.");
            var pending = Pending!;
            if (canonical.Status == "대기" && pending.RejectedClientRequestId == pending.ClientRequestId
                && pending.RejectionStatusCode is >= 400 and < 500 and not 408 and not 429)
            {
                await _store.RemoveAsync(owner, token); EnsureCurrent(owner, revision, token);
                Pending = null; IsServerConfirmed = false; ResolutionState = "ConfirmedRejected"; Changed?.Invoke();
                return Unknown("운행 시작이 거절됐고 현재 대기 상태임을 확인했습니다. 입력과 권한을 확인한 뒤 다시 시작해 주세요.");
            }
            if (canonical.WorkStartClientRequestId != pending.ClientRequestId || !IsCargoRunning(canonical, owner))
            {
                if (canonical.WorkStartClientRequestId == pending.ClientRequestId && canonical.Status == "대기")
                {
                    await _store.RemoveAsync(owner, token); EnsureCurrent(owner, revision, token);
                    Pending = null; IsServerConfirmed = false; ResolutionState = "ConfirmedStopped"; Changed?.Invoke();
                    return Unknown("해당 운행이 종료된 상태를 확인했습니다. 새 운행은 다시 시작해 주세요.");
                }
                ResolutionState = "PendingCanonicalMismatch";
                return Unknown("저장된 시작 요청과 현재 근무가 일치하지 않습니다. 새 요청이나 위치 송신을 시작하지 않았습니다. 시작 거절 여부가 확인되지 않으면 운영자에게 확인해 주세요.");
            }
            return await ContinueLocationAsync(owner, revision, canonical, startLocation, token,
                "운행 확정을 확인했고 위치 송신을 시작했습니다. 커뮤니티 공개 결과는 별도 조회에서 확인해 주세요.");
        }
        finally
        {
            _active = null; IsBusy = false; Changed?.Invoke(); _gate.Release();
        }
    }

    public async Task ClearAfterStopAsync(CancellationToken cancellationToken = default)
    {
        var owner = Owner(); var revision = _session.SessionRevision;
        await _store.RemoveAsync(owner, cancellationToken); EnsureCurrent(owner, revision, cancellationToken);
        Pending = null; IsServerConfirmed = false; ResolutionState = "ConfirmedStopped"; Changed?.Invoke();
    }

    private async Task<기사운행상태응답?> ReadCanonicalAsync(string owner, long revision, CancellationToken token)
    {
        try
        {
            var current = await _api.운행상태조회Async(token);
            EnsureCurrent(owner, revision, token);
            return current is not null && string.Equals(current.DriverId, owner, StringComparison.Ordinal) ? current : null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { EnsureCurrent(owner, revision, token); return null; }
    }

    private async Task<DriverWorkStartRecoveryResult> ContinueLocationAsync(string owner, long revision,
        기사운행상태응답 canonical, Func<string, CancellationToken, Task> startLocation, CancellationToken token, string message)
    {
        try
        {
            EnsureCurrent(owner, revision, token);
            IsServerConfirmed = true; ResolutionState = "ConfirmedLocationPending"; Changed?.Invoke();
            await startLocation(canonical.Status, token);
            EnsureCurrent(owner, revision, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            EnsureCurrent(owner, revision, token);
            return new(true, false, "서버 운행은 확정됐지만 위치 송신을 시작하지 못했습니다. 현재 운행 확인으로 다시 이어갈 수 있습니다.");
        }
        if (Pending is not null)
        {
            // If encrypted removal fails, keep the same recovery request; do not replace it with another POST.
            try { await _store.RemoveAsync(owner, token); EnsureCurrent(owner, revision, token); Pending = null; }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { EnsureCurrent(owner, revision, token); }
        }
        Changed?.Invoke();
        ResolutionState = "Confirmed";
        return new(true, true, message);
    }

    private static bool IsCargoRunning(기사운행상태응답 current, string owner)
        => string.Equals(current.DriverId, owner, StringComparison.Ordinal) && current.ShiftId is > 0
           && current.Status == "운행중" && current.운송실행유형 == 운송실행유형코드.화물운송;
    private static DriverWorkStartRecoveryResult Unknown(string message) => new(false, false, message);
    private string Owner()
        => !_disposed && _session.IsAuthenticated && !string.IsNullOrWhiteSpace(_session.UserId)
            ? _session.UserId! : throw new OperationCanceledException("로그인 후 운행 상태를 확인해 주세요.");
    private void EnsureCurrent(string owner, long revision, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (_disposed || !_session.IsAuthenticated || _session.SessionRevision != revision
            || !string.Equals(_session.UserId, owner, StringComparison.Ordinal))
            throw new OperationCanceledException("로그인 정보 또는 화면이 변경되었습니다.", token);
    }
    private void SessionChanged()
    {
        if (_session.IsAuthenticated && _session.SessionRevision == _activeRevision
            && string.Equals(_session.UserId, _activeOwner, StringComparison.Ordinal)) return;
        _active?.Cancel(); Pending = null; IsServerConfirmed = false; ResolutionState = "Unknown"; Changed?.Invoke();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _session.Changed -= SessionChanged; _active?.Cancel(); Pending = null;
    }
}
