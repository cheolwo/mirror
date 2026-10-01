using Hongdal.Contracts.Common.Drivers;
using Hongdal.Contracts.Common.Operations;

namespace DriverApp.Services;

public sealed class DriverOperatingProfileService
{
    private const string OperatingMarketPreferenceKey = "driver.operating_profile.market_code";

    public DriverOperatingProfileService()
    {
        var savedMarket = Preferences.Default.Get(
            OperatingMarketPreferenceKey,
            OperatingMarketCodes.Korea);
        Current = DriverOperatingProfileCatalog.Get(savedMarket);
    }

    public event Action? Changed;

    public DriverOperatingProfile Current { get; private set; }
    public bool IsKorea => Current.IsKorea;
    public bool IsUnitedStates => Current.IsUnitedStates;

    public void SetMarket(string marketCode)
    {
        var next = DriverOperatingProfileCatalog.Get(marketCode);
        if (Current.MarketCode == next.MarketCode)
        {
            return;
        }

        Current = next;
        Preferences.Default.Set(OperatingMarketPreferenceKey, next.MarketCode);
        Changed?.Invoke();
    }
}
