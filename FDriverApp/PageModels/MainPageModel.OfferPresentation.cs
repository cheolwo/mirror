using Ssalddel.Contracts.Driver.Food;

namespace FDriverApp.PageModels;

public sealed partial class MainPageModel
{
    private readonly Dictionary<string, (int Round, DateTime? ExpiresAtUtc)> _offerPresentationCandidates = new(StringComparer.Ordinal);
    private readonly HashSet<(string OfferId, int Round)> _acknowledgedOfferPresentations = [];
    private readonly HashSet<(string OfferId, int Round)> _offerPresentationAttempts = [];
    private long _offerPresentationGeneration;
    public string? DisplayedRecommendationOfferId => LatestRecommendationForDisplay?.TicketId;

    private void CaptureOfferPresentationCandidates(IEnumerable<FoodDeliveryDriverOfferDto> offers)
    {
        _offerPresentationGeneration++;
        _offerPresentationCandidates.Clear(); _offerPresentationAttempts.Clear();
        foreach (var offer in offers)
            _offerPresentationCandidates[offer.OfferId] = (offer.RecommendationRound, offer.ExpiresAtUtc);
    }

    /// <summary>실제 보이는 새 추천 요약 한 건의 앱 표시 확인입니다. 사람의 읽음·수락은 아닙니다.</summary>
    public async Task AcknowledgeVisibleRecommendationAsync(string offerId, CancellationToken cancellationToken = default)
    {
        if (!_workspaceActive || !_workspacePageVisible || !Navigation.IsDelivery || !IsRegularWorkspaceVisible
            || !IsAuthenticated || IsBusy || !DispatchIntentKnown || !ReceivesNewDispatches || !HasNewRecommendations
            || offerId != DisplayedRecommendationOfferId
            || !_offerPresentationCandidates.TryGetValue(offerId, out var candidate)
            || candidate.Round <= 0 || candidate.ExpiresAtUtc is not { } expiry || expiry <= _time.GetUtcNow().UtcDateTime) return;
        var key = (offerId, candidate.Round);
        if (_acknowledgedOfferPresentations.Contains(key) || !_offerPresentationAttempts.Add(key)) return;
        var owner = _authSession.UserId;
        var revision = _authSession.SessionRevision;
        var generation = _offerPresentationGeneration;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _workspaceCancellation.Token);
        try
        {
            await _api.AcknowledgeOfferPresentationAsync(offerId, candidate.Round, linked.Token);
            if (!linked.IsCancellationRequested && _workspaceActive && _workspacePageVisible && IsAuthenticated
                && owner == _authSession.UserId && revision == _authSession.SessionRevision && generation == _offerPresentationGeneration)
                _acknowledgedOfferPresentations.Add(key);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception) { /* 표시 확인 오류는 현재 업무를 막지 않습니다. 다음 조회 주기에 같은 차수를 재확인합니다. */ }
    }

    private void ClearOfferPresentations()
    {
        _offerPresentationGeneration++;
        _offerPresentationCandidates.Clear(); _offerPresentationAttempts.Clear(); _acknowledgedOfferPresentations.Clear();
    }
}
