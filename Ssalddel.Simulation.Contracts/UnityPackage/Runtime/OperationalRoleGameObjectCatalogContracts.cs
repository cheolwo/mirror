using System;

namespace Ssalddel.Simulation.Contracts
{
    public static class OperationalRoleObjectCatalogSchemas
    {
        public const string V3 = "operational-unity-transfer-catalog.v3";
    }

    public static class OperationalRoleObjectKindCodes
    {
        public const string Actor = "Actor";
        public const string Facility = "Facility";
        public const string Vehicle = "Vehicle";
        public const string WorkObject = "WorkObject";
    }

    public static class OperationalRoleObjectRepresentationCodes
    {
        public const string Candidate = "Candidate";
        public const string NoUnityRepresentation = "NoUnityRepresentation";
    }

    public static class OperationalRoleObjectSpawnModeCodes
    {
        public const string SnapshotProjection = "SnapshotProjection";
        public const string SimulationAnalog = "SimulationAnalog";
        public const string NotSpawnable = "NotSpawnable";
    }

    public static class OperationalRoleObjectIdentityPolicyCodes
    {
        public const string PseudonymousStableId = "PseudonymousStableId";
        public const string AggregateStableId = "AggregateStableId";
        public const string NoIdentity = "NoIdentity";
    }

    [Serializable]
    public sealed class OperationalUnityTransferObjectCatalog
    {
        public string SchemaVersion { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public OperationalRoleObjectCandidate[] RoleObjectCandidates { get; set; } =
            Array.Empty<OperationalRoleObjectCandidate>();
    }

    [Serializable]
    public sealed class OperationalRoleObjectCandidate
    {
        public string ObjectArchetypeId { get; set; } = string.Empty;
        public string DisplayNameKo { get; set; } = string.Empty;
        public string ObjectKindCode { get; set; } = string.Empty;
        public string[] SourceActorCodes { get; set; } = Array.Empty<string>();
        public string[] OperatingSystemIds { get; set; } = Array.Empty<string>();
        public string[] WorkflowCodes { get; set; } = Array.Empty<string>();
        public string ObservationRoleCode { get; set; } = string.Empty;
        public string VisualKey { get; set; } = string.Empty;
        public string RepresentationDecisionCode { get; set; } = string.Empty;
        public string SpawnModeCode { get; set; } = string.Empty;
        public string IdentityPolicyCode { get; set; } = string.Empty;
        public string[] PresentationStateCodes { get; set; } = Array.Empty<string>();
        public string[] ObservationProfileRefs { get; set; } = Array.Empty<string>();
        public string[] UnityImplementationRefs { get; set; } = Array.Empty<string>();
        public bool IsExecutionAuthority { get; set; }
        public bool ContainsPrivateData { get; set; }
        public bool PrefabReady { get; set; }
        public bool SceneReady { get; set; }
        public string Boundary { get; set; } = string.Empty;
    }
}
