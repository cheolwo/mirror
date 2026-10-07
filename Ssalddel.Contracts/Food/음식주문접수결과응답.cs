namespace Ssalddel.Contracts.Food;

/// <summary>로그인 소유자의 제출 요청 ID로 이미 접수된 주문번호만 돌려줍니다.</summary>
public sealed class 음식주문접수결과응답
{
    public string 주문번호 { get; set; } = string.Empty;
}
