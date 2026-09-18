namespace Ssalddel.Contracts.Common.Finance;

public static class 관리계정StableIds
{
    public const string 가용현금 = "management.cash.available";
    public const string Pg미수 = "management.receivable.pg";
    public const string 고객결제정산대기 = "management.clearing.customer-payment";
    public const string 음식점미지급 = "management.payable.merchant";
    public const string 기사정상수행대금미지급 = "management.payable.courier.normal";
    public const string 기사취소수행대금미지급 = "management.payable.courier.cancellation";
    public const string 지사운영보상미지급 = "management.payable.branch";
    public const string 고객환불의무 = "management.liability.customer-refund";
    public const string 플랫폼수수료수익후보 = "management.revenue.platform-commission";
    public const string 배달료수익후보 = "management.revenue.delivery-fee";
    public const string Pg수수료비용후보 = "management.expense.pg-fee";
    public const string 미대사가계정 = "management.suspense.unmatched";
}

public static class 재무영향ProfileIds
{
    public const string 결제준비 = "financial.payment-prepared.v1";
    public const string 결제승인 = "financial.payment-approved.v1";
    public const string 고객환불상태기록 = "financial.customer-refund-recorded.v1";
    public const string 기사지급승인 = "financial.courier-payout-authorized.v1";
    public const string 기사지급정산 = "financial.courier-payout-settled.v1";
    public const string 수기수익Simulation = "financial.manual-revenue-scenario.v1";
}

public static class 재무의미Codes
{
    public const string 결제준비 = "PaymentPrepared";
    public const string 고객결제승인 = "CustomerPaymentApproved";
    public const string 고객환불상태기록 = "CustomerRefundRecorded";
    public const string 기사지급승인 = "CourierPayoutAuthorized";
    public const string 기사지급정산 = "CourierPayoutSettled";
    public const string 수기수익Simulation = "ManualRevenueScenario";
}

public static class 재무영향승인상태Codes
{
    public const string 관리후보 = "ManagementCandidate";
    public const string Simulation승인 = "ApprovedForSimulation";
}

public enum 재무영향유형
{
    RecognitionCandidate,
    SettlementCandidate,
    ReversalCandidate,
    AuthorizationOnly
}

public enum 재무인식시점
{
    BusinessOccurrence,
    CashMovement,
    ReportingAdjustment
}

public enum 관리계정정상잔액방향
{
    Debit,
    Credit
}

public enum 재무영향계정역할
{
    DebitCandidate,
    CreditCandidate,
    RelatedAccount
}

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class Ssalddel재무영향ProfileAttribute : Attribute
{
    public Ssalddel재무영향ProfileAttribute(string profileStableId)
    {
        if (string.IsNullOrWhiteSpace(profileStableId))
        {
            throw new ArgumentException("재무 영향 Profile Stable ID는 비워 둘 수 없습니다.", nameof(profileStableId));
        }

        ProfileStableId = profileStableId.Trim();
    }

    public string ProfileStableId { get; }

    /// <summary>
    /// Profile이 적용되는 결과 조건을 설명하며 권한과 실행 규칙을 대체하지 않습니다.
    /// </summary>
    public string ConditionCode { get; set; } = string.Empty;
}

public sealed record 관리계정Definition(
    string StableId,
    string DisplayName,
    string CategoryCode,
    관리계정정상잔액방향 NormalBalanceSide,
    string Meaning,
    bool IsActualAccountingAccountApproved = false);

public sealed record 재무영향계정Definition(
    string ManagementAccountStableId,
    재무영향계정역할 Role);

public sealed record 재무영향ProfileDefinition(
    string StableId,
    string FinancialMeaningCode,
    재무영향유형 ImpactKind,
    재무인식시점 RecognitionTiming,
    string AmountBasisCode,
    string MappingRevision,
    string ApprovalStatusCode,
    bool IsSimulationOnly,
    bool OperationalPostingAllowed,
    IReadOnlyList<재무영향계정Definition> Accounts);

