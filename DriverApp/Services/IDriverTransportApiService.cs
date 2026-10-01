using Hongdal.Contracts.Driver.Transport;

namespace DriverApp.Services;

public interface IDriverTransportApiService
{
    Task<IReadOnlyList<기사운송요약응답>> 목록조회Async(CancellationToken cancellationToken = default);
    Task<기사운송요약응답?> 현재조회Async(CancellationToken cancellationToken = default);
    Task<기사운송상세응답?> 상세조회Async(long transportId, CancellationToken cancellationToken = default);
    Task<기사운송상태변경응답?> 상차지도착Async(long transportId, CancellationToken cancellationToken = default);
    Task<기사운송상태변경응답?> 상차완료Async(
        long transportId,
        기사운송상차완료요청 request,
        CancellationToken cancellationToken = default);
    Task<기사운송상태변경응답?> 하차지도착Async(long transportId, CancellationToken cancellationToken = default);
    Task<기사운송상태변경응답?> 하차완료Async(
        long transportId,
        기사운송하차완료요청 request,
        CancellationToken cancellationToken = default);
    Task<기사운송요약응답?> 문제신고Async(
        long transportId,
        기사운송문제신고요청 request,
        CancellationToken cancellationToken = default);
    Task<기사운송요약응답?> 예외신고Async(
        long transportId,
        기사운송문제신고요청 request,
        CancellationToken cancellationToken = default);
}
