namespace Ssalddel.Contracts.Common.Versioning;

public static class VersionFeatureFlagsRoutes
{
    public const string Metadata = "api/v1/version-feature-flags";
}

public sealed class VersionFeatureFlagsResponse
{
    public IReadOnlyDictionary<string, bool> Flags { get; init; } = new Dictionary<string, bool>();

    public IReadOnlyList<WorkflowFlagStateDto> Workflows { get; init; } = [];

    public IReadOnlyList<WorkflowRelationDto> WorkflowRelations { get; init; } = [];

    public IReadOnlyList<OperatingSystemDto> OperatingSystems { get; init; } = [];

    public IReadOnlyList<OperatingSystemCurrentStructureDto> OperatingSystemCurrentStructures { get; init; } = [];

    public IReadOnlyList<OperatingSystemInteractionDto> OperatingSystemInteractions { get; init; } = [];

    public IReadOnlyList<WorkflowApiEndpointDto> ApiEndpoints { get; init; } = [];

    public IReadOnlyList<PageCapabilityDto> PageCapabilities { get; init; } = [];
}

public sealed class WorkflowFlagStateDto
{
    public string WorkflowCode { get; init; } = string.Empty;

    public string WorkflowName { get; init; } = string.Empty;

    public string FlagKey { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public string BoundarySummary { get; init; } = string.Empty;

    public IReadOnlyList<WorkflowParticipantDto> Participants { get; init; } = [];

    public IReadOnlyList<WorkflowScreenDto> Screens { get; init; } = [];

    public IReadOnlyList<WorkflowUseCaseDto> UseCases { get; init; } = [];

    public IReadOnlyList<WorkflowProcessManagerDto> ProcessManagers { get; init; } = [];
}

public sealed class WorkflowParticipantDto
{
    public string ActorCode { get; init; } = string.Empty;

    public string ActorName { get; init; } = string.Empty;

    public bool IsPrimary { get; init; }

    public string Responsibility { get; init; } = string.Empty;
}

public sealed class WorkflowScreenDto
{
    public string ActorCode { get; init; } = string.Empty;

    public string AppCode { get; init; } = string.Empty;

    public string AppName { get; init; } = string.Empty;

    public string ScreenName { get; init; } = string.Empty;

    public string Route { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;
}

public sealed class WorkflowUseCaseDto
{
    public string UseCaseCode { get; init; } = string.Empty;

    public string UseCaseName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public bool IsRequired { get; init; }

    public IReadOnlyList<WorkflowUseCaseActorDto> PrimaryActors { get; init; } = [];

    public IReadOnlyList<WorkflowUseCaseActorDto> SupportingActors { get; init; } = [];

    public IReadOnlyList<WorkflowUseCaseRelationDto> Relations { get; init; } = [];
}

public sealed class WorkflowUseCaseActorDto
{
    public string ActorCode { get; init; } = string.Empty;

    public string ActorName { get; init; } = string.Empty;

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;
}

public sealed class WorkflowUseCaseRelationDto
{
    public string RelationKindCode { get; init; } = string.Empty;

    public string RelationKindName { get; init; } = string.Empty;

    public string TargetUseCaseCode { get; init; } = string.Empty;

