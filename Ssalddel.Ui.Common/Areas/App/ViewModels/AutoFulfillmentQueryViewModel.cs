using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Fulfillment;
using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>한 소유자·한 원 요청의 자동 수행 조건/복구를 소유합니다. 응답 유실은 같은 명령 ID로 재확인합니다.</summary>
public sealed class AutoFulfillmentQueryViewModel(
    Func<string, AutoFulfillmentConsent, CancellationToken, Task<AutoFulfillmentSnapshot?>> consent,
    Func<string, AutoFulfillmentActionRequest, CancellationToken, Task<AutoFulfillmentSnapshot?>> refresh,
    Func<string, AutoFulfillmentActionRequest, CancellationToken, Task<AutoFulfillmentSnapshot?>> cancel) : ObservableObject
{
    private string? _sourceId, _sourceKind, _ownerScope, _pendingKind;
    private AutoFulfillmentConsent? _pendingConsent;
    private AutoFulfillmentActionRequest? _pendingAction;
    private long _generation;
    private bool _fresh;
    public AutoFulfillmentSnapshot? Value { get; private set; }
    public AutoFulfillmentConditionsViewModel Conditions { get; } = new();
    public bool IsSending { get; private set; }
    public bool HasPending => _pendingKind is not null;
    public string? Error { get; private set; }
    public bool CanRenew => _fresh && !IsSending && !HasPending && AutoFulfillmentPresentation.CanRenew(Value);
    public void Bind(string sourceKind, string sourceId, string ownerScope, AutoFulfillmentSnapshot? value)
    {
        if (_sourceId != sourceId || _sourceKind != sourceKind || _ownerScope != ownerScope)
        { Clear(); _sourceId = sourceId; _sourceKind = sourceKind; _ownerScope = ownerScope; }
        if (value is not null && (value.SourceKind != sourceKind || value.SourceId != sourceId))
        { Error = "선택한 주문과 배송 처리 결과가 다릅니다. 다시 확인해 주세요."; Changed(); return; }
        if (value is not null && Value is not null && value.Revision < Value.Revision) return;
        if (value?.Revision != Value?.Revision || value?.StateCode != Value?.StateCode) Conditions.InvalidateAcknowledgment();
        Value = Clone(value);
        _fresh = true;
        if (value is not null) Conditions.ConfigureDefault(value.FrozenRestaurantSupportKrw, value.RestaurantSupportRevision, sourceKind != FulfillmentSourceKinds.FoodOrder, value.CostSharingPolicy);
        Changed();
    }
    public Task RenewAsync(CancellationToken ct = default)
    {
        if (!CanRenew || !Conditions.IsComplete || Value is null) return Task.CompletedTask;
        _pendingConsent = Conditions.BuildConsent(Value.ConsentRevision); _pendingKind = "consent"; return RunAsync(ct);
    }
    public Task RevokeAsync(CancellationToken ct = default)
    {
        if (IsSending || HasPending || Value is null || Value.ExecutionPaused || Value.SourceClosed || Value.StateCode == AutoFulfillmentStates.Completed) return Task.CompletedTask;
        _pendingConsent = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Value.ConsentRevision, Revoked = true };
        _pendingKind = "consent"; return RunAsync(ct);
    }
    public Task RefreshAsync(CancellationToken ct = default)
    {
        if (IsSending || Value is null || Value.ExecutionPaused) return Task.CompletedTask;
        if (HasPending) return RunAsync(ct);
        _pendingAction = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Value.Revision };
        _pendingKind = "refresh"; return RunAsync(ct);
    }
    public Task CancelAsync(CancellationToken ct = default)
    {
        if (IsSending || HasPending || Value?.CanCancel != true || Value.ExecutionPaused) return Task.CompletedTask;
        _pendingAction = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Value.Revision };
        _pendingKind = "cancel"; return RunAsync(ct);
    }
    private async Task RunAsync(CancellationToken ct)
    {
        if (IsSending || _sourceId is null || _pendingKind is null) return;
        var id = _sourceId; var kind = _pendingKind; var generation = _generation;
        IsSending = true; Error = null; Changed();
        try
        {
            var response = kind switch
            {
                "consent" => await consent(id, _pendingConsent!, ct),
                "cancel" => await cancel(id, _pendingAction!, ct),
                _ => await refresh(id, _pendingAction!, ct)
            };
            if (generation != _generation) return;
            if (response is null || response.SourceId != id || response.SourceKind != _sourceKind || response.Revision < (Value?.Revision ?? 0))
            { Error = "원 요청의 처리 결과를 확인하지 못했습니다. 같은 요청 결과를 다시 확인해 주세요."; return; }
            Value = Clone(response); _fresh = true; ClearPending(); Conditions.CommandCompleted(); Conditions.InvalidateAcknowledgment();
            Conditions.ConfigureDefault(response.FrozenRestaurantSupportKrw, response.RestaurantSupportRevision, response.SourceKind != FulfillmentSourceKinds.FoodOrder, response.CostSharingPolicy);
        }
        catch (Exception ex)
        {
            if (generation != _generation) return;
            Error = "처리 결과를 확인하지 못했습니다. 원 요청 결과를 다시 확인해 주세요.";
            if (ex is Ssalddel.Ui.Common.Areas.App.Services.SsalddelApiException { StatusCode: 400 or 403 or 409 or 422 })
            { Error = "현재 조건이나 진행 상태가 바뀌었습니다. 배송 진행을 새로고침해 주세요."; _fresh = false; ClearPending(); }
            if (ex is Ssalddel.Ui.Common.Areas.App.Services.SsalddelApiException { StatusCode: 401 }) Clear();
        }
        finally { if (generation == _generation) { IsSending = false; Changed(); } }
    }
    public void Clear()
    { ++_generation; _fresh = false; ClearPending(); _sourceId = null; _sourceKind = null; _ownerScope = null; Value = null; Error = null; IsSending = false; Conditions.Clear(); Changed(); }
    private void ClearPending() { _pendingKind = null; _pendingConsent = null; _pendingAction = null; }
    private static T? Clone<T>(T? value) => value is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
    private void Changed() => OnPropertyChanged(string.Empty);
}
