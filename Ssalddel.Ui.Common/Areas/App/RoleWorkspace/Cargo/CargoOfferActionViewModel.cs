using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed class CargoOfferActionViewModel : IDisposable
{
    private readonly CargoDriverWorkspaceApiClient _client;
    private readonly CargoInputRecovery recovery;
    public CargoOfferActionViewModel(CargoDriverWorkspaceApiClient client, IRoleWorkspaceAccess access,
        IWorkflowRecoveryStore? recoveryStore = null, IWorkflowRecoveryContext? recoveryContext = null)
    {
        _client = client; recovery = new(access, recoveryStore, recoveryContext);
        Lifetime = new(access, RoleWorkspaceCatalog.CargoDriver, Clear);
    }
    public CargoInputLifetime Lifetime { get; }
    public string ItemId { get; private set; } = "";
    public 기사배차추천항목응답? Offer { get; private set; }
    public 기사운송의뢰상세응답? Source { get; private set; }
    public bool ConditionsConfirmed { get; set; }
    public bool AcceptConfirmed { get; set; }
    public bool Saved { get; private set; }
    public bool RecommendationChanged { get; private set; }
    public string? TransportId { get; private set; }
    public bool AwaitingResult => recovery.Pending is not null;
    public string ReturnHref => CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.CargoDriver, TransportId ?? ("offer:" + ItemId));
    public IReadOnlyList<string> Warnings => OfferWarnings(Offer);
    public bool ServerCandidateAvailable => Source?.차량적합여부 == true && Offer?.차량적합여부 == true
        && !CargoWorkspacePresentation.IsClosed(Source.배차상태)
        && Source.배차상태 is not ("확정" or "배차확정")
        && (Offer.추천만료시각 is null || Offer.추천만료시각.Value.ToUniversalTime() > DateTime.UtcNow);
    public bool CanSubmit => recovery.CanSubmit && !Lifetime.IsBusy && !Lifetime.HasError && !Saved && ServerCandidateAvailable
        && CargoDriverPayoutPresentation.OfferAmount(Offer).HasValue && ConditionsConfirmed && AcceptConfirmed;

    public async Task LoadAsync(string id)
    {
        if (ItemId != id) { Lifetime.Reset(); ItemId = id; recovery.Reset(id, "accept"); }
        var reloading = Offer is not null;
        RecommendationChanged = false;
        await Lifetime.RunAsync(async ct =>
        {
            _ = CargoWorkspaceRoutes.Segment(id);
            await recovery.LoadAsync(ct);
            RequireCurrent(ct);
            if (AwaitingResult)
            {
                await ConfirmAcceptedTransportAsync(ct);
                return;
            }
            await RefreshAsync(ct);
        }, reloading ? "최신 추천 조건을 확인했습니다. 보수와 주의사항을 확인해 주세요." : null);
    }
    private async Task RefreshAsync(CancellationToken ct)
    {
        var offers = await _client.RecommendationsAsync(ct);
        RequireCurrent(ct);
        var offer = offers.FirstOrDefault(item => item.의뢰Id == ItemId)
            ?? throw new InvalidOperationException("추천이 만료되었거나 현재 계정에 제공된 운송이 아닙니다.");
        var source = await _client.RequestAsync(ItemId, ct);
        RequireCurrent(ct);
        if (source.의뢰Id != ItemId) throw new InvalidOperationException("선택한 운송과 조회 결과가 일치하지 않습니다.");
        if (Offer is { } previous && (CargoDriverPayoutPresentation.OfferAmount(previous) != CargoDriverPayoutPresentation.OfferAmount(offer)
            || previous.추천라운드 != offer.추천라운드 || !OfferWarnings(previous).SequenceEqual(OfferWarnings(offer))
            || previous.추천시작시각 != offer.추천시작시각 || previous.추천만료시각 != offer.추천만료시각))
        {
            RecommendationChanged = true;
            ConditionsConfirmed = AcceptConfirmed = false;
        }
        Offer = offer; Source = source;
    }
    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        var round = Offer!.추천라운드;
        var payout = CargoDriverPayoutPresentation.OfferAmount(Offer);
        var startedAt = Offer.추천시작시각;
        var expiresAt = Offer.추천만료시각;
        var warnings = Warnings.ToArray();
        await Lifetime.RunAsync(async ct =>
        {
            await RefreshAsync(ct);
            RequireCurrent(ct);
            if (!ServerCandidateAvailable || Offer!.추천라운드 != round || !warnings.SequenceEqual(Warnings)
                || CargoDriverPayoutPresentation.OfferAmount(Offer) != payout
                || Offer.추천시작시각 != startedAt || Offer.추천만료시각 != expiresAt)
            {
                ConditionsConfirmed = AcceptConfirmed = false;
                RecommendationChanged = true;
                if (CargoDriverPayoutPresentation.OfferAmount(Offer) != payout)
                    throw new InvalidOperationException("기사 보수 조건이 변경되었습니다. 다시 확인해 주세요.");
                return; // 변경된 조건을 표시한 뒤 사용자가 다시 확인한다. 수락 POST는 보내지 않는다.
            }
            await recovery.BeginAsync(ct);
            RequireCurrent(ct);
            var result = await _client.AcceptAsync(ItemId, new 기사화물배차수락요청
            {
                ExpectedRecommendationRound = Offer.추천시작시각.HasValue && Offer.추천만료시각.HasValue ? Offer.추천라운드 : null,
                // 전문 기사 앱의 기존 차량 주의사항 확인 계약을 동일하게 사용한다.
                AcknowledgedWarningCodes = Offer.차량경고.Length > 0 ? ["FreightVehicleAdvisory"] : []
            }, ct);
            RequireCurrent(ct);
            if (result?.RequestId != ItemId) throw new InvalidOperationException("참여 처리 응답을 확인하지 못했습니다. 운송 상태를 다시 확인해 주세요.");
            await ConfirmAcceptedTransportAsync(ct);
        }, null,
            "수락 결과를 확인하지 못했습니다. 추천 조건과 현재 운송 상태를 다시 확인해 주세요.");
    }
    private async Task ConfirmAcceptedTransportAsync(CancellationToken ct)
    {
        var workspace = await _client.WorkspaceAsync(ct);
        RequireCurrent(ct);
        var transport = workspace.활성운송목록.SingleOrDefault(item => item.운송번호 == ItemId && item.Id > 0);
        if (transport is null)
            throw new InvalidOperationException("수락 결과를 확인 중입니다. 다시 수락하지 말고 현재 운송 상태를 확인해 주세요.");
        await recovery.ConfirmAsync(ct);
        RequireCurrent(ct);
        TransportId = CargoWorkspaceRoutes.Number(transport.Id); Saved = true;
    }
    private void RequireCurrent(CancellationToken ct) { Lifetime.RequireCurrent(ct); recovery.RequireCurrent(ct); }
    private static IReadOnlyList<string> OfferWarnings(기사배차추천항목응답? offer)
        => offer?.경고.Concat(offer.차량경고).Concat(offer.일정위반사유).Distinct(StringComparer.Ordinal).ToArray() ?? [];
    private void Clear() { Offer = null; Source = null; TransportId = null; Saved = RecommendationChanged = ConditionsConfirmed = AcceptConfirmed = false; recovery.Reset(ItemId, "accept"); }
    public void Dispose() => Lifetime.Dispose();
}
