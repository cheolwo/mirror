using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Food;

public sealed record 음식주문저장결과(
    음식주문응답 주문,
    bool 새로생성됨);

public sealed record 음식주문변경결과(
    음식주문응답 주문,
    bool 새로변경됨);

public interface ISsalddelFoodOrderStore
{
    음식주문목록응답 GetOrders();

    음식주문응답? GetOrder(string orderNo);

    /// <summary>동일 주문자의 제출 요청으로 이미 접수된 원장을 조회합니다.</summary>
    음식주문응답? 접수주문조회(string 주문자UserId, Guid 클라이언트요청Id)
        => null;

    음식주문응답 AddOrder(음식주문등록요청 request);

    음식주문저장결과 멱등등록(음식주문등록요청 request)
        => new(AddOrder(request), true);

    음식주문응답? 음식점수락(string orderNo, 음식점주문수락요청 request);

    음식주문변경결과? 음식점수락멱등(
        string orderNo,
        음식점주문수락요청 request,
        string 처리UserId)
    {
        var order = 음식점수락(orderNo, request);
        return order is null ? null : new 음식주문변경결과(order, true);
    }

    음식주문변경결과? 음식점진행변경(
        string orderNo,
        음식점주문진행변경요청 request,
        string 처리UserId)
        => throw new NotSupportedException("이 음식 주문 저장소는 음식점 진행 변경을 지원하지 않습니다.");

    음식주문변경결과? 주문자취소(
        string orderNo,
        주문자음식주문취소요청 request,
        string 주문자UserId)
        => throw new NotSupportedException("이 음식 주문 저장소는 주문자 취소를 지원하지 않습니다.");

    음식주문변경결과? 주문자수령확인(
        string orderNo,
        주문자음식주문수령확인요청 request,
        string 주문자UserId)
        => throw new NotSupportedException("이 음식 주문 저장소는 주문자 수령 확인을 지원하지 않습니다.");

    음식주문응답? 배차대기반영(string orderNo, long dispatchWaitId, DateTime dispatchRequestedAtUtc);
}

public interface I커뮤니티원장반영가능음식주문Store
{
    음식주문응답? 커뮤니티원장반영(
        string orderNo,
        string ledgerId,
        string ledgerTemplateKey,
        string ledgerState,
        DateTime syncedAtUtc);
}
