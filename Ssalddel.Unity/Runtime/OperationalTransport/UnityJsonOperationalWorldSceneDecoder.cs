#if UNITY_5_3_OR_NEWER
using System;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;
using UnityEngine;

namespace Ssalddel.Unity.OperationalTransport
{
    /// <summary>ASP.NET Core camelCase JSON을 Unity 비영속 장면 계약으로 변환합니다.</summary>
    public sealed class UnityJsonOperationalWorldSceneDecoder : IOperationalWorldSceneDecoder
    {
        public OperationalWorldSceneResponse Decode(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("OperationalWorldSceneJsonEmpty", nameof(json));
            var wire = JsonUtility.FromJson<SceneResponseWire>(json);
            if (wire == null) throw new FormatException("OperationalWorldSceneJsonInvalid");
            return wire.ToContract();
        }
    }

    [Serializable]
    internal sealed class SceneResponseWire
    {
        public string schemaVersion = string.Empty;
        public string areaStableId = string.Empty;
        public long cursor;
        public bool isFullSnapshot;
        public string asOfUtc = string.Empty;
        public int refreshAfterSeconds;
        public SceneItemWire[] items = Array.Empty<SceneItemWire>();
        public SceneFailureWire[] sourceFailures = Array.Empty<SceneFailureWire>();

        public OperationalWorldSceneResponse ToContract()
            => new OperationalWorldSceneResponse
            {
                SchemaVersion = schemaVersion,
                AreaStableId = areaStableId,
                Cursor = cursor,
                IsFullSnapshot = isFullSnapshot,
                AsOfUtc = ParseUtc(asOfUtc),
                RefreshAfterSeconds = refreshAfterSeconds,
                Items = Array.ConvertAll(items ?? Array.Empty<SceneItemWire>(), item => item.ToContract()),
                SourceFailures = Array.ConvertAll(
                    sourceFailures ?? Array.Empty<SceneFailureWire>(),
                    failure => failure.ToContract())
            };

        internal static DateTime ParseUtc(string value)
            => DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed)
                ? parsed.ToUniversalTime()
                : default;
    }

    [Serializable]
    internal sealed class SceneItemWire
    {
        public string snapshotStableId = string.Empty;
        public string areaStableId = string.Empty;
        public string operatingSystemId = string.Empty;
        public string itemKind = string.Empty;
        public string roleCode = string.Empty;
        public string activityCode = string.Empty;
        public long revision;
        public string occurredAtUtc = string.Empty;
        public string publishedAtUtc = string.Empty;
        public string expiresAtUtc = string.Empty;
        public bool isTombstone;
        public string dataPolicyCode = string.Empty;
        public bool localStorageAllowed;
        public bool replayAllowed;
        public string representationDataJson = "{}";

        public OperationalWorldSceneItem ToContract()
            => new OperationalWorldSceneItem
            {
                SnapshotStableId = snapshotStableId,
                AreaStableId = areaStableId,
                OperatingSystemId = operatingSystemId,
                ItemKind = itemKind,
                RoleCode = roleCode,
                ActivityCode = activityCode,
                Revision = revision,
                OccurredAtUtc = SceneResponseWire.ParseUtc(occurredAtUtc),
                PublishedAtUtc = SceneResponseWire.ParseUtc(publishedAtUtc),
                ExpiresAtUtc = SceneResponseWire.ParseUtc(expiresAtUtc),
                IsTombstone = isTombstone,
                DataPolicyCode = dataPolicyCode,
                LocalStorageAllowed = localStorageAllowed,
                ReplayAllowed = replayAllowed,
                RepresentationDataJson = representationDataJson
            };
    }

    [Serializable]
    internal sealed class SceneFailureWire
    {
        public string sourceCode = string.Empty;
        public string errorCode = string.Empty;
        public bool retryable;

        public OperationalWorldSceneSourceFailure ToContract()
            => new OperationalWorldSceneSourceFailure
            {
                SourceCode = sourceCode,
                ErrorCode = errorCode,
                Retryable = retryable
            };
    }
}
#endif
