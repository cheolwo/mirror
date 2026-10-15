using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Settlement;
using 살뜰.도메인.정산;
using C = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementCodes;
using W = Ssalddel.Contracts.Common.Settlement.NeighborhoodSettlementWorkflowCodes;

namespace Ssalddel.Application.Settlement;

public sealed partial class NeighborhoodSettlementUseCase
{
    private static IReadOnlyList<string> StorageEvidence(NeighborhoodSettlementSourceSnapshot source)
        => source.StorageEvidenceReferences.Count > 0 ? source.StorageEvidenceReferences
            : source.Workflow is null ? source.CompletionEvidenceReferences : [];

    private static string WorkflowKind(string sourceKind) => sourceKind switch
    {
        C.DistributedLot => W.InboundStorage,
        C.DistributedOrder or C.MartOrder => W.OutboundDelivery,
        _ => W.Other
    };

    private static NeighborhoodSettlementAgreementRequest WorkflowTerms(NeighborhoodSettlementAgreementRequest request)
    {
        // 요청을 변경하면 동일 RequestId 재조회 시 입력 지문이 달라지므로 복사본만 정규화합니다.
        var terms = Deserialize<NeighborhoodSettlementAgreementRequest>(Serialize(request));
        var kind = WorkflowKind(terms.SourceKind);
        Require(terms.WorkflowKind is null || terms.WorkflowKind == kind, "NeighborhoodSettlementWorkflowSourceMismatch", 400);
        terms.WorkflowKind = kind;
        var release = kind == W.OutboundDelivery ? W.CustomerReceived : W.WorkCompleted;
        Require(terms.PaymentReleaseCondition is null || terms.PaymentReleaseCondition == release,
            "NeighborhoodSettlementPaymentReleaseInvalid", 400);
        terms.PaymentReleaseCondition = release;
        ValidateWorkflowLines(terms);
        return terms;
    }

    private static void ValidateWorkflowLines(NeighborhoodSettlementAgreementRequest terms)
    {
        var kind = WorkflowKind(terms.SourceKind);
        Require(terms.Lines is not null, "NeighborhoodSettlementLinesInvalid", 400);
        foreach (var line in terms.Lines)
            Require(line is not null && (kind switch
            {
                W.InboundStorage => line.Kind is C.Inbound or C.Storage,
                W.OutboundDelivery => line.Kind is C.Picking or C.Packing or C.Delivery or C.ProductSale or C.SalesAgency or C.OwnerRemittance,
                _ => true
            }), "NeighborhoodSettlementWorkflowLineUnsupported", 400);
    }

