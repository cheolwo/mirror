using System.Reflection;
using Ssalddel.ApiMetadata;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Contracts.Common.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using 살뜰.Services.Versioning;

namespace Ssalddel.Application.Versioning;

public interface I버전워크플로우UseCase
{
    VersionFeatureFlagsResponse 조회();
}

[SsalddelApiWorkflow(SsalddelWorkflow.DomesticTransport)]
[SsalddelUseCase("버전 워크플로우 조회", Summary = "버전 플래그, 워크플로우 관계, 참여자, 화면, 유스케이스 메타데이터를 조회합니다.")]
[SsalddelUseCaseActor(SsalddelActor.PlatformOperator)]
public sealed class 버전워크플로우UseCase : I버전워크플로우UseCase
{
    private readonly IVersionFeatureFlagService _featureFlagService;

    public 버전워크플로우UseCase(IVersionFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public VersionFeatureFlagsResponse 조회()
    {
        var flags = _featureFlagService.GetAll();
        return new VersionFeatureFlagsResponse
        {
            Flags = flags,
            Workflows = BuildWorkflowStates(flags),
            WorkflowRelations = SsalddelWorkflowRelations.GetAll().Select(ToDto).ToArray(),
            OperatingSystems = SsalddelOperatingSystems.GetAll().Select(item => ToDto(item, flags)).ToArray(),
            OperatingSystemCurrentStructures = OperatingSystemInteractionCatalog.GetCurrentStructure()
                .Select(ToOperatingSystemCurrentStructureDto)
                .ToArray(),
            OperatingSystemInteractions = OperatingSystemInteractionCatalog.GetAll()
                .Select(ToOperatingSystemInteractionDto)
                .ToArray(),
            ApiEndpoints = BuildApiEndpoints(flags),
            PageCapabilities = BuildPageCapabilities(flags)
        };
    }

    private static IReadOnlyList<PageCapabilityDto> BuildPageCapabilities(
        IReadOnlyDictionary<string, bool> flags)
    {
        return SsalddelPageCapabilityCatalog.GetAll()
            .Select(capability => new PageCapabilityDto
            {
                PageKey = capability.PageKey,
                AppCode = capability.AppCode,
                RoutePattern = capability.RoutePattern,
                MatchKindCode = capability.MatchKind.ToString(),
                StageCode = capability.Stage.ToString(),
                StageName = PageCapabilityLabels.StageName(capability.Stage),
                BoundaryCode = capability.Boundary.ToString(),
                BoundaryName = PageCapabilityLabels.BoundaryName(capability.Boundary),
                RequiresAuthentication = capability.RequiresAuthentication,
                HasExternalEffects = capability.HasExternalEffects,
                IntroducedVersion = capability.IntroducedVersion,
                FeatureKeys = capability.FeatureKeys,
                IsFeatureEnabled = capability.FeatureKeys.Count == 0
                    || capability.FeatureKeys.Any(featureKey =>
                        flags.TryGetValue(featureKey, out var enabled) && enabled),
                WorkflowCodes = capability.WorkflowCodes,
                Notice = capability.Notice
            })
            .OrderBy(capability => capability.AppCode, StringComparer.Ordinal)
            .ThenBy(capability => capability.RoutePattern, StringComparer.Ordinal)
            .ThenBy(capability => capability.PageKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<WorkflowFlagStateDto> BuildWorkflowStates(IReadOnlyDictionary<string, bool> flags)
    {
        return
        [
            ToWorkflowState(SsalddelWorkflow.DomesticTransport, VersionFeatureFlagKeys.DomesticTransportWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.WarehouseFulfillment, VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.CustomsAndTradeData, VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.GroupPurchaseDemand, VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.GroupPurchaseImport, VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.SalesChannelFulfillment, VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.CommunityTrust, VersionFeatureFlagKeys.CommunityTrustWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.HrParticipation, VersionFeatureFlagKeys.HrParticipationWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.FoodDelivery, VersionFeatureFlagKeys.FoodDeliveryWorkflow, flags),
            ToWorkflowState(SsalddelWorkflow.SsalddelMart, VersionFeatureFlagKeys.SsalddelMartWorkflow, flags)
        ];
    }

    private static WorkflowFlagStateDto ToWorkflowState(
        SsalddelWorkflow workflow,
        string flagKey,
        IReadOnlyDictionary<string, bool> flags)
    {
        return new WorkflowFlagStateDto
        {
            WorkflowCode = workflow.ToString(),
            WorkflowName = SsalddelWorkflowLabels.GetLabel(workflow),
            FlagKey = flagKey,
            IsEnabled = flags.TryGetValue(flagKey, out var enabled) && enabled,
            BoundarySummary = SsalddelWorkflowParticipants.GetBoundarySummary(workflow),
            Participants = SsalddelWorkflowParticipants.GetByWorkflow(workflow).Select(ToParticipantDto).ToArray(),
            Screens = SsalddelWorkflowScreens.GetByWorkflow(workflow).Select(ToScreenDto).ToArray(),
            UseCases = BuildUseCases(workflow),
            ProcessManagers = BuildProcessManagers(workflow)
        };
    }

    private static IReadOnlyList<WorkflowProcessManagerDto> BuildProcessManagers(SsalddelWorkflow workflow)
    {
        return typeof(버전워크플로우UseCase).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Name.EndsWith("ProcessManager", StringComparison.Ordinal))
            .Where(type => type
                .GetCustomAttributes<SsalddelApiWorkflowAttribute>(inherit: true)
                .Any(attribute => attribute.Workflow == workflow))
            .SelectMany(type => SsalddelCodeMetadataReader.Read(type))
            .Select(ToProcessManagerDto)
            .OrderBy(processManager => processManager.FlowOrder)
            .ThenBy(processManager => processManager.ProcessManagerCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static WorkflowProcessManagerDto ToProcessManagerDto(
        SsalddelCodeMetadataDescriptor metadata)
    {
        return new WorkflowProcessManagerDto
        {
            ProcessManagerCode = metadata.ComponentType.Name,
            ContractCode = metadata.ContractType?.Name ?? string.Empty,
            FeatureKey = metadata.FeatureKey,
            Responsibility = metadata.Responsibility,
            Boundary = metadata.Boundary,
            FlowOrder = metadata.FlowOrder,
            EffectCodes = Enum.GetValues<SsalddelCodeEffect>()
                .Where(effect => effect != SsalddelCodeEffect.None && metadata.Effects.HasFlag(effect))
                .Select(effect => effect.ToString())
                .ToArray()
        };
    }

    private static IReadOnlyList<WorkflowUseCaseDto> BuildUseCases(SsalddelWorkflow workflow)
    {
        return typeof(버전워크플로우UseCase).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => type.Name.EndsWith("UseCase", StringComparison.Ordinal))
            .Where(type => type
                .GetCustomAttributes<SsalddelApiWorkflowAttribute>(inherit: true)
                .Any(attribute => attribute.Workflow == workflow))
            .Select(ToUseCaseDto)
            .OrderBy(useCase => useCase.UseCaseCode, StringComparer.Ordinal)
            .ToArray();
    }

    private static WorkflowUseCaseDto ToUseCaseDto(Type useCaseType)
    {
        var useCase = useCaseType.GetCustomAttribute<SsalddelUseCaseAttribute>(inherit: true);
        var actors = useCaseType
            .GetCustomAttributes<SsalddelUseCaseActorAttribute>(inherit: true)
            .Select(ToUseCaseActorDto)
            .ToArray();
        var relations = useCaseType
            .GetCustomAttributes<SsalddelUseCaseRelationAttribute>(inherit: true)
            .Select(ToUseCaseRelationDto)
            .ToArray();

        return new WorkflowUseCaseDto
        {
            UseCaseCode = useCaseType.Name,
            UseCaseName = useCase?.Name ?? useCaseType.Name,
            Summary = useCase?.Summary ?? string.Empty,
            IsRequired = useCase?.IsRequired ?? true,
            PrimaryActors = actors
                .Where(actor => actor.RoleCode == SsalddelUseCaseActorRole.Primary.ToString())
                .ToArray(),
            SupportingActors = actors
                .Where(actor => actor.RoleCode == SsalddelUseCaseActorRole.Supporting.ToString())
                .ToArray(),
            Relations = relations
        };
    }

    private static WorkflowUseCaseActorDto ToUseCaseActorDto(SsalddelUseCaseActorAttribute actor)
    {
        return new WorkflowUseCaseActorDto
        {
            ActorCode = actor.ActorCode,
            ActorName = actor.ActorLabel,
            RoleCode = actor.Role.ToString(),
            RoleName = actor.RoleLabel
        };
    }

    private static WorkflowUseCaseRelationDto ToUseCaseRelationDto(SsalddelUseCaseRelationAttribute relation)
    {
        return new WorkflowUseCaseRelationDto
        {
            RelationKindCode = relation.Kind.ToString(),
            RelationKindName = relation.KindLabel,
            TargetUseCaseCode = relation.TargetUseCaseCode,
            Condition = relation.Condition,
            Summary = relation.Summary
        };
    }

    private static IReadOnlyList<WorkflowApiEndpointDto> BuildApiEndpoints(
        IReadOnlyDictionary<string, bool> flags)
    {
        return typeof(버전워크플로우UseCase).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .SelectMany(type => BuildControllerEndpoints(type, flags))
            .OrderBy(endpoint => endpoint.RoutePattern, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.Method, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<WorkflowApiEndpointDto> BuildControllerEndpoints(
        Type controllerType,
        IReadOnlyDictionary<string, bool> flags)
    {
        var controllerRoute = controllerType.GetCustomAttribute<RouteAttribute>(inherit: true)?.Template ?? string.Empty;
        var controllerVersion = controllerType.GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true);
        var controllerFeature = controllerType.GetCustomAttribute<SsalddelApiFeatureAttribute>(inherit: true);
        var controllerCapabilities = controllerType.GetCustomAttributes<SsalddelApiCapabilityAttribute>(inherit: true).ToArray();
        var controllerAudiences = controllerType.GetCustomAttributes<SsalddelApiAudienceAttribute>(inherit: true).ToArray();
        var controllerOperations = controllerType.GetCustomAttributes<SsalddelApiOperationAttribute>(inherit: true).ToArray();
        var controllerClassification = SsalddelApiClassificationReader.Read(controllerType);
        var controllerWorkflows = controllerType.GetCustomAttributes<SsalddelApiWorkflowAttribute>(inherit: true).ToArray();
        var controllerGrowthTracks = controllerType.GetCustomAttributes<SsalddelApiGrowthTrackAttribute>(inherit: true).ToArray();
        var controllerAuthorize = controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToArray();
        var controllerAllowsAnonymous = controllerType.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();
        var controllerContractName =
            controllerType.GetCustomAttribute<SsalddelApiContractNameAttribute>(inherit: true)?.Name
            ?? controllerType.Name;

        foreach (var action in GetActionMethods(controllerType))
        {
            var actionContractName =
                action.GetCustomAttribute<SsalddelApiContractNameAttribute>(inherit: true)?.Name
                ?? action.Name;
            var declaredActionVersion = action.GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true);
            var actionVersion = declaredActionVersion ?? controllerVersion;
            var declaredActionFeature = action.GetCustomAttribute<SsalddelApiFeatureAttribute>(inherit: true);
            var featureKey = !string.IsNullOrWhiteSpace(declaredActionFeature?.FeatureKey)
                ? declaredActionFeature.FeatureKey
                : !string.IsNullOrWhiteSpace(controllerFeature?.FeatureKey)
                    ? controllerFeature.FeatureKey
                    : !string.IsNullOrWhiteSpace(declaredActionVersion?.FeatureKey)
                ? declaredActionVersion.FeatureKey
                : controllerVersion?.FeatureKey;
            var actionCapabilities = ResolveActionMetadata(
                action.GetCustomAttributes<SsalddelApiCapabilityAttribute>(inherit: true),
                controllerCapabilities);
            var actionAudiences = ResolveActionMetadata(
                action.GetCustomAttributes<SsalddelApiAudienceAttribute>(inherit: true),
                controllerAudiences);
            var actionOperations = ResolveActionMetadata(
                action.GetCustomAttributes<SsalddelApiOperationAttribute>(inherit: true),
                controllerOperations);
            var actionWorkflows = action.GetCustomAttributes<SsalddelApiWorkflowAttribute>(inherit: true).DefaultIfEmpty().Where(x => x is not null).Cast<SsalddelApiWorkflowAttribute>().ToArray();
            if (actionWorkflows.Length == 0)
            {
                actionWorkflows = controllerWorkflows;
            }

            var actionGrowthTracks = action.GetCustomAttributes<SsalddelApiGrowthTrackAttribute>(inherit: true).DefaultIfEmpty().Where(x => x is not null).Cast<SsalddelApiGrowthTrackAttribute>().ToArray();
            if (actionGrowthTracks.Length == 0)
            {
                actionGrowthTracks = controllerGrowthTracks;
            }

            var authorizeAttributes = controllerAuthorize
                .Concat(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
                .ToArray();
            var allowsAnonymous = controllerAllowsAnonymous ||
                action.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any();

            foreach (var httpAttribute in action.GetCustomAttributes<HttpMethodAttribute>(inherit: true))
            {
                foreach (var method in httpAttribute.HttpMethods)
                {
                    yield return new WorkflowApiEndpointDto
                    {
                        EndpointKey = $"{controllerContractName}.{actionContractName}",
                        ControllerName = controllerContractName,
                        ActionName = actionContractName,
                        Method = method,
                        RoutePattern = CombineRoutes(controllerRoute, httpAttribute.Template),
                        ProductVersionCode = actionVersion?.Version.ToString() ?? string.Empty,
                        ProductVersionName = actionVersion?.VersionLabel ?? string.Empty,
                        ProductName = actionVersion?.ProductName ?? string.Empty,
                        ProductVersionDisplayName = actionVersion?.VersionDisplayName ?? string.Empty,
                        FeatureKey = featureKey ?? string.Empty,
                        IsEnabled = string.IsNullOrWhiteSpace(featureKey)
                            || flags.TryGetValue(featureKey, out var enabled) && enabled,
                        CapabilityCodes = actionCapabilities.Length == 0
                            ? controllerClassification.CapabilityCodes
                            : actionCapabilities.Select(attribute => attribute.Capability.ToString()).Distinct(StringComparer.Ordinal).ToArray(),
                        CapabilityNames = actionCapabilities.Length == 0
                            ? controllerClassification.Capabilities
                            : actionCapabilities.Select(attribute => attribute.CapabilityLabel).Distinct(StringComparer.Ordinal).ToArray(),
                        AudienceCodes = actionAudiences.Length == 0
                            ? controllerClassification.AudienceCodes
                            : actionAudiences.Select(attribute => attribute.Actor.ToString()).Distinct(StringComparer.Ordinal).ToArray(),
                        AudienceNames = actionAudiences.Length == 0
                            ? controllerClassification.Audiences
                            : actionAudiences.Select(attribute => attribute.ActorLabel).Distinct(StringComparer.Ordinal).ToArray(),
                        OperationCodes = actionOperations.Length == 0
                            ? controllerClassification.OperationCodes
                            : actionOperations.Select(attribute => attribute.Operation.ToString()).Distinct(StringComparer.Ordinal).ToArray(),
                        OperationNames = actionOperations.Length == 0
                            ? controllerClassification.Operations
                            : actionOperations.Select(attribute => attribute.OperationLabel).Distinct(StringComparer.Ordinal).ToArray(),
                        WorkflowCodes = actionWorkflows.Select(attribute => attribute.Workflow.ToString()).Distinct(StringComparer.Ordinal).ToArray(),
                        WorkflowNames = actionWorkflows.Select(attribute => attribute.WorkflowLabel).Distinct(StringComparer.Ordinal).ToArray(),
                        GrowthTrackCodes = actionGrowthTracks.Select(attribute => attribute.Track.ToString()).Distinct(StringComparer.Ordinal).ToArray(),
                        GrowthTrackNames = actionGrowthTracks.Select(attribute => attribute.TrackLabel).Distinct(StringComparer.Ordinal).ToArray(),
                        AuthorizationPolicy = string.Join(", ", authorizeAttributes.Select(x => x.Policy).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal)),
                        AuthorizationRoles = string.Join(", ", authorizeAttributes.Select(x => x.Roles).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal)),
                        AllowsAnonymous = allowsAnonymous
                    };
                }
            }
        }
    }

    private static TAttribute[] ResolveActionMetadata<TAttribute>(
        IEnumerable<TAttribute> actionAttributes,
        IReadOnlyList<TAttribute> controllerAttributes)
        where TAttribute : Attribute
    {
        var declared = actionAttributes.ToArray();
        return declared.Length == 0 ? controllerAttributes.ToArray() : declared;
    }

    private static IEnumerable<MethodInfo> GetActionMethods(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any());
    }

    private static string CombineRoutes(string controllerRoute, string? actionRoute)
    {
        var left = (controllerRoute ?? string.Empty).Trim('/');
        var right = (actionRoute ?? string.Empty).Trim('/');

        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        return string.IsNullOrWhiteSpace(right) ? left : $"{left}/{right}";
    }

    private static WorkflowParticipantDto ToParticipantDto(SsalddelWorkflowParticipant participant)
    {
        return new WorkflowParticipantDto
        {
            ActorCode = participant.ActorCode,
            ActorName = participant.ActorName,
            IsPrimary = participant.IsPrimary,
            Responsibility = participant.Responsibility
        };
    }

    private static WorkflowScreenDto ToScreenDto(SsalddelWorkflowScreen screen)
    {
        return new WorkflowScreenDto
        {
            ActorCode = screen.ActorCode,
            AppCode = screen.AppCode,
            AppName = screen.AppName,
            ScreenName = screen.ScreenName,
            Route = screen.Route,
            Purpose = screen.Purpose
        };
    }

    private static WorkflowRelationDto ToDto(SsalddelWorkflowRelation relation)
    {
        return new WorkflowRelationDto
        {
            SourceWorkflowCode = relation.Source.ToString(),
            SourceWorkflowName = SsalddelWorkflowLabels.GetLabel(relation.Source),
            TargetWorkflowCode = relation.Target.ToString(),
            TargetWorkflowName = SsalddelWorkflowLabels.GetLabel(relation.Target),
            RelationKindCode = relation.Kind.ToString(),
            RelationKindName = SsalddelWorkflowRelationKindLabels.GetLabel(relation.Kind),
            Summary = relation.Summary
        };
    }

    private static OperatingSystemDto ToDto(
        SsalddelOperatingSystemDefinition operatingSystem,
        IReadOnlyDictionary<string, bool> flags)
    {
        var canonicalId = SsalddelOperatingSystems.GetCanonicalId(operatingSystem.OperatingSystem);
        var featureKey = GetOperatingSystemFeatureKey(operatingSystem.OperatingSystem);
        return new OperatingSystemDto
        {
            OperatingSystemCode = operatingSystem.OperatingSystem.ToString(),
            CanonicalOperatingSystemId = canonicalId,
            OperatingSystemAliases = OperatingSystemIds.GetAliases(canonicalId),
            OperatingSystemName = operatingSystem.Name,
            Purpose = operatingSystem.Purpose,
            FeatureKey = featureKey ?? string.Empty,
            IsEnabled = featureKey is null || flags.TryGetValue(featureKey, out var enabled) && enabled,
            LifecycleStages = OperatingSystemLifecycleCatalog.TryGet(canonicalId, out var lifecycle)
                ? lifecycle.Stages.Select(ToOperatingSystemLifecycleStageDto).ToArray()
                : [],
            Workflows = operatingSystem.Workflows.Select(ToOperatingSystemWorkflowDto).ToArray(),
            Engines = operatingSystem.Engines
                .Select(engine => ToOperatingSystemEngineDto(canonicalId, engine))
                .ToArray(),
            SchedulingPolicies = operatingSystem.SchedulingPolicies.Select(ToOperatingSystemSchedulingPolicyDto).ToArray()
        };
    }

    private static string? GetOperatingSystemFeatureKey(SsalddelOperatingSystem operatingSystem)
        => operatingSystem switch
        {
            SsalddelOperatingSystem.DomesticCargoTransport => VersionFeatureFlagKeys.DomesticTransportWorkflow,
            SsalddelOperatingSystem.WarehouseCommerceFulfillment => VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow,
            SsalddelOperatingSystem.GroupPurchaseDemand => VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow,
            SsalddelOperatingSystem.GroupPurchaseImport => VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow,
            SsalddelOperatingSystem.FoodDelivery => VersionFeatureFlagKeys.FoodDeliveryWorkflow,
            SsalddelOperatingSystem.SsalddelMartUrbanLogistics => VersionFeatureFlagKeys.SsalddelMartWorkflow,
            SsalddelOperatingSystem.CommunityTrust => VersionFeatureFlagKeys.CommunityTrustWorkflow,
            SsalddelOperatingSystem.PlatformOperations => null,
            SsalddelOperatingSystem.ShipperTransportManagement => VersionFeatureFlagKeys.DomesticTransportWorkflow,
            _ => throw new ArgumentOutOfRangeException(nameof(operatingSystem), operatingSystem, "Unknown Ssalddel operating system.")
        };

    private static OperatingSystemWorkflowDto ToOperatingSystemWorkflowDto(SsalddelWorkflow workflow)
    {
        return new OperatingSystemWorkflowDto
        {
            WorkflowCode = workflow.ToString(),
            WorkflowName = SsalddelWorkflowLabels.GetLabel(workflow)
        };
    }

    private static OperatingSystemLifecycleStageDto ToOperatingSystemLifecycleStageDto(
        OperatingSystemLifecycleStageDefinition stage)
        => new()
        {
            StageId = stage.StageId,
            Sequence = stage.Sequence,
            Name = stage.Name,
            Responsibility = stage.Responsibility
        };

    private static OperatingSystemCurrentStructureDto ToOperatingSystemCurrentStructureDto(
        OperatingSystemCurrentStructureDefinition definition)
        => new()
        {
            CatalogRevision = OperatingSystemInteractionCatalog.CatalogRevision,
            OperatingSystemId = definition.OperatingSystemId,
            HasLifecycle = definition.HasLifecycle,
            DefinedStageCount = definition.DefinedStageCount,
            OrderSegments = definition.OrderSegments,
            Status = definition.Status
        };

    private static OperatingSystemInteractionDto ToOperatingSystemInteractionDto(
        OperatingSystemInteractionDefinition definition)
        => new()
        {
            CatalogRevision = OperatingSystemInteractionCatalog.CatalogRevision,
            InteractionId = definition.InteractionId,
            ContractCode = definition.ContractCode,
            ContractRevision = definition.ContractRevision,
            SourceOperatingSystemId = definition.SourceOperatingSystemId,
            SourceLifecycleStageId = definition.SourceLifecycleStageId ?? string.Empty,
            TargetOperatingSystemId = definition.TargetOperatingSystemId,
            TargetLifecycleStageId = definition.TargetLifecycleStageId ?? string.Empty,
            Mode = definition.Mode,
            Cardinality = definition.Cardinality,
            LifecycleBindingStatus = definition.LifecycleBindingStatus,
            ReturnInteractionId = definition.ReturnInteractionId ?? string.Empty,
            Responsibility = definition.Responsibility
        };

    private static OperatingSystemEngineDto ToOperatingSystemEngineDto(
        string operatingSystemId,
        SsalddelOperatingSystemEngine engine)
    {
        var catalogEntries = OperatingSystemEngineCatalog
            .GetByOperatingSystemAndFamily(operatingSystemId, engine.EngineCode);
        var implementationIds = catalogEntries
            .Select(binding => binding.ImplementationId)
            .ToArray();

        return new OperatingSystemEngineDto
        {
            EngineCode = engine.EngineCode,
            EngineFamilyId = engine.EngineCode,
            ImplementationIds = implementationIds,
            CatalogEntries = catalogEntries.Select(ToOperatingSystemEngineCatalogEntryDto).ToArray(),
            RuntimeStatus = catalogEntries.Any(entry =>
                entry.ActivationStatus == OperatingSystemEngineActivationStatuses.Active)
                ? RuntimeCapabilityStatuses.Active
                : RuntimeCapabilityStatuses.Declared,
            EngineName = engine.EngineName,
            AdjustmentPolicy = engine.AdjustmentPolicy
        };
    }

    private static OperatingSystemEngineCatalogEntryDto ToOperatingSystemEngineCatalogEntryDto(
        OperatingSystemEngineCatalogEntry entry)
        => new()
        {
            CatalogRevision = OperatingSystemEngineCatalog.CatalogRevision,
            ImplementationId = entry.ImplementationId,
            Role = entry.Role,
            ActivationStatus = entry.ActivationStatus,
            InputContractRevision = entry.InputContractRevision,
            ResultContractRevision = entry.ResultContractRevision,
            PolicyRevision = entry.PolicyRevision,
            FallbackForImplementationId = entry.FallbackForImplementationId ?? string.Empty
        };

    private static OperatingSystemSchedulingPolicyDto ToOperatingSystemSchedulingPolicyDto(SsalddelSchedulingPolicy policy)
    {
        return new OperatingSystemSchedulingPolicyDto
        {
            RuntimeStatus = SchedulingPolicyImplementationCatalog.IsActive(policy.PolicyCode)
                ? RuntimeCapabilityStatuses.Active
                : RuntimeCapabilityStatuses.Declared,
            PolicyKindCode = policy.Kind.ToString(),
            PolicyKindName = SsalddelSchedulingPolicyKindLabels.GetLabel(policy.Kind),
            PolicyCode = policy.PolicyCode,
            PolicyName = policy.PolicyName,
            TargetQueue = policy.TargetQueue,
            AppliedEngineCode = policy.AppliedEngineCode,
            Rule = policy.Rule,
            StarvationGuard = policy.StarvationGuard
        };
    }
}
