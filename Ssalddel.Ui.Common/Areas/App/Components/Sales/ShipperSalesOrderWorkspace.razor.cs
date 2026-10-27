using Microsoft.AspNetCore.Components;
using MudBlazor;
using Ssalddel.Contracts.Common.Sales;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Sales;

public enum ShipperSalesOrderWorkspaceMode
{
    List,
    Detail,
    Combined
}

public partial class ShipperSalesOrderWorkspace
{
    private bool _initialized;
    private bool _initialListStateApplied;
    private bool _disposed;
    private bool _loadingWorkspace;
    private bool _recheckingAccess;
    private bool _identityInvalidated;
    private 현재사용자Snapshot? _loadedUser;
    private ISsalddel현재사용자변경알림? _userChanges;
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    [Parameter]
    public long? OrderId { get; set; }

    [Parameter]
    public ShipperSalesOrderWorkspaceMode Mode { get; set; } = ShipperSalesOrderWorkspaceMode.List;

    [Parameter]
    public string? InitialSearch { get; set; }

    [Parameter]
    public string? InitialSyncScope { get; set; }

    [Parameter]
    public string? InitialStatus { get; set; }

    [Parameter]
    public int InitialPage { get; set; } = 1;

    [Parameter]
    public string HomeHref { get; set; } = "/shipper";

    [Parameter]
    public string HomeLabel { get; set; } = "화주 홈으로";

    [Parameter]
    public string PublicHref { get; set; } = "/community";

    [Parameter]
    public string PublicLabel { get; set; } = "공개 커뮤니티 보기";

    private 판매채널페이지접근ViewModel Access => ViewModel.접근;
    private 판매채널주문목록PageViewModel List => ViewModel.목록;
    private 판매채널주문상세PageViewModel Detail => ViewModel.상세;

    // 재확인 중 동일 사용자의 입력 요소만 유지한다. 서버 권한 판정을 대신하지 않는다.
    private bool LoadedUserMatchesCurrent => SameUser(_loadedUser, Access.현재사용자);
    private bool CanShowWorkspace => !_identityInvalidated && LoadedUserMatchesCurrent &&
        (Access.사용가능 || (_recheckingAccess && Access.화면상태 == 판매채널페이지접근상태.확인전));
    private bool ReadActionsBlocked => List.처리중 || Access.처리중 || _recheckingAccess || !CanShowWorkspace;

    private bool ShowList => Mode is ShipperSalesOrderWorkspaceMode.List or ShipperSalesOrderWorkspaceMode.Combined;
    private bool ShowDetail => Mode is ShipperSalesOrderWorkspaceMode.Detail or ShipperSalesOrderWorkspaceMode.Combined;
    private string WorkspaceLayoutClass => Mode == ShipperSalesOrderWorkspaceMode.Combined
        ? "sales-order-layout"
        : "sales-order-screen";

