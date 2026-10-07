using System.Globalization;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FDriverApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace FDriverApp.PageModels;

/// <summary>One completed delivery and its page-owned, non-persistent private display lifetime.</summary>
public sealed partial class FDriverCompletedDeliveryDetailPageModel : ObservableObject
{
    private readonly IFoodDeliveryDriverApiService _api;
    private readonly IFDriverAuthSession _session;
    private readonly TimeProvider _clock;
    private CancellationTokenSource? _pageCancellation;
    private CancellationTokenSource? _queryCancellation;
    private CancellationTokenSource? _expiryCancellation;
    private string _settlementId = string.Empty;
    private string? _expectedOrderNo;
    private string? _expectedAttemptId;
    private string? _observedAccountId;
    private bool _observedDriverAccess;
    private long _revision;
    private long _activationRevision;
    private bool _active;
    private bool _foreground;
    private bool _needsRefresh;

    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasDetail))]
    private FDriverCompletedDeliveryDetailItem? _detail;
    [ObservableProperty] private string _statusText = "배달 상세를 조회해 주세요.";
    [ObservableProperty] private string _accessNotice = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAuthenticationRequired;
    [ObservableProperty] private bool _hasTarget;
    public bool HasDetail => Detail is not null;

    public FDriverCompletedDeliveryDetailPageModel(IFoodDeliveryDriverApiService api,
        IFDriverAuthSession session, TimeProvider? clock = null)
    {
        _api = api;
        _session = session;
        _clock = clock ?? TimeProvider.System;
    }

    public void SetTarget(string settlementId, string? expectedOrderNo = null, string? expectedAttemptId = null)
    {
        InvalidateRequests();
        ClearDetail();
        _settlementId = settlementId?.Trim() ?? string.Empty;
        _expectedOrderNo = expectedOrderNo;
        _expectedAttemptId = expectedAttemptId;
        HasTarget = !string.IsNullOrWhiteSpace(_settlementId);
        _needsRefresh = HasTarget;
        StatusText = HasTarget ? "배달 상세를 조회해 주세요." : "확인할 배달이 선택되지 않았습니다.";
        AccessNotice = string.Empty;
    }

    public async Task ActivateAsync()
    {
        if (!_active)
        {
            _active = true;
            ++_activationRevision;
            _pageCancellation = new();
            _observedAccountId = _session.UserId;
            _observedDriverAccess = HasDriverSession();
            _session.SessionChanged += OnSessionChanged;
        }
        _foreground = true;
        await RefreshAsync();
    }

    public void Deactivate()
    {
        _active = _foreground = false;
        ++_activationRevision;
        _session.SessionChanged -= OnSessionChanged;
        InvalidateRequests();
        _pageCancellation?.Cancel();
        _pageCancellation?.Dispose();
        _pageCancellation = null;
        ClearDetail();
        _needsRefresh = HasTarget;
        AccessNotice = string.Empty;
        StatusText = HasTarget ? "배달 상세를 조회해 주세요." : "확인할 배달이 선택되지 않았습니다.";
    }

    public void Pause()
    {
        if (!_active) return;
        _foreground = false;
        _needsRefresh = HasTarget;
        InvalidateRequests();
        Detail?.ClearPrivateDetails();
        if (HasTarget) AccessNotice = "앱에 돌아오면 주문·고객 정보 열람 권한을 다시 확인합니다.";
    }

    public async Task ResumeAsync()
    {
        if (!_active) return;
        _foreground = true;
        if (_needsRefresh) await RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_active || !_foreground || _pageCancellation is not { IsCancellationRequested: false } pageLifetime) return;
        if (!HasTarget)
        {
            InvalidateRequests();
            ClearDetail();
            StatusText = "확인할 배달이 선택되지 않았습니다.";
            return;
        }
        if (!HasDriverSession())
        {
            InvalidateRequests();
            ClearDetail();
            ShowSessionRequiredNotice();
            return;
        }

        InvalidateRequests();
        Detail?.ClearPrivateDetails();
        IsAuthenticationRequired = false;
        AccessNotice = "주문·고객 정보 열람 권한을 확인하고 있습니다.";
        IsLoading = true;
        StatusText = "배달 상세를 불러오고 있습니다.";
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(pageLifetime.Token);
        _queryCancellation = lifetime;
        var revision = Volatile.Read(ref _revision);
        var target = _settlementId;
        var owner = _session.UserId;
        var expectedOrder = _expectedOrderNo;
        var expectedAttempt = _expectedAttemptId;
        var startedAt = _clock.GetTimestamp();
        _needsRefresh = false;
        try
        {
            var response = await _api.GetCompletedDeliveryDetailAsync(target, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (!IsCurrent(revision, target, owner)) return;
            var row = response.Settlement;
            if (row is null || row.DriverId != owner || row.SettlementId != target
                || (expectedOrder is not null && row.OrderNo != expectedOrder)
                || (expectedAttempt is not null && row.DeliveryAttemptId != expectedAttempt))
                throw new FDriverApiException("선택한 배달의 상세 정보를 확인하지 못했습니다.", null);

            Detail?.ClearPrivateDetails();
            Detail = new() { Settlement = FDriverDailySettlementItem.From(row) };
            StatusText = "선택한 배달의 정산 내역입니다.";
            var responseTimestamp = _clock.GetTimestamp();
            var remaining = response.DetailExpiresAtUtc.HasValue
                && response.DetailExpiresAtUtc.Value.Kind == DateTimeKind.Utc
                && response.DetailExpiresAtUtc.Value <= DateTime.MaxValue.AddHours(-9)
                && response.ServerNowUtc != default && response.ServerNowUtc.Kind == DateTimeKind.Utc
                ? response.DetailExpiresAtUtc.Value - response.ServerNowUtc
                    - _clock.GetElapsedTime(startedAt, responseTimestamp)
                : TimeSpan.Zero;
            if (response.DetailAccessStatusCode != "Allowed" || remaining <= TimeSpan.Zero)
            {
                AccessNotice = response.DetailAccessStatusCode switch
                {
                    "PolicyNotConfigured" => "주문·고객 정보의 열람 기간이 설정되지 않았습니다. 정산 내역은 확인할 수 있습니다.",
                    "CompletionEvidenceUnavailable" => "완료 기록을 확인하지 못해 주문·고객 정보를 표시할 수 없습니다.",
                    "Expired" or "Allowed" => "주문·고객 정보 열람 기간이 지났거나 기한을 확인하지 못했습니다. 정산 내역은 계속 확인할 수 있습니다.",
                    _ => "주문·고객 정보 열람 권한을 확인하지 못했습니다. 다시 조회해 주세요."
                };
                return;
            }

            Detail.ApplyPrivateDetails(response);
            AccessNotice = "주문·고객 정보 열람 기한: "
                + response.DetailExpiresAtUtc!.Value.AddHours(9).ToString("M/d HH:mm", CultureInfo.InvariantCulture)
                + " (한국 시간)";
            var expiryLifetime = CancellationTokenSource.CreateLinkedTokenSource(pageLifetime.Token);
            _expiryCancellation = expiryLifetime;
            _ = ExpireAsync(revision, target, owner, remaining, responseTimestamp, expiryLifetime);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (FDriverApiException ex) when (!lifetime.IsCancellationRequested)
        {
            if (!IsCurrent(revision, target, owner)) return;
            Detail?.ClearPrivateDetails();
            // Arbitrary backend error text must not copy private information back into the screen.
            StatusText = "배달 상세를 조회하지 못했습니다. 다시 조회해 주세요.";
            AccessNotice = "주문·고객 정보를 표시할 수 없습니다.";
            if (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                ClearDetail();
                _observedDriverAccess = false;
                IsAuthenticationRequired = true;
                StatusText = ex.StatusCode == HttpStatusCode.Unauthorized
                    ? "로그인이 만료되었습니다. 다시 로그인해 주세요."
                    : "기사 권한이 있는 계정으로 로그인해 주세요.";
                if (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    try { await _session.ClearAsync(); }
                    catch (Exception clearError) when (clearError is not OperationCanceledException
                        && _session.CurrentState == ClientAuthSessionRestoreState.Anonymous)
                    {
                        // Session memory was already cleared; storage removal failure never preserves private UI.
                    }
                }
            }
        }
        finally
        {
            if (revision == Volatile.Read(ref _revision)) IsLoading = false;
            if (ReferenceEquals(_queryCancellation, lifetime)) _queryCancellation = null;
        }
    }

    private void OnSessionChanged(object? sender, EventArgs args)
    {
        var previousOwner = _observedAccountId;
        var previousDriverAccess = _observedDriverAccess;
        var owner = _session.UserId;
        _observedAccountId = owner;
        _observedDriverAccess = HasDriverSession();
        // An unchanged driver identity preserves its original deadline during token refresh.
        // Losing the role must cancel in-flight work before a queued UI callback can run.
        if (previousOwner == owner && previousDriverAccess && _observedDriverAccess) return;
        InvalidateRequests(updateLoading: false);
        var revision = Volatile.Read(ref _revision);
        var activation = _activationRevision;
        _ = MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (!_active || activation != _activationRevision || revision != Volatile.Read(ref _revision) || owner != _session.UserId) return;
            IsLoading = false;
            ClearDetail();
            _needsRefresh = HasTarget;
            if (!HasDriverSession())
            {
                ShowSessionRequiredNotice();
                return;
            }
            IsAuthenticationRequired = false;
            StatusText = previousOwner == owner
                ? "기사 권한을 확인했습니다. 다시 조회해 주세요."
                : "계정이 바뀌었습니다. 다시 조회해 주세요.";
            AccessNotice = string.Empty;
        });
    }

    private bool HasAuthenticatedIdentity() => _session.CurrentState != ClientAuthSessionRestoreState.Anonymous
        && !string.IsNullOrWhiteSpace(_session.UserId);

    private bool HasDriverSession() => HasAuthenticatedIdentity()
        && (_session.Roles.Contains("Driver", StringComparer.OrdinalIgnoreCase)
            || _session.Roles.Contains("기사", StringComparer.OrdinalIgnoreCase));

    private void ShowSessionRequiredNotice()
    {
        _observedDriverAccess = false;
        IsAuthenticationRequired = true;
        StatusText = HasAuthenticatedIdentity()
            ? "기사 권한이 있는 계정으로 로그인해 주세요." : "기사 로그인이 필요합니다.";
        AccessNotice = "로그인한 기사 본인의 완료 배달만 확인할 수 있습니다.";
    }

    private bool IsCurrent(long revision, string target, string? owner)
        => revision == Volatile.Read(ref _revision) && _active && _foreground && target == _settlementId
            && owner == _session.UserId && HasDriverSession();

    private async Task ExpireAsync(long revision, string target, string? owner, TimeSpan remaining,
        long responseTimestamp, CancellationTokenSource lifetime)
    {
        var startedAt = _clock.GetTimestamp();
        remaining -= _clock.GetElapsedTime(responseTimestamp, startedAt);
        try
        {
            while (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : remaining, _clock, lifetime.Token);
                remaining -= _clock.GetElapsedTime(startedAt);
                startedAt = _clock.GetTimestamp();
            }
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                // Expiry must clear the bound view even if a token became anonymous without an event.
                if (lifetime.IsCancellationRequested || revision != Volatile.Read(ref _revision) || target != _settlementId
                    || !_active || !_foreground) return;
                Detail?.ClearPrivateDetails();
                AccessNotice = "주문·고객 정보 열람 기간이 지났습니다. 정산 내역은 계속 확인할 수 있습니다.";
            });
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        finally
        {
            if (ReferenceEquals(_expiryCancellation, lifetime)) _expiryCancellation = null;
            lifetime.Dispose();
        }
    }

    private void InvalidateRequests(bool updateLoading = true)
    {
        Interlocked.Increment(ref _revision);
        CancelSource(_queryCancellation);
        CancelSource(_expiryCancellation);
        _queryCancellation = _expiryCancellation = null;
        if (updateLoading) IsLoading = false;
    }

    private static void CancelSource(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { /* A completed request or expiry worker already released this source. */ }
    }

    private void ClearDetail()
    {
        Detail?.ClearPrivateDetails();
        Detail = null;
    }
}
