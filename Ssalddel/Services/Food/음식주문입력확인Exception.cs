using Ssalddel.Contracts.Food;

namespace Ssalddel.Services.Food;

/// <summary>주문 저장 전에 확인된 수정 가능한 입력 또는 메뉴 상태의 실패입니다.</summary>
public sealed class 음식주문입력확인Exception(
    string message,
    string errorCode = FoodOrderSubmissionErrorCodes.InputInvalid) : ArgumentException(message)
{
    public string ErrorCode { get; } = errorCode;
}
