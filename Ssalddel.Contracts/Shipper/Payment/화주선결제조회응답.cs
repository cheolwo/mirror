namespace Ssalddel.Contracts.Shipper.Payment;

/// <summary>현재 화주의 같은 의뢰에 대한 접수 전 결제 상태입니다. PG 키·계좌·원문 응답은 제공하지 않습니다.</summary>
public sealed class 화주선결제조회응답
{
    public string 의뢰Id { get; set; } = string.Empty;
    public int Amount { get; set; }
    public bool Enabled { get; set; }
    public bool CanPay { get; set; }
    public bool SimulationAvailable { get; set; }
    public bool PaymentConfirmed { get; set; }
    public bool ResultCheckRequired { get; set; }
    public bool SimulationPayment { get; set; }
    public string DispatchStatus { get; set; } = string.Empty;
}
