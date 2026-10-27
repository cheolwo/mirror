using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

/// <summary>원 명령의 입력 사본과 전송·응답 상태만 보관합니다. 조회·재전송·보호저장 수명은 페이지가 조율합니다.</summary>
internal sealed class 분산명령영수증Session
{
    private 분산명령영수증Session(string action, string suffix, DistributedCommandRequest body,
        string? aggregateId, int? quantity, string? reservationId)
    { Action = action; Suffix = suffix; Body = body; AggregateId = aggregateId; Quantity = quantity; ReservationId = reservationId; }

    internal string Action { get; }
    internal string Suffix { get; }
    internal DistributedCommandRequest Body { get; }
    internal string? AggregateId { get; }
    internal int? Quantity { get; }
    internal string? ReservationId { get; }
    internal DistributedCommandResponse? Acknowledgement { get; private set; }
    internal bool Sent { get; private set; }

    internal static 분산명령영수증Session Freeze(string action, string suffix, DistributedCommandRequest body,
        string? aggregateId, int? quantity, string? reservationId)
    {
        var snapshot = (DistributedCommandRequest?)JsonSerializer.Deserialize(JsonSerializer.Serialize(body, body.GetType()), body.GetType())
            ?? throw new InvalidOperationException("입력 내용을 확인하지 못했습니다.");
        return new(action, suffix, snapshot, aggregateId, quantity, reservationId);
    }

    internal static 분산명령영수증Session Restore(string action, string suffix, DistributedCommandRequest body,
        string? aggregateId, int? quantity, string? reservationId, bool sent, DistributedCommandResponse? acknowledgement)
        => new(action, suffix, body, aggregateId, quantity, reservationId) { Sent = sent, Acknowledgement = acknowledgement };

    internal void MarkSent() => Sent = true;

    internal void RecoverAcknowledgement(DistributedReceiptDto receipt)
        => Acknowledgement = new(receipt.RequestId, receipt.AggregateId, receipt.Revision, "Recorded", true);

    internal void AcceptAcknowledgement(DistributedCommandResponse? acknowledgement)
    {
        if (acknowledgement is null || acknowledgement.RequestId != Body.RequestId
            || string.IsNullOrWhiteSpace(acknowledgement.AggregateId) || string.IsNullOrWhiteSpace(acknowledgement.StatusCode)
            || AggregateId is { } expected && acknowledgement.AggregateId != expected
            || acknowledgement.Revision <= Body.ExpectedRevision)
            throw new InvalidOperationException("저장 응답의 요청·대상·판본을 확인하지 못했습니다. 같은 요청을 다시 확인해 주세요.");
        Acknowledgement = acknowledgement;
    }

    internal void ValidateReceipt(DistributedReceiptDto receipt)
    {
        var acknowledgement = Acknowledgement!;
        if (receipt.RequestId != Body.RequestId || receipt.AggregateId != acknowledgement.AggregateId
            || AggregateId is { } expectedAggregate && receipt.AggregateId != expectedAggregate
            || receipt.Revision <= Body.ExpectedRevision
            || receipt.Revision != acknowledgement.Revision || receipt.Kind != ReceiptKind(Action)
            || Quantity is { } quantity && receipt.Quantity != quantity
            || ReservationId is { } reservation && receipt.ReservationId != reservation)
            throw new InvalidOperationException("같은 요청의 저장 근거를 확인하지 못했습니다.");
    }

    internal static void ValidateRecoveredPhoto(DistributedReceiptDto receipt, DistributedEvidenceWorkRequest work,
        string action, int? quantity)
    {
        if (receipt.RequestId != 분산사진업무Session.Body(work)?.RequestId || receipt.AggregateId != work.AggregateId
            || receipt.Kind != ReceiptKind(PhotoCompletionAction(action)) || receipt.Revision <= 분산사진업무Session.Body(work)!.ExpectedRevision
            || quantity is { } expectedQuantity && receipt.Quantity != expectedQuantity)
            throw new InvalidDataException("이전 사진 완료의 업무·대상·수량을 확인하지 못했습니다.");
    }

    internal static string PhotoCompletionAction(string action)
        => action is "import-lot" or "handover-stock-release" ? action : action + "-batch";

    private static string ReceiptKind(string action) => action switch
    { "create-site" => "CreateSite", "consent-owner" or "consent-manager" or "revoke-owner" or "revoke-manager" => "Consent",
      "create-location" => "CreateLocation", "import-lot" => "ImportLot", "plan-inbound" => "PlanInbound", "receive" => "Receive",
      "create-wave" => "CreateWave", "add-order" => "AddOrder", "close" => "Close", "plan" => "Plan", "accept" => "Accept",
      "retry-order-ledger" => "LinkOrderLedger",
      "propose-stock-release" => "StockReleaseProposal", "consent-stock-release" or "cancel-stock-release" => "StockReleaseConsent",
      "handover-stock-release" => "StockReleaseHandover", "close-site" => "CloseSite",
      "pick" => "Pick", "deliver" => "Deliver", "cancel" => "Cancel", "return" => "Return", "acknowledge" => "Acknowledge",
      "receive-batch" => "ReceiveBatch", "pick-batch" => "PickBatch", "deliver-batch" => "DeliverBatch", "return-batch" => "ReturnBatch",
      "create-agreement" => "CreateAgreement", "change-terms" => "AgreementTerms", "consent" or "revoke" or "consent-pending-terms" => "AgreementConsent",
      "set-acceptance-policy" => "SiteAcceptancePolicy", "propose-inbound" => "InboundProposal", "consent-inbound" => "InboundConsent",
      "reject-inbound" or "cancel-inbound" => "InboundDecision", "note-inbound" => "InboundNote", _ => "" };
}
