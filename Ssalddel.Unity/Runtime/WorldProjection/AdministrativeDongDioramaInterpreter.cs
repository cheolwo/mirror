using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Data.WorldProjection
{
    public sealed class AdministrativeDongDioramaApplyResult
    {
        public bool Accepted { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public AdministrativeDongDioramaManifest Manifest { get; set; } = new AdministrativeDongDioramaManifest();
        public AdministrativeDongDioramaTile[] Tiles { get; set; } = Array.Empty<AdministrativeDongDioramaTile>();
        public AdministrativeDongDisplayOverlay[] DisplayOverlays { get; set; } = Array.Empty<AdministrativeDongDisplayOverlay>();
    }

    public sealed class AdministrativeDongOperationalBindingResult
    {
        public OperationalWorldSceneItem[] MappedItems { get; set; } = Array.Empty<OperationalWorldSceneItem>();
        public int UnresolvedItemCount { get; set; }
    }

    /// <summary>
    /// 행정동 기반 배경 자료와 표시 오버레이를 검증해 메모리에만 보관합니다. 법정동 단위
    /// 운영 객체는 명시된 SemanticPlaceStableId가 없으면 행정동마다 복제하지 않고 미해결 수로 남깁니다.
    /// </summary>
    public sealed class AdministrativeDongDioramaInterpreter
    {
        private AdministrativeDongDioramaManifest? manifest;
        private AdministrativeDongDioramaTile[] tiles = Array.Empty<AdministrativeDongDioramaTile>();
        private AdministrativeDongDisplayOverlay[] overlays = Array.Empty<AdministrativeDongDisplayOverlay>();

        public AdministrativeDongDioramaApplyResult Apply(
            AdministrativeDongDioramaManifest incomingManifest,
            AdministrativeDongDioramaTile[] incomingTiles,
            AdministrativeDongDisplayOverlayResponse incomingOverlays)
        {
            if (incomingManifest == null) throw new ArgumentNullException(nameof(incomingManifest));
            if (incomingTiles == null) throw new ArgumentNullException(nameof(incomingTiles));
            if (incomingOverlays == null) throw new ArgumentNullException(nameof(incomingOverlays));
            var error = Validate(incomingManifest, incomingTiles, incomingOverlays);
            if (!string.IsNullOrEmpty(error)) return Rejected(error);

            manifest = incomingManifest;
            tiles = incomingTiles.OrderBy(tile => tile.TileStableId, StringComparer.Ordinal).ToArray();
            overlays = incomingOverlays.Items
                .OrderBy(item => item.OverlayKindCode, StringComparer.Ordinal)
                .ThenBy(item => item.OverlayStableId, StringComparer.Ordinal)
                .ToArray();
            return Snapshot(true, string.Empty);
        }

        public AdministrativeDongOperationalBindingResult BindOperationalItems(
            IEnumerable<OperationalWorldSceneItem> operationalItems)
        {
            if (operationalItems == null) throw new ArgumentNullException(nameof(operationalItems));
            if (manifest == null) return new AdministrativeDongOperationalBindingResult
                { UnresolvedItemCount = operationalItems.Count() };
            var places = new HashSet<string>(manifest.SemanticPlaceStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            var mapped = new List<OperationalWorldSceneItem>();
            var unresolved = 0;
            foreach (var item in operationalItems)
            {
                if (!string.IsNullOrWhiteSpace(item.SemanticPlaceStableId) && places.Contains(item.SemanticPlaceStableId))
                    mapped.Add(item);
                else
                    unresolved++;
            }
            return new AdministrativeDongOperationalBindingResult
            {
                MappedItems = mapped.OrderBy(item => item.SnapshotStableId, StringComparer.Ordinal).ToArray(),
                UnresolvedItemCount = unresolved
            };
        }

        public void Clear()
        {
            manifest = null;
            tiles = Array.Empty<AdministrativeDongDioramaTile>();
            overlays = Array.Empty<AdministrativeDongDisplayOverlay>();
        }

        private static string Validate(
            AdministrativeDongDioramaManifest value,
            AdministrativeDongDioramaTile[] tileValues,
            AdministrativeDongDisplayOverlayResponse overlayValues)
        {
            if (!string.Equals(value.SchemaVersion, AdministrativeDongDioramaPolicy.SchemaVersion, StringComparison.Ordinal))
                return "AdministrativeDongDioramaSchemaVersionUnsupported";
            if (!AdministrativeDongDioramaPolicy.IsAdministrativeAreaStableId(value.AdministrativeAreaStableId))
                return "AdministrativeDongStableIdInvalid";
            if (!value.ObservationPresentationOnly || value.TraversalReady || value.GameplayReady
                || !string.Equals(value.DataPolicyCode, AdministrativeDongDioramaPolicy.ObservationPresentationOnly, StringComparison.Ordinal))
                return "AdministrativeDongDioramaAuthorityBoundaryInvalid";
            if (string.IsNullOrWhiteSpace(value.ProjectionHashSha256))
                return "AdministrativeDongDioramaProjectionHashRequired";
            var summaries = value.Tiles ?? Array.Empty<AdministrativeDongDioramaTileSummary>();
            if (summaries.GroupBy(item => item.TileStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
                || tileValues.GroupBy(item => item.TileStableId, StringComparer.Ordinal).Any(group => group.Count() != 1)
                || summaries.Length != tileValues.Length)
                return "AdministrativeDongDioramaTileSetMismatch";
            var summaryMap = summaries.ToDictionary(item => item.TileStableId, StringComparer.Ordinal);
            foreach (var tile in tileValues)
                if (!string.Equals(tile.AdministrativeAreaStableId, value.AdministrativeAreaStableId, StringComparison.Ordinal)
                    || !string.Equals(tile.ProjectionHashSha256, value.ProjectionHashSha256, StringComparison.Ordinal)
                    || !summaryMap.TryGetValue(tile.TileStableId, out var summary)
                    || !string.Equals(summary.TileHashSha256, tile.TileHashSha256, StringComparison.Ordinal))
                    return "AdministrativeDongDioramaTileVersionMismatch";
            if (!string.Equals(overlayValues.SchemaVersion, AdministrativeDongDioramaPolicy.DisplayOverlaySchemaVersion, StringComparison.Ordinal)
                || !string.Equals(overlayValues.AdministrativeAreaStableId, value.AdministrativeAreaStableId, StringComparison.Ordinal))
                return "AdministrativeDongDioramaOverlayVersionMismatch";
            foreach (var item in overlayValues.Items ?? Array.Empty<AdministrativeDongDisplayOverlay>())
            {
                if (string.IsNullOrWhiteSpace(item.OverlayStableId) || string.IsNullOrWhiteSpace(item.SemanticPlaceStableId))
                    return "AdministrativeDongDioramaOverlayIdentityRequired";
                if (string.Equals(item.OverlayKindCode, AdministrativeDongDisplayOverlayKinds.Sponsorship, StringComparison.Ordinal)
                    && (!item.AdvertisementDisclosureRequired
                        || (item.BadgeText.IndexOf("광고", StringComparison.Ordinal) < 0
                            && item.BadgeText.IndexOf("후원", StringComparison.Ordinal) < 0)))
                    return "AdministrativeDongDioramaSponsorshipDisclosureRequired";
            }
            return string.Empty;
        }

        private AdministrativeDongDioramaApplyResult Rejected(string errorCode) => Snapshot(false, errorCode);

        private AdministrativeDongDioramaApplyResult Snapshot(bool accepted, string errorCode)
            => new AdministrativeDongDioramaApplyResult
            {
                Accepted = accepted,
                ErrorCode = errorCode,
                Manifest = manifest ?? new AdministrativeDongDioramaManifest(),
                Tiles = tiles,
                DisplayOverlays = overlays
            };
    }
}
