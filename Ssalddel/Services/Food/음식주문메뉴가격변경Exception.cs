namespace Ssalddel.Services.Food;

/// <summary>주문자가 확인한 가격과 최신 메뉴 가격이 달라 아직 주문을 저장하지 않았습니다.</summary>
public sealed class 음식주문메뉴가격변경Exception()
    : InvalidOperationException("메뉴 가격이 변경되었습니다. 최신 메뉴와 금액을 확인한 뒤 다시 주문해 주세요.")
{
}
