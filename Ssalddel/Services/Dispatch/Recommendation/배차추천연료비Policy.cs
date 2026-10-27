namespace 살뜰.Services.Dispatch.Recommendation;

internal static class 배차추천연료비Policy
{
    internal static decimal? ResolvePricePerLiter(IReadOnlyList<OpinetAveragePriceItem> items)
    {
        if (items.Count == 0)
        {
            return 1700m;
        }

        var diesel = items.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ProductName)
                                               && x.ProductName.Contains("경유", StringComparison.OrdinalIgnoreCase)
                                               && x.Price.HasValue);
        if (diesel?.Price is not null)
        {
            return diesel.Price.Value;
        }

        return items.FirstOrDefault(x => x.Price.HasValue)?.Price ?? 1700m;
    }

    internal static decimal ResolveEfficiencyKmPerLiter(string? vehicle)
    {
        if (string.IsNullOrWhiteSpace(vehicle))
        {
            return 7m;
        }

        if (vehicle.Contains("전기", StringComparison.OrdinalIgnoreCase))
        {
            return 999m;
        }

        if (vehicle.Contains("냉동", StringComparison.OrdinalIgnoreCase))
        {
            return 5.5m;
        }

        if (vehicle.Contains("5톤", StringComparison.OrdinalIgnoreCase) || vehicle.Contains("대형", StringComparison.OrdinalIgnoreCase))
        {
            return 4.5m;
        }

        if (vehicle.Contains("1톤", StringComparison.OrdinalIgnoreCase) || vehicle.Contains("카고", StringComparison.OrdinalIgnoreCase) || vehicle.Contains("용달", StringComparison.OrdinalIgnoreCase))
        {
            return 8m;
        }

        return 7m;
    }

    internal static decimal? EstimateCost(decimal? totalDistanceKm, decimal fuelEfficiencyKmPerLiter, decimal? fuelPricePerLiter)
    {
        if (!totalDistanceKm.HasValue || !fuelPricePerLiter.HasValue || fuelEfficiencyKmPerLiter <= 0)
        {
            return null;
        }

        return totalDistanceKm.Value / fuelEfficiencyKmPerLiter * fuelPricePerLiter.Value;
    }
}
