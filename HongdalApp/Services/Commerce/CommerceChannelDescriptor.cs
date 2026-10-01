namespace HongdalApp.Services.Commerce;

public sealed record CommerceChannelDescriptor(
    string ChannelKey,
    string DisplayName,
    string ProviderName,
    bool SupportsProductCreate,
    bool SupportsProductUpdate,
    bool SupportsProductDelete,
    string IntegrationStatus);
