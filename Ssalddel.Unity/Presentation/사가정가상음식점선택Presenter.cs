using System;
using System.Linq;
using Ssalddel.Simulation.Contracts;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Presentation
{
    public sealed class 사가정가상음식점선택Model
    {
        public string ProfileStableId { get; set; } = string.Empty;
        public string RestaurantStableId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string MenuItemStableId { get; set; } = string.Empty;
        public string DisplayRouteStableId { get; set; } = string.Empty;
        public string DisplayRouteKindCode { get; set; } = string.Empty;
        public bool SyntheticFixture { get; set; }
        public bool PublicBusinessLinked { get; set; }
        public bool DistributionApproved { get; set; }
        public bool RouteApplied { get; set; }
        public bool TraversalReady { get; set; }
    }

    /// <summary>
    /// 사가정 첫 주문 화면이 세 가상 음식점을 나열하고 주문 사본의 음식점을 식별하도록 돕습니다.
    /// 실제 상호·좌표를 추측하거나 주문 Command를 실행하지 않습니다.
    /// </summary>
    [Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
        Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E2,
        "사가정 세 합성 음식점 선택 목록과 음식배달 수명주기 표시를 읽기 전용으로 결속한다.",
        Boundary = "표시 모델만 만들며 실제 사업장·광고·Scene 배치·주문 상태를 변경하지 않는다.")]
    public sealed class 사가정가상음식점선택Presenter
    {
        public 사가정가상음식점선택Model[] 목록()
            => 사가정가상음식점Catalog.목록().Select(Model).ToArray();

        public (사가정가상음식점선택Model Restaurant, 음식배달수명주기표현 Lifecycle) 주문표현(
            음식배달수명주기Snapshot source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!string.Equals(source.SourceCode, 음식배달상태원천코드.SimulationCore, StringComparison.Ordinal)
                || !string.Equals(source.SourceRevision, 사가정가상음식점Policy.Revision, StringComparison.Ordinal))
                throw new ArgumentException("SagajeongSyntheticRestaurantSourceBoundaryInvalid", nameof(source));
            var profiles = 사가정가상음식점Catalog.목록();
            var profile = profiles.SingleOrDefault(value =>
                string.Equals(value.FacilityStableId, source.RestaurantStableId, StringComparison.Ordinal));
            if (profile == null)
                throw new ArgumentException("SagajeongSyntheticRestaurantBindingMissing", nameof(source));
            var sourceRefs = source.SourceRefs ?? Array.Empty<string>();
            var knownProfiles = profiles.Select(value => value.ProfileStableId).ToHashSet(StringComparer.Ordinal);
            var knownRoutes = profiles.Select(value => value.DisplayRouteStableId).ToHashSet(StringComparer.Ordinal);
            if (sourceRefs.Count(value => value == profile.ProfileStableId) != 1
                || sourceRefs.Count(value => value == profile.DisplayRouteStableId) != 1
                || sourceRefs.Count(knownProfiles.Contains) != 1
                || sourceRefs.Count(knownRoutes.Contains) != 1
                || sourceRefs.Any(IsActualBusinessReference))
                throw new ArgumentException("SagajeongSyntheticRestaurantProvenanceMissing", nameof(source));
            return (Model(profile), new 음식배달수명주기표현(source));
        }

        private static bool IsActualBusinessReference(string? value)
            => value != null
               && (value.StartsWith("claim:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("public-business:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("business-observation:", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("directory:sagajeong:", StringComparison.OrdinalIgnoreCase));

        private static 사가정가상음식점선택Model Model(사가정가상음식점Profile profile)
            => new 사가정가상음식점선택Model
            {
                ProfileStableId = profile.ProfileStableId,
                RestaurantStableId = profile.FacilityStableId,
                DisplayName = profile.DisplayName,
                MenuItemStableId = profile.MenuItemStableId,
                DisplayRouteStableId = profile.DisplayRouteStableId,
                DisplayRouteKindCode = profile.DisplayRouteKindCode,
                SyntheticFixture = profile.SourceKindCode == 사가정가상음식점Policy.SourceKindCode,
                PublicBusinessLinked = !string.IsNullOrEmpty(profile.PublicBusinessObservationStableId)
                                       || !string.IsNullOrEmpty(profile.ClaimStableId),
                DistributionApproved = profile.DistributionApproved,
                RouteApplied = profile.RouteApplied,
                TraversalReady = profile.TraversalReady,
            };
    }
}
