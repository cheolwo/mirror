using RestaurantDeskApp.Models.Restaurant;
using Ssalddel.Contracts.Food;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Services;

// 계정 수명과 진행 요청의 원본 입력·판본·보호 저장을 소유한다. 재전송은 명시 행동에서만 수행한다.
internal sealed class 음식점주문ProgressSession : IDisposable
{
    private readonly object _gate;
    private readonly 음식점주문InboxProjection _inbox;
    private readonly I음식주문ApiClient _foodOrderClient;
    private readonly RestaurantAuthService? _authService;
    private readonly IRestaurantProgressPendingStore _pendingStore;
    private readonly Dictionary<string, Guid> _operationRequestIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RestaurantProgressPendingRequest> _pendingProgress = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _pendingPersistenceGate = new(1, 1);
    private string? _pendingLoadedOwner;
    private string? _cacheOwner;

    public 음식점주문ProgressSession(object gate, 음식점주문InboxProjection inbox,
        I음식주문ApiClient foodOrderClient, RestaurantAuthService? authService,
        IRestaurantProgressPendingStore pendingStore)
    {
        _gate = gate;
        _inbox = inbox;
        _foodOrderClient = foodOrderClient;
        _authService = authService;
        _pendingStore = pendingStore;
        if (_authService is not null)
        {
            _authService.SessionEnding += EndSession;
            _authService.SessionEndingAsync += EndSessionAsync;
        }
    }

    public 음식점주문DeskItem CompleteAcceptance(string orderNo, 음식주문응답 detail, int preparationMinutes)
    {
        lock (_gate)
        {
            var item = _inbox.ApplyAcceptedOrder(orderNo, detail, preparationMinutes);
            RemoveOperationRequestId(orderNo, "acceptance");
            return item;
        }
    }

    public async Task<음식점주문DeskItem?> 진행변경Async(
        string 주문번호,
        string 작업,
        int? 조리예상분,
        string 사유,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(주문번호);

        var actor = CaptureActor();
        await RestorePendingProgressAsync(actor, cancellationToken);
        var key = ProgressKey(actor.Owner, 주문번호, 작업);
        RestaurantProgressPendingRequest snapshot;
        bool recovering;
        await _pendingPersistenceGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCurrentActor(actor, cancellationToken);
            lock (_gate) recovering = _pendingProgress.TryGetValue(key, out snapshot!);
            if (!recovering)
            {
                snapshot = new(주문번호.Trim(), Guid.NewGuid(), GetExpectedRevision(주문번호, 작업), 작업, 조리예상분, 사유, null);
                RestaurantProgressPendingRequest[] next;
                lock (_gate) next = [.. _pendingProgress.Values, snapshot];
                await SavePendingProgressAsync(actor, next, cancellationToken);
                lock (_gate) _pendingProgress[key] = snapshot;
            }
        }
        finally { _pendingPersistenceGate.Release(); }