    private int CurrentPageLineCount => List.주문목록.Sum(item => item.출고라인수);
    private int CurrentPageWarehouseCount => List.주문목록.Sum(item => item.출고창고수);
    private int CurrentPageTransportCount => List.주문목록.Count(item => item.운송인계여부);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _userChanges = Access.현재사용자변경알림;
        if (_userChanges is not null) _userChanges.Changed += CurrentUserChanged;
    }

    private void CurrentUserChanged()
        => _ = InvokeAsync(() =>
        {
            if (_disposed || _loadedUser is null || LoadedUserMatchesCurrent) return;
            // 변경 후 원래 계정으로 돌아오더라도 정본을 새로 읽기 전에는 이전 자료를 재노출하지 않는다.
            _identityInvalidated = true;
            List.작업취소();
            Detail.작업취소();
            StateHasChanged();
        });

    protected override async Task OnInitializedAsync()
    {
        ApplyInitialListState();
        await CheckAccessAndLoadAsync();
        if (!_disposed) _initialized = true;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_disposed || !_initialized || _loadingWorkspace || !CanShowWorkspace || Access.처리중)
        {
            return;
        }

        if (ShowDetail && OrderId is long orderId)
        {
            if (Detail.요청OrderId != orderId)
            {
                await Detail.조회Async(orderId, _lifetimeCancellation.Token);
            }
        }
        else if (ShowDetail && Detail.요청OrderId.HasValue)
        {
            Detail.선택해제();
        }
    }

    private async Task CheckAccessAndLoadAsync()
    {
        if (_disposed) return;
        var token = _lifetimeCancellation.Token;
        var previousUser = _loadedUser;
        _loadingWorkspace = true;
        try
        {
            if (!await Access.확인Async(token) || _disposed || token.IsCancellationRequested || !Access.사용가능)
            {
                return;
            }

            var user = CopyUser(Access.현재사용자);
            if (previousUser is not null && (_identityInvalidated || !SameUser(previousUser, user)))
            {
                List.필터초기화();
                Detail.선택해제();
            }
            var page = List.초기화됨 && SameUser(previousUser, user) ? List.현재페이지 : Math.Max(1, InitialPage);
            var listTask = ShowList ? List.페이지조회Async(page, token) : Task.FromResult(true);
            var detailTask = ShowDetail && OrderId is long orderId
                ? Detail.조회Async(orderId, token)
                : Task.FromResult(true);
            var readResults = await Task.WhenAll(listTask, detailTask);
            if (!_disposed && !token.IsCancellationRequested && SameUser(user, Access.현재사용자)
                && (!_identityInvalidated || readResults.All(result => result)))
            {
                _loadedUser = user;
                _identityInvalidated = false;
            }
        }
        finally
        {
            _loadingWorkspace = false;
            if (!_disposed) StateHasChanged();
        }
    }

    /// <summary>같은 목록의 입력과 페이지를 유지하며 접근 조건과 정본만 다시 확인합니다.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_initialized || !ShowList || Access.처리중 || List.처리중 || _recheckingAccess)
        {
            return;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCancellation.Token);
        _recheckingAccess = true;
        try
        {
            if (!await Access.확인Async(linked.Token) || _disposed || linked.IsCancellationRequested || !Access.사용가능 || _identityInvalidated || !LoadedUserMatchesCurrent)
            {
                return;
            }

            if (!List.처리중) await List.페이지조회Async(List.현재페이지, linked.Token);
        }
        finally
        {
            _recheckingAccess = false;
            if (!_disposed) StateHasChanged();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            if (_userChanges is not null) _userChanges.Changed -= CurrentUserChanged;
            _userChanges = null;
            _lifetimeCancellation.Cancel();
            Access.작업취소();
            List.작업취소();
            Detail.작업취소();
            _lifetimeCancellation.Dispose();
        }

        base.Dispose(disposing);
    }

    private Task RetryAccessAsync() => CheckAccessAndLoadAsync();

    private Task SearchAsync() => ReadActionsBlocked ? Task.CompletedTask : List.조회Async();

    private Task ReloadListAsync() => ReadActionsBlocked ? Task.CompletedTask : List.페이지조회Async(List.현재페이지);

    private Task ChangePageAsync(int page) => ReadActionsBlocked ? Task.CompletedTask : List.페이지조회Async(page);

    private async Task ClearFiltersAsync()
    {
        if (ReadActionsBlocked) return;
        List.필터초기화();
        await List.조회Async();
    }

    private Task SelectOrderAsync(long orderId)
        => ReadActionsBlocked ? Task.CompletedTask : Detail.조회Async(orderId);

    private static 현재사용자Snapshot CopyUser(현재사용자Snapshot user)
        => user with { Roles = user.Roles.ToArray() };

    private static bool SameUser(현재사용자Snapshot? loaded, 현재사용자Snapshot current)
        => loaded is { 인증됨: true } && current.인증됨 &&
           string.Equals(loaded.UserId, current.UserId, StringComparison.Ordinal) &&
           loaded.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(current.Roles);

    private void ApplyInitialListState()
    {
        if (_initialListStateApplied || !ShowList)
        {
            return;
        }

        List.검색어 = InitialSearch ?? string.Empty;
        List.국내외구분 = InitialSyncScope;
        List.출고상태 = InitialStatus;
        _initialListStateApplied = true;
    }

    private string DetailPath(long orderId)
    {
        var listPath = new SalesOrderNavigationContext()
            .WithListState(List.검색어, List.국내외구분, List.출고상태, List.현재페이지)
            .PathFor(SalesOrderScreenKind.List);

        return new SalesOrderNavigationContext { From = listPath }
            .PathFor(SalesOrderScreenKind.Detail, orderId);
    }

    private static string ChannelName(string? channelType)
        => channelType?.Trim() switch
        {
            CommerceChannelKeys.SmartStore => "네이버 스마트스토어",
            CommerceChannelKeys.Coupang => "쿠팡 Wing",
            CommerceChannelKeys.ElevenStreet => "11번가",
            CommerceChannelKeys.Shopify => "Shopify",
            CommerceChannelKeys.Amazon => "Amazon",
            CommerceChannelKeys.Ebay => "eBay",
            CommerceChannelKeys.Walmart => "Walmart",
            CommerceChannelKeys.Etsy => "Etsy",
            CommerceChannelKeys.TikTokShop => "TikTok Shop",
            CommerceChannelKeys.Shopee => "Shopee",
            CommerceChannelKeys.Lazada => "Lazada",
            _ => string.IsNullOrWhiteSpace(channelType) ? "채널 확인 필요" : channelType.Trim()
        };

    private static string ScopeName(string? scope)
        => string.Equals(scope, CommerceChannelOrderSyncScopes.Domestic, StringComparison.OrdinalIgnoreCase)
            ? "국내 채널"
            : string.Equals(scope, CommerceChannelOrderSyncScopes.Overseas, StringComparison.OrdinalIgnoreCase)
                ? "해외 채널"
                : "구분 확인 필요";

    private static Color StatusColor(string? status)
        => status?.Trim() switch
        {
            "출고완료" => Color.Success,
            "출고준비중" => Color.Info,
            "출고취소" => Color.Error,
            "출고예정" => Color.Warning,
            _ => Color.Default
        };

    private static string WarehouseName(판매채널주문출고라인응답 line)
        => string.IsNullOrWhiteSpace(line.출고창고명)
            ? $"창고 #{line.출고창고Id}"
            : line.출고창고명;

    private static string NullableIdLabel(long? value) => value?.ToString() ?? "—";

    private static string DateTimeLabel(DateTime? value)
        => value is null || value.Value == default
            ? "—"
            : value.Value.ToLocalTime().ToString("yyyy.MM.dd HH:mm");

    private static string ValueOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string AccessErrorMessage(Api작업오류? error, string? fallback)
        => error?.Http상태코드 switch
        {
            401 => "로그인 세션이 만료되었습니다. 다시 로그인해 주세요.",
            403 => "화주 또는 판매자 역할이 있는 계정으로 이용해 주세요.",
            _ => string.IsNullOrWhiteSpace(fallback) ? "서버 응답을 확인할 수 없습니다." : fallback
        };
}
