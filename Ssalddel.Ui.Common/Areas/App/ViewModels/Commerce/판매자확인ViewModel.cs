using Ssalddel.Contracts.Common.Commerce;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels.Commerce;

public sealed class 판매자확인ViewModel(I통신판매보호Client client, ISsalddel현재사용자Context user) : NeighborhoodWorkflowViewModel(user)
{
    public 판매자등록Request Draft { get; private set; } = NewDraft();
    public 판매자확인Response? Value { get; private set; }
    public 통신판매안내Response? Policy { get; private set; }
    public bool Loaded { get; private set; }
    public bool HasPending { get; private set; }
    private 판매자등록Request? _pending;
    public async Task LoadAsync()
    {
        if (!CheckOwner() || IsLoading || IsSending) return;
        var op = Begin(); IsLoading = true; Error = null; Changed();
        try
        {
            var policy = await client.안내Async(Token) ?? throw new InvalidOperationException();
            var value = await client.판매자Async(Token) ?? throw new InvalidOperationException();
            if (!Current(op)) return;
            Policy = policy; Value = value; Loaded = true;
            if (!HasPending) Draft.ExpectedRevision = value.Revision;
        }
        catch (Exception ex) { if (Current(op)) ApplyError(ex); }
        finally { if (Current(op)) { IsLoading = false; Changed(); } }
    }
    public async Task SaveAsync()
    {
        if (!CheckOwner() || IsSending || Policy is null || !Draft.CollectionConsentAccepted) return;
        _pending ??= new 판매자등록Request
        {
            ClientRequestId = Draft.ClientRequestId, ExpectedRevision = Draft.ExpectedRevision, SellerKind = Draft.SellerKind,
            DisplayName = Draft.DisplayName, RepresentativeName = Draft.RepresentativeName, BusinessAddress = Draft.BusinessAddress,
            PhoneNumber = Draft.PhoneNumber, Email = Draft.Email, BusinessRegistrationNumber = Draft.BusinessRegistrationNumber,
            MailOrderRegistrationNumber = Draft.MailOrderRegistrationNumber, MailOrderRegistrationExemptionReason = Draft.MailOrderRegistrationExemptionReason,
            NoticeVersion = Policy.Version, CollectionConsentAccepted = true
        };
        var op = Begin(); IsSending = true; Error = null; Notice = null; HasPending = true; Changed();
        try
        {
            var value = await client.등록Async(_pending, Token) ?? throw new InvalidOperationException();
            if (!Current(op)) return;
            Value = value; Draft = NewDraft(); Draft.ExpectedRevision = value.Revision; _pending = null; HasPending = false;
            Notice = "판매자 정보를 저장했습니다. 신원 확인 완료와는 별도입니다.";
        }
        catch (Exception ex) { if (Current(op)) { ApplyError(ex, true); if (DefinitiveRejection(ex)) { _pending = null; HasPending = false; } } }
        finally { if (Current(op)) { IsSending = false; Changed(); } }
    }
    protected override void ResetForOwner() { Value = null; Policy = null; Loaded = false; Draft = NewDraft(); _pending = null; HasPending = false; }
    private static 판매자등록Request NewDraft() => new() { ClientRequestId = Guid.NewGuid(), SellerKind = 판매자유형Codes.개인 };
}
