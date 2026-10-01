using Hongdal.Contracts.Driver.Transport;

namespace DriverApp.Services;

public sealed class DriverTransportApiService : IDriverTransportApiService
{
    private const string BasePath = "api/v1/driver/transports";
    private readonly IDriverApiClient _client;

    public DriverTransportApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<기사운송요약응답>> 목록조회Async(
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<기사운송요약응답>>(
            BasePath,
            "운송 목록 조회",
            cancellationToken) ?? [];

    public Task<기사운송요약응답?> 현재조회Async(CancellationToken cancellationToken = default)
        => _client.GetAsync<기사운송요약응답>(
            $"{BasePath}/current",
            "현재 운송 조회",
            cancellationToken);

    public Task<기사운송상세응답?> 상세조회Async(
        long transportId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사운송상세응답>(
            $"{BasePath}/{transportId}",
            "운송 상세 조회",
            cancellationToken);

    public Task<기사운송상태변경응답?> 상차지도착Async(
        long transportId,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운송상태변경응답>(
            $"{BasePath}/{transportId}/arrive-pickup",
            "상차지 도착",
            cancellationToken);

    public Task<기사운송상태변경응답?> 상차완료Async(
        long transportId,
        기사운송상차완료요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운송상차완료요청, 기사운송상태변경응답>(
            $"{BasePath}/{transportId}/pickup-complete",
            request,
            "상차 완료",
            cancellationToken);

    public Task<기사운송상태변경응답?> 하차지도착Async(
        long transportId,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운송상태변경응답>(
            $"{BasePath}/{transportId}/arrive-dropoff",
            "하차지 도착",
            cancellationToken);

    public Task<기사운송상태변경응답?> 하차완료Async(
        long transportId,
        기사운송하차완료요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운송하차완료요청, 기사운송상태변경응답>(
            $"{BasePath}/{transportId}/complete",
            request,
            "하차 완료",
            cancellationToken);

    public Task<기사운송요약응답?> 예외신고Async(
        long transportId,
        기사운송문제신고요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운송문제신고요청, 기사운송요약응답>(
            $"{BasePath}/{transportId}/report-exception",
            request,
            "운송 예외 신고",
            cancellationToken);

    public Task<기사운송요약응답?> 문제신고Async(
        long transportId,
        기사운송문제신고요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사운송문제신고요청, 기사운송요약응답>(
            $"{BasePath}/{transportId}/report-issue",
            request,
            "운송 문제 신고",
            cancellationToken);
}
