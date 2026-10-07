namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IEnumerable<SsalddelPageCapabilityRule> CreateProtectionSupportItems()
    {
        yield return Prefix("admin-privacy-support", SsalddelPageAppCodes.Admin, "/privacy-support",
            PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
            "운영자 권한과 사건별 담당 배정을 확인해 검토·안내문 준비·실제 전달 증빙을 기록합니다. 외부 발송과 미지원 개인정보 처리는 실행하지 않습니다.");
        yield return Exact("food-driver-support-login", SsalddelPageAppCodes.FoodDeliveryDriver, "/driver-support-login",
            PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, false, "0.0",
            "기사 로그인 화면으로 돌아갑니다. 접수와 배달 상태를 변경하지 않습니다.");
        yield return Exact("food-driver-commerce-notices", SsalddelPageAppCodes.FoodDeliveryDriver, "/commerce/notices",
            PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, false, "0.0",
            "거래·개인정보 처리 기준을 확인합니다. 실제 거래 운영 가능 여부는 별도입니다.");
        yield return Prefix("food-driver-commerce-disputes", SsalddelPageAppCodes.FoodDeliveryDriver, "/commerce/disputes",
            PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
            "실제 수행 원장의 주문번호로 본인 배달 문제를 접수합니다. 주문자 개인정보를 접수 경로에 복사하지 않습니다.");
        yield return Prefix("food-driver-commerce-privacy", SsalddelPageAppCodes.FoodDeliveryDriver, "/commerce/privacy",
            PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
            "로그인한 본인의 개인정보 요청을 접수합니다. 실제 처리 확인 전에는 완료를 가정하지 않습니다.");
        yield return Exact("food-driver-commerce-seller", SsalddelPageAppCodes.FoodDeliveryDriver, "/commerce/seller",
            PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
            "공통 안내에서 연결된 본인 판매자 정보와 실제 확인 상태를 조회합니다. 기사 권한을 판매자 확인 완료로 바꾸지 않습니다.");
    }
}
