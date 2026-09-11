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
            if (!string.Equals(response.SchemaVersion, OperationalWorldScenePolicy.SchemaVersion, StringComparison.Ordinal))
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
            var failedSources = new HashSet<string>(
                (response.SourceFailures ?? Array.Empty<OperationalWorldSceneSourceFailure>())
                    .Select(failure => failure.SourceCode),
                StringComparer.Ordinal);

            foreach (var incoming in response.Items ?? Array.Empty<OperationalWorldSceneItem>())
            {
                if (string.IsNullOrWhiteSpace(incoming.SnapshotStableId)
                    || !string.Equals(incoming.AreaStableId, areaStableId, StringComparison.Ordinal))
                    continue;

                incomingIds.Add(incoming.SnapshotStableId);
                if (incoming.IsTombstone || incoming.ExpiresAtUtc <= utcNow)
                {
                    if (items.Remove(incoming.SnapshotStableId)) removed++;
                    continue;
                }

                if (!items.TryGetValue(incoming.SnapshotStableId, out var current))
                {
                    items.Add(incoming.SnapshotStableId, incoming);
                    added++;
                    continue;
                }

                if (incoming.Revision < current.Revision) continue;
                if (incoming.Revision == current.Revision
                    && incoming.PublishedAtUtc <= current.PublishedAtUtc) continue;
                items[incoming.SnapshotStableId] = incoming;
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
                CurrentItems = Snapshot()
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

        private OperationalWorldSceneItem[] Snapshot()
            => items.Values
                .OrderBy(item => item.ItemKind, StringComparer.Ordinal)
                .ThenBy(item => item.SnapshotStableId, StringComparer.Ordinal)
                .ToArray();
    }
}
