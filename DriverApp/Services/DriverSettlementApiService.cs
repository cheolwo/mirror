using Hongdal.Contracts.Driver.Settlement;

namespace DriverApp.Services;

public interface IDriverSettlementApiService
{
    Task<IReadOnlyList<기사정산월요약응답>> 목록조회Async(CancellationToken cancellationToken = default);
    Task<기사정산응답?> 월별조회Async(int year, int month, CancellationToken cancellationToken = default);
    Task<기사정산응답?> 현재월조회Async(CancellationToken cancellationToken = default);
}

public sealed class DriverSettlementApiService : IDriverSettlementApiService
{
    private const string BasePath = "api/v1/driver/settlements";
    private readonly IDriverApiClient _client;

    public DriverSettlementApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<기사정산월요약응답>> 목록조회Async(
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<기사정산월요약응답>>(
            BasePath,
            "기사 정산 목록 조회",
            cancellationToken) ?? [];

    public Task<기사정산응답?> 월별조회Async(
        int year,
        int month,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사정산응답>(
            $"{BasePath}/{year}/{month}",
            "기사 월별 정산 조회",
            cancellationToken);

    public Task<기사정산응답?> 현재월조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사정산응답>(
            $"{BasePath}/current-month",
            "기사 현재 월 정산 조회",
            cancellationToken);
}
