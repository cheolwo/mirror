using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Ssalddel.Contracts.Admin.Food;
using Ssalddel.Contracts.Common.Dispatch;
using Ssalddel.Contracts.Food;
using SsalddelAdmin.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;

namespace SsalddelAdmin.Components.Pages;

public partial class FoodDeliveryInterruptionReviewWorkspace
{
    [Parameter, EditorRequired] public string OrderNo { get; set; } = string.Empty;
    [Parameter] public EventCallback OnBack { get; set; }
    private FoodDeliveryInterruptionReviewState state = default!;
    private bool disposed;
    private bool InputLocked => state.IsBusy || state.RequiresRefresh || state.RequiresLogin;
    private string LoginUrl => "/login?returnUrl=" + Uri.EscapeDataString("/food/order-trace?orderNo=" + Uri.EscapeDataString(OrderNo));

    protected override void OnInitialized()
    {
        state = new(TraceService, () => Session.로그인됨 && Session.서버관리자인가 ? Session.UserId + "\u001f" + Session.AccessToken : string.Empty);
        Navigation.LocationChanged += LocationChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (state.BindOrder(OrderNo)) await state.RefreshAsync();
    }

    private void LocationChanged(object? sender, LocationChangedEventArgs args) => Dispose();
    private void SelectAttempt(string attemptId) => state.SelectAttempt(attemptId);
    private Task RefreshAsync() => state.RefreshAsync();
    private Task SubmitAsync() => state.SubmitAsync();
    private async Task BackAsync()
    {
        if (disposed) return;
        state.Dispose();
        await OnBack.InvokeAsync();
    }

    private bool IsLatest(음식배달시도운영응답 attempt)
        => state.Trace?.배달시도목록.Max(x => x.시도순번) == attempt.시도순번;

    private static string DateText(DateTime? value) => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "미확인";
    private static string AttemptStatus(string code) => code switch
    {
        음식배달시도상태Code.수락 => "수락 · 현재 수행",
        음식배달시도상태Code.가게도착 => "가게 도착 · 현재 수행",
        음식배달시도상태Code.픽업완료 => "픽업 완료 · 현재 수행",
        음식배달시도상태Code.중단 => "중단",
        음식배달시도상태Code.전달완료 => "전달 완료",
        _ => string.IsNullOrWhiteSpace(code) ? "미확인" : code
    };
    private static string ReasonText(string code) => code switch
    {
        음식배달중단사유Code.사고 => "사고", 음식배달중단사유Code.조리지연 => "조리 지연",
        음식배달중단사유Code.배터리부족 => "배터리 부족", 음식배달중단사유Code.배달수단고장 => "배달 수단 고장",
        음식배달중단사유Code.위험기상 => "위험 기상", 음식배달중단사유Code.개인긴급 => "개인 긴급",
        음식배달중단사유Code.기타 => "기타", _ => string.IsNullOrWhiteSpace(code) ? "기록 없음" : code
    };
    private static string ResponsibilityText(string code) => code switch
    {
        운영배차책임Code.보호대상 => "보호 대상", 운영배차책임Code.기사 => "기사",
        운영배차책임Code.음식점 => "음식점", 운영배차책임Code.플랫폼 => "플랫폼",
        운영배차책임Code.미확정 => "미확정", 운영배차책임Code.해당없음 => "해당 없음",
        _ => string.IsNullOrWhiteSpace(code) ? "미확인" : code
    };

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Navigation.LocationChanged -= LocationChanged;
        state.Dispose();
    }
}
