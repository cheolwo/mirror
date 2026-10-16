using Ssalddel.Contracts.Common.Dispatch;

namespace DriverApp.Services;

public interface IDriverOperationalDispatchApiService
{
    Task<운영배차수신상태Dto> GetAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<운영배차수신상태Dto> ChangeIntentAsync(운영배차수신의사변경요청 request,
        CancellationToken cancellationToken = default);
    Task AcknowledgeOfferPresentationAsync(string requestId, int recommendationRound,
        CancellationToken cancellationToken = default);
}

/// <summary>기존 기사 인증 transport로 본인 신규 수신 의사와 실제 표시된 추천 차수를 전달한다.</summary>
public sealed class DriverOperationalDispatchApiService(IDriverApiClient client) : IDriverOperationalDispatchApiService
{
    private const string Root = "api/v1/driver/operational-dispatch";

    public async Task<운영배차수신상태Dto> GetAvailabilityAsync(CancellationToken cancellationToken = default)
        => await client.GetAsync<운영배차수신상태Dto>($"{Root}/availability", "신규 배차 수신 상태 조회", cancellationToken)
           ?? throw new InvalidOperationException("신규 배차 수신 상태를 확인하지 못했습니다.");

    public async Task<운영배차수신상태Dto> ChangeIntentAsync(운영배차수신의사변경요청 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.클라이언트요청Id == Guid.Empty
            || request.수신의사Code is not (운영배차수신의사Code.On or 운영배차수신의사Code.Off)
            || request.미응답자동중지해제 && request.수신의사Code != 운영배차수신의사Code.On)
            throw new ArgumentException("신규 배차 수신 변경 요청을 확인해 주세요.", nameof(request));
        return await client.PutAsync<운영배차수신의사변경요청, 운영배차수신상태Dto>(
            $"{Root}/availability/intent", request, "신규 배차 수신 변경", cancellationToken)
            ?? throw new InvalidOperationException("신규 배차 수신 변경 결과를 확인하지 못했습니다.");
    }

    public Task AcknowledgeOfferPresentationAsync(string requestId, int recommendationRound,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(requestId) || recommendationRound <= 0)
            throw new ArgumentException("표시된 추천 의뢰와 차수를 확인해 주세요.");
        return client.PutAsync($"{Root}/offers/{Uri.EscapeDataString(requestId)}/receipt",
            new 운영배차제안표시확인요청 { 추천라운드 = recommendationRound }, "추천 표시 확인", cancellationToken);
    }
}
