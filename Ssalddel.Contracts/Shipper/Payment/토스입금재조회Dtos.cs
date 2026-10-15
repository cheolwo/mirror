namespace Ssalddel.Contracts.Shipper.Payment;

/// <summary>클라이언트의 입금 상태·금액을 신뢰하지 않고 저장된 같은 결제를 PG에서 조회합니다.</summary>
public sealed class 토스입금재조회요청
{
    public string OrderId { get; set; } = string.Empty;
}
