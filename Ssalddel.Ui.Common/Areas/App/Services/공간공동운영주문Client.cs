using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I공간공동운영주문Client
{
    Task<공간공동운영주문목록Response> ListAsync(string ledgerId, CancellationToken ct);
    Task<공간공동운영주문후보목록Response> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct);
    Task<공간공동운영주문Response?> ReadAsync(string ledgerId, Guid orderRequestId, CancellationToken ct);
    Task<공간공동운영주문Response?> CommandAsync(string ledgerId, Guid orderRequestId, 공간공동운영주문명령Request request, CancellationToken ct);
    Task<공간공동운영주문Response?> ReceiptAsync(string ledgerId, Guid clientRequestId, CancellationToken ct);
}

public sealed class 공간공동운영주문Client(ISsalddelJsonApiClient api) : I공간공동운영주문Client
{
    private static string Base(string id) => $"{공간공동운영Routes.Api}/{Uri.EscapeDataString(id)}/orders";
    public async Task<공간공동운영주문목록Response> ListAsync(string ledgerId, CancellationToken ct)
        => await api.GetAsync<공간공동운영주문목록Response>(Base(ledgerId), "공동운영 주문", false, ct)
            ?? throw new InvalidDataException("주문 목록을 확인하지 못했습니다.");
    public async Task<공간공동운영주문후보목록Response> CandidatesAsync(string ledgerId, string cycleId, CancellationToken ct)
        => await api.GetAsync<공간공동운영주문후보목록Response>(Base(ledgerId) + "/candidates?cycleId=" + Uri.EscapeDataString(cycleId), "확인할 수 있는 내 수요", false, ct)
            ?? throw new InvalidDataException("주문 후보를 확인하지 못했습니다.");
    public Task<공간공동운영주문Response?> ReadAsync(string ledgerId, Guid orderRequestId, CancellationToken ct)
        => api.GetAsync<공간공동운영주문Response>(Base(ledgerId) + $"/{orderRequestId:D}", "주문 조건과 진행", cancellationToken: ct);
    public Task<공간공동운영주문Response?> CommandAsync(string ledgerId, Guid orderRequestId, 공간공동운영주문명령Request request, CancellationToken ct)
        => api.SendAsync<공간공동운영주문명령Request, 공간공동운영주문Response>(HttpMethod.Post,
            Base(ledgerId) + $"/{orderRequestId:D}/commands", request, "주문 조건 확인", cancellationToken: ct);
    public Task<공간공동운영주문Response?> ReceiptAsync(string ledgerId, Guid clientRequestId, CancellationToken ct)
        => api.GetAsync<공간공동운영주문Response>(Base(ledgerId) + $"/requests/{clientRequestId:D}", "원 주문 요청 결과", cancellationToken: ct);
}
