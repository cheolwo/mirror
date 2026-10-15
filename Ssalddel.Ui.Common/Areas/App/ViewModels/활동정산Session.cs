using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>같은 로그인 범위의 미확인 요청 번호를 보존합니다. 계정 전환 때 원문과 번호를 모두 비웁니다.</summary>
public sealed class 활동정산Session
{
    public string? Owner { get; private set; }
    public string? AgreementId { get; private set; }
    public string? Kind { get; private set; }
    public NeighborhoodSettlementCommand? Command { get; private set; }
    public void Bind(string? owner)
    {
        if (Owner == owner) return;
        Owner = owner; Clear();
    }
    public void Prepare(string agreementId, string kind, NeighborhoodSettlementCommand command)
    { AgreementId = agreementId; Kind = kind; Command = command; }
    public void Clear() { AgreementId = null; Kind = null; Command = null; }
}
