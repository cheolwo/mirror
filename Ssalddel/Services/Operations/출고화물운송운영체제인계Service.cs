using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using 살뜰.Services.Operations;
using 살뜰.도메인.창고;

namespace Ssalddel.Services.Operations;

public interface I출고화물운송운영체제인계Service
{
    Task<운영체제업무인계Dto> 인계Async(출고예정 plan, CancellationToken cancellationToken);
}

/// <summary>
/// 창고 출고 완료가 이미 생성된 화물 운송 의뢰의 실행 책임으로 넘어갔음을 OS 인계 원장에 기록합니다.
/// 운송 의뢰나 배차를 새로 확정하지 않으며, 기존 Cargo 업무 고유 식별자만 결속합니다.
/// </summary>
public sealed class 출고화물운송운영체제인계Service(
    I운영체제업무인계Coordinator coordinator) : I출고화물운송운영체제인계Service
{
    public async Task<운영체제업무인계Dto> 인계Async(
        출고예정 plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.상태 != 출고상태.출고완료
            || plan.출고처리일시 is null
            || string.IsNullOrWhiteSpace(plan.운송의뢰Id))
        {
            throw new InvalidOperationException("완료된 출고와 기존 화물 운송 의뢰가 있어야 OS 인계를 기록할 수 있습니다.");
        }

        var handoffAt = AsUtc(plan.출고처리일시.Value);
        var requestId = DeterministicGuid($"warehouse-outbound:{plan.Id}:cargo-handoff:request:v1");
        var decisionId = DeterministicGuid($"warehouse-outbound:{plan.Id}:cargo-handoff:accept:v1");
        var handoff = await coordinator.요청Async(new 운영체제업무인계생성요청
        {
            클라이언트요청Id = requestId,
            출발운영체제Id = OperatingSystemIds.WarehouseCommerceFulfillment,
            도착운영체제Id = OperatingSystemIds.DomesticCargoTransport,
            출발업무유형Code = "WarehouseOutboundPlan",
            출발업무StableId = $"warehouse-outbound:{plan.Id}",
            출발업무Revision = handoffAt.Ticks,
            인계계약Code = "WarehouseOutboundToCargoTransport",
            인계계약Revision = "warehouse-outbound-cargo-handoff.v1",
            최소상태사본Json = JsonSerializer.Serialize(new
            {
                outboundPlanStableId = $"warehouse-outbound:{plan.Id}",
                cargoRequestStableId = $"cargo-request:{plan.운송의뢰Id}",
                quantity = plan.수량,
                handedOffAtUtc = handoffAt
            }),
            만료시각Utc = handoffAt.AddHours(2)
        }, cancellationToken);

        return await coordinator.결정Async(handoff.인계StableId, new 운영체제업무인계결정요청
        {
            클라이언트요청Id = decisionId,
            예상Revision = handoff.Revision,
            응답운영체제Id = OperatingSystemIds.DomesticCargoTransport,
            결정Code = 운영체제업무인계결정Codes.수락,
            도착업무StableId = $"cargo-request:{plan.운송의뢰Id}"
        }, cancellationToken);
    }

    private static DateTime AsUtc(DateTime value)
        => value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, bytes.Length);
        return new Guid(bytes);
    }
}
