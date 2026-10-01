using Hongdal.Contracts.Driver.Reservation;

namespace DriverApp.Services;

public interface IDriverReservationApiService
{
    Task<IReadOnlyList<기사예약목록응답>> 목록조회Async(CancellationToken cancellationToken = default);
    Task<기사예약응답?> 생성Async(기사예약요청 request, CancellationToken cancellationToken = default);
    Task<기사예약취소응답?> 취소Async(long reservationId, CancellationToken cancellationToken = default);
    Task<기사예약응답?> 상세조회Async(long reservationId, CancellationToken cancellationToken = default);
}

public sealed class DriverReservationApiService : IDriverReservationApiService
{
    private const string BasePath = "api/v1/driver/reservations";
    private readonly IDriverApiClient _client;

    public DriverReservationApiService(IDriverApiClient client)
    {
        _client = client;
    }

    public async Task<IReadOnlyList<기사예약목록응답>> 목록조회Async(
        CancellationToken cancellationToken = default)
        => await _client.GetAsync<IReadOnlyList<기사예약목록응답>>(
            BasePath,
            "기사 예약 목록 조회",
            cancellationToken) ?? [];

    public Task<기사예약응답?> 생성Async(
        기사예약요청 request,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사예약요청, 기사예약응답>(
            BasePath,
            request,
            "기사 예약 생성",
            cancellationToken);

    public Task<기사예약취소응답?> 취소Async(
        long reservationId,
        CancellationToken cancellationToken = default)
        => _client.PostAsync<기사예약취소응답>(
            $"{BasePath}/{reservationId}/cancel",
            "기사 예약 취소",
            cancellationToken);

    public Task<기사예약응답?> 상세조회Async(
        long reservationId,
        CancellationToken cancellationToken = default)
        => _client.GetAsync<기사예약응답>(
            $"{BasePath}/{reservationId}",
            "기사 예약 상세 조회",
            cancellationToken);
}
