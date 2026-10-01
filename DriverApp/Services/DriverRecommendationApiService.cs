using Hongdal.Contracts.Driver.Recommendation;

namespace DriverApp.Services;

public interface IDriverRecommendationApiService
{
    Task<IReadOnlyList<기사배차추천항목응답>> 전체조회Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사배차추천항목응답>> 비운행중조회Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사배차추천항목응답>> 운행중조회Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사배차추천항목응답>> 위치검색Async(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사배차추천항목응답>> 전국콜조회Async(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<기사배차추천항목응답>> 공개배차조회Async(CancellationToken cancellationToken = default);
    Task<기사배차추천요약응답?> 요약조회Async(CancellationToken cancellationToken = default);
    Task<기사운송의뢰상세응답?> 운송의뢰상세조회Async(
        string requestId,
        CancellationToken cancellationToken = default);
}

public sealed class DriverRecommendationApiService : IDriverRecommendationApiService
{
    private const string RecommendationsPath = "api/v1/driver/recommendations";
    private readonly IDriverApiClient _client;

    public DriverRecommendationApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public Task<IReadOnlyList<기사배차추천항목응답>> 전체조회Async(
        CancellationToken cancellationToken = default)
        => GetListAsync(RecommendationsPath, "기사 추천 목록 조회", cancellationToken);

    public Task<IReadOnlyList<기사배차추천항목응답>> 비운행중조회Async(
        CancellationToken cancellationToken = default)
        => GetListAsync($"{RecommendationsPath}/idle", "비운행 기사 추천 조회", cancellationToken);

    public Task<IReadOnlyList<기사배차추천항목응답>> 운행중조회Async(
        CancellationToken cancellationToken = default)
        => GetListAsync($"{RecommendationsPath}/driving", "운행중 기사 추천 조회", cancellationToken);

    public Task<IReadOnlyList<기사배차추천항목응답>> 위치검색Async(
        decimal latitude,
        decimal longitude,
        decimal radiusKm,
        CancellationToken cancellationToken = default)
    {
        var query = FormattableString.Invariant(
            $"{RecommendationsPath}/search?latitude={latitude}&longitude={longitude}&radiusKm={radiusKm}");
        return GetListAsync(query, "위치 기반 기사 추천 검색", cancellationToken);
    }

    public Task<IReadOnlyList<기사배차추천항목응답>> 전국콜조회Async(
        CancellationToken cancellationToken = default)
        => GetListAsync($"{RecommendationsPath}/national", "전국콜 조회", cancellationToken);

    public Task<IReadOnlyList<기사배차추천항목응답>> 공개배차조회Async(
        CancellationToken cancellationToken = default)
        => GetListAsync("api/v1/driver/public-dispatches", "공개배차 조회", cancellationToken);

    public Task<기사배차추천요약응답?> 요약조회Async(
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사배차추천요약응답>(
            $"{RecommendationsPath}/summary",
            "기사 추천 요약 조회",
            cancellationToken);

    public Task<기사운송의뢰상세응답?> 운송의뢰상세조회Async(
        string requestId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사운송의뢰상세응답>(
            $"api/v1/driver/requests/{Uri.EscapeDataString(requestId)}",
            "기사 운송의뢰 상세 조회",
            cancellationToken);

    private async Task<IReadOnlyList<기사배차추천항목응답>> GetListAsync(
        string path,
        string operationName,
        CancellationToken cancellationToken)
        => await _client.GetAsync<IReadOnlyList<기사배차추천항목응답>>(
            path,
            operationName,
            cancellationToken) ?? [];
}
