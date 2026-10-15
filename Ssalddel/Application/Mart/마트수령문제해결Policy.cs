using Ssalddel.Contracts.Mart;
using 살뜰.도메인.마트;

namespace Ssalddel.Application.Mart;

/// <summary>양측 진술의 순서와 정본 결속만 판독합니다. 반품·환불·입금 완료를 생성하지 않습니다.</summary>
public static class 마트수령문제해결Policy
{
    public const int MaxEvents = 24;
    public static long OriginalRevision(마트수령근거Snapshot state)
        => state.Envelope?.Resolutions?.FirstOrDefault()?.ExpectedRevision ?? state.AttemptRevision;

    public static string ProposalDigest(long orderId, 마트수령해결제안요청 r)
        => 마트고객수령근거Envelope.Digest(new { Kind = "ResolutionProposal", OrderId = orderId,
            r.ClientRequestId, r.ExpectedAttemptStableId, r.ExpectedRevision, r.OutcomeCode, r.ConfirmedQuantity, r.EvidenceNote });
    public static string ConsentDigest(long orderId, 마트수령해결동의요청 r)
        => 마트고객수령근거Envelope.Digest(new { Kind = "ResolutionConsent", OrderId = orderId,
            r.ClientRequestId, r.ExpectedAttemptStableId, r.ExpectedRevision, r.ProposalId, r.Granted, r.ConfirmedQuantity, r.EvidenceNote });
    public static bool NoteValid(string? note)
        => !string.IsNullOrWhiteSpace(note) && note.Length <= 240 && !note.Any(char.IsControl);
    public static bool OutcomeValid(string? outcome)
        => outcome is MartReceiptResolutionCodes.FullReceiptReconfirmed or MartReceiptResolutionCodes.OperationalReview;

    public static bool ScopeValid(마트수령근거Snapshot state, DateTime now)
    {
        var events = state.Envelope?.Resolutions;
        if (events is null || events.Count > MaxEvents) return false;
        if (events.Count == 0) return true;
        var hold = state.Hold;
        if (hold is null || state.BuyerUserId == state.SellerUserId || string.IsNullOrWhiteSpace(state.SellerUserId)) return false;
        var revision = hold.ResultRevision; var at = hold.RecordedAtUtc;
        마트수령문제해결근거? proposal = null;
        var actors = new HashSet<string>(StringComparer.Ordinal);
        var requests = new HashSet<Guid> { hold.ClientRequestId };
        if (state.ConfirmedProof is { } original) requests.Add(original.ClientRequestId);
        var fullGranted = true;
        foreach (var entry in events)
        {
            if (entry is null || !requests.Add(entry.ClientRequestId) || entry.ClientRequestId == Guid.Empty
                || entry.MartOrderId != state.MartOrderId || entry.OrderReference != state.OrderReference
                || entry.AttemptStableId != state.AttemptStableId || entry.HoldRequestId != hold.ClientRequestId
                || entry.BuyerUserId != state.BuyerUserId || entry.SellerUserId != state.SellerUserId
                || entry.ActorUserId != state.BuyerUserId && entry.ActorUserId != state.SellerUserId
                || revision <= 0 || revision == long.MaxValue || entry.ExpectedRevision != revision || entry.ResultRevision != revision + 1
                || entry.RecordedAtUtc.Kind != DateTimeKind.Utc || entry.RecordedAtUtc < at || entry.RecordedAtUtc > now
                || !NoteValid(entry.EvidenceNote) || !OutcomeValid(entry.OutcomeCode) || entry.ConfirmedQuantity < 0
                || entry.SourceFingerprint is null || entry.SourceFingerprint.Length != 64
                || proposal is not null && actors.Count == 2 && fullGranted && proposal.OutcomeCode == MartReceiptResolutionCodes.FullReceiptReconfirmed)
                return false;
            if (entry.Kind == "Proposal")
            {
                if (entry.Granted is not null || entry.ProposalId != $"mart-receipt-resolution:{state.MartOrderId}:{entry.ClientRequestId:N}") return false;
                var input = new 마트수령해결제안요청 { ClientRequestId = entry.ClientRequestId, ExpectedAttemptStableId = entry.AttemptStableId,
                    ExpectedRevision = entry.ExpectedRevision, OutcomeCode = entry.OutcomeCode, ConfirmedQuantity = entry.ConfirmedQuantity,
                    EvidenceNote = entry.EvidenceNote };
                if (entry.InputDigest != ProposalDigest(state.MartOrderId, input)) return false;
                proposal = entry; actors.Clear(); fullGranted = true;
            }
            else if (entry.Kind == "Consent")
            {
                if (proposal is null || entry.Granted is null || entry.ProposalId != proposal.ProposalId
                    || entry.OutcomeCode != proposal.OutcomeCode || entry.ConfirmedQuantity != proposal.ConfirmedQuantity
                    || entry.SourceFingerprint != proposal.SourceFingerprint || !actors.Add(entry.ActorUserId)) return false;
                var input = new 마트수령해결동의요청 { ClientRequestId = entry.ClientRequestId, ExpectedAttemptStableId = entry.AttemptStableId,
                    ExpectedRevision = entry.ExpectedRevision, ProposalId = entry.ProposalId, Granted = entry.Granted.Value,
                    ConfirmedQuantity = entry.ConfirmedQuantity, EvidenceNote = entry.EvidenceNote };
                if (entry.InputDigest != ConsentDigest(state.MartOrderId, input)) return false;
                fullGranted &= entry.Granted.Value;
            }
            else return false;
            revision = entry.ResultRevision; at = entry.RecordedAtUtc;
        }
        return revision == state.AttemptRevision;
    }

