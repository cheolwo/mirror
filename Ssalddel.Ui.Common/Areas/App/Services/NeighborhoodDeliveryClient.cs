using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Privacy;
using Ssalddel.Contracts.Common.Fulfillment;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public interface INeighborhoodDeliveryClient
{
    Task<신청개인정보동의증적Response?> ConsentAsync(신청개인정보동의기록Request request, CancellationToken ct);
    Task<NeighborhoodDeliveryQuoteResponse?> QuoteAsync(NeighborhoodDeliveryRequest request, CancellationToken ct);
    Task<NeighborhoodDeliveryResponse?> CreateAsync(NeighborhoodDeliveryRequest request, CancellationToken ct);
    Task<IReadOnlyList<NeighborhoodDeliveryResponse>> MineAsync(int page, CancellationToken ct);
    Task<NeighborhoodDeliveryResponse?> ReadAsync(string requestId, CancellationToken ct);
    Task<NeighborhoodDeliveryResponse?> ReadSubmissionAsync(Guid clientRequestId, CancellationToken ct)
        => Task.FromResult<NeighborhoodDeliveryResponse?>(null);
    Task<NeighborhoodDeliveryInputPreviewResponse?> PreviewInputAsync(NeighborhoodDeliveryInputPreviewRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryInputPreviewResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryInputTemplateResponse?> GetInputTemplateAsync(CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryInputTemplateResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> ChangeDispatchAsync(string requestId, NeighborhoodDispatchChoiceRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryDisclosureResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryQuoteResponse?> FulfillmentOptionsAsync(string requestId, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryQuoteResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> ChooseFulfillmentAsync(string requestId, NeighborhoodFulfillmentChoiceRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> RefreshFulfillmentAsync(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> CancelFulfillmentAsync(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> AutoConsentAsync(string requestId, AutoFulfillmentConsent request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> AutoRefreshAsync(string requestId, AutoFulfillmentActionRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
    Task<NeighborhoodDeliveryResponse?> AutoCancelAsync(string requestId, AutoFulfillmentActionRequest request, CancellationToken ct)
        => Task.FromException<NeighborhoodDeliveryResponse?>(new NotSupportedException());
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
    public Task<NeighborhoodDeliveryResponse?> ReadSubmissionAsync(Guid clientRequestId, CancellationToken ct)
        => api.GetAsync<NeighborhoodDeliveryResponse>($"{NeighborhoodDeliveryRoutes.Api}/by-client-request/{clientRequestId:D}",
            "원 배송 접수 결과 확인", cancellationToken: ct);
    public Task<NeighborhoodDeliveryInputPreviewResponse?> PreviewInputAsync(NeighborhoodDeliveryInputPreviewRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDeliveryInputPreviewRequest, NeighborhoodDeliveryInputPreviewResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/input-preview", request, "배송 엑셀 초안 확인", cancellationToken: ct);
    public Task<NeighborhoodDeliveryInputTemplateResponse?> GetInputTemplateAsync(CancellationToken ct)
        => api.GetAsync<NeighborhoodDeliveryInputTemplateResponse>($"{NeighborhoodDeliveryRoutes.Api}/input-template",
            "배송 엑셀 양식", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> ChangeDispatchAsync(string requestId, NeighborhoodDispatchChoiceRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDispatchChoiceRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/dispatch-choice", request, "배송 배차 선택", cancellationToken: ct);
    public Task<NeighborhoodDeliveryDisclosureResponse?> RecordDisclosureAsync(string requestId, NeighborhoodDeliveryDisclosureRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodDeliveryDisclosureRequest, NeighborhoodDeliveryDisclosureResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/driver-disclosure", request,
            "기사 정보 제공 동의", cancellationToken: ct);
    public Task<NeighborhoodDeliveryQuoteResponse?> FulfillmentOptionsAsync(string requestId, CancellationToken ct)
        => api.GetAsync<NeighborhoodDeliveryQuoteResponse>($"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/fulfillment-options",
            "배송 처리처 견적", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> ChooseFulfillmentAsync(string requestId, NeighborhoodFulfillmentChoiceRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodFulfillmentChoiceRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/fulfillment-choice", request, "배송 처리처 확정", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> RefreshFulfillmentAsync(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodFulfillmentActionRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/fulfillment-refresh", request, "배송 처리 결과 확인", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> CancelFulfillmentAsync(string requestId, NeighborhoodFulfillmentActionRequest request, CancellationToken ct)
        => api.SendAsync<NeighborhoodFulfillmentActionRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/fulfillment-cancel", request, "배송 처리처 취소", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> AutoConsentAsync(string requestId, AutoFulfillmentConsent request, CancellationToken ct)
        => api.SendAsync<AutoFulfillmentConsent, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/auto-fulfillment-consent", request, "자동 배송 조건 확인", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> AutoRefreshAsync(string requestId, AutoFulfillmentActionRequest request, CancellationToken ct)
        => api.SendAsync<AutoFulfillmentActionRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/auto-fulfillment-refresh", request, "자동 배송 진행 확인", cancellationToken: ct);
    public Task<NeighborhoodDeliveryResponse?> AutoCancelAsync(string requestId, AutoFulfillmentActionRequest request, CancellationToken ct)
        => api.SendAsync<AutoFulfillmentActionRequest, NeighborhoodDeliveryResponse>(HttpMethod.Post,
            $"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/auto-fulfillment-cancel", request, "자동 배송 수행 취소", cancellationToken: ct);
}
