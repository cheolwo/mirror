using Ssalddel.Contracts.Food;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface I음식주문취소합의Client
{
    Task<IReadOnlyList<음식주문취소합의응답>> 목록Async(string orderNo, CancellationToken ct);
    Task<음식주문취소합의응답?> 조회Async(string orderNo, string agreementId, Guid requestId, CancellationToken ct);
    Task<음식주문취소합의응답> 제안Async(string orderNo, 음식주문취소합의제안요청 request, CancellationToken ct);
    Task<음식주문취소합의응답> 동의Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct);
    Task<음식주문취소합의응답> 실행Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct);
}

public sealed class 음식주문취소합의Client(ISsalddelJsonApiClient api) : I음식주문취소합의Client
{
    private static string Path(string orderNo) => "api/v1/food-orders/" + Uri.EscapeDataString(orderNo) + "/cancellation-agreements";
    public async Task<IReadOnlyList<음식주문취소합의응답>> 목록Async(string orderNo, CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<음식주문취소합의응답>>(Path(orderNo), "취소 협의 확인", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidOperationException("취소 조건을 확인하지 못했습니다.");
    public Task<음식주문취소합의응답?> 조회Async(string orderNo, string agreementId, Guid requestId, CancellationToken ct)
        => api.GetAsync<음식주문취소합의응답>(Path(orderNo) + "/" + Uri.EscapeDataString(agreementId) + "?clientRequestId=" + requestId.ToString("D"),
            "취소 협의 요청 결과 확인", allowNotFound: true, cancellationToken: ct);
    public async Task<음식주문취소합의응답> 제안Async(string orderNo, 음식주문취소합의제안요청 request, CancellationToken ct)
        => await api.SendAsync<음식주문취소합의제안요청, 음식주문취소합의응답>(HttpMethod.Post, Path(orderNo), request, "취소 조건 제안", cancellationToken: ct)
            ?? throw new InvalidOperationException("제안 결과를 확인하지 못했습니다.");
    public async Task<음식주문취소합의응답> 동의Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct)
        => await api.SendAsync<음식주문취소합의변경요청, 음식주문취소합의응답>(HttpMethod.Post, Path(orderNo) + "/" + Uri.EscapeDataString(agreementId) + "/consents", request, "취소 조건 동의", cancellationToken: ct)
            ?? throw new InvalidOperationException("동의 결과를 확인하지 못했습니다.");
    public async Task<음식주문취소합의응답> 실행Async(string orderNo, string agreementId, 음식주문취소합의변경요청 request, CancellationToken ct)
        => await api.SendAsync<음식주문취소합의변경요청, 음식주문취소합의응답>(HttpMethod.Post, Path(orderNo) + "/" + Uri.EscapeDataString(agreementId) + "/apply", request, "합의 취소 반영", cancellationToken: ct)
            ?? throw new InvalidOperationException("취소 결과를 확인하지 못했습니다.");
}