    public static 마트수령문제해결근거? LatestProposal(마트수령근거Snapshot state)
        => state.Envelope?.Resolutions?.LastOrDefault(row => row.Kind == "Proposal");
    public static IReadOnlyList<마트수령문제해결근거> Consents(마트수령근거Snapshot state)
        => LatestProposal(state) is { } p ? state.Envelope!.Resolutions.Where(row => row.Kind == "Consent" && row.ProposalId == p.ProposalId).ToArray() : [];
    public static bool FullResolutionValid(마트수령근거Snapshot state)
        => state.CanonicalDeliveryValid && state.StoredEvidenceValid && state.Hold is not null
            && LatestProposal(state) is { OutcomeCode: MartReceiptResolutionCodes.FullReceiptReconfirmed } p
            && p.SourceFingerprint == state.SourceFingerprint && p.ConfirmedQuantity == state.TotalOrderQuantity && p.ConfirmedQuantity > 0
            && Consents(state).Count == 2 && Consents(state).All(row => row.Granted == true);
    // 이미 완료한 양측 진술은 정본 변화로 자동 재개하거나 소급 변경하지 않습니다.
    public static bool HistorySealed(마트수령근거Snapshot state)
        => LatestProposal(state) is { OutcomeCode: MartReceiptResolutionCodes.FullReceiptReconfirmed }
            && Consents(state).Count == 2 && Consents(state).All(row => row.Granted == true);

    public static string Status(마트수령근거Snapshot state)
        => !state.StoredEvidenceValid || !state.CanonicalDeliveryValid ? "EvidenceReviewRequired"
            : state.Hold is null ? "NoHold"
            : FullResolutionValid(state) ? "ResolvedFullReceipt"
            : HistorySealed(state) ? "OperationalReview"
            : LatestProposal(state) is not { } p ? "AwaitingProposal"
            : p.SourceFingerprint != state.SourceFingerprint ? "SourceChanged"
            : p.OutcomeCode == MartReceiptResolutionCodes.OperationalReview || Consents(state).Any(row => row.Granted == false) ? "OperationalReview"
            : "AwaitingConsents";
    public static string NextActor(마트수령근거Snapshot state)
    {
        var status = Status(state);
        if (status is "ResolvedFullReceipt" or "NoHold") return "None";
        if (status is "OperationalReview" or "EvidenceReviewRequired") return "OperatorReview";
        if (status is "AwaitingProposal" or "SourceChanged") return "BuyerOrSeller";
        var consent = Consents(state);
        return !consent.Any(row => row.ActorUserId == state.BuyerUserId) ? "Buyer" : "Seller";
    }
}