public static class 관리계정Catalog
{
    private static readonly IReadOnlyList<관리계정Definition> Items =
    [
        new(관리계정StableIds.가용현금, "가용현금", "AssetCandidate", 관리계정정상잔액방향.Debit, "정책·고정비·지급에 사용할 수 있는 내부 관리 현금 후보"),
        new(관리계정StableIds.Pg미수, "PG 미수", "AssetCandidate", 관리계정정상잔액방향.Debit, "PG 승인 후 실제 입금 전까지의 받을 금액 후보"),
        new(관리계정StableIds.고객결제정산대기, "고객 결제 정산대기", "Clearing", 관리계정정상잔액방향.Credit, "이행·계약 근거가 생기기 전 고객 결제액을 수익과 통과자금으로 분리하지 않는 중립 정산대기 계정"),
        new(관리계정StableIds.음식점미지급, "음식점 미지급", "LiabilityCandidate", 관리계정정상잔액방향.Credit, "음식점에 지급할 상품대금 의무 후보"),
        new(관리계정StableIds.기사정상수행대금미지급, "기사 정상 수행대금 미지급", "LiabilityCandidate", 관리계정정상잔액방향.Credit, "정상 완료 수행에 대한 기사 지급 의무 후보"),
        new(관리계정StableIds.기사취소수행대금미지급, "기사 취소 수행대금 미지급", "LiabilityCandidate", 관리계정정상잔액방향.Credit, "비귀책 취소 수행에 대한 기사 지급 의무 후보"),
        new(관리계정StableIds.지사운영보상미지급, "지사 운영보상 미지급", "LiabilityCandidate", 관리계정정상잔액방향.Credit, "지사 운영 보상 의무 후보"),
        new(관리계정StableIds.고객환불의무, "고객 환불 의무", "LiabilityCandidate", 관리계정정상잔액방향.Credit, "확정된 고객 환불 의무 후보"),
        new(관리계정StableIds.플랫폼수수료수익후보, "플랫폼 수수료 수익 후보", "RevenueCandidate", 관리계정정상잔액방향.Credit, "수행의무와 본인·대리인 판단 전의 수수료 수익 후보"),
        new(관리계정StableIds.배달료수익후보, "배달료 수익 후보", "RevenueCandidate", 관리계정정상잔액방향.Credit, "고객 배달료 중 플랫폼 몫으로 판정될 수 있는 금액 후보"),
        new(관리계정StableIds.Pg수수료비용후보, "PG 수수료 비용 후보", "ExpenseCandidate", 관리계정정상잔액방향.Debit, "PG 정산 근거가 있는 결제 처리 비용 후보"),
        new(관리계정StableIds.미대사가계정, "미대사 가계정", "Suspense", 관리계정정상잔액방향.Debit, "원인·상대방·금액을 확정하지 못한 대사 예외 후보")
    ];

    public static IReadOnlyList<관리계정Definition> GetAll() => Items;

    public static 관리계정Definition? Find(string? stableId)
        => Items.FirstOrDefault(item => string.Equals(item.StableId, stableId?.Trim(), StringComparison.Ordinal));
}

public static class 재무영향ProfileCatalog
{
    private static readonly IReadOnlyList<재무영향ProfileDefinition> Items =
    [
        new(
            재무영향ProfileIds.결제준비,
            재무의미Codes.결제준비,
            재무영향유형.AuthorizationOnly,
            재무인식시점.BusinessOccurrence,
            "RequestedPaymentAmount",
            "management-financial-impact.v1",
            재무영향승인상태Codes.관리후보,
            false,
            false,
            []),
        new(
            재무영향ProfileIds.결제승인,
            재무의미Codes.고객결제승인,
            재무영향유형.RecognitionCandidate,
            재무인식시점.BusinessOccurrence,
            "ApprovedPaymentAmount",
            "management-financial-impact.v1",
            재무영향승인상태Codes.Simulation승인,
            false,
            false,
            [
                new(관리계정StableIds.Pg미수, 재무영향계정역할.DebitCandidate),
                new(관리계정StableIds.고객결제정산대기, 재무영향계정역할.CreditCandidate)
            ]),
        new(
            재무영향ProfileIds.고객환불상태기록,
            재무의미Codes.고객환불상태기록,
            재무영향유형.ReversalCandidate,
            재무인식시점.BusinessOccurrence,
            "ConfirmedRefundAmount",
            "management-financial-impact.v1",
            재무영향승인상태Codes.관리후보,
            true,
            false,
            [
                new(관리계정StableIds.고객결제정산대기, 재무영향계정역할.RelatedAccount),
                new(관리계정StableIds.고객환불의무, 재무영향계정역할.RelatedAccount)
            ]),
        new(
            재무영향ProfileIds.기사지급승인,
            재무의미Codes.기사지급승인,
            재무영향유형.AuthorizationOnly,
            재무인식시점.BusinessOccurrence,
            "ConfirmedExpectedPayoutAmount",
            "management-financial-impact.v1",
            재무영향승인상태Codes.관리후보,
            false,
            false,
            [new(관리계정StableIds.기사정상수행대금미지급, 재무영향계정역할.RelatedAccount)]),
        new(
            재무영향ProfileIds.기사지급정산,
            재무의미Codes.기사지급정산,
            재무영향유형.SettlementCandidate,
            재무인식시점.CashMovement,
            "PaidPayoutAmount",
            "management-financial-impact.v1",
            재무영향승인상태Codes.관리후보,
            false,
            false,
            [
                new(관리계정StableIds.기사정상수행대금미지급, 재무영향계정역할.DebitCandidate),
                new(관리계정StableIds.가용현금, 재무영향계정역할.CreditCandidate)
            ]),
        new(
            재무영향ProfileIds.수기수익Simulation,
            재무의미Codes.수기수익Simulation,
            재무영향유형.RecognitionCandidate,
            재무인식시점.ReportingAdjustment,
            "ManualScenarioAmount",
            "management-financial-impact.v1",
            재무영향승인상태Codes.관리후보,
            true,
            false,
            [new(관리계정StableIds.플랫폼수수료수익후보, 재무영향계정역할.RelatedAccount)])
    ];

    public static IReadOnlyList<재무영향ProfileDefinition> GetAll() => Items;

    public static 재무영향ProfileDefinition? Find(string? stableId)
        => Items.FirstOrDefault(item => string.Equals(item.StableId, stableId?.Trim(), StringComparison.Ordinal));
}
