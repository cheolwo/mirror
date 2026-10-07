using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Hubs;
using Ssalddel.Services.Community;
using 살뜰.도메인.공통;
using 살뜰.도메인.운송;
using 살뜰.도메인.화주;

namespace Ssalddel.Application.Driver.Recommendation;

/// <summary>생활 배송의 명시적 기사 제공 동의를 기존 화물 조회에 결속합니다.</summary>
public static class 생활배송기사정보공개Policy
{
    public const string PickupPending = "픽업 장소 · 정보 제공 동의 대기";
    public const string DropoffPending = "전달 장소 · 정보 제공 동의 대기";

    public static bool 생활배송인가(string? clientRequestId)
        => clientRequestId?.StartsWith(NeighborhoodDeliveryRoutes.ClientRequestPrefix, StringComparison.Ordinal) == true;

    public static bool 현재관계있는기사인가(운송원장? queue, string driverId, DateTime nowUtc)
    {
        if (queue is null || string.IsNullOrWhiteSpace(driverId)
            || queue.배차업무유형 != 상태값.배차업무유형.용달운송) return false;
        if (!string.IsNullOrWhiteSpace(queue.확정기사Id))
            return string.Equals(queue.확정기사Id, driverId, StringComparison.Ordinal);
        return queue.배차노출상태 == 상태값.배차노출상태.추천중
               && queue.추천만료시각 > nowUtc
               && string.Equals(queue.현재추천대상기사Id, driverId, StringComparison.Ordinal);
    }

    public static Task<bool> 정보제공가능인가Async(
        화주운송의뢰 request, string driverId, I생활배송기사정보제공동의Service? disclosure,
        CancellationToken cancellationToken)
        => 정보제공가능인가Async(request.클라이언트요청Id, request.의뢰Id, request.주문자UserId,
            driverId, disclosure, cancellationToken);

    public static Task<bool> 정보제공가능인가Async(
        string? clientRequestId, string requestId, string ownerUserId, string driverId,
        I생활배송기사정보제공동의Service? disclosure, CancellationToken cancellationToken)
        => !생활배송인가(clientRequestId)
            ? Task.FromResult(true)
            : disclosure?.유효한기사제공동의인가Async(requestId, driverId, ownerUserId, cancellationToken)
              ?? Task.FromResult(false);

    public static void 추천정보가림(DispatchRecommendationDto value)
    {
        value.픽업지 = PickupPending; value.하차지 = DropoffPending;
        value.픽업_위도 = null; value.픽업_경도 = null; value.하차_위도 = null; value.하차_경도 = null;
    }

    public static void 의뢰상세가림(기사운송의뢰상세응답 value)
    {
        value.픽업지 = PickupPending; value.픽업상세지 = string.Empty;
        value.하차지 = DropoffPending; value.하차상세지 = string.Empty;
        value.픽업위도 = null; value.픽업경도 = null; value.하차위도 = null; value.하차경도 = null;
        value.정산메모 = null; value.화물설명 = string.Empty;
    }

    public static void 의뢰내부정보가림(기사운송의뢰상세응답 value)
        => value.정산메모 = null;

    public static void 운송내부정보가림(기사운송요약응답 value)
    {
        if (value is 기사운송상세응답 detail) { detail.첨부Json = string.Empty; detail.메모 = string.Empty; }
    }

    public static void 운송정보가림(기사운송요약응답 value)
    {
        value.개인정보제공보류 = true;
        value.출발지 = PickupPending; value.도착지 = DropoffPending;
        value.상차담당자명 = string.Empty; value.상차연락처 = string.Empty;
        value.수령자명 = string.Empty; value.수령자연락처 = string.Empty; value.전달요청 = string.Empty;
        value.최근예외메시지 = string.Empty;
        value.예외검토목록 = [];
        value.가능한행동 = [];
        value.다음행동안내 = "선택한 기사에게 정보를 제공하는 동의를 기다리고 있습니다.";
        운송내부정보가림(value);
    }
}
