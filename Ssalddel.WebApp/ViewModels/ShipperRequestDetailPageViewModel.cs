using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.WebApp.Services;

namespace Ssalddel.WebApp.ViewModels;

/// <summary>
/// Web 상세 route의 인증, feature flag와 서버 원장 재조회를 담당합니다.
/// 표현과 책임별 화면은 Ui.Common Screen이 맡습니다.
/// </summary>
public sealed class ShipperRequestDetailPageViewModel : IDisposable
{
    private const string DomesticTransportFeatureKey = "DomesticTransportWorkflow";

    private readonly 화주결제정산Service _settlementService;
    private readonly WebAuthSessionService _authSession;
    private readonly ICommunityProcurementClient _workflowMetadata;
    private readonly object _queryGate = new();
    private CancellationTokenSource? _pendingQuery;
    private long _queryRevision;
    private SessionIdentity _observedSession;
    private SessionIdentity? _queryOwner;
    private bool _disposed;

    public ShipperRequestDetailPageViewModel(
        화주결제정산Service settlementService,
        WebAuthSessionService authSession,
        ICommunityProcurementClient workflowMetadata)
    {
        _settlementService = settlementService;
        _authSession = authSession;
        _workflowMetadata = workflowMetadata;
        _observedSession = CurrentSession();
        _authSession.Changed += OnSessionChanged;
    }

    public ShipperRequestDetailPageState State { get; } = new()
    {
        SourceBoundaryMessage = "Web과 모바일이 같은 서버 운송 의뢰 endpoint와 ID를 다시 조회합니다."
    };

    public async Task LoadAsync(
        string? requestId,
        bool created = false,
        bool showMessage = true,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;
        var normalized = NormalizeRequestId(requestId);
        CancellationTokenSource query;
        CancellationTokenSource? previous;
        long revision;
        lock (_queryGate)
        {
            if (_disposed) return;
            previous = _pendingQuery;
            query = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pendingQuery = query;
            revision = ++_queryRevision;
            _queryOwner = null;
            State.LookupRequestId = normalized ?? string.Empty;
            State.Created = created;
            State.Request = null;
            State.QueryError = null;
            State.RequiresLogin = false;
            State.IsWorkflowEnabled = null;
            State.IsBusy = normalized is not null;
            SetStatus(normalized is null ? "등록 결과에 표시된 운송 의뢰 ID를 입력해 주세요."
                : showMessage ? "서버에서 운송 의뢰 원장을 조회하는 중입니다." : string.Empty,
                ShipperRequestDetailMessageTone.Info);
        }
        Cancel(previous);
        using var ownedQuery = query;
        SessionIdentity? owner = null;
        try
        {
            if (normalized is null) return;
            await _authSession.RestoreAsync(query.Token);
            if (!ApplyIfCurrent(() =>
            {
                owner = CurrentSession();
                _queryOwner = owner;
                State.RequiresLogin = !owner.Value.IsLoggedIn;
                if (State.RequiresLogin)
                    SetStatus("운송 의뢰 상세는 로그인 후 조회할 수 있습니다.", ShipperRequestDetailMessageTone.Warning);
            }) || State.RequiresLogin) return;

            if (!await EnsureWorkflowEnabledAsync(query.Token, ApplyIfCurrent)) return;

            var source = await _settlementService.의뢰조회Async(normalized, query.Token);
            ApplyIfCurrent(() =>
            {
                if (!string.Equals(source.의뢰Id, normalized, StringComparison.Ordinal))
                    throw new InvalidOperationException("조회한 의뢰가 선택한 의뢰와 다릅니다.");
                State.Request = ShipperRequestDetailSnapshot.FromContract(source);
                State.QueryError = null;
                SetStatus(string.Empty, ShipperRequestDetailMessageTone.Success);
            });
        }
        catch (OperationCanceledException) when (query.IsCancellationRequested) { }
        catch (Exception ex)
        {
            ApplyIfCurrent(() =>
            {
                State.Request = null;
                State.QueryError = Api작업오류.변환(ex);
                State.RequiresLogin = IsAuthenticationFailure(ex);
                if (IsFeatureDisabledFailure(ex)) State.IsWorkflowEnabled = false;
                SetStatus(ResolveFailureMessage(ex), ResolveFailureTone(ex));
            });
        }
        finally
        {
            // 자연 만료는 Changed 이벤트 없이도 발생하므로 늦은 응답을 버린 뒤 조회중 상태까지 정리한다.
            OnSessionChanged();
            ApplyIfCurrent(() =>
            {
                State.IsBusy = false;
                if (query.IsCancellationRequested) SetStatus(string.Empty, ShipperRequestDetailMessageTone.Info);
                _pendingQuery = null;
            }, allowCancellation: true);
        }

        bool ApplyIfCurrent(Action apply, bool allowCancellation = false)
        {
            lock (_queryGate)
            {
                if (_disposed || revision != _queryRevision || !ReferenceEquals(_pendingQuery, query)
                    || (!allowCancellation && query.IsCancellationRequested)
                    || (owner.HasValue && owner.Value != CurrentSession())) return false;
                apply();
                return true;
            }
        }
    }

