using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>의뢰자의 픽업 전 취소는 원 의뢰 원장과 회원 배송의 판본을 함께 확인합니다.</summary>
public sealed class NeighborhoodMemberRequestViewModel(INeighborhoodMemberDeliveryClient member, INeighborhoodDeliveryClient sources,
    ISsalddel현재사용자Context user, NeighborhoodMemberDeliverySession session) : NeighborhoodMemberPageViewModel(user, session)
{
    public NeighborhoodDeliveryResponse? Source { get; private set; }
    public bool CancellationAgreed { get; set; }
    public bool CanCancel => !IsSending && !IsLoading && Source is { MemberDeliveryRequested: true } source
        && (Session.Matches("request-cancel", source.RequestId) || Pending is null && source.CanCancelMemberDelivery && CancellationAgreed);
    protected override void ResetForOwner() { Source = null; CancellationAgreed = false; }
    public async Task LoadAsync(string requestId)
    {
        if (IsSending || !await PrepareAsync()) return;
        var operation = Begin(); IsLoading = true; Error = null; Changed();
        try
        {
            var source = await sources.ReadAsync(requestId, Token); if (!Current(operation)) return;
            if (source?.RequestId != requestId || !source.MemberDeliveryRequested) throw new InvalidOperationException("본인 회원 배송 의뢰 미확인");
            Source = source;
            if (Session.Matches("request-cancel", requestId) && source.MemberWorkStateCode == "Cancelled"
                && source.MemberWorkRevision > Pending!.ExpectedRevision)
            { await Session.ClearAsync(Owner!, Token); if (Current(operation)) Notice = "배송 취소를 확인했습니다."; }
        }
        catch (Exception ex) { if (Current(operation)) { Source = null; ApplyError(ex); } }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task<bool> CancelAsync()
    {
        if (!await PrepareAsync() || !CanCancel || Source is not { } source || Owner is null) return false;
        var operation = Begin(); IsSending = true; Error = Notice = null; Changed();
        try
        {
            var body = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = source.MemberWorkRevision };
            body = await PrepareCommandAsync(Owner, "request-cancel", source.RequestId, body.ClientRequestId, body.ExpectedRevision, body, Token);
            if (!Current(operation)) return false;
            await member.CancelRequestAsync(source.RequestId, body, Token); if (!Current(operation)) return false;
            var latest = await sources.ReadAsync(source.RequestId, Token); if (!Current(operation)) return false;
            if (latest?.RequestId != source.RequestId || latest.MemberWorkStateCode != "Cancelled" || latest.MemberWorkRevision <= body.ExpectedRevision)
                throw new InvalidOperationException("원 의뢰 취소 결과 미확인");
            await Session.ClearAsync(Owner, Token, body.ClientRequestId); if (!Current(operation)) return false;
            Source = latest; CancellationAgreed = false; Notice = "픽업 전 배송 취소를 확인했습니다. 지급 확인은 별도로 처리합니다."; return true;
        }
        catch (Exception ex) { await RejectPendingAsync(ex, operation); return false; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
}
