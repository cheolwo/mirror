using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

/// <summary>생활 회원 화면 수명과 계정별 미확정 명령을 연결합니다.</summary>
public abstract class NeighborhoodMemberPageViewModel(ISsalddel현재사용자Context user, NeighborhoodMemberDeliverySession session)
    : NeighborhoodWorkflowViewModel(user)
{
    protected NeighborhoodMemberDeliverySession Session => session;
    private Guid? _activeRequestId;
    protected async Task<T> PrepareCommandAsync<T>(string owner, string kind, string target, Guid requestId, long revision, T body, CancellationToken ct)
    {
        var result = await Session.PrepareAsync(owner, kind, target, requestId, revision, body, ct);
        _activeRequestId = Session.Pending?.ClientRequestId;
        return result;
    }
    public NeighborhoodMemberPendingOperation? Pending => session.Owner == Owner ? session.Pending : null;
    public bool InputsLocked => IsLoading || IsSending || Pending is not null;
    protected new void ApplyError(Exception exception, bool writing = false)
    {
        if (exception is SsalddelApiException { ErrorCode: "MemberDeliveryDisabled" }) Error = "생활 배송 서비스가 아직 열리지 않았습니다. 준비가 끝나면 제안을 받을 수 있습니다.";
        else base.ApplyError(exception, writing);
    }
    protected async Task<bool> PrepareAsync()
    {
        if (!CheckOwner() || Owner is null) return false;
        var owner = Owner;
        try { await session.BindAsync(owner, Token); }
        catch (Exception ex) { ApplyError(ex); Changed(); return false; }
        return !Disposed && IsAuthenticated && Owner == owner && user.현재사용자.UserId == owner;
    }
    protected async Task RejectPendingAsync(Exception ex, (long Generation, string? Owner) operation)
    {
        if (!Current(operation)) return;
        if (DefinitiveRejection(ex) && operation.Owner is not null)
        {
            try { await Session.ClearAsync(operation.Owner, Token, _activeRequestId); }
            catch { if (Current(operation)) Error = "원 요청의 보관 상태를 확인하지 못했습니다. 처리 결과를 다시 확인해 주세요."; return; }
        }
        if (Current(operation)) ApplyError(ex, writing: true);
    }
    public string? PendingHref => Pending is { } pending ? pending.Kind switch
    {
        "participation" => Ssalddel.Contracts.Common.Community.NeighborhoodMemberDeliveryRoutes.Home,
        "accept" or "decline" => Ssalddel.Contracts.Common.Community.NeighborhoodMemberDeliveryRoutes.Offer(pending.Target),
        "request-cancel" => Ssalddel.Contracts.Common.Community.NeighborhoodDeliveryRoutes.Detail(pending.Target),
        _ => Ssalddel.Contracts.Common.Community.NeighborhoodMemberDeliveryRoutes.WorkDetail(pending.Target)
    } : null;
}
