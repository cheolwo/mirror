namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IEnumerable<SsalddelPageCapabilityRule> CreateCommerceProtectionItems()
    {
        yield return Exact("orderer-commerce-login", SsalddelPageAppCodes.Orderer, "/login",
            PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, false, "0.0",
            "계정 인증 뒤 안전한 보호 업무 경로로 복귀하며 거래·접수 요청을 자동 제출하지 않습니다.");
        // Legal notices and support rights remain reachable when an unrelated role feature is disabled.
        foreach (var (app, key) in new[]
        {
            (SsalddelPageAppCodes.IntegratedWeb, "web-commerce"),
            (SsalddelPageAppCodes.Shipper, "shipper-commerce"),
            (SsalddelPageAppCodes.Orderer, "orderer-commerce"),
            (SsalddelPageAppCodes.RestaurantDesk, "restaurant-commerce")
        })
        {
            yield return Exact(key + "-notices", app, "/commerce/notices",
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, false, "0.0",
                "현재 중개·거래·개인정보 안내와 운영 준비 상태를 로그인 없이 조회합니다. 거래 실행 가능 여부는 서버가 따로 확인합니다.");
            yield return Exact(key + "-seller", app, "/commerce/seller",
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
                "로그인한 본인의 판매자 정보를 등록하고 실제 인증 제공자의 확인 상태를 조회합니다. 등록만으로 확인 완료를 인정하지 않습니다.");
            yield return Prefix(key + "-privacy", app, "/commerce/privacy",
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
                "로그인한 본인의 개인정보 권리 요청을 접수하고 처리 상태·기한·보존 사유를 확인합니다. 접수만으로 삭제·탈퇴가 완료되지 않습니다.");
            yield return Prefix(key + "-disputes", app, "/commerce/disputes",
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, "0.0",
                "본인 거래의 문제를 접수하고 허용된 추가 자료 제출·이의 제기만 수행합니다. 운영자 처리 권한이나 상대방의 비공개 증거를 노출하지 않습니다.");
        }
    }
}
