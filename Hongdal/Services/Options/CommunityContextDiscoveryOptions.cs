namespace 홍달.Services.Options;

public sealed class CommunityContextDiscoveryOptions
{
    public const string SectionName = "CommunityContextDiscovery";

    public string FoodApiBaseUrl { get; set; } = string.Empty;
    public decimal MaximumNearbyRadiusKm { get; set; } = 7m;
    public int RestaurantCandidateLimit { get; set; } = 12;
    public int FreightProviderCandidateLimit { get; set; } = 12;
    public int PublicFreightCandidateLimit { get; set; } = 12;
    public int TimeoutSeconds { get; set; } = 8;
    public bool RestaurantSourceIsSimulation { get; set; } = true;
}
