using DriverApp.Models.Driver.Samples;

namespace DriverApp.Services;

/// <summary>Owns one visible native home read, including the driver session and page lifetime.</summary>
public sealed class DriverNativeHomeWorkspaceState(IDriverSampleDataService samples, IAuthSession session)
{
    private CancellationTokenSource? _lifetime;
    private long _generation;
    private long _sessionRevision;
    private string? _owner;
    private bool _subscribed;

    public bool IsLoading { get; private set; }
    public bool IsReady { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public 기사운송샘플항목? CurrentTransport { get; private set; }
    public event Action? Invalidated;

    public async Task ActivateAsync()
    {
        Deactivate();
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
            if (!session.IsAuthenticated)
            {
                ErrorMessage = "로그인 후 현재 운송을 확인해 주세요.";
                return;
            }

            _owner = session.UserId;
            _sessionRevision = session.SessionRevision;
            session.Changed += OnSessionChanged;
            _subscribed = true;
            // Every return must read the server; a cached transport may still say '상차 전'.
            await samples.RefreshAsync(cancellationToken, force: true);
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsCurrent(generation)) return;
            CurrentTransport = samples.현재운송조회();
            IsReady = true;
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

    public void Deactivate()
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
        IsLoading = false;
        IsReady = false;
        CurrentTransport = null;
    }

    private bool IsCurrent(long generation)
        => generation == _generation && session.IsAuthenticated
           && session.SessionRevision == _sessionRevision
           && string.Equals(session.UserId, _owner, StringComparison.Ordinal);

    private void OnSessionChanged()
    {
        if (session.IsAuthenticated && session.SessionRevision == _sessionRevision
            && string.Equals(session.UserId, _owner, StringComparison.Ordinal)) return;
        Deactivate();
        ErrorMessage = "로그인 정보가 변경되었습니다. 현재 운송을 다시 확인해 주세요.";
        Invalidated?.Invoke();
    }
}
