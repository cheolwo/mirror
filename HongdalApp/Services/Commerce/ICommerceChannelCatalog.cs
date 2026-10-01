namespace HongdalApp.Services.Commerce;

public interface ICommerceChannelCatalog
{
    IReadOnlyList<CommerceChannelDescriptor> GetSupportedChannels();

    CommerceChannelDescriptor? FindByChannelType(string channelType);
}
