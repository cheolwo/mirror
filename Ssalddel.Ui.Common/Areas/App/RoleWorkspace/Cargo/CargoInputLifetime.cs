using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

/// <summary>독립 입력 화면에서 계정 변경과 늦은 조회 응답을 차단합니다. 초안은 현재 화면 수명만 가집니다.</summary>
public sealed class CargoInputLifetime : IDisposable
{
    private readonly IRoleWorkspaceAccess _access;
    private readonly string _role;
    private readonly Action _clear;
    private RoleWorkspaceIdentity? _owner;
    private CancellationTokenSource? _pending;
    private long _generation;
    private bool _disposed;
    public CargoInputLifetime(IRoleWorkspaceAccess access, string role, Action clear)
    {
        _access = access; _role = role; _clear = clear;
        _access.Changed += SessionChanged;
    }
    public event Action? Changed;
    public bool IsBusy { get; private set; }
    public bool RequiresLogin { get; private set; }
    public bool HasError { get; private set; }
    public string? Message { get; private set; }

    public void Reset()
    {
        ++_generation;
        _pending?.Cancel();
        _pending = null;
        _clear();
        IsBusy = false; HasError = false; Message = null;
    }

    public async Task RunAsync(Func<CancellationToken, Task> work, string? success = null)
    {
        if (_disposed || IsBusy) return;
        using var pending = new CancellationTokenSource();
        _pending = pending;
        var generation = ++_generation;
        IsBusy = true; HasError = false; Message = null; Changed?.Invoke();
        try
        {
            await _access.EnsureInitializedAsync(_role, pending.Token);
            if (_disposed || generation != _generation) return;
            var owner = _access.GetIdentity(_role);
            if (!owner.IsAuthenticated || string.IsNullOrWhiteSpace(owner.OwnerId))
            {
                _clear(); RequiresLogin = true;
                Message = "이 업무를 처리하려면 먼저 로그인해 주세요.";
                return;
            }
            if (_owner is not null && _owner != owner) _clear();
            _owner = owner; RequiresLogin = false;
            await work(pending.Token);
            RequireCurrent(pending.Token);
            if (success is not null) Message = success;
        }
        catch (OperationCanceledException) { }
        catch (RoleWorkspaceAccessException ex) when (ex.StatusCode is 401 or 403)
        {
            if (_disposed || generation != _generation) return;
            _clear(); RequiresLogin = ex.StatusCode == 401; HasError = true;
            Message = ex.StatusCode == 401 ? "로그인이 만료되었습니다. 다시 로그인해 주세요." : "현재 계정으로 이 업무를 처리할 수 없습니다.";
        }
        catch (Exception)
        {
            if (_disposed || generation != _generation) return;
            HasError = true;
            Message = "업무를 처리하지 못했습니다. 연결과 현재 상태를 확인한 뒤 다시 시도해 주세요.";
        }
        finally
        {
            if (!_disposed && generation == _generation)
            {
                _pending = null; IsBusy = false; Changed?.Invoke();
            }
        }
    }

    public void RequireCurrent(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_disposed || _owner is null || _access.GetIdentity(_role) != _owner)
            throw new OperationCanceledException("입력 화면의 계정이 변경되었습니다.", ct);
    }
    public void Notify() => Changed?.Invoke();
    private void SessionChanged()
    {
        if (_disposed) return;
        var identity = _access.GetIdentity(_role);
        if (_owner is null || identity == _owner) return;
        Reset(); _owner = null;
        RequiresLogin = !identity.IsAuthenticated;
        Message = "계정이 변경되었습니다. 업무를 다시 조회해 주세요.";
        Changed?.Invoke();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _access.Changed -= SessionChanged;
        Reset();
    }
}
