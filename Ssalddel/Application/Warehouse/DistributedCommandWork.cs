using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.도메인.창고;

namespace Ssalddel.Application.Warehouse;

// 준비 단계에서 검증한 동일 DbContext의 추적 entity를 복제 없이 실행 책임에 전달합니다.
internal sealed record DistributedReceiveWork(분산입고계획Record Plan, 분산거점Record Site, 분산적재위치Record Location, 분산보관합의Record Agreement, 분산재고LotRecord[] Lots);
internal sealed record DistributedCustodyWork(분산배송WaveRecord Wave, DistributedStopDto? Stop, 분산거점Record Site, 분산적재위치Record Location, 분산재고LotRecord[] Lots,
        분산재고예약Record[] Reservations, 분산배송주문Record[] Orders);
internal sealed record DistributedImportWork(분산거점Record Site, 분산적재위치Record Location, 입고상품 Item, 분산보관합의Record Agreement);
