using CommunityToolkit.Mvvm.ComponentModel;
using Ssalddel.Contracts.Common.Fulfillment;
using System.Text.Json;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed record AutoFulfillmentProviderChoice(string Id, string Name, bool IsSimulation);

/// <summary>사용자의 자동 접수 허용 조건만 편집합니다. 선정·요금·실행 판단은 서버가 소유합니다.</summary>
public sealed class AutoFulfillmentConditionsViewModel : ObservableObject
{
    private decimal? _maximumUserChargeKrw;
    private DateTime? _desiredCompletionLocal;
    private bool _sharing, _automatic, _directPayment;
    private Guid _requestId = Guid.NewGuid();
    private readonly HashSet<string> _allowed = new(StringComparer.Ordinal);
    private DeliveryCostSharingPolicy? _costSharingPolicy;
    public IReadOnlyList<AutoFulfillmentProviderChoice> Providers { get; private set; } = [];
    public string ConsentVersion { get; private set; } = "";
    public decimal MerchantContributionKrw { get; private set; }
    public long? MerchantSettingsRevision { get; private set; }
    public bool RequiredDirectPayment { get; private set; } = true;
    public DeliveryCostSharingPolicy? CostSharingPolicy => _costSharingPolicy is null ? null : DeliveryCostSharingCalculator.Clone(_costSharingPolicy);
    public decimal? MaximumUserChargeKrw { get => _maximumUserChargeKrw; set { if (_maximumUserChargeKrw == value) return; _maximumUserChargeKrw = value; ConditionsChanged(); } }
    public DateTime? DesiredCompletionLocal { get => _desiredCompletionLocal; set { if (_desiredCompletionLocal == value) return; _desiredCompletionLocal = value; ConditionsChanged(); } }
    public bool ProviderInformationSharingAgreed { get => _sharing; set { _sharing = value; Changed(); } }
    public bool AutomaticSubmissionAgreed { get => _automatic; set { _automatic = value; Changed(); } }
    public bool DirectPaymentAgreed { get => _directPayment; set { _directPayment = value; Changed(); } }
    public IReadOnlyList<string> AllowedProviderIds => _allowed.Order(StringComparer.Ordinal).ToArray();
    public bool HasExternalProvider => Providers.Any(value => _allowed.Contains(value.Id) && value.Id != "internal");
    public bool HasAssumedProvider => Providers.Any(value => AssumedFulfillmentProviderCatalog.IsAssumed(value.Id));
    public bool IsComplete => MaximumUserChargeKrw is >= 0 && DesiredCompletionLocal > DateTime.Now
        && _allowed.Count > 0 && _allowed.All(id => Providers.Any(value => value.Id == id))
        && !string.IsNullOrWhiteSpace(ConsentVersion) && AutomaticSubmissionAgreed
        && (_costSharingPolicy is null || DeliveryCostSharingCalculator.ValidationError(_costSharingPolicy) is null)
        && ProviderInformationSharingAgreed && (!RequiredDirectPayment || !Allows("internal") || DirectPaymentAgreed);
    public bool Allows(string providerId) => _allowed.Contains(providerId);
    public void SetAllowed(string providerId, bool allowed)
    {
        if (!Providers.Any(value => value.Id == providerId)) return;
        if (!(allowed ? _allowed.Add(providerId) : _allowed.Remove(providerId))) return;
        _sharing = false; ConditionsChanged();
    }
    public void Configure(IReadOnlyList<AutoFulfillmentProviderChoice> providers, string consentVersion,
        decimal merchantContributionKrw = 0, long? merchantSettingsRevision = null, bool requiredDirectPayment = true,
        DeliveryCostSharingPolicy? costSharingPolicy = null)
    {
        var policy = !requiredDirectPayment && merchantSettingsRevision.HasValue
            ? DeliveryCostSharingCalculator.Effective(costSharingPolicy, merchantContributionKrw) : null;
        var changed = ConsentVersion != consentVersion || MerchantContributionKrw != merchantContributionKrw
            || MerchantSettingsRevision != merchantSettingsRevision || RequiredDirectPayment != requiredDirectPayment || !Providers.SequenceEqual(providers)
            || JsonSerializer.Serialize(_costSharingPolicy) != JsonSerializer.Serialize(policy);
        Providers = providers; ConsentVersion = consentVersion; MerchantContributionKrw = merchantContributionKrw;
        MerchantSettingsRevision = merchantSettingsRevision;
        RequiredDirectPayment = requiredDirectPayment;
        _costSharingPolicy = policy is null ? null : DeliveryCostSharingCalculator.Clone(policy);
        _allowed.RemoveWhere(id => !providers.Any(value => value.Id == id));
        if (changed) { _sharing = false; _automatic = false; _directPayment = false; _requestId = Guid.NewGuid(); }
        Changed();
    }
    public void Clear()
    {
        _maximumUserChargeKrw = null; _desiredCompletionLocal = null; _sharing = false; _automatic = false; _directPayment = false;
        _requestId = Guid.NewGuid();
        _allowed.Clear(); Providers = []; ConsentVersion = ""; MerchantContributionKrw = 0; MerchantSettingsRevision = null; _costSharingPolicy = null; Changed();
    }
    public void ConfigureDefault(decimal merchantContributionKrw = 0, long? merchantSettingsRevision = null, bool requiredDirectPayment = true,
        DeliveryCostSharingPolicy? costSharingPolicy = null)
        => Configure(AutoFulfillmentNotice.Providers.Select(value => new AutoFulfillmentProviderChoice(value.ProviderId, value.ProviderName, value.IsSimulation)).ToArray(),
            AutoFulfillmentNotice.Version, merchantContributionKrw, merchantSettingsRevision, requiredDirectPayment, costSharingPolicy);
    public AutoFulfillmentConsent BuildConsent(long expectedRevision = 0) => new()
    {
        ClientRequestId = _requestId, ExpectedRevision = expectedRevision,
        MaxUserChargeKrw = MaximumUserChargeKrw ?? 0,
        DesiredCompletionUtc = DesiredCompletionLocal is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime() : null,
        AllowedProviderIds = AllowedProviderIds, AutomaticSubmissionAgreed = AutomaticSubmissionAgreed,
        ProviderInformationSharingAgreed = ProviderInformationSharingAgreed, DirectPaymentAgreed = DirectPaymentAgreed,
        NoticeVersion = ConsentVersion, InformationFields = AutoFulfillmentNotice.InformationFields,
        ConfirmedRestaurantSupportKrw = MerchantSettingsRevision.HasValue ? MerchantContributionKrw : null,
        ConfirmedRestaurantSupportRevision = MerchantSettingsRevision,
        ConfirmedCostSharingPolicy = _costSharingPolicy is null ? null : DeliveryCostSharingCalculator.Clone(_costSharingPolicy)
    };
    public void InvalidateAcknowledgment()
    {
        if (!_automatic && !_sharing && !_directPayment) return;
        _automatic = false; _sharing = false; _directPayment = false; _requestId = Guid.NewGuid(); Changed();
    }
    public void CommandCompleted() => _requestId = Guid.NewGuid();
    private void ConditionsChanged() { _automatic = false; _directPayment = false; _requestId = Guid.NewGuid(); Changed(); }
    private void Changed() => OnPropertyChanged(string.Empty);
}
