using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Contracts.Common.Versioning;

public static partial class SsalddelPageCapabilityCatalog
{
    private static IEnumerable<SsalddelPageCapabilityRule> CreateNeighborhoodMemberDeliveryItems()
    {
        foreach (var app in new[] { SsalddelPageAppCodes.IntegratedWeb, SsalddelPageAppCodes.Shipper })
        {
            var key = app == SsalddelPageAppCodes.Shipper ? "shipper-member-delivery" : "community-member-delivery";
            yield return Exact(key + "-participation", app, NeighborhoodMemberDeliveryRoutes.Home,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true,
                SsalddelProductRoadmapCatalog.TransportVersion,
                "본인 배송 참여 의사와 비공개 활동 기준점을 저장합니다. 설정으로 기사 역할이나 자동 수락 권한을 부여하지 않습니다.",
                featureKeys: [Community, DomesticTransport], workflowCodes: ["CommunityTrust", "DomesticTransport"]);
            yield return Exact(key + "-offers", app, NeighborhoodMemberDeliveryRoutes.Offers,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, true,
                SsalddelProductRoadmapCatalog.TransportVersion,
                "본인에게 제공된 생활 배송 제안을 다시 조회합니다. 다른 회원의 정보나 상세 인계 주소는 공개하지 않습니다.",
                featureKeys: [Community, DomesticTransport], workflowCodes: ["CommunityTrust", "DomesticTransport"]);
            yield return Prefix(key + "-offer-detail", app, NeighborhoodMemberDeliveryRoutes.Offers,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true,
                SsalddelProductRoadmapCatalog.TransportVersion,
                "본인 제안의 조건과 현재 위치를 확인하여 한 건을 수락하거나 거절합니다. 원자적 확정은 서버가 결정합니다.", true,
                [Community, DomesticTransport], ["CommunityTrust", "DomesticTransport"]);
            yield return Exact(key + "-work", app, NeighborhoodMemberDeliveryRoutes.Work,
                PageCapabilityStage.Beta, PageInteractionBoundary.ReadOnly, true,
                SsalddelProductRoadmapCatalog.TransportVersion,
                "본인에게 확정된 배송 목록을 조회합니다. 배송 완료와 수령·입금 확인을 구분합니다.",
                featureKeys: [Community, DomesticTransport], workflowCodes: ["CommunityTrust", "DomesticTransport"]);
            yield return Prefix(key + "-work-detail", app, NeighborhoodMemberDeliveryRoutes.Work,
                PageCapabilityStage.Beta, PageInteractionBoundary.PlatformPersistence, true,
                SsalddelProductRoadmapCatalog.TransportVersion,
                "본인의 확정 배송만 사진 전송 후 픽업·전달을 완료하고 문제를 접수합니다. 일반 기사·음식·창고 업무 권한을 대신하지 않습니다.", true,
                [Community, DomesticTransport], ["CommunityTrust", "DomesticTransport"]);
        }
    }
}
