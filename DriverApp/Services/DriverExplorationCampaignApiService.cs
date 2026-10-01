using Hongdal.Contracts.Common.Exploration;

namespace DriverApp.Services;

public interface IDriverExplorationCampaignApiService
{
    Task<IReadOnlyList<탐색캠페인목록항목응답>> 목록조회Async(CancellationToken cancellationToken = default);
    Task<탐색캠페인응답?> 생성Async(
        탐색캠페인생성요청 request,
        CancellationToken cancellationToken = default);
    Task<탐색캠페인상세응답?> 상세조회Async(long campaignId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<탐색캠페인추천대상응답>> 추천대상조회Async(
        long campaignId,
        CancellationToken cancellationToken = default);
    Task<탐색캠페인상세응답?> 발송Async(
        long campaignId,
        탐색캠페인발송요청 request,
        CancellationToken cancellationToken = default);
}

public sealed class DriverExplorationCampaignApiService : IDriverExplorationCampaignApiService
{
    private const string BasePath = "api/v1/driver/exploration-campaigns";
    private readonly IDriverApiClient _client;

    public DriverExplorationCampaignApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<탐색캠페인목록항목응답>> 목록조회Async(
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<탐색캠페인목록항목응답>>(
            BasePath,
            "기사 탐색 캠페인 목록 조회",
            cancellationToken) ?? [];

    public Task<탐색캠페인응답?> 생성Async(
        탐색캠페인생성요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<탐색캠페인생성요청, 탐색캠페인응답>(
            BasePath,
            request,
            "기사 탐색 캠페인 생성",
            cancellationToken);

    public Task<탐색캠페인상세응답?> 상세조회Async(
        long campaignId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<탐색캠페인상세응답>(
            $"{BasePath}/{campaignId}",
            "기사 탐색 캠페인 상세 조회",
            cancellationToken);

    public async Task<IReadOnlyList<탐색캠페인추천대상응답>> 추천대상조회Async(
        long campaignId,
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<탐색캠페인추천대상응답>>(
            $"{BasePath}/{campaignId}/recommendations",
            "기사 탐색 캠페인 추천 대상 조회",
            cancellationToken) ?? [];

    public Task<탐색캠페인상세응답?> 발송Async(
        long campaignId,
        탐색캠페인발송요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<탐색캠페인발송요청, 탐색캠페인상세응답>(
            $"{BasePath}/{campaignId}/send",
            request,
            "기사 탐색 캠페인 발송",
            cancellationToken);
}
