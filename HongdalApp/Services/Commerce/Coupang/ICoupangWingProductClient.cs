using System.Text.Json.Nodes;

namespace HongdalApp.Services.Commerce.Coupang;

public interface ICoupangWingProductClient
{
    Task<CoupangWingApiResult> CreateProductAsync(JsonNode payload, CancellationToken cancellationToken = default);

    Task<CoupangWingApiResult> GetProductAsync(long sellerProductId, CancellationToken cancellationToken = default);

    Task<CoupangWingApiResult> UpdateProductAsync(JsonNode payload, CancellationToken cancellationToken = default);

    Task<CoupangWingApiResult> UpdateProductPartialAsync(long sellerProductId, JsonNode payload, CancellationToken cancellationToken = default);

    Task<CoupangWingApiResult> DeleteProductAsync(long sellerProductId, CancellationToken cancellationToken = default);
}
