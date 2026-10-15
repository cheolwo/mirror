namespace 살뜰.Services.Options;

/// <summary>배송료 선결제의 신규 진입과 입금 알림 연결. 운영 활성화는 별도 설정입니다.</summary>
public sealed class TransportPrepaymentOptions
{
    public const string SectionName = "TransportPrepayment";
    public bool Enabled { get; set; }
    public bool EnableDepositWebhook { get; set; }
    public string PolicyRevision { get; set; } = "transport-prepayment-r73";
}
