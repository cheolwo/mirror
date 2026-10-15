namespace Ssalddel.Contracts.Common.Settlement;

/// <summary>입고·보관과 출고·배송의 정산 경계입니다. 기존 source ID와 업무 상태는 유지합니다.</summary>
public static class NeighborhoodSettlementWorkflowCodes
{
    public const string InboundStorage = "InboundStorage", OutboundDelivery = "OutboundDelivery", Other = "Other", LegacyMixed = "LegacyMixed";
    public const string WorkCompleted = "WorkCompleted", CustomerReceived = "CustomerReceived";
}

/// <summary>현재 전문 원장에서 읽은 업무 진전입니다. 금액 발생·양측 동의·실제 지급 완료를 대신하지 않습니다.</summary>
public sealed class NeighborhoodSettlementWorkflowDto
{
    public string Kind { get; set; } = NeighborhoodSettlementWorkflowCodes.Other;
    public string StateCode { get; set; } = "Unknown";
    public bool CompletionConfirmed { get; set; }
    /// <summary>합의한 업무 완료 관문만의 충족 여부입니다. 지급 채무·금액 확정·은행 확인과는 별개입니다.</summary>
    public bool PaymentReleaseReady { get; set; }
    public string? WaitingReasonCode { get; set; }
    /// <summary>입고와 출고를 잇는 실제 재고 묶음입니다. 고객 주소·증빙 원본은 포함하지 않습니다.</summary>
    public IReadOnlyList<string> InventoryLotIds { get; set; } = [];
}

/// <summary>현재 전문 원본으로 검증한 고객의 전량 수령 근거입니다. 작업 보수·성과금·대금 수령 진술과는 별개입니다.</summary>
public sealed class NeighborhoodSettlementCustomerReceiptDto
{
    public string SourceId { get; init; } = "";
    public string ReceiptId { get; init; } = "";
    public long Revision { get; init; }
    public decimal ConfirmedQuantity { get; init; }
    public DateTime ConfirmedAtUtc { get; init; }
    /// <summary>고객 주소·연락처·보호 원문 없이 검증된 업무 근거의 stable ID만 포함합니다.</summary>
    public IReadOnlyList<string> EvidenceReferences { get; init; } = [];
}