        if (recovering)
        {
            // GET 실패는 미접수의 증거가 아니다. 확인 전에는 POST하지 않는다.
            var canonical = await _foodOrderClient.주문상세조회Async(주문번호, cancellationToken);
            EnsureCurrentActor(actor, cancellationToken);
            if (canonical is null || !string.Equals(canonical.주문번호, 주문번호, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("이전 진행 변경의 결과를 확인하지 못했습니다. 같은 주문을 다시 조회해 주세요.");
            await ResolveConfirmedProgressAsync(canonical, actor, cancellationToken);
            var current = _inbox.UpsertServerOrder(canonical, 음식점주문복구출처.서버재조회);
            if (canonical.상태이력.Any(history => history.클라이언트요청Id == snapshot.RequestId)) return current;
        }

        // HTTP 재시도와 명시 재시도 모두 처음의 입력/판본을 사용한다.
        var request = snapshot.ToRequest();

        음식주문응답? detail;
        try
        {
            // 응답 유실 뒤 자동 재송신하지 않습니다. 다음 명시 행동이 GET 후 같은 요청을 사용합니다.
            detail = await _foodOrderClient.음식점진행변경Async(주문번호, request, cancellationToken);
            EnsureCurrentActor(actor, cancellationToken);
        }
        catch (SsalddelApiException ex) when (
            ex.RequiresStateRefresh
            && 업무복구행동목록.포함(ex.AvailableRecoveryActions, 업무복구행동Ids.상태전체재조회))
        {
            var refreshed = await TryRefreshCanonicalOrderAsync(주문번호, actor, cancellationToken);
            // 명시 판본 충돌은 이번 입력이 거절된 응답이다. 정본 확인 후 다음 변경은 새 요청이다.
            if (refreshed && ex.StatusCode == 409)
                await RemoveProgressRequestAsync(key, snapshot.RequestId, actor, cancellationToken);
            throw;
        }
        if (detail is null)
        {
            return null;
        }

        if (!string.Equals(detail.주문번호, 주문번호, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("진행 변경 응답의 주문번호가 요청과 다릅니다. 같은 주문을 다시 조회해 주세요.");
        await RemoveProgressRequestAsync(key, snapshot.RequestId, actor, cancellationToken);
        return _inbox.UpsertServerOrder(
            detail,
            음식점주문복구출처.서버재조회);
    }

    private async Task<bool> TryRefreshCanonicalOrderAsync(
        string orderNo,
        (string Owner, long Generation) actor,
        CancellationToken cancellationToken)
    {
        try
        {
            var canonical = await _foodOrderClient.주문상세조회Async(orderNo, cancellationToken);
            EnsureCurrentActor(actor, cancellationToken);
            if (canonical is not null && string.Equals(canonical.주문번호, orderNo, StringComparison.OrdinalIgnoreCase))
            {
                await ResolveConfirmedProgressAsync(canonical, actor, cancellationToken);
                _inbox.UpsertServerOrder(canonical, 음식점주문복구출처.서버재조회);
                return true;
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or RestaurantPendingStorageException)
        {
            throw;
        }
        catch
        {
            // 원래 명령 실패를 보존한다. 다음 수신함 조회가 다시 정본 상태를 복원한다.
        }
        return false;
    }

    public (string Owner, long Generation) CaptureActor()
    {
        var owner = _authService?.Session.UserId ?? string.Empty;
        if (_authService is not null && (!_authService.Session.IsAuthenticated || string.IsNullOrWhiteSpace(owner)))
            throw new UnauthorizedAccessException("음식점 로그인이 필요합니다.");
        lock (_gate)
        {
            if (_cacheOwner != owner)
            {
                _cacheOwner = owner;
                _inbox.Clear();
                _operationRequestIds.Clear();
                _pendingProgress.Clear();
                _pendingLoadedOwner = null;
            }
        }
        return (owner, _authService?.SessionGeneration ?? 0);
    }

    public void EnsureCurrentActor((string Owner, long Generation) actor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _authService?.ThrowIfActorChanged(actor.Owner, actor.Generation);
    }

    private static string ProgressKey(string owner, string orderNo, string operation)
        => $"{owner}\u001f{orderNo.Trim()}::{operation}";

    public async Task RestorePendingProgressAsync((string Owner, long Generation) actor, CancellationToken cancellationToken)
    {
        await _pendingPersistenceGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCurrentActor(actor, cancellationToken);
            if (_pendingLoadedOwner == actor.Owner) return;
            RestaurantProgressPendingSnapshot? saved;
            try
            {
                saved = await _pendingStore.LoadAsync(cancellationToken);
                EnsureCurrentActor(actor, cancellationToken);
                if (saved is not null && saved.OwnerId != actor.Owner)
                {
                    await _pendingStore.ClearAsync(saved.OwnerId, cancellationToken);
                    EnsureCurrentActor(actor, cancellationToken);
                    saved = null;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new RestaurantPendingStorageException(ex); }
            lock (_gate)
            {
                _pendingProgress.Clear();
                foreach (var pending in saved?.Requests ?? [])
                    _pendingProgress[ProgressKey(actor.Owner, pending.OrderNo, pending.Operation)] = pending;
                _pendingLoadedOwner = actor.Owner;
            }
        }
        finally { _pendingPersistenceGate.Release(); }
    }

    private async Task SavePendingProgressAsync((string Owner, long Generation) actor,
        IReadOnlyList<RestaurantProgressPendingRequest> next, CancellationToken cancellationToken)
    {
        EnsureCurrentActor(actor, cancellationToken);
        try
        {
            await _pendingStore.SaveAsync(new(actor.Owner, next), cancellationToken);
            EnsureCurrentActor(actor, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _pendingLoadedOwner = null;
            // 다른 계정의 늦은 저장은 다음 로그인에 복원하지 않습니다.
            if (_authService?.Session.UserId is { } nextOwner && nextOwner != actor.Owner)
                await _pendingStore.ClearAsync(actor.Owner, CancellationToken.None);
            throw;
        }
        catch (Exception ex) { _pendingLoadedOwner = null; throw new RestaurantPendingStorageException(ex); }
    }

    public async Task ResolveConfirmedProgressAsync(음식주문응답 detail,
        (string Owner, long Generation) actor, CancellationToken cancellationToken)
    {
        foreach (var operation in 음식점주문진행작업코드.전체)
        {
            var key = ProgressKey(actor.Owner, detail.주문번호, operation);
            RestaurantProgressPendingRequest? pending;
            lock (_gate) _pendingProgress.TryGetValue(key, out pending);
            if (pending is not null && detail.상태이력.Any(history => history.클라이언트요청Id == pending.RequestId))
                await RemoveProgressRequestAsync(key, pending.RequestId, actor, cancellationToken);
        }
    }

    private async Task RemoveProgressRequestAsync(string key, Guid requestId,
        (string Owner, long Generation) actor, CancellationToken cancellationToken)
    {
        await _pendingPersistenceGate.WaitAsync(cancellationToken);
        try
        {
            EnsureCurrentActor(actor, cancellationToken);
            RestaurantProgressPendingRequest[] next;
            lock (_gate)
            {
                if (!_pendingProgress.TryGetValue(key, out var pending) || pending.RequestId != requestId) return;
                next = _pendingProgress.Where(x => x.Key != key).Select(x => x.Value).ToArray();
            }
            await SavePendingProgressAsync(actor, next, cancellationToken);
            lock (_gate) _pendingProgress.Remove(key);
        }
        finally { _pendingPersistenceGate.Release(); }
    }

    private async Task EndSessionAsync(bool explicitLogout, CancellationToken cancellationToken)
    {
        if (!explicitLogout) return;
        var owner = _authService?.Session.UserId ?? string.Empty;
        await _pendingPersistenceGate.WaitAsync(cancellationToken);
        try
        {
            try { await _pendingStore.ClearAsync(owner, cancellationToken); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { throw new RestaurantPendingStorageException(ex); }
        }
        finally { _pendingPersistenceGate.Release(); }
    }

    private void EndSession(bool explicitLogout)
    {
        lock (_gate)
        {
            _inbox.Clear();
            _operationRequestIds.Clear();
            if (explicitLogout)
            {
                _pendingProgress.Clear();
                _cacheOwner = null;
                _pendingLoadedOwner = null;
            }
        }
    }

    public void Dispose()
    {
        if (_authService is not null)
        {
            _authService.SessionEnding -= EndSession;
            _authService.SessionEndingAsync -= EndSessionAsync;
        }
        EndSession(explicitLogout: true);
    }


    private long? GetExpectedRevision(string orderNo, string operation)
    {
        var actionId = operation switch
        {
            음식점주문진행작업코드.조리시작 => 음식배달가능행동Ids.음식점조리시작,
            음식점주문진행작업코드.거절 => 음식배달가능행동Ids.음식점주문거절,
            음식점주문진행작업코드.조리시간변경 => 음식배달가능행동Ids.음식점조리시간변경,
            음식점주문진행작업코드.픽업준비 => 음식배달가능행동Ids.음식점픽업준비완료,
            _ => null
        };
        if (actionId is null)
        {
            return null;
        }

        return _inbox.GetExpectedRevision(orderNo, actionId);
    }

    public Guid GetOperationRequestId(string orderNo, string operation)
    {
        var key = $"{orderNo.Trim()}::{operation}";
        lock (_gate)
        {
            if (_operationRequestIds.TryGetValue(key, out var existing))
            {
                return existing;
            }

            var created = Guid.NewGuid();
            _operationRequestIds[key] = created;
            return created;
        }
    }

    private void RemoveOperationRequestId(string orderNo, string operation)
    {
        var key = $"{orderNo.Trim()}::{operation}";
        lock (_gate)
        {
            _operationRequestIds.Remove(key);
        }
    }

}
