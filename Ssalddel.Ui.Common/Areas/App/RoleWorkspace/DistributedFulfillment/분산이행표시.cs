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
        "DeliverBatch" => "같은 전달지 물품 전달", "ReturnBatch" => "같은 위치 반품 입고",
        _ => ExistingAction(code)
    };
    private static string ExistingAction(string code) => code switch
    { "create-site" or "CreateSite" => "거점 등록", "consent-owner" => "소유자 동의", "consent-manager" => "관리자 동의", "Consent" => "공간 동의 변경", "revoke-owner" or "revoke-manager" => "동의 철회", "create-location" or "CreateLocation" => "적재 위치 등록", "import-lot" or "ImportLot" => "기존 상품 위치 연결", "plan-inbound" or "PlanInbound" => "입고 계획", "receive" or "Receive" => "입고 확인", "create-wave" or "CreateWave" => "출고 묶음 생성", "add-order" or "AddOrder" => "주문 추가", "close" or "Close" => "접수 마감", "plan" or "Plan" => "순회 계획", "accept" or "Accept" => "순회 수락", "pick" or "Pick" => "직접 피킹", "deliver" or "Deliver" => "기사 전달 기록", "acknowledge" or "Acknowledge" => "고객 수령 확인", "cancel" or "Cancel" => "주문 취소", "return" or "Return" => "반품 확인", _ => "업무 처리" };
    public static string Owner(string label) => string.IsNullOrWhiteSpace(label) ? "등록된 상품 소유자" : label;
    public static string Product(string label, string sku) => string.IsNullOrWhiteSpace(label) ? sku : label;
    public static string Condition(string code) => code == "DistributedCargoAcceptancePending" ? "기존 화물 의뢰의 기사 수락 확인 대기" : "서버의 진행 조건 재확인 필요";
}
