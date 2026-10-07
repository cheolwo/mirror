using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface INeighborhoodDeliveryClient
{
    Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct);
    Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct);
    Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct);
    Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct);
    Task<NeighborhoodDeliveryResponse?> ChangeDispatchAsync(string requestId, NeighborhoodDispatchChoiceRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryDisclosureResponse?>(new NotSupportedException());
}

/// <summary>공개 글과 분리된 본인 배송 API입니다. 주소와 연락처는 보호 전송 클라이언트를 사용합니다.</summary>
public sealed class NeighborhoodDeliveryClient(ISsalddelJsonApiClient api) : INeighborhoodDeliveryClient
{
    public Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct)
        => api.SendAsync<신청개인정보동의기록Request, 신청개인정보동의증적Response>(HttpMethod.Post,
            "api/v1/common/application-privacy-consents", request, "배송 개인정보 동의", cancellationToken: ct);
    public Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDeliveryRequest, NeighborhoodDeliveryQuoteResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/quote", request, "배송 견적", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDeliveryRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            NeighborhoodDeliveryRoutes.Api, request, "배송 의뢰 등록", cancellationToken: ct);
    public async Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct)
        => await api.GetAsync<IReadOnlyList<NeighborhoodDeliveryResponse>>($"{NeighborhoodDeliveryRoutes.Api}/mine?page={Math.Clamp(page, 1, 10000)}",
            "내 배송 의뢰", allowNotFound: false, cancellationToken: ct)
            ?? throw new InvalidOperationException("배송 목록 응답을 확인하지 못했습니다.");
    public Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct)
        => api.GetAsync<NeighborhoodDeliveryResponse>($"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}",
            "배송 진행 조회", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> ChangeDispatchAsync(string requestId, NeighborhoodDispatchChoiceRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDispatchChoiceRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/dispatch-choice", request, "배송 배차 선택", cancellationToken: ct);
    public Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDeliveryDisclosureRequest, NeighborhoodDeliveryDisclosureResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/driver-disclosure", request,
            "기사 정보 제공 동의", cancellationToken: ct);
}
