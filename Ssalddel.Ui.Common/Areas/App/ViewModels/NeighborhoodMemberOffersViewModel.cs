using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodMemberOffersViewModel(INeighborhoodMemberDeliveryClient client, ISsalddel현재사용자Context user,
    NeighborhoodMemberDeliverySession session, IRoleWorkspaceLocationProvider location) : NeighborhoodMemberPageViewModel(user, session)
{
    public IReadOnlyList<NeighborhoodMemberOfferResponse> Offers { get; private set; } = [];
    public NeighborhoodMemberOfferResponse? Selected { get; private set; }
    public string? OfferId { get; private set; }
    public bool Loaded { get; private set; }
    public string? AcceptedRequestId { get; private set; }
    public bool CanAccept => !IsLoading && !IsSending && Selected is { CanAccept: true } value
        && value.ExpiresAtUtc > DateTime.UtcNow && (Pending is null || Session.Matches("accept", value.OfferId));
    protected override void ResetForOwner() { Offers = []; Selected = null; Loaded = false; AcceptedRequestId = null; }
    public async Task LoadAsync(string? offerId = null)
    {
        if (IsSending || !await PrepareAsync()) return;
        if (OfferId != offerId) { Selected = null; AcceptedRequestId = null; }
        OfferId = offerId; var operation = Begin(); IsLoading = true; Error = null; Loaded = false; Changed();
        try
        {
            if (offerId is null)
            {
                var values = await client.OffersAsync(Token); if (!Current(operation)) return; Offers = values;
            }
            else
            {
                var value = await client.OfferAsync(offerId, Token); if (!Current(operation)) return;
                if (value is null || value.OfferId != offerId) throw new InvalidOperationException("제안 조회 결과가 없습니다.");
                Selected = value;
                if (value.StateCode == "Accepted")
                {
                    var work = await client.WorkAsync(value.RequestId, Token); if (!Current(operation)) return;
                    if (work?.RequestId == value.RequestId)
                    {
                        AcceptedRequestId = work.RequestId;
                        if (Session.Matches("accept", offerId)) await Session.ClearAsync(Owner!, Token);
                    }
                }
                if (value.StateCode == "Declined" && Session.Matches("decline", offerId)) await Session.ClearAsync(Owner!, Token);
                if (!Current(operation)) return;
            }
            Loaded = true;
        }
        catch (Exception ex) { if (Current(operation)) { Offers = []; Selected = null; ApplyError(ex); } }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task<string?> AcceptAsync()
    {
        if (IsSending || IsLoading || !await PrepareAsync() || Selected is not { } selected || Owner is null) return null;
        var retry = Session.Matches("accept", selected.OfferId);
        if (!retry && !CanAccept) return null;
        var operation = Begin(); IsSending = true; Error = null; Changed();
        try
        {
            NeighborhoodMemberAcceptRequest request;
            if (retry) request = System.Text.Json.JsonSerializer.Deserialize<NeighborhoodMemberAcceptRequest>(Pending!.PayloadJson)!;
            else
            {
                var point = await location.GetCurrentAsync(Token); if (!Current(operation)) return null;
                if (!NeighborhoodMemberDeliveryPresentation.ValidLocation(point))
                { Error = "수락하려면 현재 위치를 확인해야 합니다. 위치 권한과 GPS를 확인해 주세요."; return null; }
                request = new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = selected.Revision,
                    Latitude = (decimal)point!.Latitude, Longitude = (decimal)point.Longitude,
                    AccuracyM = (decimal)point.AccuracyMeters!.Value, LocationAtUtc = point.MeasuredAt.UtcDateTime };
            }
            request = await PrepareCommandAsync(Owner, "accept", selected.OfferId, request.ClientRequestId, request.ExpectedRevision, request, Token);
            if (!Current(operation)) return null;
            var result = await client.AcceptAsync(selected.OfferId, request, Token); if (!Current(operation)) return null;
            if (result?.RequestId != selected.RequestId) throw new InvalidOperationException("배정 결과 미확인");
            var latest = await client.WorkAsync(selected.RequestId, Token); if (!Current(operation)) return null;
            if (latest?.RequestId != selected.RequestId) throw new InvalidOperationException("배정 원장 미확인");
            await Session.ClearAsync(Owner!, Token, request.ClientRequestId); if (!Current(operation)) return null;
            AcceptedRequestId = latest.RequestId; Notice = "배송을 수락했습니다. 픽업 장소를 확인해 주세요."; return latest.RequestId;
        }
        catch (Exception ex) { await RejectPendingAsync(ex, operation); return null; }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
    public async Task DeclineAsync()
    {
        if (IsSending || IsLoading || !await PrepareAsync() || Selected is not { } value || Owner is null
            || Pending is not null && !Session.Matches("decline", value.OfferId)
            || Pending is null && (!value.CanAccept || value.ExpiresAtUtc <= DateTime.UtcNow)) return;
        var operation = Begin(); IsSending = true; Error = Notice = null; Changed();
        try
        {
            var request = new NeighborhoodMemberCommandRequest { ClientRequestId = Guid.NewGuid(), ExpectedRevision = value.Revision };
            request = await PrepareCommandAsync(Owner, "decline", value.OfferId, request.ClientRequestId, request.ExpectedRevision, request, Token);
            if (!Current(operation)) return;
            await client.DeclineAsync(value.OfferId, request, Token); if (!Current(operation)) return;
            var latest = await client.OfferAsync(value.OfferId, Token); if (!Current(operation)) return;
            if (latest?.OfferId != value.OfferId || latest.StateCode != "Declined") throw new InvalidOperationException("거절 원장 미확인");
            await Session.ClearAsync(Owner!, Token, request.ClientRequestId); if (!Current(operation)) return;
            Selected = latest; Notice = "배송 제안을 거절했습니다.";
        }
        catch (Exception ex) { await RejectPendingAsync(ex, operation); }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
}
