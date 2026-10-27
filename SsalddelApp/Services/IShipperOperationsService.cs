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
    Task<차량추천응답> RecommendVehiclesAsync(차량추천요청 request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("현재 화물 조건의 차량 추천 연결을 확인해 주세요.");

    Task<decimal> EstimateFareAsync(string vehicleType, decimal distanceKm, CancellationToken cancellationToken = default);

    // 구판 구현에는 거리만 있는 요청만 위임합니다. 확인된 좌표·추가비를 조용히 버리지 않습니다.
    Task<decimal> EstimateFareAsync(화주운송기준운임견적요청 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.상차위도.HasValue || request.상차경도.HasValue || request.하차위도.HasValue || request.하차경도.HasValue
            || request.대기료 is not null and not 0m || request.수작업비 is not null and not 0m || request.할증 is not null and not 0m)
            throw new NotSupportedException("현재 운임 서비스가 위치·추가비를 포함한 견적을 지원하지 않습니다.");
        return EstimateFareAsync(request.차량종류 ?? string.Empty, request.예상거리Km ?? 0m, cancellationToken);
    }

    Task<ShipperRequestItem> AddRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default);

    Task<ShipperRequestItem> UpdateRequestAsync(ShipperRequestItem request, CancellationToken cancellationToken = default);

    Task DeleteRequestAsync(string requestId, CancellationToken cancellationToken = default);
}
