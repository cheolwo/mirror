using Ssalddel.Contracts.Common.Community;
namespace Ssalddel.Services.Community;

/// <summary>개인 배송 입력은 기존 보호된 협업 payload 안에만 보관합니다.</summary>
public sealed class 생활배송연결IntentRecord
{
    public Guid ClientRequestId { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public long TermsRevision { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string StateCode { get; set; } = "pending";
    public NeighborhoodDeliveryRequest Request { get; set; } = new();
}
