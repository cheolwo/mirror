using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Ui.Common.Areas.App.Components.Community;

public partial class 공간공동운영업무상세
{
    [Parameter] public string? LedgerId { get; set; }
    [Parameter] public string? StageKey { get; set; }
    [Parameter] public string? CycleId { get; set; }
    [Parameter] public string LoginHref { get; set; } = "/community/login";
    private 공간공동운영업무상세ViewModel? model;
    private 공간공동운영업무상세ViewModel Model => model!;
    private string? loadedKey;
    private bool disposed;
    protected override void OnInitialized() { model = Services.GetService<공간공동운영업무상세ViewModel>(); if (model is not null) model.PropertyChanged += ModelChanged; }
    protected override async Task OnParametersSetAsync()
    { var key = LedgerId + "\n" + StageKey + "\n" + CycleId; if (key == loadedKey) return; loadedKey = key; await ReloadAsync(); }
    private Task ReloadAsync() => model?.LoadAsync(LedgerId, StageKey, CycleId) ?? Task.CompletedTask;
    public async Task NotifyAuthenticationChangedAsync() { model?.NotifyAuthenticationChanged(); loadedKey = null; await ReloadAsync(); }
    private void ModelChanged(object? sender, PropertyChangedEventArgs args) { if (!disposed) _ = InvokeAsync(StateHasChanged); }
    private static string StageTitle(string key) => 공간공동운영업무연결Presentation.Stages.First(stage => stage.Key == key).Title;
    private IReadOnlyList<공간공동운영통합주문Dto> LinkedOrders(공간공동운영통합원천Dto source)
    { var keys = Model.Flow.Relations.Where(link => link.ToKey == source.NodeKey && link.RelationCode == "source-lineage").Select(link => link.FromKey).ToHashSet(StringComparer.Ordinal); return Model.Flow.Cycle?.Orders.Where(order => keys.Contains(order.NodeKey)).ToArray() ?? []; }
    private static string ContactStatus(string? status) => status switch
    {
        "not-linked" => "연락처를 확인할 수 있는 업무가 아직 연결되지 않았어요.",
        "unregistered" => "이 업무에는 등록된 연락처가 없어요.",
        "not-visible" => "현재 계정에서는 이 업무의 연락처를 확인할 수 없어요.",
        "source-changed" => "업무 정보가 바뀌었어요. 상세 내용을 다시 불러와 주세요.",
        "unsupported" => "이 업무의 연락처는 원 업무에서 확인해 주세요.",
        _ => "연락처를 지금 확인하지 못했어요. 다시 확인해 주세요."
    };
    public void Dispose() { disposed = true; if (model is not null) { model.PropertyChanged -= ModelChanged; model.Dispose(); } }
}
