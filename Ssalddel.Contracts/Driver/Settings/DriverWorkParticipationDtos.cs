namespace Ssalddel.Contracts.Driver.Settings;

public static class DriverWorkKinds
{
    public const string FoodDelivery = "FoodDelivery";
    public const string QuickDelivery = "QuickDelivery";
    public const string CargoTransport = "CargoTransport";
}

public static class DriverVehicleKinds
{
    public const string Motorcycle = "Motorcycle";
    public const string Car = "Car";
    public const string Van = "Van";
    public const string Truck = "Truck";
    public const string Bicycle = "Bicycle";
    public const string Walking = "Walking";
    public static bool IsKnown(string? value) => value is Motorcycle or Car or Van or Truck or Bicycle or Walking;
}

/// <summary>기사의 희망 조건입니다. 업무 수행 자격이나 확인된 물품의 크기를 대신하지 않습니다.</summary>
public sealed record DriverWorkParticipationSettings
{
    public long Revision { get; init; }
    public bool FoodDelivery { get; init; }
    public bool QuickDelivery { get; init; }
    public bool CargoTransport { get; init; }
    public bool DirectPicking { get; init; }
    public string VehicleKind { get; init; } = string.Empty;
    public int MaxConcurrentJobs { get; init; } = 1;
    public decimal? CapacityKg { get; init; }
    public decimal? CapacityLitres { get; init; }
    public bool FoodCargoSeparationConfirmed { get; init; }
    public bool ReceiveAdditionalOffers { get; init; } = true;
}

public sealed record DriverWorkParticipationUpdate
{
    public long ExpectedRevision { get; init; }
    public DriverWorkParticipationSettings Settings { get; init; } = new();
}

public sealed record DriverWorkParticipationResponse
{
    public string DriverId { get; init; } = string.Empty;
    public DriverWorkParticipationSettings Settings { get; init; } = new();
    public bool SharedAdmissionEnabled { get; init; }
    public bool FoodEligible { get; init; }
    public bool CargoEligible { get; init; }
    public int OperationalMaxConcurrentJobs { get; init; } = 4;
}

public static class DriverWorkParticipationSettingsRules
{
    public const decimal MaxCapacityKg = 50000m;
    public const decimal MaxCapacityLitres = 1000000m;
    public static bool ValidMeasure(decimal? value, decimal upper)
        => value is null || value > 0m && value <= upper && decimal.Round(value.Value, 3) == value;
}
