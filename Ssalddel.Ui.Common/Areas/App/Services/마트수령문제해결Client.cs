using Ssalddel.Contracts.Mart;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I마트수령문제해결Client
{
    Task<마트수령문제해결Dto?> ReadAsync(long id, CancellationToken ct);
    Task<마트수령문제해결Dto?> ProposeAsync(long id, 마트수령해결제안요청 request, CancellationToken ct);
    Task<마트수령문제해결Dto?> ConsentAsync(long id, 마트수령해결동의요청 request, CancellationToken ct);
    Task<마트수령문제해결Dto?> ResultAsync(long id, Guid requestId, CancellationToken ct);
}
public sealed class 마트수령문제해결Client(ISsalddelJsonApiClient api) : I마트수령문제해결Client
{
    private static string Path(long id) => $"api/v1/mart/orders/{id}/receipt-resolution";
    public Task<마트수령문제해결Dto?> ReadAsync(long id, CancellationToken ct)
        => api.GetAsync<마트수령문제해결Dto>(Path(id), "수령 문제 해결 현황", allowNotFound: false, cancellationToken: ct);
    public Task<마트수령문제해결Dto?> ProposeAsync(long id, 마트수령해결제안요청 request, CancellationToken ct)
        => api.SendAsync<마트수령해결제안요청, 마트수령문제해결Dto>(HttpMethod.Post, Path(id) + "/proposals", request, "수령 해결 제안", cancellationToken: ct);
    public Task<마트수령문제해결Dto?> ConsentAsync(long id, 마트수령해결동의요청 request, CancellationToken ct)
        => api.SendAsync<마트수령해결동의요청, 마트수령문제해결Dto>(HttpMethod.Post, Path(id) + "/consents", request, "수령 해결 조건 확인", cancellationToken: ct);
    public Task<마트수령문제해결Dto?> ResultAsync(long id, Guid requestId, CancellationToken ct)
        => api.GetAsync<마트수령문제해결Dto>(Path(id) + $"/requests/{requestId:D}", "이전 해결 요청 결과", cancellationToken: ct);
}
