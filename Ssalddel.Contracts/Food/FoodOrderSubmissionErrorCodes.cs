namespace Ssalddel.Contracts.Food;

/// <summary>주문 저장 전 확인된 입력·메뉴 변경만 나타내는 제출 오류입니다.</summary>
public static class FoodOrderSubmissionErrorCodes
{
    public const string MenuPriceChanged = "FOOD_MENU_PRICE_CHANGED";
    public const string MenuUnavailable = "FOOD_MENU_UNAVAILABLE";
    public const string InputInvalid = "FOOD_ORDER_INPUT_INVALID";
}
