using System.Globalization;
namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
public static class 분산이행표시
{
    public static string Time(DateTime? value) => value is { } time ? DateTime.SpecifyKind(time, DateTimeKind.Utc).ToLocalTime().ToString("MM월 dd일 HH:mm", CultureInfo.InvariantCulture) : "미확정";
    public static string Status(string code) => code switch
    { "PendingConsent" => "동의 확인 대기", "Active" => "사용 가능", "Paused" => "사용 중단", "Revoked" => "동의 철회", "RevocationPendingCustody" => "동의 철회·보관 물품 정리 대기", "Planned" => "계획됨", "NeedsReplan" => "순회 재계획 필요", "Partial" or "PartiallyReceived" => "일부 입고 확인", "Received" => "입고 확인", "Open" => "주문 접수 중", "Closed" => "접수 마감", "Offered" or "AcceptancePending" => "기사 수락 확인 대기", "Accepted" => "기사 수락", "InProgress" => "진행 중", "Completed" => "전달 기록 완료", "Delivered" => "기사 전달 기록", "Cancelled" => "취소됨", "ReturnRequired" => "반품 확인 필요", "Picked" => "피킹 확인", "PartiallyPicked" => "일부 피킹 확인", "Pending" => "확인 대기", _ => "상태 재확인 필요" };
    public static string Action(string code) => code switch
    {
        "CreateAgreement" or "create-agreement" => "보관 합의 작성", "AgreementTerms" or "change-terms" => "보관 조건 변경",
        "AgreementConsent" => "보관 조건 동의 변경", "ReceiveBatch" => "같은 위치 입고 확인", "PickBatch" => "같은 위치 물품 피킹",
        "SiteAcceptancePolicy" or "set-acceptance-policy" => "공간 수용 조건 설정", "InboundProposal" or "propose-inbound" => "입고 조건 제안",
        "InboundConsent" or "consent-inbound" => "입고 조건 동의", "InboundDecision" or "reject-inbound" or "cancel-inbound" => "입고 협의 결정",
        "InboundNote" or "note-inbound" => "입고 협의 메모",
        "StockReleaseProposal" or "propose-stock-release" => "반출 조건 제안", "StockReleaseConsent" or "consent-stock-release" or "cancel-stock-release" => "반출 조건 동의 변경",
        "StockReleaseHandover" or "handover-stock-release" => "잔여 물품 인계", "CloseSite" or "close-site" => "공간 사용 종료",
        "DeliverBatch" => "같은 전달지 물품 전달", "ReturnBatch" => "같은 위치 반품 입고",
        _ => ExistingAction(code)
    };
    private static string ExistingAction(string code) => code switch
    { "create-site" or "CreateSite" => "거점 등록", "consent-owner" => "소유자 동의", "consent-manager" => "관리자 동의", "Consent" => "공간 동의 변경", "revoke-owner" or "revoke-manager" => "동의 철회", "create-location" or "CreateLocation" => "적재 위치 등록", "import-lot" or "ImportLot" => "기존 상품 위치 연결", "plan-inbound" or "PlanInbound" => "입고 계획", "receive" or "Receive" => "입고 확인", "create-wave" or "CreateWave" => "출고 묶음 생성", "add-order" or "AddOrder" => "주문 추가", "close" or "Close" => "접수 마감", "plan" or "Plan" => "순회 계획", "accept" or "Accept" => "순회 수락", "pick" or "Pick" => "직접 피킹", "deliver" or "Deliver" => "기사 전달 기록", "acknowledge" or "Acknowledge" => "고객 수령 확인", "cancel" or "Cancel" => "주문 취소", "return" or "Return" => "반품 확인", _ => "업무 처리" };
    public static string Owner(string label) => string.IsNullOrWhiteSpace(label) ? "등록된 상품 소유자" : label;
    public static string Product(string label, string sku) => string.IsNullOrWhiteSpace(label) ? sku : label;
    public static string Condition(string code) => code == "DistributedCargoAcceptancePending" ? "기존 화물 의뢰의 기사 수락 확인 대기" : "서버의 진행 조건 재확인 필요";
    public static string? InboundReason(string? code) => code switch
    {
        "DistributedSiteInboundPending" => "공급자·물품 소유자·공간 제공자가 남은 입고 수량을 먼저 정리해 주세요. 실제 입고 기록은 유지됩니다.",
        "DistributedSiteStockRemaining" => "공간에 남은 가용·예약·불량 물품을 먼저 확인해 주세요. 남은 재고가 있으면 공간을 종료할 수 없습니다.",
        "DistributedStockReleasePickedCustodyRemaining" => "기사에게 인계된 물품의 전달·반환부터 확인해 주세요.",
        "DistributedStockReleaseReservationRemaining" => "연결된 주문의 미해제 예약이 남아 있습니다. 주문 담당자가 취소·전달·반환 상태를 먼저 확인해 주세요.",
        "DistributedStockReleaseReservedOrInsufficient" or "DistributedStockReleaseInventoryChanged" => "물품의 예약이나 잔량이 바뀌었습니다. 최신 재고를 확인한 뒤 반출 수량을 다시 협의해 주세요.",
        "DistributedStockReleaseConsentRequired" => "공간 제공자와 물품 소유자의 현재 반출 동의를 먼저 확인해 주세요.",
        "DistributedStockReleasePending" => "진행 중인 반출 합의를 완료하거나 남은 협의를 취소한 뒤 공간을 종료해 주세요.",
        "DistributedStockReleaseMetadataInvalid" => "반출 기록을 확인할 수 없습니다. 기존 기록을 유지한 상태로 운영 담당자에게 확인을 요청해 주세요.",
        "DistributedInboundProposalRequired" => "공급자가 이번 물량과 일정을 먼저 제안해 주세요.",
        "DistributedInboundApprovalRequired" => "물품 소유자와 공간 제공자의 입고 동의를 기다립니다.",
        "DistributedInboundPolicyChanged" => "공간의 수용 조건이 바뀌었습니다. 현재 조건으로 다시 협의해 주세요.",
        "DistributedStorageAgreementTermsChanged" or "DistributedInboundAgreementChanged" or "DistributedInboundAgreementVersionMismatch" => "보관 계약의 판본이 바뀌었습니다. 현재 계약을 다시 확인해 주세요.",
        "DistributedStorageAgreementRequired" or "DistributedStorageAgreementInactive" or "DistributedAcceptancePolicyRequired" => "공간 수용 조건과 유효한 보관 합의에 양측 동의한 뒤 진행해 주세요.",
        "DistributedInboundArrivalWindowClosed" => "승인한 도착 시간이 아닙니다. 도착 일정을 다시 협의해 주세요.",
        "DistributedInboundPhysicalStockRequiresReview" => "이미 받은 물품은 유지됩니다. 수량 차이나 반송은 별도로 협의해 주세요.",
        "DistributedInboundClosed" => "이번 입고 협의는 종료되었습니다. 새 입고가 필요하면 별도로 계획해 주세요.",
        "DistributedInboundTermsVersionMismatch" => "협의 판본이 바뀌었습니다. 최신 조건을 확인한 뒤 다시 동의해 주세요.",
        "GoodsCategoryDenied" or "GoodsCategoryNotAllowed" => "이 공간에서 받지 않는 품목입니다. 다른 공간이나 수용 조건을 협의해 주세요.",
        "FoodNotAllowed" or "UnprocessedFoodNotAllowed" => "공간의 식품 수용 조건과 맞지 않습니다. 물품의 조건을 확인해 주세요.",
        "GoodsFactsUnknown" or "GoodsCategoryUnknown" or "FoodClassificationUnknown" or "ProcessingClassificationUnknown" or "PackagedFoodFactsUnknown" or "GoodsClassificationConflict" => "물품 분류와 식품·가공 여부를 확인해 주세요.",
        "PackagingUnknown" or "PackagingNotAllowed" => "상품 포장과 공간에서 허용한 포장을 확인해 주세요.",
        "TemperatureUnknown" or "TemperatureNotAllowed" => "상품의 필요 온도와 공간의 보관 온도를 확인해 주세요.",
        "CapacityUnitMismatch" or "ExistingCapacityUnitMismatch" => "상품 점유량과 공간 용량의 단위를 맞춰 주세요.",
        "CapacityExceeded" or "WeightExceeded" or "VolumeExceeded" or "DistributedStorageAgreementCapacityExceeded" => "공간의 수용량이 부족합니다. 수량이나 보관 일정을 조정해 주세요.",
        "ExistingCapacityUnknown" or "ExistingWeightUnknown" or "ExistingVolumeUnknown" => "기존 보관 물품과 승인 예약의 점유량을 먼저 확인해 주세요.",
        "GoodsCapacityUnknown" or "GoodsWeightUnknown" or "GoodsVolumeUnknown" or "InvalidGoodsDimensions" => "상품 한 개의 점유량·중량·부피를 확인해 주세요.",
        "InvalidInboundSchedule" or "StoragePeriodExceeded" or "AccessHoursConfirmationRequired" => "도착·보관 기간과 출입 가능 시간을 다시 확인해 주세요.",
        "DistributedAcceptancePolicyInvalid" => "공간의 허용 품목·포장·온도와 용량 조건을 확인해 주세요.",
        _ => null
    };
    // 작성 화면과 조회 화면이 같은 순서와 표기를 사용합니다. 목록 자체는 수정할 수 없습니다.
    public static IReadOnlyList<(string Key, string Label)> InboundCategoryOptions { get; } = Array.AsReadOnly<(string Key, string Label)>([("agricultural", "농산물"), ("industrial", "공산품"), ("packaged-food", "포장식품")]);
    public static IReadOnlyList<(string Key, string Label)> InboundPackagingOptions { get; } = Array.AsReadOnly<(string Key, string Label)>([("sealed", "밀봉"), ("boxed", "상자 포장"), ("unpacked", "비포장")]);
    public static IReadOnlyList<(string Key, string Label)> InboundTemperatureOptions { get; } = Array.AsReadOnly<(string Key, string Label)>([("ambient", "상온"), ("chilled", "냉장"), ("frozen", "냉동")]);
    public static IReadOnlyList<(string Key, string Label)> InboundSupplierOptions { get; } = Array.AsReadOnly<(string Key, string Label)>([("farm", "농가"), ("factory", "공장"), ("food-company", "식품회사"), ("manufacturer", "제조업체"), ("individual", "개인"), ("other", "기타")]);

