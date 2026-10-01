using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Naver;

public interface INaverSmartStoreProductClient
{
    Task<NaverCommerceApiResult> RegisterProductAsync(JsonNode payload, CancellationToken cancellationToken = default);

    Task<NaverCommerceApiResult> GetChannelProductAsync(long channelProductNo, CancellationToken cancellationToken = default);

    Task<NaverCommerceApiResult> UpdateChannelProductAsync(long channelProductNo, JsonNode payload, CancellationToken cancellationToken = default);

    Task<NaverCommerceApiResult> DeleteChannelProductAsync(long channelProductNo, CancellationToken cancellationToken = default);

    Task<NaverCommerceApiResult> GetOriginProductAsync(long originProductNo, CancellationToken cancellationToken = default);

    Task<NaverCommerceApiResult> UpdateOriginProductAsync(long originProductNo, JsonNode payload, CancellationToken cancellationToken = default);

    Task<NaverCommerceApiResult> DeleteOriginProductAsync(long originProductNo, CancellationToken cancellationToken = default);
}
