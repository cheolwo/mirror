using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ssalddel.Contracts.Common.Operations;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Warehouse;
using 살뜰.Services.Dispatch.Engine;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Operations;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.운영;

namespace Ssalddel.Services.Operations;

public interface I살뜰마트라스트마일배차인계Service
{
    Task<살뜰마트라스트마일배차인계결과> 인계Async(
        살뜰마트라스트마일배차인계요청 요청,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 마트 OS가 소유한 주문·출고와 음식배달 OS가 소유할 라스트마일 배차 업무 사이를 조율합니다.
/// 마트 주문 전체가 아니라 별도 라스트마일 자식 업무만 인계하며, 배차 원장이 만들어진 뒤에만 수락합니다.
/// </summary>
public sealed class 살뜰마트라스트마일배차인계Service(
    I운영체제업무인계Coordinator handoffCoordinator,
    I운송의뢰배차대기Service dispatchQueueService,
    살뜰마트라스트마일배차Policy policy) : I살뜰마트라스트마일배차인계Service
{
    public async Task<살뜰마트라스트마일배차인계결과> 인계Async(
        살뜰마트라스트마일배차인계요청 요청,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(요청);
        ArgumentException.ThrowIfNullOrWhiteSpace(요청.주문참조번호);
        ArgumentNullException.ThrowIfNull(요청.배차대상);
        if (요청.출발업무Revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(요청), "출발 업무 revision은 음수일 수 없습니다.");
        }
        if (요청.라인수 <= 0 || 요청.총수량 <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(요청), "인계할 마트 주문에는 한 개 이상의 라인과 수량이 필요합니다.");
        }

        var orderRef = 요청.주문참조번호.Trim();
        var decision = policy.Evaluate(요청.정책입력);
        if (!decision.후보등록)
        {
            throw new InvalidOperationException("피킹 시작 전에는 마트 라스트마일 인계 후보를 등록할 수 없습니다.");
        }

        var requestId = DeterministicGuid($"mart-last-mile:{orderRef}:handoff:v1");
        var handoffStableId = $"os-handoff:{requestId:N}";
        var handoff = await handoffCoordinator.조회Async(handoffStableId, cancellationToken);
        if (handoff is not null)
        {
            기존인계검증(handoff, orderRef, 요청.출발업무Revision);
        }
        else
        {
            handoff = await handoffCoordinator.요청Async(new 운영체제업무인계생성요청
            {
                클라이언트요청Id = requestId,
                출발운영체제Id = OperatingSystemIds.SsalddelMartUrbanLogistics,
                도착운영체제Id = OperatingSystemIds.FoodDelivery,
                출발업무유형Code = "MartLastMileDeliveryRequest",
                출발업무StableId = $"mart-last-mile:{orderRef}",
                출발업무Revision = 요청.출발업무Revision,
                인계계약Code = "SsalddelMartLastMileToFoodDelivery",
                인계계약Revision = "ssalddel-mart-last-mile-handoff.v1",
                최소상태사본Json = JsonSerializer.Serialize(new
                {
                    martOrderStableId = $"mart-order:{orderRef}",
                    lastMileRequestStableId = $"mart-last-mile:{orderRef}",
                    preparationStageCode = 요청.정책입력.준비단계Code,
                    orderConfirmedAtUtc = 요청.정책입력.주문확정시각Utc,
                    evaluatedAtUtc = 요청.정책입력.판단시각Utc,
                    expectedReadyAtUtc = 요청.정책입력.픽업준비예정시각Utc,
                    internalTargetAtUtc = decision.내부목표시각Utc,
                    customerPromiseDeadlineAtUtc = decision.고객약속상한시각Utc,
                    strategyCode = decision.전략Code,
                    policyRevision = decision.정책Revision,
                    maxBundleOrderCount = decision.최대묶음주문수,
                    pendingOrderCount = 요청.정책입력.배차대기주문수,
                    availableCourierCount = 요청.정책입력.배차가능기사수,
                    reasonCodes = decision.사유Codes,
                    lineCount = 요청.라인수,
                    totalQuantity = 요청.총수량
                }),
                만료시각Utc = 요청.정책입력.판단시각Utc.AddHours(24)
            }, cancellationToken);
        }

        if (handoff.상태Code is 운영체제업무인계상태Codes.거절됨 or 운영체제업무인계상태Codes.만료됨)
        {
            throw new InvalidOperationException($"종결된 마트 라스트마일 인계로 배차를 만들 수 없습니다. Status={handoff.상태Code}");
        }

        if (!decision.지금기사제안)
        {
            return new 살뜰마트라스트마일배차인계결과(handoff, null, decision);
        }

        var queue = await dispatchQueueService.생성또는조회Async(
            요청.배차대상,
            new 운송의뢰배차대기생성옵션
            {
                의뢰Id = orderRef,
                화주Id = 요청.배차대상.판매자UserId,
                배차업무유형 = 상태값.배차업무유형.음식배달,
                원본의뢰유형 = 운송의뢰배차원천유형.살뜰마트포장완료주문,
                원본의뢰Id = orderRef,
                상태 = 상태값.배차대기상태.대기
            },
            cancellationToken);

        var accepted = await handoffCoordinator.결정Async(
            handoff.인계StableId,
            new 운영체제업무인계결정요청
            {
                클라이언트요청Id = DeterministicGuid($"mart-last-mile:{orderRef}:accept:v1"),
                예상Revision = handoff.Revision,
                응답운영체제Id = OperatingSystemIds.FoodDelivery,
                결정Code = 운영체제업무인계결정Codes.수락,
                도착업무StableId = $"food-delivery-dispatch-request:{queue.의뢰Id}"
            },
            cancellationToken);

        return new 살뜰마트라스트마일배차인계결과(accepted, queue, decision);
    }

    private static void 기존인계검증(
        운영체제업무인계Dto handoff,
        string orderRef,
        long sourceRevision)
    {
        if (handoff.출발운영체제Id != OperatingSystemIds.SsalddelMartUrbanLogistics
            || handoff.도착운영체제Id != OperatingSystemIds.FoodDelivery
            || handoff.출발업무유형Code != "MartLastMileDeliveryRequest"
            || handoff.출발업무StableId != $"mart-last-mile:{orderRef}"
            || handoff.출발업무Revision != sourceRevision
            || handoff.인계계약Code != "SsalddelMartLastMileToFoodDelivery"
            || handoff.인계계약Revision != "ssalddel-mart-last-mile-handoff.v1")
        {
            throw new InvalidOperationException("기존 마트 라스트마일 인계가 현재 주문·revision·계약과 일치하지 않습니다.");
        }
    }

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, bytes.Length);
        return new Guid(bytes);
    }
}

public sealed record 살뜰마트라스트마일배차인계요청(
    string 주문참조번호,
    long 출발업무Revision,
    int 라인수,
    int 총수량,
    출고예정운송대상 배차대상,
    살뜰마트라스트마일배차입력 정책입력);

public sealed record 살뜰마트라스트마일배차인계결과(
    운영체제업무인계Dto 인계,
    운송원장? 배차대기,
    살뜰마트라스트마일배차판정 정책판정);
