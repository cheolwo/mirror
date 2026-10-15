using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Contracts.Common.Privacy;
using SsalddelApp.Models.Shipper;

namespace SsalddelApp.Services;

public interface IShipperOperationsService
{
    Task<신청개인정보동의증적Response?> RecordPrivacyConsentAsync(신청개인정보동의기록Request request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("운송 개인정보 동의 기록이 구성되지 않았습니다.");

    Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default);

    Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default);

    // 인수 확인에는 개발 sample fallback 없이 서버의 같은 의뢰만 조회합니다.
    Task<ShipperRequestItem?> GetRequestForReceiptAsync(string requestId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("인수증 결과 확인이 구성되지 않았습니다.");

    Task RegisterReceiptAsync(string requestId, 화주운송의뢰인수증등록요청 request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("인수증 등록이 구성되지 않았습니다.");

    Task<ShipperRequestItem?> GetRequestByClientRequestIdAsync(string clientRequestId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("접수 요청 조회가 구성되지 않았습니다.");

    Task<IReadOnlyList<공개화물요약응답>> GetPublicCargoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<창고요약응답>> GetWarehousesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<입고요청항목응답>> GetInboundsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<재고항목응답>> GetInventoryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetVehicleTypesAsync(CancellationToken cancellationToken = default);

    Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default);

    Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default);

    Task<ShipperRequestItem> UpdateRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default);

    Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
