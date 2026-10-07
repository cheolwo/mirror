using CommunityToolkit.Mvvm.ComponentModel;
using DriverApp.Services;
using Ssalddel.Contracts.Driver.Transport;

namespace DriverApp.ViewModels.Driver.Transport;

/// <summary>내역의 계정·페이지·최신 조회 수명만 소유하며 운송 명령을 실행하지 않습니다.</summary>
public sealed class 기사운송내역PageViewModel : ObservableObject, IDisposable
{
    private readonly IDriverTransportApiService _api;
    private readonly IAuthSession _auth;
    private readonly object _stateGate = new();
    private readonly CancellationTokenSource _pageCancellation = new();
    private readonly CancellationToken _pageToken;
    private CancellationTokenSource? _queryCancellation;
    private IReadOnlyList<기사운송요약응답> _rows = [];
    private long _queryVersion;
    private long _observedSession;
    private long _rowsSession = -1;
    private bool _disposed;
    private bool _loginRequired;

    public 기사운송내역PageViewModel(IDriverTransportApiService api, IAuthSession auth)
    {
        _api = api;
        _auth = auth;
        _observedSession = auth.SessionRevision;
        _pageToken = _pageCancellation.Token;
        _auth.Changed += OnAuthenticationChanged;
    }

    public bool 불러오는중 { get; private set; } = true;
    public bool 로그인필요 => _loginRequired || !_auth.IsAuthenticated;
    public string? 오류메시지 { get; private set; }
    public IReadOnlyList<기사운송요약응답> 운송목록
        => !_disposed && _auth.IsAuthenticated && _rowsSession == _auth.SessionRevision ? _rows : [];

    public async Task InitializeAsync()
    {
        try { await _auth.RestoreAsync(_pageToken); }
        catch (OperationCanceledException) when (_pageToken.IsCancellationRequested) { return; }
        catch (Exception)
        {
            lock (_stateGate)
            {
                if (_disposed) return;
                불러오는중 = false;
                오류메시지 = "로그인 정보를 확인하지 못했습니다. 다시 시도해 주세요.";
            }
            OnPropertyChanged(string.Empty);
            return;
        }
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        CancellationTokenSource query;
        CancellationTokenSource? previous;
        long version;
        long session;
        bool authenticated;
        lock (_stateGate)
        {
            if (_disposed) return;
            query = CancellationTokenSource.CreateLinkedTokenSource(_pageToken);
            previous = _queryCancellation;
            _queryCancellation = query;
            version = ++_queryVersion;
            session = _auth.SessionRevision;
            _observedSession = session;
            authenticated = _auth.IsAuthenticated;
            _loginRequired = false;
            _rows = [];
            _rowsSession = -1;
            오류메시지 = null;
            불러오는중 = authenticated;
        }
        Cancel(previous);
        OnPropertyChanged(string.Empty);
        try
        {
            if (!authenticated) return;
            var rows = await _api.목록조회Async(query.Token);
            lock (_stateGate)
            {
                if (!IsCurrent(query, version, session)) return;
                _rows = rows.ToArray();
                _rowsSession = session;
            }
        }
        catch (OperationCanceledException) when (query.IsCancellationRequested) { }
        catch (Exception ex)
        {
            lock (_stateGate)
            {
                if (!IsCurrent(query, version, session)) return;
                _loginRequired = ex is UnauthorizedAccessException;
                오류메시지 = _loginRequired ? null
                    : "배달 내역을 불러오지 못했습니다. 네트워크와 서버 연결을 확인한 뒤 다시 시도해 주세요.";
            }
        }
        finally
        {
            var notify = false;
            lock (_stateGate)
            {
                if (!_disposed && ReferenceEquals(_queryCancellation, query) && version == _queryVersion)
                {
                    _queryCancellation = null;
                    불러오는중 = false;
                    notify = true;
                }
            }
            query.Dispose();
            if (notify) OnPropertyChanged(string.Empty);
        }
    }

    private bool IsCurrent(CancellationTokenSource query, long version, long session)
        => !_disposed && !query.IsCancellationRequested && ReferenceEquals(_queryCancellation, query)
           && version == _queryVersion && _auth.IsAuthenticated && session == _auth.SessionRevision;

    private void OnAuthenticationChanged()
    {
        CancellationTokenSource? previous;
        lock (_stateGate)
        {
            if (_disposed || _auth.IsAuthenticated && _observedSession == _auth.SessionRevision) return;
            _observedSession = _auth.SessionRevision;
            ++_queryVersion;
            previous = _queryCancellation;
            _queryCancellation = null;
            _rows = [];
            _rowsSession = -1;
            _loginRequired = !_auth.IsAuthenticated;
            불러오는중 = false;
            오류메시지 = _auth.IsAuthenticated
                ? "로그인 정보가 변경되었습니다. 다시 시도해 현재 계정의 내역을 확인해 주세요." : null;
        }
        Cancel(previous);
        OnPropertyChanged(string.Empty);
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (_stateGate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_queryVersion;
            previous = _queryCancellation;
            _queryCancellation = null;
            _rows = [];
            _rowsSession = -1;
            불러오는중 = false;
            오류메시지 = null;
        }
        _auth.Changed -= OnAuthenticationChanged;
        Cancel(previous);
        _pageCancellation.Cancel();
        _pageCancellation.Dispose();
    }
}
