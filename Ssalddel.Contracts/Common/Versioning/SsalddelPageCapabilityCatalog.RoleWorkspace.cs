namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IReadOnlyList<SsalddelPageCapabilityRule> CreateRoleWorkspaceItems()
    {
        var rules = new List<SsalddelPageCapabilityRule>();
        foreach (var app in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            var prefix = app == SsalddelPageAppCodes.IntegratedWeb ? "web" : "shipper";
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
        }
        return rules;
    }
}
