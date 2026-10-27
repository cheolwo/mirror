using RestaurantDeskApp.Models.Restaurant;
using Ssalddel.Contracts.Food;

namespace RestaurantDeskApp.Services;

public enum RestaurantPosQueue { All, New, InProgress, Attention }

// 목록의 선택·필터와 표시만 소유한다. 업무 가능 여부는 서버 AvailableActions를 따른다.
public sealed class RestaurantPosPresentation
{
    public RestaurantPosQueue Queue { get; set; } = RestaurantPosQueue.All;
    public string? SelectedOrderNo { get; private set; }
    public bool CommandInProgress { get; private set; }

    public bool Select(string orderNo)
    {
        if (CommandInProgress || string.IsNullOrWhiteSpace(orderNo)) return false;
        SelectedOrderNo = orderNo;
        return true;
    }

    public void SetCommandInProgress(bool busy) => CommandInProgress = busy;
    public void Clear() { SelectedOrderNo = null; CommandInProgress = false; }

    public static bool IsNew(음식점주문DeskItem item) => item.상태 == 음식점주문Desk상태코드.주문대기;
    public static bool IsClosed(음식점주문DeskItem item) => item.상태 is 음식점주문Desk상태코드.취소
        or 음식점주문Desk상태코드.거절 or 음식점주문Desk상태코드.수령확인;
    public static bool NeedsAttention(음식점주문DeskItem item) => !IsClosed(item)
        && (item.상태 == 음식점주문Desk상태코드.상세조회실패
            || item.배차상태 == 음식주문배차상태코드.배차불가);
    public static bool IsInProgress(음식점주문DeskItem item) => !IsNew(item) && !IsClosed(item);

    public IReadOnlyList<음식점주문DeskItem> VisibleOrders(IEnumerable<음식점주문DeskItem> orders)
        => orders.Where(item => Queue switch
        {
            RestaurantPosQueue.New => IsNew(item),
            RestaurantPosQueue.InProgress => IsInProgress(item),
            RestaurantPosQueue.Attention => NeedsAttention(item),
            _ => true
        }).OrderBy(item => item.접수시각).ThenBy(item => item.주문번호, StringComparer.Ordinal).ToArray();

    public static string StatusLabel(음식점주문DeskItem item) => item.재조리주문 && !IsClosed(item)
        && item.상태 is 음식주문상태코드.주문확인 or 음식점주문Desk상태코드.기사배정
            or 음식점주문Desk상태코드.조리중 or 음식점주문Desk상태코드.픽업대기
        ? item.현재픽업준비완료 ? "재조리 준비 완료" : item.현재조리시작확인 ? "재조리 중" : "재조리 대기"
        : item.상태 switch
        {
            음식점주문Desk상태코드.주문대기 => "신규 주문",
            음식주문상태코드.주문확인 => "기사 배정 대기",
            음식점주문Desk상태코드.수락처리중 => "수락 확인 중",
            음식점주문Desk상태코드.기사배정 => "조리 시작 대기",
            음식점주문Desk상태코드.조리중 => "조리 중",
            음식점주문Desk상태코드.픽업대기 => "픽업 준비 완료",
            음식점주문Desk상태코드.픽업완료 => "기사 픽업 완료",
            음식점주문Desk상태코드.전달완료 => "전달 완료 · 수령 대기",
            음식점주문Desk상태코드.수령확인 => "수령 확인",
            음식점주문Desk상태코드.취소 => "주문 취소",
            음식점주문Desk상태코드.거절 => "주문 거절",
            음식점주문Desk상태코드.상세조회실패 => "상태 확인 필요",
            _ => item.상태
        };
}
