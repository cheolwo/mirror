using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using SsalddelApp.Models.Shipper;

namespace SsalddelApp.Services;

public interface IShipperOperationsService
{
    Task<IReadOnlyList<ShipperRequestItem>> GetRequestsAsync(CancellationToken cancellationToken = default);

    Task<ShipperRequestItem?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default);

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
