using System.Globalization;
using Ssalddel.Contracts.Common.Settlement;

namespace Ssalddel.Ui.Common.Areas.App.Models;

public static class 활동정산표시
{
    public static string Money(decimal? amount) => amount is null ? "금액 미확정"
        : amount.Value.ToString(amount.Value % 1 == 0 ? "N0" : "N2", CultureInfo.GetCultureInfo("ko-KR")) + "원";
    public static string Percent(decimal? rate) => rate is null ? "미합의"
        : rate.Value.ToString("0.######", CultureInfo.GetCultureInfo("ko-KR")) + "%";
    public static string Kind(string kind) => kind switch
    {
        NeighborhoodSettlementCodes.Inbound => "입고 기본보수", NeighborhoodSettlementCodes.Storage => "보관 기본보수",
        NeighborhoodSettlementCodes.Picking => "피킹 기본보수", NeighborhoodSettlementCodes.Delivery => "배송 기본보수",
        NeighborhoodSettlementCodes.SalesAgency => "판매대행 기본보수", NeighborhoodSettlementCodes.ProductSale => "상품대금",
        NeighborhoodSettlementCodes.OwnerRemittance => "소유자 전달금", NeighborhoodSettlementCodes.Adjustment => "조정 금액",
        NeighborhoodSettlementCodes.ActivityReward => "공동 초과성과금", "PlatformShare" => "플랫폼 몫", _ => "활동 보수"
    };
    public static bool IsBasic(string kind) => kind is NeighborhoodSettlementCodes.Inbound or NeighborhoodSettlementCodes.Storage
        or NeighborhoodSettlementCodes.Picking or NeighborhoodSettlementCodes.Delivery or NeighborhoodSettlementCodes.SalesAgency;
    public static string State(string code) => code switch
    {
        NeighborhoodSettlementCodes.Draft => "약정 작성 중", NeighborhoodSettlementCodes.AwaitingConsent => "약정 동의 대기",
        NeighborhoodSettlementCodes.Active => "약정 적용 중", NeighborhoodSettlementCodes.Superseded => "이전 약정",
        NeighborhoodSettlementCodes.Revoked => "약정 동의 철회", "ReconfirmationRequired" => "변경 근거 재확인 필요",
        NeighborhoodSettlementCodes.Expected => "예정", NeighborhoodSettlementCodes.Accrued => "발생 · 당사자 확인 대기",
        NeighborhoodSettlementCodes.Confirmed => "금액 확정", NeighborhoodSettlementCodes.Disputed => "이의 검토 중",
        NeighborhoodSettlementCodes.Reported => "지급 신고", NeighborhoodSettlementCodes.PartiallyConfirmed => "일부 수령 확인",
        NeighborhoodSettlementCodes.RecipientConfirmed => "수령 확인", "WaitingForEvidence" => "근거 대기",
        "Ready" => "성과금 예정", "InvalidInput" => "계산 근거 재확인 필요", _ => "상태 확인 필요"
    };
    public static string Activity(string code) => code switch
    { NeighborhoodSettlementCodes.PreparedFood => "음식 배달", NeighborhoodSettlementCodes.StoredGoods => "보관 물품", NeighborhoodSettlementCodes.TransportOnly => "운송", _ => "활동 정산" };
    public static IReadOnlyList<string> WorkflowGroups { get; } =
        [NeighborhoodSettlementWorkflowCodes.InboundStorage, NeighborhoodSettlementWorkflowCodes.OutboundDelivery, NeighborhoodSettlementWorkflowCodes.Other];
    public static string WorkflowGroup(NeighborhoodSettlementSummaryDto value) => value.Workflow?.Kind switch
    {
        NeighborhoodSettlementWorkflowCodes.InboundStorage => NeighborhoodSettlementWorkflowCodes.InboundStorage,
        NeighborhoodSettlementWorkflowCodes.OutboundDelivery => NeighborhoodSettlementWorkflowCodes.OutboundDelivery,
        _ => NeighborhoodSettlementWorkflowCodes.Other
    };
    public static string WorkflowGroupTitle(string code) => code switch
    {
        NeighborhoodSettlementWorkflowCodes.InboundStorage => "입고·보관",
        NeighborhoodSettlementWorkflowCodes.OutboundDelivery => "출고·배송",
        _ => "기타·기존 약정"
    };
    public static string WorkflowKind(string? code) => code switch
    {
        NeighborhoodSettlementWorkflowCodes.InboundStorage => "입고·보관",
        NeighborhoodSettlementWorkflowCodes.OutboundDelivery => "출고·배송",
        NeighborhoodSettlementWorkflowCodes.Other => "기타 활동",
        NeighborhoodSettlementWorkflowCodes.LegacyMixed => "기존 혼합 약정 · 업무 구분 확인 필요",
        _ => "기존 약정 · 업무 구분 확인 필요"
    };
    public static string WorkflowState(string? code, string? kind = null) => code switch
    {
        "Planned" => "입고 예정", "PartiallyReceived" => "일부 입고 확인",
        "Received" => kind switch { NeighborhoodSettlementWorkflowCodes.InboundStorage => "입고 확인 완료",
            NeighborhoodSettlementWorkflowCodes.OutboundDelivery => "고객 수령 확인 완료", _ => "수령 확인 완료" },
        "Draft" => "출고 주문 작성 중", "Reserved" => "재고 예약", "PartiallyPicked" => "일부 피킹 완료", "Picked" => "피킹 완료",
        "InDelivery" => "배송 중", "Delivered" or "DeliveredAwaitingReceipt" => "전달 완료 · 고객 수령 확인 대기",
        "Completed" => "업무 완료", "WaitingForEvidence" => "완료 근거 확인 대기", "LegacyMixed" => "기존 약정 확인 필요",
        _ => "현재 단계 확인 필요"
    };
    public static string PaymentReleaseCondition(string? code) => code switch
    {
        NeighborhoodSettlementWorkflowCodes.WorkCompleted => "작업 완료 확인 후",
        NeighborhoodSettlementWorkflowCodes.CustomerReceived => "고객 수령 확인 후",
        _ => "기존 약정의 지급 조건 확인 필요"
    };
    public static string PaymentReleaseStatus(NeighborhoodSettlementWorkflowDto? value)
        => value is null || value.Kind == NeighborhoodSettlementWorkflowCodes.LegacyMixed ? "기존 약정 조건 유지"
            : value.PaymentReleaseReady ? "업무 완료 조건 충족" : "조건 확인 대기";
    public static string EntryPaymentWaitingReason(string? code) => code switch
    {
        "DeliveryReceiptRequired" => "고객 수령 확인 후 정산",
        "InboundCompletionRequired" => "입고 완료 확인 후 정산",
        _ => "업무 완료 조건 확인 후 정산"
    };
    public static string WorkflowReason(string? code) => code switch
    {
        "DeliveryReceiptRequired" => "최종 배송과 고객의 수령 확인을 기다리고 있습니다.",
        "InboundCompletionRequired" => "입고 수량·적재 완료 근거 확인을 기다리고 있습니다.",
        "SourceReconfirmationRequired" => "원본 업무가 바뀌어 약정을 다시 확인해야 합니다.",
        "LegacyWorkflowAgreement" => "기존 약정입니다. 과거 지급 조건을 바꾸지 않으며 업무 구분은 별도 확인이 필요합니다.",
        "SourceEvidenceRequired" => "해당 작업의 완료 수량과 근거 확인이 필요합니다.",
        _ => "업무 완료와 약정의 지급 조건을 확인하고 있습니다."
    };
    public static string Direction(string payer, string payee, string? owner) => payer == owner && payee == owner ? "내부 작업"
        : payee == owner ? "내가 받을 금액" : payer == owner ? "내가 부담할 금액" : "본인 관련 항목";
    public static string Time(DateTime time) => time == default ? "시각 미확인" : time.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
    public static string TermsAmount(NeighborhoodSettlementTermLine line) => line.Billing switch
    {
        NeighborhoodSettlementCodes.PerUnit => $"{Money(line.UnitPriceKrw)} × {(line.Quantity?.ToString("0.##", CultureInfo.InvariantCulture) ?? "수량 미확정")}",
        NeighborhoodSettlementCodes.Free => line.AmountKrw == 0 ? "0원 · 무료 합의" : "무료 조건 확인 필요",
        _ => Money(line.AmountKrw)
    };
    public static string Unit(string code) => code switch
    { NeighborhoodActivityRewardCodes.Unit => "개", NeighborhoodActivityRewardCodes.StorageDay => "수량·일", _ => "단위 확인 필요" };
    public static string RewardReason(string code) => code switch
    {
        "ActivityRewardSourceReconfirmationRequired" => "원본 업무가 바뀌어 약정을 다시 확인해야 합니다.",
        "ActivityRewardLatestConsentRequired" => "현재 약정에 필요한 당사자 동의를 기다리고 있습니다.",
        "ActivityRewardTermsExpired" => "약정 기간을 다시 확인해야 합니다.",
        "ActivityRewardCustomerReceiptRequired" => "최종 배송과 주문자의 수령 확인을 기다리고 있습니다.",
        "ActivityRewardBasicWorkCostUnresolved" or "ActivityRewardBasicWorkCostUnderstated" => "기본 작업 보수 비용의 확인이 필요합니다.",
        "ActivityRewardBasicWorkCostDisputed" => "기본 작업 보수의 분쟁이나 조정 확인이 필요합니다.",
        "ActivityRewardCompletedQuantityExceeded" or "ActivityRewardCompletedQuantityIncomplete" => "최종 수령 수량과 활동별 완료 근거를 다시 확인해야 합니다.",
        "ActivityRewardOrderWorkEvidenceRequired" or "ContributionsMissing" or "ContributionEvidenceMissing" => "해당 주문에 연결된 실제 활동 근거가 필요합니다.",
        "ActivityRewardStorageAttributionRequired" => "해당 주문에 연결되는 보관 기간과 수량의 근거가 필요합니다.",
        "InventoryCostEvidenceRequired" or "InventoryCostIncomplete" => "해당 주문의 입고·보관 비용 연결 근거가 필요합니다. 기본보수 발생 내역은 유지됩니다.",
        "InventoryCostSelectionChanged" => "연결한 입고·보관 비용이 바뀌어 현재 약정을 다시 확인해야 합니다.",
        "InventoryCostAmbiguous" => "중복되는 입고·보관 비용의 연결을 확인해야 합니다.",
        "InventoryCostNotConfirmed" => "연결된 입고·보관 비용의 금액 확정이나 이의 내역을 확인해야 합니다.",
        "InventoryCostQuantityExceeded" => "입고·보관 비용에 연결한 수량이 실제 수량을 넘지 않는지 확인해야 합니다.",
        "InventoryStoragePeriodNotAttributable" => "해당 주문에 연결되는 보관 기간의 근거를 확인해야 합니다.",
        "InventoryCostBudgetUnderstated" => "입고·보관 연결 비용과 약정의 비용 기준을 다시 확인해야 합니다.",
        "PlatformShareConfigurationRequired" => "플랫폼 보유 비율과 분배 조건 합의가 필요합니다.",
        "ActivityRewardWorkerConsentRequired" => "실제 수행자의 성과금 약정 동의가 필요합니다.",
        "ActivityRewardWorkerRoleChanged" => "실제 수행자와 현재 역할 연결을 다시 확인해야 합니다.",
        "ActivityRewardParticipantContributionRequired" => "참여자의 실제 수행 기여가 확인되지 않았습니다.",
        "RewardPolicyMissing" or "PolicyVersionMissing" or "RewardWeightsMissing" or "ContributionWeightMissing" or "RewardWeightMissing" or "ReturnRateMissing" or "EligibleWeightMissing" => "환원 비율과 활동별 가중치 합의가 필요합니다.",
        "FinancialBasisMissing" or "FinancialBasisKindMissing" or "FinancialBasisReferenceMissing" or "RevenueMissing" or "BasicWorkCostMissing" or "OtherCostMissing" or "ReserveMissing" => "수입·기본보수·기타비용·예비금 근거를 확인해야 합니다.",
        _ => "합의 조건과 계산 근거를 다시 확인해 주세요."
    };
}
