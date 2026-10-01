using Hongdal.Contracts.Shipper.Request;

namespace HongdalApp.Services;

public interface IShipperBulkRequestService
{
    Task<화주운송의뢰일괄미리보기응답?> 미리보기Async(Stream stream, string fileName, CancellationToken cancellationToken = default);

    Task<화주운송의뢰일괄등록결과응답?> 등록Async(화주운송의뢰일괄확정등록요청 confirmRequest, CancellationToken cancellationToken = default);
}
