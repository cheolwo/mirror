using Ssalddel.Contracts.Driver.Action;
using Ssalddel.Contracts.Driver.Recommendation;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;

public sealed class CargoOfferActionViewModel : IDisposable
{
    private readonly CargoDriverWorkspaceApiClient _client;
    public CargoOfferActionViewModel(CargoDriverWorkspaceApiClient client, IRoleWorkspaceAccess access)
    {
        _client = client; Lifetime = new(access, RoleWorkspaceCatalog.CargoDriver, Clear);
    }
    public CargoInputLifetime Lifetime { get; }
    public string ItemId { get; private set; } = "";
    public 기사배차추천항목응답? Offer { get; private set; }
    public 기사운송의뢰상세응답? Source { get; private set; }
    public bool ConditionsConfirmed { get; set; }
    public bool AcceptConfirmed { get; set; }
    public bool Saved { get; private set; }
    public string? TransportId { get; private set; }
    public string ReturnHref => CargoWorkspaceRoutes.ReturnTo(RoleWorkspaceCatalog.CargoDriver, TransportId ?? ("offer:" + ItemId));
    public IReadOnlyList<string> Warnings => Offer?.경고.Concat(Offer.차량경고).Concat(Offer.일정위반사유).Distinct(StringComparer.Ordinal).ToArray() ?? [];
    public bool ServerCandidateAvailable => Source?.차량적합여부 == true && Offer?.차량적합여부 == true
        && !CargoWorkspacePresentation.IsClosed(Source.배차상태)
        && Source.배차상태 is not ("확정" or "배차확정")
        && (Offer.추천만료시각 is null || Offer.추천만료시각.Value.ToUniversalTime() > DateTime.UtcNow);
    public bool CanSubmit => !Lifetime.IsBusy && !Lifetime.HasError && !Saved && ServerCandidateAvailable && ConditionsConfirmed && AcceptConfirmed;

    public async Task LoadAsync(string id)
    {
        if (ItemId != id) { Lifetime.Reset(); ItemId = id; }
        await Lifetime.RunAsync(async ct =>
        {
            _ = CargoWorkspaceRoutes.Segment(id);
            await RefreshAsync(ct);
        });
    }
    private async Task RefreshAsync(CancellationToken ct)
    {
        var offers = await _client.RecommendationsAsync(ct);
        Lifetime.RequireCurrent(ct);
        var offer = offers.FirstOrDefault(item => item.의뢰Id == ItemId)
            ?? throw new InvalidOperationException("추천이 만료되었거나 현재 계정에 제공된 운송이 아닙니다.");
        var source = await _client.RequestAsync(ItemId, ct);
        Lifetime.RequireCurrent(ct);
        if (source.의뢰Id != ItemId) throw new InvalidOperationException("선택한 운송과 조회 결과가 일치하지 않습니다.");
        Offer = offer; Source = source;
    }
    public async Task SubmitAsync()
    {
        if (!CanSubmit) return;
        var round = Offer!.추천라운드;
        var warnings = Warnings.ToArray();
        await Lifetime.RunAsync(async ct =>
        {
            await RefreshAsync(ct);
            Lifetime.RequireCurrent(ct);
            if (!ServerCandidateAvailable || Offer!.추천라운드 != round || !warnings.SequenceEqual(Warnings))
            {
                ConditionsConfirmed = AcceptConfirmed = false;
                throw new InvalidOperationException("추천 조건이 변경되었습니다. 다시 확인해 주세요.");
            }
            var result = await _client.AcceptAsync(ItemId, new 기사화물배차수락요청
            {
                ExpectedRecommendationRound = Offer.추천시작시각.HasValue && Offer.추천만료시각.HasValue ? Offer.추천라운드 : null,
                // 전문 기사 앱의 기존 차량 주의사항 확인 계약을 동일하게 사용한다.
                AcknowledgedWarningCodes = Offer.차량경고.Length > 0 ? ["FreightVehicleAdvisory"] : []
            }, ct);
            Lifetime.RequireCurrent(ct);
            if (result?.RequestId != ItemId) throw new InvalidOperationException("참여 처리 응답을 확인하지 못했습니다. 운송 상태를 다시 확인해 주세요.");
            var workspace = await _client.WorkspaceAsync(ct);
            Lifetime.RequireCurrent(ct);
            var transport = workspace.활성운송목록.FirstOrDefault(item => item.운송번호 == ItemId);
            if (transport is null) throw new InvalidOperationException("참여 응답 후 진행 중 운송을 확인하지 못했습니다. 지도로 돌아가 새로고침해 주세요.");
            TransportId = CargoWorkspaceRoutes.Number(transport.Id); Saved = true;
        }, "진행 중 운송을 확인했습니다. 지도로 돌아가 상차 업무를 시작해 주세요.");
    }
    private void Clear() { Offer = null; Source = null; TransportId = null; Saved = ConditionsConfirmed = AcceptConfirmed = false; }
    public void Dispose() => Lifetime.Dispose();
}
