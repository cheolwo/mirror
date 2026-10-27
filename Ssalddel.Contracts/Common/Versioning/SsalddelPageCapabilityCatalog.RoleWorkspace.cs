namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IReadOnlyList<SsalddelPageCapabilityRule> CreateRoleWorkspaceItems()
    {
        var rules = new List<SsalddelPageCapabilityRule>();
        foreach (var app in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            var prefix = app == SsalddelPageAppCodes.IntegratedWeb ? "web" : "shipper";
            if (app == SsalddelPageAppCodes.Shipper)
                rules.Add(Exact(prefix + "-driver-recommendation-alert-settings", app, "/driver/notifications/settings", PageCapabilityStage.Beta,
                    PageInteractionBoundary.PlatformPersistence, true, "3.5",
                    "현재 기사 본인의 음식·화물 추천 알림 허용과 소리·진동·야간 무음을 저장하고 재조회합니다. 배차 수락·현재 수행·운영 권한은 변경하지 않습니다."));
            rules.Add(Exact(prefix + "-driver-current-work", app, "/driver/current-work", PageCapabilityStage.Beta,
                PageInteractionBoundary.ReadOnly, true, "3.5",
                "같은 계정의 음식·화물·직접 피킹 현재 업무를 조회하고 기존 전문 화면으로 이동합니다. 수락·완료·위치 갱신·권한 변경은 수행하지 않습니다.",
                workflowCodes: ["FoodDelivery", "DomesticTransport", "WarehouseFulfillment"]));
            rules.Add(Exact(prefix + "-home-participation-details", app, "/overview/participation", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0",
                "공개 화물 참여 집계의 누적·월별 수치와 기준만 조회합니다. 개인 원장과 업무 처리는 기존 로그인·역할 권한을 따릅니다."));
            foreach (var route in new[] { "/services", "/quick" })
            {
                rules.Add(Exact(prefix + (route == "/services" ? "-service-introduction" : "-quick-introduction-alias"),
                    app, route, PageCapabilityStage.Live, PageInteractionBoundary.ReadOnly, false, "0.0",
                    "음식·퀵/화물·공간 작업의 이용 방법과 준비 상태를 안내합니다. 업무 실행·수납·지급은 각 역할의 별도 경계를 따릅니다."));
            }
            rules.Add(Prefix(prefix + "-activity-settlements", app, "/activity-settlements", PageCapabilityStage.Beta,
                PageInteractionBoundary.Simulation, true, "3.5",
                "기본 비활성 정산 연습 기능입니다. 인증한 본인의 기본보수·공동 초과성과금 근거를 조회하고 약정 동의·항목 확인·이의·수령 진술을 기록합니다. 실제 이체나 확정 이익·은행 입금 인증은 제공하지 않습니다.",
                workflowCodes: ["WarehouseFulfillment", "FoodDelivery", "DomesticTransport"]));
            rules.Add(Prefix(prefix + "-role-workspace-login", app, "/workspace-login", PageCapabilityStage.Beta,
                PageInteractionBoundary.PlatformPersistence, false, "3.5",
                "역할의 계정 인증만 수행하고 원래 업무로 복귀합니다. 역할 선택은 업무 권한을 부여하지 않습니다."));
            rules.Add(Prefix(prefix + "-role-workspace", app, "/workspace", PageCapabilityStage.Beta,
                PageInteractionBoundary.PlatformPersistence, false, "3.5",
                "공통 지도 화면에서 현재 업무를 조회하고 명시적으로 처리합니다. 보호 조회와 명령은 역할별 기존 서버 권한을 확인합니다."));
            rules.Add(Exact(prefix + "-role-workspace-community", app, "/workspace/community", PageCapabilityStage.Live,
                PageInteractionBoundary.ReadOnly, false, "0.0",
                "기존 생활 지도를 재사용하며 비공개 업무는 현재 계정의 허용 범위에서만 표시합니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]));
            foreach (var role in new[] { "orderer", "restaurant", "food-driver", "operator" })
                rules.Add(Exact(prefix + "-role-workspace-" + role, app, "/workspace/" + role, PageCapabilityStage.Beta,
                    PageInteractionBoundary.PlatformPersistence, false, "3.5",
                    "기존 음식 주문·배달의 현재 상태와 허용 행동을 사용합니다. 관리자 인증과 음식 수행 권한은 별도로 확인합니다.",
                    featureKeys: [Food], workflowCodes: ["FoodDelivery"]));
            foreach (var role in new[] { "restaurant", "food-driver", "shipper", "cargo-driver" })
            {
                var food = role is "restaurant" or "food-driver";
                rules.Add(Exact(prefix + "-agreed-rates-" + role, app, "/workspace/" + role + "/agreed-rates", PageCapabilityStage.Beta,
                    PageInteractionBoundary.PlatformPersistence, false, "3.0",
                    "인증한 당사자가 사전 요금표를 제안·응답하고 실제 업무에서 명시 선택합니다. 합의와 배차 수락·지급 완료는 별개입니다.", true,
                    [food ? Food : DomesticTransport], [food ? "FoodDelivery" : "DomesticTransport"]));
            }
        }
        rules.Add(Exact("food-driver-agreed-rates", SsalddelPageAppCodes.FoodDeliveryDriver, "/agreed-rates", PageCapabilityStage.Beta,
            PageInteractionBoundary.PlatformPersistence, false, "3.0", "본인 음식 배달 요금표를 제안·응답합니다. 배차 자동 수락이나 지급 처리를 하지 않습니다.", true,
            [Food], ["FoodDelivery"]));
        rules.Add(Exact("food-driver-rates-return", SsalddelPageAppCodes.FoodDeliveryDriver, "/rates-return", PageCapabilityStage.Beta,
            PageInteractionBoundary.ReadOnly, false, "3.0", "네이티브 내 정보 화면으로 돌아갑니다.", featureKeys: [Food], workflowCodes: ["FoodDelivery"]));
        rules.Add(Exact("restaurant-agreed-rates", SsalddelPageAppCodes.RestaurantDesk, "/settings/agreed-rates", PageCapabilityStage.Beta,
            PageInteractionBoundary.PlatformPersistence, false, "3.0", "본인 음식점 요금표와 특정 주문의 적용 근거를 확인하고 변경 결과를 재조회합니다.", true,
            [Food], ["FoodDelivery"]));
        rules.Add(Exact("restaurant-order-alert-settings", SsalddelPageAppCodes.RestaurantDesk, "/settings/notifications", PageCapabilityStage.Beta,
            PageInteractionBoundary.PlatformPersistence, false, "0.0", "현재 계정의 기기 주문 알림 방식과 OS 권한을 확인합니다. 주문 상태를 변경하지 않습니다."));
        return rules;
    }
}
