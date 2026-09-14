namespace Ssalddel.Contracts.Food;

public static class 음식배달가능행동Ids
{
    public const string 주문취소 = "FoodOrder.Cancel";
    public const string 주문수령확인 = "FoodOrder.ConfirmReceipt";
    public const string 음식점주문수락 = "FoodOrder.RestaurantAccept";
    public const string 음식점주문거절 = "FoodOrder.RestaurantReject";
    public const string 음식점조리시간변경 = "FoodOrder.ChangePreparationTime";
    public const string 음식점픽업준비완료 = "FoodOrder.MarkReadyForPickup";
    public const string 기사제안수락 = "FoodDelivery.AcceptOffer";
    public const string 기사제안거절 = "FoodDelivery.RejectOffer";
    public const string 기사가게도착 = "FoodDelivery.RecordRestaurantArrival";
    public const string 기사픽업확인 = "FoodDelivery.ConfirmPickup";
    public const string 기사전달완료 = "FoodDelivery.CompleteDelivery";
}
