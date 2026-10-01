namespace Hongdal.Contracts.Common.Versioning;

public sealed class VersionFeatureFlagsResponse
{
    public IReadOnlyDictionary<string, bool> Flags { get; init; } = new Dictionary<string, bool>();

    public IReadOnlyList<WorkflowFlagStateDto> Workflows { get; init; } = [];

    public IReadOnlyList<WorkflowRelationDto> WorkflowRelations { get; init; } = [];

    public IReadOnlyList<OperatingSystemDto> OperatingSystems { get; init; } = [];

    public IReadOnlyList<WorkflowApiEndpointDto> ApiEndpoints { get; init; } = [];
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

public sealed class WorkflowApiEndpointDto
{
    public string EndpointKey { get; init; } = string.Empty;

    public string ControllerName { get; init; } = string.Empty;

    public string ActionName { get; init; } = string.Empty;

    public string Method { get; init; } = string.Empty;

    public string RoutePattern { get; init; } = string.Empty;

    public string ProductVersionCode { get; init; } = string.Empty;

    public string ProductVersionName { get; init; } = string.Empty;

    public string FeatureKey { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }

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

    public IReadOnlyList<OperatingSystemWorkflowDto> Workflows { get; init; } = [];

    public IReadOnlyList<OperatingSystemEngineDto> Engines { get; init; } = [];

    public IReadOnlyList<OperatingSystemSchedulingPolicyDto> SchedulingPolicies { get; init; } = [];
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

    public string RuntimeStatus { get; init; } = RuntimeCapabilityStatuses.Declared;

    public string EngineName { get; init; } = string.Empty;

    public string AdjustmentPolicy { get; init; } = string.Empty;
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
