using MediatR;
using Microsoft.EntityFrameworkCore;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Shipper;
using 살뜰.Data;

namespace Ssalddel.Application.Shipper.Queries;

/// <summary>화주 ID는 인증된 Controller만 공급하며 외부 조회 조건으로 받지 않습니다.</summary>
public sealed record ShipperWarehouseSummaryQuery(string ShipperUserId) : IRequest<ShipperWarehouseSummaryResponse>;

[SsalddelUseCase("화주 본인 창고 요약", Summary = "본인 주문의 입고 예정과 본인 소유 가용 재고 건수만 읽습니다.")]
[SsalddelUseCaseActor(SsalddelActor.Shipper)]
[SsalddelCodeMetadata(SsalddelCodeFeatureKeys.WarehouseInboundVertical, SsalddelCodeLayer.Application,
    "화주 홈의 입고·재고 요약을 인증 주체의 소유 범위로 집계",
    StepKey = "application.shipper-warehouse-summary", FlowOrder = 20,
    ExecutionStage = SsalddelCodeExecutionStage.Preview,
    ReadsFrom = SsalddelCodeDataScope.OperationalState, WritesTo = SsalddelCodeDataScope.None,
    Effects = SsalddelCodeEffect.PersistentRead,
    Boundary = "창고 소유·작업자 역할로 범위를 확대하지 않고 주문자/재고 소유자 본인 건수만 반환한다. 연락처·주소·원자료와 업무 실행 권한을 제공하지 않는다.")]
public sealed class ShipperWarehouseSummaryQueryHandler(SsalddelContext db)
    : IRequestHandler<ShipperWarehouseSummaryQuery, ShipperWarehouseSummaryResponse>
{
    public async Task<ShipperWarehouseSummaryResponse> Handle(ShipperWarehouseSummaryQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ShipperUserId);
        var userId = request.ShipperUserId;
        var pending = await db.입고요청.AsNoTracking()
            .CountAsync(item => item.주문자UserId == userId && item.상태 == 입고상태코드.예정, cancellationToken);
        var available = await db.입고상품.AsNoTracking()
            .CountAsync(item => item.소유자UserId == userId && item.가용수량 > 0, cancellationToken);
        return new() { PendingInboundCount = pending, ActionableInventoryCount = available };
    }
}
