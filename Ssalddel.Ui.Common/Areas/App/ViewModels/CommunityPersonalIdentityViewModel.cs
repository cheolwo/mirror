using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public enum CommunityPersonalIdentityState { Restoring, Anonymous, Authenticated, Failed }
public sealed record CommunityPersonalIdentitySnapshot(CommunityPersonalIdentityState State, string DisplayName = "");

/// <summary>기존 로그인을 복원해 개인 화면의 표시만 갱신하며 업무 권한이나 전문 자격은 부여하지 않습니다.</summary>
public sealed class CommunityPersonalIdentityViewModel : IDisposable
{
    private readonly ISsalddel현재사용자Context currentUser;
    private readonly ISsalddel현재사용자변경알림 source;
    private readonly Func<CancellationToken, Task> restore;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private long generation;
    private bool disposed;

    public CommunityPersonalIdentityViewModel(ISsalddel현재사용자Context currentUser,
        ISsalddel현재사용자변경알림 source, Func<CancellationToken, Task> restore)
    {
        this.currentUser = currentUser; this.source = source; this.restore = restore;
        source.Changed += SourceChanged;
    }

    public CommunityPersonalIdentitySnapshot Snapshot { get; private set; } = new(CommunityPersonalIdentityState.Restoring);
    public event Action? Changed;

    public async Task RestoreAsync()
    {
        long operation;
        lock (gate)
        {
            if (disposed) return;
            operation = ++generation;
            Snapshot = new(CommunityPersonalIdentityState.Restoring);
        }
        Changed?.Invoke();
        try
        {
            await restore(lifetime.Token);
            lock (gate)
            {
                if (disposed || operation != generation) return;
                Snapshot = ReadCurrent();
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
        catch
        {
            lock (gate)
            {
                if (disposed || operation != generation) return;
                Snapshot = new(CommunityPersonalIdentityState.Failed);
            }
        }
        Changed?.Invoke();
    }

    private void SourceChanged()
    {
        lock (gate)
        {
            if (disposed) return;
            ++generation;
            Snapshot = ReadCurrent();
        }
        Changed?.Invoke();
    }

    private CommunityPersonalIdentitySnapshot ReadCurrent()
    {
        var user = currentUser.현재사용자;
        return user.인증됨
            ? new(CommunityPersonalIdentityState.Authenticated, string.IsNullOrWhiteSpace(user.UserName) ? "로그인 사용자" : user.UserName.Trim())
            : new(CommunityPersonalIdentityState.Anonymous);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true; ++generation;
            Snapshot = new(CommunityPersonalIdentityState.Anonymous);
        }
        source.Changed -= SourceChanged;
        lifetime.Cancel(); lifetime.Dispose();
    }
}
