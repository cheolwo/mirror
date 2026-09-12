using System;
using System.Collections.Generic;
using System.Linq;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Data.WorldProjection
{
    public sealed class OperationalWorldSceneApplyResult
    {
        public bool Accepted { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int RemovedCount { get; set; }
        public long Cursor { get; set; }
        public OperationalWorldSceneItem[] CurrentItems { get; set; } = Array.Empty<OperationalWorldSceneItem>();
        public OperationalWorldSceneApplyDiagnostic[] Diagnostics { get; set; } =
            Array.Empty<OperationalWorldSceneApplyDiagnostic>();
    }

    public sealed class OperationalWorldSceneApplyDiagnostic
    {
        public string SnapshotStableId { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public bool ExistingObjectFrozen { get; set; }
    }

    public static class OperationalWorldSceneApplyDiagnosticCodes
    {
        public const string SnapshotStableIdRequired = "SnapshotStableIdRequired";
        public const string AreaMismatch = "AreaMismatch";
        public const string DuplicateSnapshotStableId = "DuplicateSnapshotStableId";
        public const string LowerRevision = "LowerRevision";
        public const string V2FieldRequired = "V2FieldRequired";
        public const string SourceKindUnsupported = "SourceKindUnsupported";
        public const string ScenarioRunStableIdRequired = "ScenarioRunStableIdRequired";
        public const string LocalPersistenceForbidden = "LocalPersistenceForbidden";
        public const string SensitiveFieldForbidden = "SensitiveFieldForbidden";
    }

    /// <summary>
    /// 서버 상태를 확정하지 않는 Unity 표현 전용 해석기입니다. 메모리 안에서만 최신 판본과 TTL을 관리하며,
    /// 앱 종료 뒤 재생하거나 로컬 저장소에 남길 계약을 제공하지 않습니다.
    /// </summary>
    public sealed class OperationalWorldSceneInterpreter
    {
        private readonly Dictionary<string, OperationalWorldSceneItem> items =
            new Dictionary<string, OperationalWorldSceneItem>(StringComparer.Ordinal);
        private string areaStableId = string.Empty;
        private long cursor;

        public OperationalWorldSceneApplyResult Apply(OperationalWorldSceneResponse response, DateTime utcNow)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (!OperationalWorldScenePolicy.IsSupported(response.SchemaVersion))
                return Rejected("SchemaVersionUnsupported");
            if (string.IsNullOrWhiteSpace(response.AreaStableId)) return Rejected("AreaStableIdRequired");
            if (!string.IsNullOrWhiteSpace(areaStableId)
                && !string.Equals(areaStableId, response.AreaStableId, StringComparison.Ordinal))
                return Rejected("AreaMismatch");

            areaStableId = response.AreaStableId;
            var added = 0;
            var updated = 0;
            var removed = RemoveExpired(utcNow);
            var incomingIds = new HashSet<string>(StringComparer.Ordinal);
            var diagnostics = new List<OperationalWorldSceneApplyDiagnostic>();
            var duplicateIds = (response.Items ?? Array.Empty<OperationalWorldSceneItem>())
                .Where(item => !string.IsNullOrWhiteSpace(item.SnapshotStableId))
                .GroupBy(item => item.SnapshotStableId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.Ordinal);
            var failedSources = new HashSet<string>(
                (response.SourceFailures ?? Array.Empty<OperationalWorldSceneSourceFailure>())
                    .Select(failure => failure.SourceCode),
                StringComparer.Ordinal);

            foreach (var incoming in response.Items ?? Array.Empty<OperationalWorldSceneItem>())
            {
                var validationError = ValidateItem(incoming, response.SchemaVersion, duplicateIds);
                if (!string.IsNullOrEmpty(validationError))
                {
                    var id = incoming.SnapshotStableId ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(id)) incomingIds.Add(id);
                    diagnostics.Add(new OperationalWorldSceneApplyDiagnostic
                    {
                        SnapshotStableId = id,
                        ErrorCode = validationError,
                        ExistingObjectFrozen = !string.IsNullOrWhiteSpace(id) && items.ContainsKey(id)
                    });
                    continue;
                }

                var snapshotId = incoming.SnapshotStableId!;
                incomingIds.Add(snapshotId);
                if (incoming.IsTombstone || incoming.ExpiresAtUtc <= utcNow)
                {
                    if (items.Remove(snapshotId)) removed++;
                    continue;
                }

                if (!items.TryGetValue(snapshotId, out var current))
                {
                    items.Add(snapshotId, incoming);
                    added++;
                    continue;
                }

                if (incoming.Revision < current.Revision)
                {
                    diagnostics.Add(new OperationalWorldSceneApplyDiagnostic
                    {
                        SnapshotStableId = snapshotId,
                        ErrorCode = OperationalWorldSceneApplyDiagnosticCodes.LowerRevision,
                        ExistingObjectFrozen = true
                    });
                    continue;
                }
                if (incoming.Revision == current.Revision
                    && incoming.PublishedAtUtc <= current.PublishedAtUtc) continue;
                items[snapshotId] = incoming;
                updated++;
            }

            if (response.IsFullSnapshot)
            {
                var missing = items.Values
                    .Where(item => !incomingIds.Contains(item.SnapshotStableId)
                                   && !failedSources.Contains(item.OperatingSystemId))
                    .Select(item => item.SnapshotStableId)
                    .ToArray();
                foreach (var stableId in missing)
                {
                    if (items.Remove(stableId)) removed++;
                }
            }

            cursor = Math.Max(cursor, response.Cursor);
            return new OperationalWorldSceneApplyResult
            {
                Accepted = true,
                AddedCount = added,
                UpdatedCount = updated,
                RemovedCount = removed,
                Cursor = cursor,
                CurrentItems = Snapshot(),
                Diagnostics = diagnostics.ToArray()
            };
        }

        public OperationalWorldSceneApplyResult Expire(DateTime utcNow)
        {
            var removed = RemoveExpired(utcNow);
            return new OperationalWorldSceneApplyResult
            {
                Accepted = true,
                RemovedCount = removed,
                Cursor = cursor,
                CurrentItems = Snapshot()
            };
        }

        public void Clear()
        {
            items.Clear();
            areaStableId = string.Empty;
            cursor = 0;
        }

        private int RemoveExpired(DateTime utcNow)
        {
            var expired = items.Values
                .Where(item => item.ExpiresAtUtc <= utcNow)
                .Select(item => item.SnapshotStableId)
                .ToArray();
            foreach (var stableId in expired) items.Remove(stableId);
            return expired.Length;
        }

        private OperationalWorldSceneApplyResult Rejected(string errorCode)
            => new OperationalWorldSceneApplyResult
            {
                Accepted = false,
                ErrorCode = errorCode,
                Cursor = cursor,
                CurrentItems = Snapshot()
            };

        private string ValidateItem(
            OperationalWorldSceneItem item,
            string schemaVersion,
            ISet<string> duplicateIds)
        {
            if (string.IsNullOrWhiteSpace(item.SnapshotStableId))
                return OperationalWorldSceneApplyDiagnosticCodes.SnapshotStableIdRequired;
            if (!string.Equals(item.AreaStableId, areaStableId, StringComparison.Ordinal))
                return OperationalWorldSceneApplyDiagnosticCodes.AreaMismatch;
            if (duplicateIds.Contains(item.SnapshotStableId))
                return OperationalWorldSceneApplyDiagnosticCodes.DuplicateSnapshotStableId;
            if (item.LocalStorageAllowed || item.ReplayAllowed
                || !string.Equals(item.DataPolicyCode, OperationalWorldScenePolicy.OnlineEphemeral, StringComparison.Ordinal))
                return OperationalWorldSceneApplyDiagnosticCodes.LocalPersistenceForbidden;
            if (!string.Equals(schemaVersion, OperationalWorldScenePolicy.SchemaVersionV2, StringComparison.Ordinal))
                return string.Empty;
            if (ContainsSensitiveFieldName(item.RepresentationDataJson))
                return OperationalWorldSceneApplyDiagnosticCodes.SensitiveFieldForbidden;
            if (string.IsNullOrWhiteSpace(item.WorkStableId)
                || string.IsNullOrWhiteSpace(item.LifecycleStageCode)
                || string.IsNullOrWhiteSpace(item.AttentionStateCode)
                || string.IsNullOrWhiteSpace(item.ObjectKindCode)
                || string.IsNullOrWhiteSpace(item.SemanticPlaceStableId))
                return OperationalWorldSceneApplyDiagnosticCodes.V2FieldRequired;
            if (!string.Equals(item.SourceKindCode, OperationalWorldSceneSourceKinds.VerificationSample, StringComparison.Ordinal)
                && !string.Equals(item.SourceKindCode, OperationalWorldSceneSourceKinds.OperationalProjection, StringComparison.Ordinal))
                return OperationalWorldSceneApplyDiagnosticCodes.SourceKindUnsupported;
            if (string.Equals(item.SourceKindCode, OperationalWorldSceneSourceKinds.VerificationSample, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(item.ScenarioRunStableId))
                return OperationalWorldSceneApplyDiagnosticCodes.ScenarioRunStableIdRequired;
            return string.Empty;
        }

        private static bool ContainsSensitiveFieldName(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            var forbidden = new[]
            {
                "\"phone\"", "\"telephone\"", "\"address\"", "\"latitude\"", "\"longitude\"",
                "\"gps\"", "\"authToken\"", "\"accessToken\""
            };
            return forbidden.Any(value => json.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private OperationalWorldSceneItem[] Snapshot()
            => items.Values
                .OrderBy(item => item.ItemKind, StringComparer.Ordinal)
                .ThenBy(item => item.SnapshotStableId, StringComparer.Ordinal)
                .ToArray();
    }
}
