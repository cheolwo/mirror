using Hongdal.Contracts.Driver.Action;

namespace DriverApp.Services;

public interface IDriverDispatchActionApiService
{
    Task<기사배차처리응답?> 수락Async(string requestId, CancellationToken cancellationToken = default);
    Task 거절Async(
        string requestId,
        기사배차거절요청 request,
        CancellationToken cancellationToken = default);
    Task 수락취소Async(
        string requestId,
        기사배차수락취소요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class DriverDispatchActionApiService : IDriverDispatchActionApiService
{
    private const string BasePath = "api/v1/driver/dispatch-actions";
    private readonly IDriverApiClient _client;

    public DriverDispatchActionApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<기사배차처리응답?> 수락Async(
        string requestId,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사배차처리응답>(
            BuildPath(requestId, "accept"),
            "기사 배차 수락",
            cancellationToken);

    public Task 거절Async(
        string requestId,
        기사배차거절요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            BuildPath(requestId, "reject"),
            request,
            "기사 배차 거절",
            cancellationToken);

    public Task 수락취소Async(
        string requestId,
        기사배차수락취소요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync(
            BuildPath(requestId, "cancel-acceptance"),
            request,
            "기사 배차 수락 취소",
            cancellationToken);

    private static string BuildPath(string requestId, string action)
        => $"{BasePath}/{Uri.EscapeDataString(requestId)}/{action}";
}
