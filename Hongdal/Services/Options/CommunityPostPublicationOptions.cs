namespace 홍달.Services.Options;

public sealed class CommunityPostPublicationOptions
{
    public const string SectionName = "CommunityPostPublication";

    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 20;
    public int LeaseTimeoutMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 60;
}
