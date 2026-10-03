using Ssalddel.Contracts.Common.Drivers;
using Ssalddel.Contracts.Common.Operations;

namespace DriverApp.Services;

public sealed class DriverOperatingProfileService
{
    private const string OperatingMarketPreferenceKey = "driver.operating_profile.market_code";

    public DriverOperatingProfileService()
    {
        var savedMarket = Preferences.Default.Get(
            OperatingMarketPreferenceKey,
            OperatingMarketCodes.Korea);
        Current = DriverOperatingProfileCatalog.Korea;
        if (!string.Equals(savedMarket, OperatingMarketCodes.Korea, StringComparison.Ordinal))
            Preferences.Default.Set(OperatingMarketPreferenceKey, OperatingMarketCodes.Korea);
    }

    public event Action? Changed;

    public DriverOperatingProfile Current { get; private set; }
    public bool IsKorea => Current.IsKorea;
    public bool IsUnitedStates => Current.IsUnitedStates;

    public void SetMarket(string marketCode)
    {
        // Keep the existing client entry point compatible, but only domestic
        // operation is available in this app, including persisted older selections.
        var next = DriverOperatingProfileCatalog.Korea;
        Preferences.Default.Set(OperatingMarketPreferenceKey, next.MarketCode);
        if (Current.MarketCode == next.MarketCode)
        {
            return;
        }

        Current = next;
        Changed?.Invoke();
    }
}