    private bool CustomerReceiptReady(NeighborhoodSettlementSourceSnapshot source)
    {
        // 기존 분산주문 판정은 유지하며 마트 수령을 성과금 결과로 대체하지 않습니다.
        if (source.SourceKind == C.DistributedOrder)
            return source.ObservedAtUtc.Kind == DateTimeKind.Utc && source.ObservedAtUtc <= Now
                && source.RewardOutcome is { ConfirmedQuantity: > 0, EvidenceReferences.Count: > 0 } outcome
                && outcome.EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x))
                && source.Workflow is { Kind: W.OutboundDelivery, CompletionConfirmed: true, PaymentReleaseReady: true };
        if (source.SourceKind != C.MartOrder || source.CustomerReceipt is not
            { Revision: > 0, ConfirmedQuantity: > 0, EvidenceReferences.Count: > 0 } receipt) return false;
        var prefix = $"mart-customer-receipt:{source.SourceId}:";
        return receipt.SourceId == source.SourceId && receipt.Revision > 0 && receipt.ConfirmedQuantity > 0
            && !string.IsNullOrWhiteSpace(receipt.ReceiptId)
            && receipt.ReceiptId.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(receipt.ReceiptId[prefix.Length..], "N", out var requestId) && requestId != Guid.Empty
            && receipt.ConfirmedAtUtc.Kind == DateTimeKind.Utc && receipt.ConfirmedAtUtc <= source.ObservedAtUtc
            && source.ObservedAtUtc.Kind == DateTimeKind.Utc && source.ObservedAtUtc <= Now
            && receipt.EvidenceReferences.Count > 0 && receipt.EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x))
            && receipt.EvidenceReferences.Contains(receipt.ReceiptId, StringComparer.Ordinal)
            && source.Workflow is { Kind: W.OutboundDelivery, CompletionConfirmed: true, PaymentReleaseReady: true };
    }

    private NeighborhoodSettlementWorkflowDto ReadWorkflow(NeighborhoodSettlementAgreementRequest terms,
        NeighborhoodSettlementSourceSnapshot source)
    {
        var current = source.Workflow;
        if (terms.WorkflowKind is null)
            return new() { Kind = W.LegacyMixed, StateCode = current?.StateCode ?? "Unknown",
                WaitingReasonCode = "LegacyWorkflowAgreement" };
        if (terms.WorkflowKind == W.Other && current is null)
        {
            var completed = source.CompletedWork.Any(x => x.Quantity > 0 && x.EvidenceReferences.Count > 0
                && (x.OccurredAtUtc is { Kind: DateTimeKind.Utc } at && at <= Now
                    || x.Kind == C.ProductSale && x.OccurredAtUtc is null && source.ObservedAtUtc.Kind == DateTimeKind.Utc && source.ObservedAtUtc <= Now));
            return new() { Kind = W.Other, StateCode = completed ? "Completed" : "WaitingForEvidence",
                CompletionConfirmed = completed, PaymentReleaseReady = completed,
                WaitingReasonCode = completed ? null : "SourceEvidenceRequired" };
        }
        var ready = terms.WorkflowKind == W.OutboundDelivery ? CustomerReceiptReady(source)
            : current?.PaymentReleaseReady == true;
        return new()
        {
            Kind = terms.WorkflowKind, StateCode = current?.StateCode ?? "Unknown",
            CompletionConfirmed = terms.WorkflowKind == W.OutboundDelivery ? ready : current?.CompletionConfirmed == true,
            PaymentReleaseReady = ready,
            WaitingReasonCode = ready ? null : source.SourceKind == C.MartOrder ? current?.WaitingReasonCode ?? "MartWorkEvidenceRequired"
                : terms.WorkflowKind == W.OutboundDelivery ? "DeliveryReceiptRequired"
                : current?.WaitingReasonCode ?? "SourceEvidenceRequired",
            InventoryLotIds = current?.InventoryLotIds ?? []
        };
    }

    private bool EntryReleaseReady(NeighborhoodSettlementEntryDto entry,
        IReadOnlyList<NeighborhoodSettlementEntryDto> entries,
        IReadOnlyDictionary<int, NeighborhoodSettlementAgreementRequest> versionTerms,
        NeighborhoodSettlementSourceSnapshot source)
    {
        // 반납할 금액의 회수는 배송 완료에 묶지 않습니다. 증액은 원래 작업의 약정을 따릅니다.
        if (entry.AmountKrw < 0) return true;
        var original = entry.OriginalEntryId is { } id ? entries.SingleOrDefault(x => x.EntryId == id) : entry;
        if (original is null || !versionTerms.TryGetValue(original.TermsVersion, out var terms)) return false;
        if (terms.WorkflowKind is null) return true; // 기존 채권·지급 진술은 소급 변경하지 않습니다.
        return terms.PaymentReleaseCondition switch
        {
            W.WorkCompleted => true, // 항목 발생 시의 실제 작업 근거 검사는 기존 원장이 소유합니다.
            W.CustomerReceived => CustomerReceiptReady(source),
            _ => false
        };
    }

    private async Task RequirePaymentRelease(NeighborhoodSettlementAgreementRecord agreement,
        NeighborhoodSettlementEntryDto entry, IReadOnlyList<NeighborhoodSettlementEntryDto> entries, CancellationToken ct)
    {
        if (entry.AmountKrw < 0) return;
        var versions = await db.Set<NeighborhoodSettlementAgreementVersionRecord>().AsNoTracking()
            .Where(x => x.AgreementId == agreement.AgreementId).ToArrayAsync(ct);
        var terms = versions.ToDictionary(x => x.TermsVersion, x => Deserialize<NeighborhoodSettlementAgreementRequest>(x.TermsJson));
        var original = entry.OriginalEntryId is { } id ? entries.SingleOrDefault(x => x.EntryId == id) : entry;
        Require(original is not null && terms.ContainsKey(original.TermsVersion), "NeighborhoodSettlementDeliveryReceiptRequired");
        var originalTerms = terms[original!.TermsVersion];
        // 이미 발생한 입고비·구형 채권은 원천 조회 장애 때문에 새 지급 제한을 받지 않습니다.
        if (originalTerms.WorkflowKind is null || originalTerms.PaymentReleaseCondition == W.WorkCompleted) return;
        var source = await sources.ResolveAsync(agreement.SourceKind, agreement.SourceId, ct);
        Require(source.SourceKind == agreement.SourceKind && source.SourceId == agreement.SourceId
            && EntryReleaseReady(entry, entries, terms, source), "NeighborhoodSettlementDeliveryReceiptRequired");
    }
}