    private async Task<bool> EnsureWorkflowEnabledAsync(CancellationToken cancellationToken,
        Func<Action, bool, bool> applyIfCurrent)
    {
        try
        {
            var metadata = await _workflowMetadata.GetVersionWorkflowMetadataAsync(cancellationToken);
            var enabled = metadata.Flags.TryGetValue(DomesticTransportFeatureKey, out var flag) && flag;
            return applyIfCurrent(() =>
            {
                State.IsWorkflowEnabled = enabled;
                if (!enabled)
                    SetStatus("현재 배포 환경에서는 국내 운송 의뢰 조회가 비활성화되어 있습니다.", ShipperRequestDetailMessageTone.Warning);
            }, false) && enabled;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            applyIfCurrent(() =>
            {
                State.IsWorkflowEnabled = null;
                SetStatus("운송 의뢰 조회 가능 여부를 확인하지 못했습니다. 잠시 후 다시 시도해 주세요.", ShipperRequestDetailMessageTone.Error);
            }, false);
            return false;
        }
    }

    private SessionIdentity CurrentSession() => new(_authSession.UserId, _authSession.AccessToken, _authSession.IsLoggedIn);

    private void OnSessionChanged()
    {
        CancellationTokenSource? previous;
        lock (_queryGate)
        {
            var session = CurrentSession();
            if (_disposed || session == _observedSession) return;
            _observedSession = session;
            // 첫 Restore는 현재 조회의 인증을 확립한다. 소유자 확정 뒤의 변경은 이전 조회를 무효화한다.
            if (_queryOwner is null) return;
            ++_queryRevision;
            previous = _pendingQuery;
            _pendingQuery = null;
            _queryOwner = null;
            State.Request = null;
            State.QueryError = null;
            State.IsBusy = false;
            State.RequiresLogin = !session.IsLoggedIn;
            SetStatus(session.IsLoggedIn ? "계정이 변경되었습니다. 운송 의뢰를 다시 조회해 주세요."
                : "운송 의뢰 상세는 로그인 후 조회할 수 있습니다.", ShipperRequestDetailMessageTone.Warning);
        }
        Cancel(previous);
    }

    public void Dispose()
    {
        CancellationTokenSource? previous;
        lock (_queryGate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_queryRevision;
            previous = _pendingQuery;
            _pendingQuery = null;
            _queryOwner = null;
            State.Request = null;
            State.QueryError = null;
            State.IsBusy = false;
        }
        _authSession.Changed -= OnSessionChanged;
        Cancel(previous);
    }

    private static void Cancel(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private readonly record struct SessionIdentity(string? UserId, string? AccessToken, bool IsLoggedIn);

    private void SetStatus(string message, ShipperRequestDetailMessageTone tone)
    {
        State.StatusMessage = message;
        State.StatusTone = tone;
    }

    private static string? NormalizeRequestId(string? requestId)
        => string.IsNullOrWhiteSpace(requestId) ? null : requestId.Trim();

    private static bool IsAuthenticationFailure(Exception exception)
        => exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized }
           || exception.Message.Contains("서버 인증이 필요", StringComparison.Ordinal);

    private static bool IsFeatureDisabledFailure(Exception exception)
        => exception.Message.Contains("FeatureDisabled", StringComparison.OrdinalIgnoreCase);

    private static ShipperRequestDetailMessageTone ResolveFailureTone(Exception exception)
        => exception is HttpRequestException
           {
               StatusCode: System.Net.HttpStatusCode.NotFound
                   or System.Net.HttpStatusCode.Unauthorized
                   or System.Net.HttpStatusCode.Forbidden
           }
            || IsAuthenticationFailure(exception)
                ? ShipperRequestDetailMessageTone.Warning
                : ShipperRequestDetailMessageTone.Error;

    private static string ResolveFailureMessage(Exception exception)
    {
        if (IsFeatureDisabledFailure(exception))
        {
            return "현재 배포 환경에서는 국내 운송 의뢰 조회가 비활성화되어 있습니다.";
        }

        if (IsAuthenticationFailure(exception))
        {
            return "로그인이 만료되었거나 필요합니다. 다시 로그인한 뒤 조회해 주세요.";
        }

        if (exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Forbidden })
        {
            return "이 운송 의뢰를 조회할 권한이 없습니다.";
        }

        if (exception is HttpRequestException { StatusCode: System.Net.HttpStatusCode.NotFound })
        {
            return "운송 의뢰를 찾을 수 없거나 현재 계정의 조회 대상이 아닙니다.";
        }

        return "운송 의뢰를 불러오지 못했습니다. 서버 연결 상태를 확인한 뒤 다시 시도해 주세요.";
    }
}
