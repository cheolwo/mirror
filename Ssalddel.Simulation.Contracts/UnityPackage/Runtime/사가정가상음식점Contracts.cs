using System;
using System.Linq;

namespace Ssalddel.Simulation.Contracts
{
    /// <summary>
    /// 실제 사업장 관측과 분리된 사가정 첫 주문 폐루프용 합성 음식점 계약입니다.
    /// 표시명·메뉴·경로 역할은 fixture이며 실제 업체의 참여나 위치를 뜻하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E1,
        "사가정 세 합성 음식점의 안정 식별자·표시명·주문 profile 경계를 고정한다.",
        Boundary = "SyntheticFixture 전용이며 공개 사업장·Claim·광고·실제 Entrance와 결합하지 않는다.")]
    public static class 사가정가상음식점Policy
    {
        public const string ScenarioStableId = "scenario:synthetic-sagajeong-food-delivery.r1";
        public const string Revision = "synthetic-sagajeong-food-delivery.r1";
        public const string SourceKindCode = "SyntheticFixture";
        public const string MainRoadRouteKindCode = "MainRoadDisplayFixture";
        public const string NeighborhoodRoadRouteKindCode = "NeighborhoodRoadDisplayFixture";
        public const string AlleyRouteKindCode = "AlleyDisplayFixture";
        public const bool PublicBusinessLinked = false;
        public const bool DistributionApproved = false;
        public const bool RouteApplied = false;
        public const bool TraversalReady = false;

        public static bool Matches(string scenarioStableId, string scenarioDataRevision)
            => string.Equals(scenarioStableId, ScenarioStableId, StringComparison.Ordinal)
               && string.Equals(scenarioDataRevision, Revision, StringComparison.Ordinal);
    }

    public sealed class 사가정가상음식점Profile
    {
        public string ProfileStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FacilityStableId { get; set; } = string.Empty;
        public string OrganizationStableId { get; set; } = string.Empty;
        public string ActorStableId { get; set; } = string.Empty;
        public string PolicyStableId { get; set; } = string.Empty;
        public string GrantStableId { get; set; } = string.Empty;
        public string MenuItemStableId { get; set; } = string.Empty;
        public string DisplayRouteStableId { get; set; } = string.Empty;
        public string DisplayRouteKindCode { get; set; } = string.Empty;
        public int PreparationDurationTicks { get; set; }
        public string SourceKindCode { get; set; } = 사가정가상음식점Policy.SourceKindCode;
        public string PublicBusinessObservationStableId { get; set; } = string.Empty;
        public string ClaimStableId { get; set; } = string.Empty;
        public bool DistributionApproved { get; set; }
        public bool RouteApplied { get; set; }
        public bool TraversalReady { get; set; }

        public 사가정가상음식점Profile Copy() => (사가정가상음식점Profile)MemberwiseClone();
    }

    public static class 사가정가상음식점Catalog
    {
        private static readonly 사가정가상음식점Profile[] Profiles =
        {
            Profile("main-road", "가상 사가정 큰길식당", "MainRoadDisplayFixture", 2),
            Profile("neighborhood-road", "가상 면목 생활길분식", "NeighborhoodRoadDisplayFixture", 3),
            Profile("alley", "가상 골목안 도시락", "AlleyDisplayFixture", 4),
        };

        public static 사가정가상음식점Profile[] 목록()
            => Profiles.Select(value => value.Copy()).ToArray();

        public static 사가정가상음식점Profile 찾기(string profileStableId)
        {
            if (string.IsNullOrWhiteSpace(profileStableId))
                throw new ArgumentException("SagajeongSyntheticRestaurantProfileMissing", nameof(profileStableId));
            var profile = Profiles.SingleOrDefault(value =>
                string.Equals(value.ProfileStableId, profileStableId, StringComparison.Ordinal));
            return profile?.Copy()
                   ?? throw new ArgumentException("SagajeongSyntheticRestaurantProfileNotFound", nameof(profileStableId));
        }

        public static 사가정가상음식점Profile 주문순서(int sequence, string residence)
        {
            if (sequence <= 0 || residence is not ("a" or "b"))
                throw new ArgumentException("SagajeongSyntheticRestaurantOrderSequenceInvalid");
            var offset = residence == "a" ? 0 : 1;
            return Profiles[(sequence - 1 + offset) % Profiles.Length].Copy();
        }

        private static 사가정가상음식점Profile Profile(
            string key,
            string displayName,
            string routeKindCode,
            int preparationDurationTicks)
        {
            var prefix = "synthetic:sagajeong:restaurant:" + key;
            return new 사가정가상음식점Profile
            {
                ProfileStableId = "restaurant-profile:" + prefix,
                DisplayName = displayName,
                FacilityStableId = "facility:" + prefix,
                OrganizationStableId = "organization:" + prefix,
                ActorStableId = "actor:" + prefix,
                PolicyStableId = "policy:" + prefix + ":cooking",
                GrantStableId = "grant:" + prefix + ":cooking",
                MenuItemStableId = "menu-item:" + prefix + ":signature",
                DisplayRouteStableId = "route:" + prefix + ":display-only",
                DisplayRouteKindCode = routeKindCode,
                PreparationDurationTicks = preparationDurationTicks,
                DistributionApproved = false,
                RouteApplied = false,
                TraversalReady = false,
            };
        }
    }
}
