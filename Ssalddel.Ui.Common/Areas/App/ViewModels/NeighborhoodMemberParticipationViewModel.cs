using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.ViewModels;

public sealed class NeighborhoodMemberParticipationViewModel(INeighborhoodMemberDeliveryClient client,
    ISsalddel현재사용자Context user, NeighborhoodMemberDeliverySession session, IRoleWorkspaceLocationProvider location)
    : NeighborhoodMemberPageViewModel(user, session)
{
    public NeighborhoodMemberParticipationResponse? Settings { get; private set; }
    public NeighborhoodMemberParticipationSaveRequest Draft { get; private set; } = new();
    public bool Loaded { get; private set; }
    public bool HasActivityPoint => Draft.Latitude.HasValue && Draft.Longitude.HasValue;
    public bool CanSave => Loaded && (Settings?.Enabled == true || !Draft.ReceiveOffers) && !IsSending && !IsLoading
        && (Pending is null || Session.Matches("participation", "settings"))
        && (!Draft.ReceiveOffers || HasActivityPoint && !string.IsNullOrWhiteSpace(Draft.ActivityLabel) && Draft.MaxWeightKg > 0);
    protected override void ResetForOwner() { Settings = null; Draft = new(); Loaded = false; }
    public async Task LoadAsync()
    {
        if (IsSending || !await PrepareAsync()) return;
        var operation = Begin(); IsLoading = true; Error = null; Changed();
        try
        {
            var value = await client.ParticipationAsync(Token);
            if (!Current(operation)) return;
            Settings = value ?? throw new InvalidOperationException("배송 참여 설정을 확인하지 못했습니다."); Loaded = true;
            Draft = Session.Matches("participation", "settings")
                ? System.Text.Json.JsonSerializer.Deserialize<NeighborhoodMemberParticipationSaveRequest>(Pending!.PayloadJson)!
                : new() { ExpectedRevision = value.Revision, ReceiveOffers = value.ReceiveOffers, ActivityLabel = value.ActivityLabel,
                    Latitude = value.Latitude, Longitude = value.Longitude, RadiusKm = value.RadiusKm,
                    VehicleType = value.VehicleType, MaxWeightKg = value.MaxWeightKg };
        }
        catch (Exception ex) { if (Current(operation)) { Settings = null; Loaded = false; ApplyError(ex); } }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task SelectCurrentLocationAsync()
    {
        if (InputsLocked || !Loaded || !await PrepareAsync()) return;
        var operation = Begin(); IsLoading = true; Error = null; Changed();
        try
        {
            var point = await location.GetCurrentAsync(Token);
            if (!Current(operation)) return;
            if (!NeighborhoodMemberDeliveryPresentation.ValidLocation(point)) { Error = "현재 위치를 확인하지 못했습니다. 위치 권한과 GPS를 확인해 주세요."; return; }
            Draft.Latitude = (decimal)point!.Latitude; Draft.Longitude = (decimal)point.Longitude;
            Notice = "현재 위치를 활동 기준점으로 선택했습니다. 저장하면 반영됩니다.";
        }
        catch (Exception ex) { if (Current(operation)) ApplyError(ex); }
        finally { if (Current(operation)) { IsLoading = false; Changed(); } }
    }
    public async Task SaveAsync()
    {
        if (!await PrepareAsync() || !CanSave || Owner is null) return;
        var operation = Begin(); IsSending = true; Error = Notice = null; Changed();
        try
        {
            Draft.ClientRequestId = Guid.NewGuid(); Draft.ExpectedRevision = Settings!.Revision;
            var request = await PrepareCommandAsync(Owner, "participation", "settings", Draft.ClientRequestId, Draft.ExpectedRevision, Draft, Token);
            if (!Current(operation)) return;
            await client.SaveParticipationAsync(request, Token);
            if (!Current(operation)) return;
            var latest = await client.ParticipationAsync(Token);
            if (!Current(operation)) return;
            if (latest is null || latest.Revision <= request.ExpectedRevision || latest.ReceiveOffers != request.ReceiveOffers
                || latest.ActivityLabel != request.ActivityLabel || latest.Latitude != request.Latitude || latest.Longitude != request.Longitude
                || latest.RadiusKm != request.RadiusKm || latest.VehicleType != request.VehicleType || latest.MaxWeightKg != request.MaxWeightKg)
                throw new InvalidOperationException("설정 저장 결과 미확인");
            await Session.ClearAsync(Owner!, Token, request.ClientRequestId);
            if (!Current(operation)) return;
            Settings = latest; Draft.ExpectedRevision = latest.Revision; Notice = "배송 참여 설정을 저장했습니다.";
        }
        catch (Exception ex) { await RejectPendingAsync(ex, operation); }
        finally { if (Current(operation)) { IsSending = false; Changed(); } }
    }
}
