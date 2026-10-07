using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using RestaurantDeskApp.Services;
using Ssalddel.Contracts.Food;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace RestaurantDeskApp.Components.Pages;

public partial class Menus : ComponentBase, IDisposable
{
    [Inject] public I음식점메뉴ApiClient MenuClient { get; set; } = default!;
    [Inject] public RestaurantAuthService AuthService { get; set; } = default!;
    [Inject] public RestaurantMenuDraftStore Drafts { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    private IReadOnlyList<음식점메뉴관리응답> menus = [];
    private bool busy, loaded, published, soldOut, editing;
    private long? editingId;
    private long revision;
    private int displayOrder;
    private string name = "", description = "", imageUrl = "";
    private decimal price;
    private string? message;
    private Severity severity = Severity.Info;
    private 음식점메뉴등록요청? pendingCreate;
    private bool requiresLogin, scopeChanged, disposed;
    private string? owner;
    private long draftGeneration, requestGeneration;
    private readonly CancellationTokenSource lifetime = new();
    private bool FormLocked => busy || pendingCreate is not null || requiresLogin || scopeChanged;

    protected override async Task OnInitializedAsync()
    {
        AuthService.SessionEnding += HandleSessionEnding;
        BindCurrentOwner();
        await ReloadAsync();
    }

    private void BindCurrentOwner()
    {
        if (!AuthService.Session.IsAuthenticated || AuthService.Session.UserId is not { Length: > 0 } id) return;
        owner = id;
        draftGeneration = Drafts.BindOwner(id);
        if (Drafts.Restore(id, draftGeneration) is not { } draft) return;
        editing = draft.Editing; editingId = draft.EditingId; revision = draft.Revision;
        displayOrder = draft.DisplayOrder; name = draft.Name; description = draft.Description;
        imageUrl = draft.ImageUrl; price = draft.Price; published = draft.Published; soldOut = draft.SoldOut;
        pendingCreate = draft.PendingCreate;
    }

    private void CaptureDraft()
    {
        if (owner is null) return;
        Drafts.Capture(owner, draftGeneration, new(editing, editingId, revision, displayOrder,
            name, description, imageUrl, price, published, soldOut, pendingCreate));
    }

    private bool IsCurrentScope(long generation, string? requestedOwner)
        => !disposed && !requiresLogin && !scopeChanged && generation == requestGeneration
            && (requestedOwner is null || AuthService.Session.UserId is null
                || string.Equals(requestedOwner, AuthService.Session.UserId, StringComparison.Ordinal))
            && (owner is null || Drafts.IsCurrent(owner, draftGeneration));

    private bool IsCurrentRequest(long generation, string? requestedOwner)
        => IsCurrentScope(generation, requestedOwner) && AuthService.Session.IsAuthenticated
            && (requestedOwner is null || string.Equals(requestedOwner, AuthService.Session.UserId, StringComparison.Ordinal));

    private void HandleSessionEnding(bool explicitLogout)
    {
        if (disposed) return;
        if (!explicitLogout) CaptureDraft();
        else ResetEditor();
        requiresLogin = true;
        requestGeneration++;
        lifetime.Cancel();
        menus = [];
        loaded = busy = false;
        _ = InvokeAsync(() =>
        {
            if (disposed) return;
            StateHasChanged();
            Navigation.NavigateTo("/login", replace: true);
        });
    }

    private async Task RequireLoginAsync()
    {
        if (AuthService.Session.IsAuthenticated && owner is not null
            && (!string.Equals(owner, AuthService.Session.UserId, StringComparison.Ordinal)
                || !Drafts.IsCurrent(owner, draftGeneration)))
        {
            AbandonPreviousScope();
            return;
        }
        if (!requiresLogin && !disposed)
            await AuthService.InvalidateRejectedSessionAsync(CancellationToken.None);
    }

    private void AbandonPreviousScope()
    {
        scopeChanged = true;
        requestGeneration++;
        lifetime.Cancel();
        menus = [];
        loaded = busy = false;
        ResetEditor();
        if (AuthService.Session.UserId is { Length: > 0 } currentOwner) Drafts.BindOwner(currentOwner);
        Navigation.NavigateTo("/orders", replace: true);
    }

    private async Task ReloadAsync()
    {
        if (busy || requiresLogin || disposed) return;
        busy = true;
        var generation = requestGeneration;
        var requestedOwner = owner;
        try
        {
            var result = await MenuClient.목록Async(lifetime.Token);
            if (!IsCurrentRequest(generation, requestedOwner)) return;
            // The dedicated API preview establishes its scoped identity during the first read.
            if (owner is null) BindCurrentOwner();
            menus = result;
            loaded = true;
            message = pendingCreate is null ? null : "등록 결과 확인이 필요합니다. 동일 요청으로 재확인해 주세요.";
            severity = Severity.Info;
        }
        catch (Exception) when (disposed || requiresLogin || lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SsalddelApiException { StatusCode: 401 })
        {
            if (IsCurrentScope(generation, requestedOwner)) await RequireLoginAsync();
        }
        catch (Exception)
        {
            if (!IsCurrentScope(generation, requestedOwner)) return;
            message = "메뉴를 조회하지 못했습니다. 연결 상태를 확인한 뒤 다시 시도하세요. 이전 목록은 유지합니다.";
            severity = Severity.Error;
        }
        finally { if (generation == requestGeneration && !disposed) busy = false; }
    }

    private void NewMenu()
    {
        if (FormLocked) return;
        ResetEditor();
        editing = true;
    }

    private void Edit(음식점메뉴관리응답 menu)
    {
        if (FormLocked) return;
        editing = true;
        editingId = menu.Id; revision = menu.Revision; displayOrder = menu.표시순서;
        name = menu.메뉴명; description = menu.설명; imageUrl = menu.대표이미지Url ?? "";
        price = menu.판매가; published = menu.공개여부; soldOut = menu.품절여부;
    }

    private void ResetEditor()
    {
        editing = false; editingId = null; revision = 0; displayOrder = 0;
        name = description = imageUrl = ""; price = 0; published = soldOut = false;
        pendingCreate = null;
    }

    private void CloseEditor()
    {
        if (FormLocked) return;
        ResetEditor();
        CaptureDraft();
        message = null;
    }

    private bool CanNavigateToLogin(string target)
    {
        if (!requiresLogin) return false;
        try
        {
            var path = Navigation.ToBaseRelativePath(target).Split('?', '#')[0].TrimEnd('/');
            return string.Equals(path, "login", StringComparison.Ordinal);
        }
        catch (ArgumentException) { return false; }
    }

    private void GuardNavigation(LocationChangingContext context)
    {
        if (!editing || CanNavigateToLogin(context.TargetLocation)) return;
        context.PreventNavigation();
        severity = Severity.Warning;
        message = "입력 중입니다. 저장하거나 ‘목록으로 · 입력 취소’를 누른 뒤 이동하세요.";
    }

    private async Task SaveAsync()
    {
        if (busy || !loaded || disposed || requiresLogin || scopeChanged) return;
        if (owner is null || !AuthService.Session.IsAuthenticated
            || !string.Equals(owner, AuthService.Session.UserId, StringComparison.Ordinal)
            || !Drafts.IsCurrent(owner, draftGeneration))
        {
            await RequireLoginAsync();
            return;
        }
        if (음식점메뉴입력Policy.오류조회(name, description, price, imageUrl) is { } inputError)
        {
            severity = Severity.Warning; message = inputError; return;
        }
        busy = true;
        var generation = requestGeneration;
        var requestedOwner = owner;
        try
        {
            if (editingId is { } id)
                await MenuClient.수정Async(id, new 음식점메뉴수정요청 { 예상Revision = revision, 메뉴명 = name.Trim(), 설명 = description.Trim(), 판매가 = price, 대표이미지Url = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(), 공개여부 = published, 품절여부 = soldOut, 표시순서 = displayOrder }, lifetime.Token);
            else
            {
                pendingCreate ??= new 음식점메뉴등록요청 { 클라이언트요청Id = Guid.NewGuid(), 메뉴명 = name.Trim(), 설명 = description.Trim(), 판매가 = price, 대표이미지Url = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(), 공개여부 = published, 품절여부 = soldOut, 표시순서 = displayOrder };
                CaptureDraft();
                await MenuClient.등록Async(pendingCreate, lifetime.Token);
            }
            if (!IsCurrentRequest(generation, requestedOwner)) return;
            ResetEditor();
            CaptureDraft();
            severity = Severity.Success; message = "메뉴를 저장했습니다.";
            try
            {
                var result = await MenuClient.목록Async(lifetime.Token);
                if (IsCurrentRequest(generation, requestedOwner)) menus = result;
            }
            catch (Exception) when (disposed || requiresLogin || lifetime.IsCancellationRequested) { }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SsalddelApiException { StatusCode: 401 })
            {
                if (IsCurrentScope(generation, requestedOwner)) await RequireLoginAsync();
            }
            catch (Exception)
            {
                if (IsCurrentRequest(generation, requestedOwner))
                {
                    severity = Severity.Warning;
                    message = "메뉴 저장은 완료했지만 목록 갱신에 실패했습니다. 다시 등록하지 말고 새로고침해 주세요.";
                }
            }
        }
        catch (Exception) when (disposed || requiresLogin || lifetime.IsCancellationRequested) { }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SsalddelApiException { StatusCode: 401 })
        {
            if (IsCurrentScope(generation, requestedOwner)) await RequireLoginAsync();
        }
        catch (메뉴저장거절Exception)
        {
            if (!IsCurrentRequest(generation, requestedOwner)) return;
            pendingCreate = null;
            CaptureDraft();
            severity = Severity.Error;
            message = "서버가 저장을 거절했습니다. 새로고침 후 같은 이름의 메뉴·최신 상태·계정 권한을 확인하세요.";
        }
        catch (Exception)
        {
            if (!IsCurrentRequest(generation, requestedOwner)) return;
            severity = Severity.Error;
            message = pendingCreate is not null
                ? "등록 결과를 확인하지 못했습니다. 입력값을 보존했습니다. 동일 요청으로 재확인해 주세요."
                : "수정을 확인하지 못했습니다. 새로고침 후 해당 메뉴를 다시 선택해 최신 상태를 확인하세요.";
        }
        finally { if (generation == requestGeneration && !disposed) busy = false; }
    }

    public void Dispose()
    {
        if (disposed) return;
        if (!requiresLogin && AuthService.Session.IsAuthenticated
            && string.Equals(owner, AuthService.Session.UserId, StringComparison.Ordinal)) CaptureDraft();
        disposed = true;
        AuthService.SessionEnding -= HandleSessionEnding;
        requestGeneration++;
        lifetime.Cancel();
        lifetime.Dispose();
        menus = [];
        ResetEditor();
    }
}
