using System.Net.Http.Json;
using Hongdal.Contracts.Common.Operations;

namespace Hongdal.Ui.Common.Areas.App.Services;

public interface IOperatingMarketProfileClient
{
    Task<OperatingMarketRuntimeProfileResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default);
}

public sealed class OperatingMarketProfileClient(HttpClient httpClient)
    : IOperatingMarketProfileClient
{
    public Task<OperatingMarketRuntimeProfileResponse?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<OperatingMarketRuntimeProfileResponse>(
            "api/v1/operations/market-profile",
            cancellationToken);
}