    // 일반 진행 상태와 입고 협의 상태, 공통 오류와 화면 대기 안내의 문맥을 구분합니다.
    public static string InboundSupplier(string code) => InboundSupplierOptions.FirstOrDefault(x => x.Key == code).Label ?? "유형 미확인";
    public static string InboundCategory(string code, string unknown = "분류 미확인") => InboundCategoryOptions.FirstOrDefault(x => x.Key == code).Label ?? unknown;
    public static string InboundFood(bool? value) => value is true ? "식품" : value is false ? "식품 아님" : "식품 여부 미확인";
    public static string InboundProcessed(bool? value) => value is true ? "가공품" : value is false ? "가공하지 않음" : "가공 여부 미확인";
    public static string InboundPackaging(string code, string unknown = "포장 미확인") => InboundPackagingOptions.FirstOrDefault(x => x.Key == code).Label ?? unknown;
    public static string InboundTemperature(string code, string unknown = "온도 미확인") => InboundTemperatureOptions.FirstOrDefault(x => x.Key == code).Label ?? unknown;
    public static string InboundCollaborationStatus(string? code) => code switch { null or "Draft" => "입고 조건 작성 전", "Proposed" or "PendingConsent" or "AwaitingOtherParty" => "양측 동의 대기", "Confirmed" or "Approved" => "입고 예약 승인", "Rejected" => "입고 거절", "Cancelled" => "입고 취소", "NeedsReview" => "조건 재확인 필요", "PartiallyReceived" => "일부 입고", "Received" => "입고 완료", _ => "최신 협의 상태 확인 필요" };
    public static string InboundHistoryKind(string code) => code switch { "Proposal" or "Proposed" => "조건 제안", "Consent" or "Granted" => "조건 동의", "ConsentWithdrawn" => "동의 철회", "Reject" or "Rejected" => "거절", "Cancel" or "Cancelled" => "취소", "Note" => "협의 메모", "Approved" or "Confirmed" => "입고 승인", _ => "협의 기록" };
    public static string InboundWaitingReason(string code) => 분산이행표시.InboundReason(code) ?? (code switch
    { "InboundProposalRequired" => "공급자가 이번 물량과 일정을 먼저 제안해 주세요.", "InboundConsentRequired" or "InboundNotApproved" => "물품 소유자와 공간 제공자의 동의를 기다립니다.",
      "AcceptancePolicyChanged" or "InboundPolicyChanged" => "공간 수용 조건이 바뀌었습니다. 현재 조건으로 다시 협의해 주세요.", "StorageAgreementChanged" or "InboundAgreementChanged" => "보관 계약의 판본이 바뀌었습니다. 현재 계약을 다시 확인해 주세요.",
      "CapacityExceeded" or "InboundCapacityExceeded" => "공간의 수용량이 부족합니다. 수량이나 일정을 조정해 주세요.", "GoodsFactsRequired" or "GoodsClassificationUnknown" => "물품 분류와 보관 조건을 확인해 주세요.",
      "StorageAgreementRequired" => "유효한 보관 합의에 양측 동의한 뒤 진행해 주세요.", "InboundRejected" => "이번 입고가 거절되었습니다. 조건을 다시 협의해 주세요.", "InboundCancelled" => "이번 입고가 취소되었습니다.",
      "InboundAlreadyReceived" => "이미 받은 물품은 유지됩니다. 수량 차이나 반송은 별도로 협의해 주세요.", _ => "현재 품목·용량·계약·일정을 다시 확인해 주세요. 승인 전에는 사진 입고를 진행할 수 없습니다." });
}
