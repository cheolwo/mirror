using System.Text.Json;

namespace Hongdal.Services.AgriculturalFisheries.Information;

public interface IKamisJsonClient
{
    Task<JsonDocument> GetDocumentAsync(
        string requestPath,
        CancellationToken cancellationToken = default);
}