    public string Condition { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class WorkflowProcessManagerDto
{
    public string ProcessManagerCode { get; init; } = string.Empty;

    public string ContractCode { get; init; } = string.Empty;

    public string FeatureKey { get; init; } = string.Empty;

    public string Responsibility { get; init; } = string.Empty;

    public string Boundary { get; init; } = string.Empty;

    public int FlowOrder { get; init; }

    public IReadOnlyList<string> EffectCodes { get; init; } = [];
}

public sealed class WorkflowApiEndpointDto
{
    public string EndpointKey { get; init; } = string.Empty;

    public string ControllerName { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string RoutePattern { get; init; } = string.Empty;

    public string ProductVersionCode { get; init; } = string.Empty;

    public string ProductVersionName { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string ProductVersionDisplayName { get; init; } = string.Empty;

    public string FeatureKey { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<string> CapabilityCodes { get; init; } = [];

    public IReadOnlyList<string> CapabilityNames { get; init; } = [];

    public IReadOnlyList<string> AudienceCodes { get; init; } = [];

    public IReadOnlyList<string> AudienceNames { get; init; } = [];

    public IReadOnlyList<string> OperationCodes { get; init; } = [];

    public IReadOnlyList<string> OperationNames { get; init; } = [];

    public IReadOnlyList<string> WorkflowCodes { get; init; } = [];

    public IReadOnlyList<string> WorkflowNames { get; init; } = [];

    public IReadOnlyList<string> GrowthTrackCodes { get; init; } = [];

    public IReadOnlyList<string> GrowthTrackNames { get; init; } = [];

    public string AuthorizationPolicy { get; init; } = string.Empty;

    public string AuthorizationRoles { get; init; } = string.Empty;

    public bool AllowsAnonymous { get; init; }
}

public sealed class WorkflowRelationDto
{
    public string SourceWorkflowCode { get; init; } = string.Empty;

    public string SourceWorkflowName { get; init; } = string.Empty;

    public string TargetWorkflowCode { get; init; } = string.Empty;

    public string TargetWorkflowName { get; init; } = string.Empty;

    public string RelationKindCode { get; init; } = string.Empty;

    public string RelationKindName { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class OperatingSystemDto
{
    public string OperatingSystemCode { get; init; } = string.Empty;

    public string CanonicalOperatingSystemId { get; init; } = string.Empty;

    public IReadOnlyList<string> OperatingSystemAliases { get; init; } = [];

    public string OperatingSystemName { get; init; } = string.Empty;

    public string Purpose { get; init; } = string.Empty;

    public string FeatureKey { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

    public IReadOnlyList<OperatingSystemLifecycleStageDto> LifecycleStages { get; init; } = [];

    public IReadOnlyList<OperatingSystemWorkflowDto> Workflows { get; init; } = [];

    public IReadOnlyList<OperatingSystemEngineDto> Engines { get; init; } = [];

    public IReadOnlyList<OperatingSystemSchedulingPolicyDto> SchedulingPolicies { get; init; } = [];
}

public sealed class OperatingSystemCurrentStructureDto
{
    public string CatalogRevision { get; init; } = string.Empty;

    public string OperatingSystemId { get; init; } = string.Empty;

    public bool HasLifecycle { get; init; }

    public int DefinedStageCount { get; init; }

    public IReadOnlyList<string> OrderSegments { get; init; } = [];

    public string Status { get; init; } = string.Empty;
}

public sealed class OperatingSystemInteractionDto
{
    public string CatalogRevision { get; init; } = string.Empty;

    public string InteractionId { get; init; } = string.Empty;

    public string ContractCode { get; init; } = string.Empty;

    public string ContractRevision { get; init; } = string.Empty;

    public string SourceOperatingSystemId { get; init; } = string.Empty;

    public string SourceLifecycleStageId { get; init; } = string.Empty;

    public string TargetOperatingSystemId { get; init; } = string.Empty;

    public string TargetLifecycleStageId { get; init; } = string.Empty;

    public string Mode { get; init; } = string.Empty;

    public string Cardinality { get; init; } = string.Empty;

    public string LifecycleBindingStatus { get; init; } = string.Empty;

    public string ReturnInteractionId { get; init; } = string.Empty;

    public string Responsibility { get; init; } = string.Empty;
}

public sealed class OperatingSystemWorkflowDto
{
    public string WorkflowCode { get; init; } = string.Empty;

    public string WorkflowName { get; init; } = string.Empty;
}

public sealed class OperatingSystemEngineDto
{
    public string EngineCode { get; init; } = string.Empty;

    public string EngineFamilyId { get; init; } = string.Empty;

    public IReadOnlyList<string> ImplementationIds { get; init; } = [];

    public IReadOnlyList<OperatingSystemEngineCatalogEntryDto> CatalogEntries { get; init; } = [];

    public string RuntimeStatus { get; init; } = RuntimeCapabilityStatuses.Declared;

    public string EngineName { get; init; } = string.Empty;

    public string AdjustmentPolicy { get; init; } = string.Empty;
}

public sealed class OperatingSystemLifecycleStageDto
{
    public string StageId { get; init; } = string.Empty;

    public int Sequence { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Responsibility { get; init; } = string.Empty;
}

public sealed class OperatingSystemEngineCatalogEntryDto
{
    public string CatalogRevision { get; init; } = string.Empty;

    public string ImplementationId { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public string ActivationStatus { get; init; } = string.Empty;

    public string InputContractRevision { get; init; } = string.Empty;

    public string ResultContractRevision { get; init; } = string.Empty;

    public string PolicyRevision { get; init; } = string.Empty;

    public string FallbackForImplementationId { get; init; } = string.Empty;
}

public sealed class OperatingSystemSchedulingPolicyDto
{
    public string RuntimeStatus { get; init; } = RuntimeCapabilityStatuses.Declared;

    public string PolicyKindCode { get; init; } = string.Empty;

    public string PolicyKindName { get; init; } = string.Empty;

    public string PolicyCode { get; init; } = string.Empty;

    public string PolicyName { get; init; } = string.Empty;

    public string TargetQueue { get; init; } = string.Empty;

    public string AppliedEngineCode { get; init; } = string.Empty;

    public string Rule { get; init; } = string.Empty;

    public string StarvationGuard { get; init; } = string.Empty;
}
