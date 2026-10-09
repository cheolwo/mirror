using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Fulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class FoodMerchantFulfillmentSettingsViewModel(
    Func<CancellationToken, Task<RestaurantFulfillmentSupportResponse?>> load,
    Func<RestaurantFulfillmentSupportRequest, CancellationToken, Task<RestaurantFulfillmentSupportResponse?>> save) : ObservableObject
{
    private RestaurantFulfillmentSupportRequest? _pending;
    private bool _fresh;
    private decimal? _fixedContribution, _restaurantSharePercent, _maximumRestaurantCharge;
    private bool _enabled, _agreed;
    private bool? _usesRestaurantLimit;
    public RestaurantFulfillmentSupportResponse? Value { get; private set; }
    public string? PolicyMode { get; private set; }
    public decimal? FixedContributionKrw { get => _fixedContribution; set { if (_fixedContribution == value) return; _fixedContribution = value; InputChanged(); } }
    public decimal? RestaurantSharePercent { get => _restaurantSharePercent; set { if (_restaurantSharePercent == value) return; _restaurantSharePercent = value; InputChanged(); } }
    public decimal? MaximumRestaurantChargeKrw { get => _maximumRestaurantCharge; set { if (_maximumRestaurantCharge == value) return; _maximumRestaurantCharge = value; InputChanged(); } }
    public bool? UsesRestaurantLimit { get => _usesRestaurantLimit; set { if (_usesRestaurantLimit == value) return; _usesRestaurantLimit = value; _maximumRestaurantCharge = null; InputChanged(); } }
    public bool Enabled { get => _enabled; set { if (_enabled == value) return; _enabled = value; InputChanged(); } }
    public bool Agreed { get => _agreed; set { _agreed = value; Changed(); } }
    public bool IsLoading { get; private set; }
    public bool IsSending { get; private set; }
    public bool HasPending => _pending is not null;
    public bool Loaded { get; private set; }
    public string? Error { get; private set; }
    public bool RequiresLimitChoice => PolicyMode is "PercentageShare" or "RestaurantPays";
    public DeliveryCostSharingPolicy? DraftPolicy => PolicyMode is null ? null : new()
    {
        Mode = PolicyMode,
        FixedAmountKrw = PolicyMode == "FixedContribution" ? FixedContributionKrw : null,
        RestaurantSharePercent = PolicyMode == "PercentageShare" ? RestaurantSharePercent : null,
        MaximumRestaurantChargeKrw = RequiresLimitChoice && UsesRestaurantLimit == true ? MaximumRestaurantChargeKrw : null
    };
    public bool PolicyComplete => DraftPolicy is { } policy && (!RequiresLimitChoice || UsesRestaurantLimit.HasValue)
        && (!RequiresLimitChoice || UsesRestaurantLimit != true || MaximumRestaurantChargeKrw.HasValue)
        && DeliveryCostSharingCalculator.ValidationError(policy) is null;
    public bool CanSave => !IsLoading && !IsSending && _fresh && Value is not null && PolicyComplete && Agreed;
    public void SelectPolicy(string mode)
    {
        if (mode is not ("CustomerPays" or "FixedContribution" or "PercentageShare" or "RestaurantPays") || PolicyMode == mode || IsSending || HasPending) return;
        PolicyMode = mode; _fixedContribution = null; _restaurantSharePercent = null; _maximumRestaurantCharge = null; _usesRestaurantLimit = null; InputChanged();
    }
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (IsLoading || IsSending || HasPending) return;
        IsLoading = true; Error = null; _fresh = false; Changed();
        try
        {
            Value = Clone(await load(ct)); Loaded = true;
            if (Value is not null && Value.RestaurantId <= 0) throw new InvalidOperationException();
            _fresh = Value is not null;
            ApplySavedPolicy(Value);
        }
        catch (Exception) { Error = "배송 지원 설정을 확인하지 못했습니다. 다시 불러와 주세요."; }
        finally { IsLoading = false; Changed(); }
    }
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (IsSending || IsLoading || (!HasPending && !CanSave)) return;
        _pending ??= new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Value!.Revision,
            FixedContributionKrw = PolicyMode == "FixedContribution" ? FixedContributionKrw!.Value : 0,
            CostSharingPolicy = DeliveryCostSharingCalculator.Clone(DraftPolicy!), Enabled = Enabled, Agreed = Agreed };
        var restaurant = Value?.RestaurantId; IsSending = true; Error = null; Changed();
        try
        {
            var response = await save(Clone(_pending)!, ct);
            if (response is null || response.RestaurantId != restaurant || response.Revision < _pending.ExpectedRevision)
                throw new InvalidOperationException();
            Value = Clone(response); _pending = null; _fresh = true; ApplySavedPolicy(Value);
        }
        catch (Exception ex)
        {
            Error = "저장 결과를 확인하지 못했습니다. 같은 설정 변경 결과를 다시 확인해 주세요.";
            if (ex is SsalddelApiException { StatusCode: 400 or 403 or 409 or 422 })
            { _pending = null; _fresh = false; Error = "설정이 변경되었습니다. 최신 설정을 다시 불러와 주세요."; }
            if (ex is SsalddelApiException { StatusCode: 401 or 403 }) Value = null;
        }
        finally { IsSending = false; Changed(); }
    }
    private void ApplySavedPolicy(RestaurantFulfillmentSupportResponse? value)
    {
        var policy = value is not null && (value.CostSharingPolicy is not null || value.Revision > 0)
            ? DeliveryCostSharingCalculator.Effective(value.CostSharingPolicy, value.FixedContributionKrw) : null;
        PolicyMode = policy?.Mode; _fixedContribution = policy?.FixedAmountKrw; _restaurantSharePercent = policy?.RestaurantSharePercent;
        _maximumRestaurantCharge = policy?.MaximumRestaurantChargeKrw; _usesRestaurantLimit = policy is null ? null : policy.MaximumRestaurantChargeKrw.HasValue;
        _enabled = value?.Enabled ?? false; _agreed = false;
    }
    private void InputChanged() { _agreed = false; Changed(); }
    private static T? Clone<T>(T? value) => value is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
    private void Changed() => OnPropertyChanged(string.Empty);
}
