namespace HongdalApp.Services;

public enum ShipperLocationType
{
    Domestic,
    Overseas
}

public sealed class ShipperOperatingProfileService
{
    private const string LocationTypeKey = "shipper.operating_profile.location_type";

    public ShipperOperatingProfileService()
    {
        var savedValue = Preferences.Default.Get(LocationTypeKey, nameof(ShipperLocationType.Domestic));
        LocationType = Enum.TryParse<ShipperLocationType>(savedValue, out var parsed)
            ? parsed
            : ShipperLocationType.Domestic;
    }

    public event Action? Changed;

    public ShipperLocationType LocationType { get; private set; }

    public bool IsDomestic => LocationType == ShipperLocationType.Domestic;
    public bool IsOverseas => LocationType == ShipperLocationType.Overseas;

    public void SetLocationType(ShipperLocationType locationType)
    {
        if (LocationType == locationType)
        {
            return;
        }

        LocationType = locationType;
        Preferences.Default.Set(LocationTypeKey, locationType.ToString());
        Changed?.Invoke();
    }
}
