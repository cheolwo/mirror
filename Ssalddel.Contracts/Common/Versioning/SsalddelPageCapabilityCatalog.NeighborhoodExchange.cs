using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IReadOnlyList<SsalddelPageCapabilityRule> CreateNeighborhoodExchangeItems()
        =>
        [
            .. CreateNeighborhoodCollaborationItems(),
            Exact("community-neighborhood-map", SsalddelPageAppCodes.IntegratedWeb, "/community/map",
                PageCapabilityStage.Live, PageInteractionBoundary.ReadOnly, false, "0.0",
                "공개 동네의 제공·필요 글을 지도 또는 목록으로 탐색합니다. 본인 생활 배송은 기존 인증·운송 기능 경계를 통과한 경우만 조회합니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("shipper-neighborhood-map", SsalddelPageAppCodes.Shipper, "/community/map",
                PageCapabilityStage.Live, PageInteractionBoundary.ReadOnly, false, "0.0",
                "역할 선택 없이 동네 교류와 본인 배송을 탐색하며 위치 선택만으로 등록·배차를 실행하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("shipper-community-login", SsalddelPageAppCodes.Shipper, NeighborhoodExchangeAuthenticationRoutes.Login,
                PageCapabilityStage.Live, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "역할을 선택하지 않고 계정 인증만 수행한 뒤 생활 교류 화면으로 돌아갑니다. 인증으로 배송 의뢰를 자동 제출하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("community-exchange-list", SsalddelPageAppCodes.IntegratedWeb, NeighborhoodExchange.Home,
                PageCapabilityStage.Live, PageInteractionBoundary.ReadOnly, false, "0.0",
                "제공하거나 필요한 것을 공개 글로 찾아보며 거래·배송을 자동으로 확정하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("community-exchange-write", SsalddelPageAppCodes.IntegratedWeb, NeighborhoodExchange.Write,
                PageCapabilityStage.Live, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "회원 또는 익명 작성자가 제공·필요 글을 명시적으로 저장하며 주소나 결제를 요구하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Prefix("community-exchange-detail", SsalddelPageAppCodes.IntegratedWeb, NeighborhoodExchange.Home + "/posts",
                PageCapabilityStage.Live, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "선택한 공개 글에 문의하거나 본인 글을 삭제하며 댓글을 실제 거래 인증으로 취급하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("shipper-community-exchange-list", SsalddelPageAppCodes.Shipper, NeighborhoodExchange.Home,
                PageCapabilityStage.Live, PageInteractionBoundary.ReadOnly, false, "0.0",
                "Web과 같은 공개 제공·필요 목록을 조회하며 거래·배송을 자동으로 확정하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("shipper-community-exchange-write", SsalddelPageAppCodes.Shipper, NeighborhoodExchange.Write,
                PageCapabilityStage.Live, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "Web과 같은 공용 화면에서 회원 또는 익명 작성자로 제공·필요 글을 명시적으로 저장합니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Prefix("shipper-community-exchange-detail", SsalddelPageAppCodes.Shipper, NeighborhoodExchange.Home + "/posts",
                PageCapabilityStage.Live, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "선택한 공개 글에 문의하거나 본인 글을 삭제하며 댓글을 실제 거래 인증으로 취급하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]),
            Exact("community-delivery-list", SsalddelPageAppCodes.IntegratedWeb, NeighborhoodDeliveryRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, true, SsalddelProductRoadmapCatalog.TransportVersion,
                "로그인한 본인의 생활 화물 배송 의뢰만 조회하며 공개 교류 글에 상세주소·연락처를 노출하지 않습니다.",
                featureKeys: [DomesticTransport], workflowCodes: ["DomesticTransport"]),
            Exact("community-delivery-create", SsalddelPageAppCodes.IntegratedWeb, NeighborhoodDeliveryRoutes.Create,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, SsalddelProductRoadmapCatalog.TransportVersion,
                "명시적 개인정보 동의와 서버 운임 확인 뒤 생활 화물 의뢰를 배차 큐에 접수합니다. 기사 제안은 기존 운영·기능 게이트를 따르며 수락·수금·지급을 확정하지 않습니다.", true,
                [DomesticTransport], ["DomesticTransport"]),
            Prefix("community-delivery-detail", SsalddelPageAppCodes.IntegratedWeb, NeighborhoodDeliveryRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, true, SsalddelProductRoadmapCatalog.TransportVersion,
                "본인 배송 의뢰의 서버 상태·운임·배차 진행을 다시 조회하며 기사 수락·수금·지급을 대신 확정하지 않습니다.",
                featureKeys: [DomesticTransport], workflowCodes: ["DomesticTransport"]),
            Exact("shipper-community-delivery-list", SsalddelPageAppCodes.Shipper, NeighborhoodDeliveryRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, true, SsalddelProductRoadmapCatalog.TransportVersion,
                "Web과 같은 공용 화면에서 본인의 생활 화물 배송 의뢰만 조회합니다.",
                featureKeys: [DomesticTransport], workflowCodes: ["DomesticTransport"]),
            Exact("shipper-community-delivery-create", SsalddelPageAppCodes.Shipper, NeighborhoodDeliveryRoutes.Create,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true, SsalddelProductRoadmapCatalog.TransportVersion,
                "Web과 같은 동의·운임 확인으로 생활 화물 의뢰를 접수합니다. 기사 제안은 기존 운영·기능 게이트를 따르며 수락·수금·지급을 확정하지 않습니다.", true,
                [DomesticTransport], ["DomesticTransport"]),
            Prefix("shipper-community-delivery-detail", SsalddelPageAppCodes.Shipper, NeighborhoodDeliveryRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, true, SsalddelProductRoadmapCatalog.TransportVersion,
                "같은 의뢰 ID의 서버 상태·운임·배차 진행을 다시 조회하며 기사 수락·수금·지급을 대신 확정하지 않습니다.",
                featureKeys: [DomesticTransport], workflowCodes: ["DomesticTransport"])
        ];

    private static IEnumerable<SsalddelPageCapabilityRule> CreateNeighborhoodCollaborationItems()
    {
        foreach (var app in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            var key = app == SsalddelPageAppCodes.Shipper ? "shipper-neighborhood" : "community-neighborhood";
            yield return Exact(key + "-work", app, NeighborhoodCollaborationRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, false, "0.0",
                "본인이 부탁한 일과 맡은 일을 상태별로 확인합니다.", featureKeys: [Community], workflowCodes: ["CommunityTrust"]);
            yield return Prefix(key + "-work-actions", app, NeighborhoodCollaborationRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "현재 조건의 양측 합의·수행·완료·공개 이력 동의를 따로 기록하며 실제 배송·입금을 대신 확정하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]);
            yield return Exact(key + "-spaces", app, NeighborhoodStorageRoutes.ListPage,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, false, "0.0",
                "동네 대표 위치의 작은 보관 능력을 탐색하며 집의 상세주소는 공개하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]);
            yield return Prefix(key + "-spaces-actions", app, NeighborhoodStorageRoutes.ListPage,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, false, "0.0",
                "본인 공간의 능력·수량·시간과 합의한 물품의 인수·반환을 기록합니다. 전문 창고 등록·재고·입고 권한은 부여하지 않습니다.",
                featureKeys: [Community], workflowCodes: ["CommunityTrust"]);
        }
    }
}
