using System.Reflection;
using Hongdal.Application.Driver.Recommendation;
using Hongdal.Application.Sales;
using Hongdal.Application.Shipper.Request;
using Hongdal.Application.Versioning;
using Hongdal.Application.Warehouse;
using Hongdal.ApiMetadata;
using Hongdal.Controllers.Admin.HumanResources;
using Hongdal.Controllers.Admin.Orderer;
using Hongdal.Controllers.Common;
using Hongdal.Controllers.Orderer;
using Hongdal.Contracts.Common.Versioning;
using Hongdal.Filters;
using Hongdal.Services.Community;
using Hongdal.Services.Orderer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using 홍달.Services.Versioning;

namespace Hongdal.Tests.ApiMetadata;

public sealed class HongdalApiVersionAttributeTests
{
    [Theory]
    [InlineData(HongdalProductVersion.V0_0, "0.0")]
    [InlineData(HongdalProductVersion.V1_0, "1.0")]
    [InlineData(HongdalProductVersion.V1_5, "1.5")]
    [InlineData(HongdalProductVersion.V2_0, "2.0")]
    [InlineData(HongdalProductVersion.V2_5, "2.5")]
    [InlineData(HongdalProductVersion.V3_0, "3.0")]
    [InlineData(HongdalProductVersion.V3_5, "3.5")]
    public void GetLabel_ReturnsStableProductVersionLabel(HongdalProductVersion version, string expected)
    {
        Assert.Equal(expected, HongdalProductVersionLabels.GetLabel(version));
    }

    [Theory]
    [InlineData(HongdalApiGrowthTrack.Community, "Community")]
    [InlineData(HongdalApiGrowthTrack.OrdererGroupCommerce, "Orderer Group Commerce")]
    public void GetLabel_ReturnsStableGrowthTrackLabel(HongdalApiGrowthTrack track, string expected)
    {
        Assert.Equal(expected, HongdalApiGrowthTrackLabels.GetLabel(track));
    }

    [Theory]
    [InlineData(HongdalWorkflow.DomesticTransport, "국내 화물 운송")]
    [InlineData(HongdalWorkflow.GroupPurchaseImport, "공동주문 수입")]
    [InlineData(HongdalWorkflow.WarehouseFulfillment, "창고 입출고")]
    [InlineData(HongdalWorkflow.SalesChannelFulfillment, "판매채널 출고")]
    [InlineData(HongdalWorkflow.HrParticipation, "참여 인력 관리")]
    public void GetLabel_ReturnsStableWorkflowLabel(HongdalWorkflow workflow, string expected)
    {
        Assert.Equal(expected, HongdalWorkflowLabels.GetLabel(workflow));
    }

    [Theory]
    [InlineData(HongdalWorkflowRelationKind.References, "참조")]
    [InlineData(HongdalWorkflowRelationKind.Calls, "호출")]
    [InlineData(HongdalWorkflowRelationKind.HandsOffTo, "인계")]
    [InlineData(HongdalWorkflowRelationKind.Feeds, "공급")]
    [InlineData(HongdalWorkflowRelationKind.PublishesSignalTo, "신호 공개")]
    [InlineData(HongdalWorkflowRelationKind.OperatesWith, "공동 운영")]
    public void GetLabel_ReturnsStableWorkflowRelationKindLabel(HongdalWorkflowRelationKind kind, string expected)
    {
        Assert.Equal(expected, HongdalWorkflowRelationKindLabels.GetLabel(kind));
    }

    [Theory]
    [InlineData(HongdalActor.Shipper, "화주")]
    [InlineData(HongdalActor.Driver, "기사")]
    [InlineData(HongdalActor.OrdererGroupLeader, "주문자 집단 대표")]
    [InlineData(HongdalActor.WarehouseManager, "창고 관리자")]
    [InlineData(HongdalActor.PlatformOperator, "플랫폼 운영자")]
    public void GetLabel_ReturnsStableActorLabel(HongdalActor actor, string expected)
    {
        Assert.Equal(expected, HongdalActorLabels.GetLabel(actor));
    }

    [Theory]
    [InlineData(HongdalUseCaseActorRole.Primary, "주 액터")]
    [InlineData(HongdalUseCaseActorRole.Supporting, "보조 액터")]
    public void GetLabel_ReturnsStableUseCaseActorRoleLabel(HongdalUseCaseActorRole role, string expected)
    {
        Assert.Equal(expected, HongdalUseCaseActorRoleLabels.GetLabel(role));
    }

    [Theory]
    [InlineData(HongdalUseCaseRelationKind.Include, "포함")]
    [InlineData(HongdalUseCaseRelationKind.Extend, "확장")]
    public void GetLabel_ReturnsStableUseCaseRelationKindLabel(HongdalUseCaseRelationKind kind, string expected)
    {
        Assert.Equal(expected, HongdalUseCaseRelationKindLabels.GetLabel(kind));
    }

    [Fact]
    public void WorkflowRelations_RecordCrossWorkflowIntegrationPoints()
    {
        var relations = HongdalWorkflowRelations.GetAll();

        Assert.Contains(relations, relation =>
            relation.Source == HongdalWorkflow.GroupPurchaseImport &&
            relation.Target == HongdalWorkflow.DomesticTransport &&
            relation.Kind == HongdalWorkflowRelationKind.HandsOffTo);
        Assert.Contains(relations, relation =>
            relation.Source == HongdalWorkflow.GroupPurchaseImport &&
            relation.Target == HongdalWorkflow.CustomsAndTradeData &&
            relation.Kind == HongdalWorkflowRelationKind.References);
        Assert.Contains(relations, relation =>
            relation.Source == HongdalWorkflow.SalesChannelFulfillment &&
            relation.Target == HongdalWorkflow.WarehouseFulfillment &&
            relation.Kind == HongdalWorkflowRelationKind.Calls);
        Assert.Contains(relations, relation =>
            relation.Source == HongdalWorkflow.DomesticTransport &&
            relation.Target == HongdalWorkflow.CommunityTrust &&
            relation.Kind == HongdalWorkflowRelationKind.PublishesSignalTo);
        Assert.All(relations, relation => Assert.False(string.IsNullOrWhiteSpace(relation.Summary)));
    }

    [Fact]
    public void WorkflowRelations_CanBeQueriedByIncomingAndOutgoingWorkflow()
    {
        var groupPurchaseOutgoing = HongdalWorkflowRelations.GetOutgoing(HongdalWorkflow.GroupPurchaseImport);
        var domesticTransportIncoming = HongdalWorkflowRelations.GetIncoming(HongdalWorkflow.DomesticTransport);

        Assert.Contains(groupPurchaseOutgoing, relation => relation.Target == HongdalWorkflow.DomesticTransport);
        Assert.Contains(groupPurchaseOutgoing, relation => relation.Target == HongdalWorkflow.SalesChannelFulfillment);
        Assert.Contains(domesticTransportIncoming, relation => relation.Source == HongdalWorkflow.GroupPurchaseImport);
        Assert.Contains(domesticTransportIncoming, relation => relation.Source == HongdalWorkflow.HongdalMart);
    }

    [Fact]
    public void WorkflowParticipants_RecordPrimaryUsersAndBoundaries()
    {
        var groupPurchaseParticipants = HongdalWorkflowParticipants.GetByWorkflow(HongdalWorkflow.GroupPurchaseImport);
        var domesticBoundary = HongdalWorkflowParticipants.GetBoundarySummary(HongdalWorkflow.DomesticTransport);

        Assert.Contains(groupPurchaseParticipants, participant =>
            participant.ActorName == "주문자 집단 대표" &&
            participant.IsPrimary);
        Assert.Contains(groupPurchaseParticipants, participant =>
            participant.ActorName == "주문자" &&
            participant.IsPrimary);
        Assert.Contains(groupPurchaseParticipants, participant =>
            participant.ActorName == "플랫폼 운영자" &&
            !participant.IsPrimary);
        Assert.Contains("상차", domesticBoundary);
        Assert.Contains("정산", domesticBoundary);
    }

    [Fact]
    public void CoreUseCases_RecordPrimaryActorMetadata()
    {
        AssertUseCaseHasPrimaryActor(typeof(화주운송의뢰UseCase), HongdalActor.Shipper);
        AssertUseCaseHasPrimaryActor(typeof(기사배차추천UseCase), HongdalActor.Driver);
        AssertUseCaseHasPrimaryActor(typeof(공동구매자동집단화UseCase), HongdalActor.Orderer);
        AssertUseCaseHasPrimaryActor(typeof(공동구매커머스이행계획UseCase), HongdalActor.OrdererGroupLeader);
        AssertUseCaseHasPrimaryActor(typeof(창고작업UseCase), HongdalActor.WarehouseManager);
        AssertUseCaseHasPrimaryActor(typeof(판매채널UseCase), HongdalActor.Seller);
    }

    [Fact]
    public void CoreUseCases_RecordWorkflowAndDisplayNameMetadata()
    {
        AssertUseCaseHasWorkflow(typeof(화주운송의뢰UseCase), HongdalWorkflow.DomesticTransport);
        AssertUseCaseHasWorkflow(typeof(기사배차추천UseCase), HongdalWorkflow.DomesticTransport);
        AssertUseCaseHasWorkflow(typeof(공동구매자동집단화UseCase), HongdalWorkflow.GroupPurchaseImport);
        AssertUseCaseHasWorkflow(typeof(공동구매커머스이행계획UseCase), HongdalWorkflow.GroupPurchaseImport);
        AssertUseCaseHasWorkflow(typeof(창고작업UseCase), HongdalWorkflow.WarehouseFulfillment);
        AssertUseCaseHasWorkflow(typeof(판매채널UseCase), HongdalWorkflow.SalesChannelFulfillment);
    }

    [Fact]
    public void CoreUseCases_RecordIncludeAndExtendRelations()
    {
        AssertUseCaseHasRelation(typeof(공동구매자동집단화UseCase), HongdalUseCaseRelationKind.Include, "공공데이터조회UseCase");
        AssertUseCaseHasRelation(typeof(화주운송의뢰UseCase), HongdalUseCaseRelationKind.Extend, "문서관리UseCase");
        AssertUseCaseHasRelation(typeof(공동구매커머스이행계획UseCase), HongdalUseCaseRelationKind.Extend, "화주운송의뢰UseCase");
        AssertUseCaseHasRelation(typeof(커뮤니티게시글UseCase), HongdalUseCaseRelationKind.Extend, "커뮤니티투표UseCase");
    }

    [Fact]
    public void WorkflowScreens_RecordAppAndScreenBoundaries()
    {
        var domesticScreens = HongdalWorkflowScreens.GetByWorkflow(HongdalWorkflow.DomesticTransport);
        var groupPurchaseScreens = HongdalWorkflowScreens.GetByWorkflowAndActor(
            HongdalWorkflow.GroupPurchaseImport,
            "Orderer");

        Assert.Contains(domesticScreens, screen =>
            screen.ActorCode == "Shipper" &&
            screen.AppCode == "HongdalApp" &&
            screen.Route == "/shipper/request");
        Assert.Contains(domesticScreens, screen =>
            screen.ActorCode == "Driver" &&
            screen.AppCode == "DriverApp" &&
            screen.Route == "/driver/recommendations");
        Assert.Contains(groupPurchaseScreens, screen =>
            screen.AppCode == "HongdalApp" &&
            screen.Route == "/community/group-import");
    }

    [Fact]
    public void VersionFeatureFlagsController_ReturnsWorkflowStatesAndRelations()
    {
        var controller = new VersionFeatureFlagsController(
            new 버전워크플로우UseCase(new FakeVersionFeatureFlagService()));

        var result = controller.Get();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<Hongdal.Contracts.Common.Versioning.VersionFeatureFlagsResponse>(ok.Value);

        Assert.Contains(response.Workflows, workflow =>
            workflow.WorkflowCode == nameof(HongdalWorkflow.GroupPurchaseImport) &&
            workflow.WorkflowName == "공동주문 수입" &&
            workflow.FlagKey == VersionFeatureFlagKeys.GroupPurchaseImportWorkflow &&
            workflow.Participants.Any(participant => participant.ActorName == "주문자 집단 대표" && participant.IsPrimary) &&
            workflow.Screens.Any(screen => screen.AppCode == "HongdalApp" && screen.Route == "/community/group-import") &&
            workflow.UseCases.Any(useCase => useCase.UseCaseCode == nameof(공동구매자동집단화UseCase) &&
                useCase.PrimaryActors.Any(actor => actor.ActorCode == nameof(HongdalActor.Orderer)) &&
                useCase.Relations.Any(relation =>
                    relation.RelationKindCode == nameof(HongdalUseCaseRelationKind.Include) &&
                    relation.TargetUseCaseCode == "공공데이터조회UseCase")) &&
            !string.IsNullOrWhiteSpace(workflow.BoundarySummary));
        Assert.Contains(response.Workflows, workflow =>
            workflow.WorkflowCode == nameof(HongdalWorkflow.DomesticTransport) &&
            workflow.UseCases.Any(useCase => useCase.UseCaseCode == nameof(화주운송의뢰UseCase) &&
                useCase.Relations.Any(relation =>
                    relation.RelationKindCode == nameof(HongdalUseCaseRelationKind.Extend) &&
                    relation.TargetUseCaseCode == "문서관리UseCase")) &&
            workflow.UseCases.Any(useCase => useCase.UseCaseCode == nameof(기사배차추천UseCase)));
        Assert.Contains(response.WorkflowRelations, relation =>
            relation.SourceWorkflowName == "공동주문 수입" &&
            relation.TargetWorkflowName == "국내 화물 운송" &&
            relation.RelationKindName == "인계");
        Assert.Contains(response.OperatingSystems, operatingSystem =>
            operatingSystem.OperatingSystemCode == nameof(HongdalOperatingSystem.DomesticCargoTransport) &&
            operatingSystem.CanonicalOperatingSystemId == OperatingSystemIds.DomesticCargoTransport &&
            operatingSystem.OperatingSystemAliases.Contains(nameof(HongdalOperatingSystem.DomesticCargoTransport)) &&
            operatingSystem.FeatureKey == VersionFeatureFlagKeys.DomesticTransportWorkflow &&
            operatingSystem.IsEnabled &&
            operatingSystem.Engines.Any(engine =>
                engine.EngineCode == EngineFamilyIds.TransportRequestDispatch &&
                engine.EngineFamilyId == EngineFamilyIds.TransportRequestDispatch &&
                engine.RuntimeStatus == RuntimeCapabilityStatuses.Active &&
                engine.ImplementationIds.Contains(EngineImplementationIds.CargoYongdalDispatch) &&
                engine.ImplementationIds.Contains(EngineImplementationIds.FoodDeliveryDispatch)) &&
            operatingSystem.SchedulingPolicies.Any(policy => policy.PolicyKindCode == nameof(HongdalSchedulingPolicyKind.Mlfq)) &&
            operatingSystem.SchedulingPolicies.Any(policy => policy.PolicyKindCode == nameof(HongdalSchedulingPolicyKind.Aging)) &&
            operatingSystem.SchedulingPolicies.All(policy => policy.RuntimeStatus == RuntimeCapabilityStatuses.Declared));
        Assert.Contains(response.OperatingSystems, operatingSystem =>
            operatingSystem.OperatingSystemCode == nameof(HongdalOperatingSystem.WarehouseCommerceFulfillment) &&
            operatingSystem.CanonicalOperatingSystemId == OperatingSystemIds.WarehouseCommerceFulfillment &&
            !operatingSystem.IsEnabled &&
            operatingSystem.SchedulingPolicies.Any(policy => policy.PolicyKindCode == nameof(HongdalSchedulingPolicyKind.Sjf)) &&
            operatingSystem.SchedulingPolicies.Any(policy => policy.PolicyKindCode == nameof(HongdalSchedulingPolicyKind.Affinity)));
        Assert.Contains(response.OperatingSystems, operatingSystem =>
            operatingSystem.OperatingSystemCode == nameof(HongdalOperatingSystem.PlatformOperations) &&
            operatingSystem.FeatureKey == string.Empty &&
            operatingSystem.IsEnabled &&
            operatingSystem.Engines.All(engine => engine.RuntimeStatus == RuntimeCapabilityStatuses.Declared));
    }

    [Fact]
    public void VersionFeatureFlagsController_ReturnsApiEndpointMetadataFromExistingRoutes()
    {
        var controller = new VersionFeatureFlagsController(
            new 버전워크플로우UseCase(new FakeVersionFeatureFlagService()));

        var result = controller.Get();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<Hongdal.Contracts.Common.Versioning.VersionFeatureFlagsResponse>(ok.Value);

        Assert.Contains(response.ApiEndpoints, endpoint =>
            endpoint.EndpointKey == "화주운송의뢰Controller.의뢰생성" &&
            endpoint.Method == "POST" &&
            endpoint.RoutePattern == "api/v1/shipper/requests" &&
            endpoint.ProductVersionName == "1.0" &&
            endpoint.FeatureKey == VersionFeatureFlagKeys.DomesticTransportWorkflow &&
            endpoint.IsEnabled);
        Assert.Contains(response.ApiEndpoints, endpoint =>
            endpoint.EndpointKey == "기사운송진행Controller.상차완료" &&
            endpoint.Method == "POST" &&
            endpoint.RoutePattern == "api/v1/driver/transports/{id:long}/pickup-complete");
        Assert.Contains(response.ApiEndpoints, endpoint =>
            endpoint.EndpointKey == "커뮤니티게시글Controller.Create" &&
            endpoint.Method == "POST" &&
            endpoint.RoutePattern == "api/v1/community/posts" &&
            endpoint.ProductVersionName == "0.0" &&
            endpoint.FeatureKey == string.Empty &&
            endpoint.IsEnabled &&
            endpoint.AllowsAnonymous);
        Assert.Contains(response.ApiEndpoints, endpoint =>
            endpoint.ControllerName == nameof(Hongdal.Controllers.Food.음식주문Controller) &&
            endpoint.FeatureKey == VersionFeatureFlagKeys.FoodDeliveryWorkflow &&
            !endpoint.IsEnabled);
    }

    [Fact]
    public void Controllers_HaveHongdalProductVersionMetadata()
    {
        var missingControllers = GetControllerTypes()
            .Where(type => type.GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true) is null)
            .Select(type => type.FullName)
            .Order()
            .ToArray();

        Assert.Empty(missingControllers);
    }

    [Fact]
    public void VersionFeatureGatedApis_RecordMatchingFeatureKeyInVersionMetadata()
    {
        var missingFeatureMetadata = new List<string>();

        foreach (var controllerType in GetControllerTypes())
        {
            var controllerVersion = controllerType.GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);
            foreach (var featureAttribute in controllerType.GetCustomAttributes<RequireVersionFeatureAttribute>(inherit: true))
            {
                var featureKey = Get필요FeatureKey(featureAttribute);
                if (!string.Equals(controllerVersion?.FeatureKey, featureKey, StringComparison.Ordinal))
                {
                    missingFeatureMetadata.Add($"{controllerType.FullName}: {featureKey}");
                }

                if (!string.Equals(controllerVersion?.WorkflowKey, featureKey, StringComparison.Ordinal))
                {
                    missingFeatureMetadata.Add($"{controllerType.FullName}: workflow {featureKey}");
                }
            }

            foreach (var action in GetActionMethods(controllerType))
            {
                var actionVersion = action.GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);
                foreach (var featureAttribute in action.GetCustomAttributes<RequireVersionFeatureAttribute>(inherit: true))
                {
                    var featureKey = Get필요FeatureKey(featureAttribute);
                    if (!string.Equals(actionVersion?.FeatureKey, featureKey, StringComparison.Ordinal))
                    {
                        missingFeatureMetadata.Add($"{controllerType.FullName}.{action.Name}: {featureKey}");
                    }

                    if (!string.Equals(actionVersion?.WorkflowKey, featureKey, StringComparison.Ordinal))
                    {
                        missingFeatureMetadata.Add($"{controllerType.FullName}.{action.Name}: workflow {featureKey}");
                    }
                }
            }
        }

        Assert.Empty(missingFeatureMetadata.Order().ToArray());
    }

    [Fact]
    public void VersionFeatureMetadata_IsResolvableForEveryControllerAction()
    {
        var unresolvedEndpoints = new List<string>();
        var featureEndpointCount = 0;

        foreach (var controllerType in GetControllerTypes())
        {
            var controllerFeatureKey = controllerType
                .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true)?
                .FeatureKey;

            foreach (var action in GetActionMethods(controllerType))
            {
                var actionFeatureKey = action
                    .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true)?
                    .FeatureKey;
                var expectedFeatureKey = string.IsNullOrWhiteSpace(actionFeatureKey)
                    ? controllerFeatureKey
                    : actionFeatureKey;
                if (string.IsNullOrWhiteSpace(expectedFeatureKey))
                {
                    continue;
                }

                featureEndpointCount++;
                var descriptor = new ControllerActionDescriptor
                {
                    ControllerName = controllerType.Name,
                    ActionName = action.Name,
                    ControllerTypeInfo = controllerType.GetTypeInfo(),
                    MethodInfo = action
                };
                var resolvedFeatureKey = HongdalApiVersionFeatureFilter.ResolveFeatureKey(descriptor);
                if (!string.Equals(expectedFeatureKey, resolvedFeatureKey, StringComparison.Ordinal))
                {
                    unresolvedEndpoints.Add($"{controllerType.FullName}.{action.Name}: {expectedFeatureKey}");
                }
            }
        }

        Assert.True(featureEndpointCount > 0);
        Assert.Empty(unresolvedEndpoints.Order().ToArray());
    }

    [Fact]
    public void CoreWorkflowEntryPoints_DeclareTheirExecutionFeatureKeys()
    {
        var cargoVersion = typeof(Hongdal.Controllers.Shipper.Request01.화주운송의뢰Controller)
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);
        var warehouseVersion = typeof(WarehouseOperationsController)
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);
        var foodVersion = typeof(Hongdal.Controllers.Food.음식주문Controller)
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);

        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, cargoVersion?.FeatureKey);
        Assert.Equal(VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow, warehouseVersion?.FeatureKey);
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, foodVersion?.FeatureKey);
    }

    [Fact]
    public void VersionFeatureFlagsController_RemainsUngatedBootstrapEndpoint()
    {
        var controllerType = typeof(VersionFeatureFlagsController);
        var controllerVersion = controllerType
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true);

        Assert.NotNull(controllerVersion);
        Assert.True(string.IsNullOrWhiteSpace(controllerVersion!.FeatureKey));

        foreach (var action in GetActionMethods(controllerType))
        {
            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerType.Name,
                ActionName = action.Name,
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = action
            };

            Assert.Null(HongdalApiVersionFeatureFilter.ResolveFeatureKey(descriptor));
        }
    }

    [Fact]
    public void WorkflowApis_AreTaggedWithWorkflowMetadata()
    {
        var missingWorkflow = new List<string>();

        AddIfMissingWorkflow(typeof(공동구매해외선적추적Controller), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(공동구매커머스이행계획Controller), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(공동구매자동집단화Controller), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(공동구매물류워크플로우Controller), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(주문자집단운영주체Controller), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(공동구매해외선적추적AdminController), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(공동구매커머스이행계획AdminController), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(공동구매물류워크플로우AdminController), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(주문자집단운영주체AdminController), HongdalWorkflow.GroupPurchaseImport, missingWorkflow);
        AddIfMissingWorkflow(typeof(SocialInsuranceFilingsController), HongdalWorkflow.HrParticipation, missingWorkflow);
        AddIfMissingWorkflow(typeof(WarehouseOperationsController), HongdalWorkflow.WarehouseFulfillment, missingWorkflow);
        AddIfMissingWorkflow(typeof(SalesChannelsController), HongdalWorkflow.SalesChannelFulfillment, missingWorkflow);
        AddIfMissingWorkflow(
            typeof(PublicDataLookupController).GetMethod(nameof(PublicDataLookupController.주문자집단배송권검색)),
            "Hongdal.Controllers.Orderer.PublicDataLookupController.주문자집단배송권검색",
            HongdalWorkflow.GroupPurchaseImport,
            missingWorkflow);

        Assert.Empty(missingWorkflow);
    }

    [Fact]
    public void CommunityApis_AreTaggedWithCommunityGrowthTrack()
    {
        var missingCommunityTrack = new List<string>();

        AddIfMissingCommunityTrack(typeof(커뮤니티게시글Controller), missingCommunityTrack);
        AddIfMissingCommunityTrack(typeof(커뮤니티활동신호Controller), missingCommunityTrack);
        AddIfMissingCommunityTrack(typeof(커뮤니티투표Controller), missingCommunityTrack);
        AddIfMissingCommunityTrack(typeof(인연연결Controller), missingCommunityTrack);
        AddIfMissingCommunityTrack(typeof(감사메시지Controller), missingCommunityTrack);
        AddIfMissingCommunityTrack(typeof(WorkRelationshipSnapshotsController), missingCommunityTrack);
        AddIfMissingCommunityTrack(
            typeof(인증Controller).GetMethod("가입온보딩인연후보조회"),
            "Hongdal.Controllers.Common.인증Controller.가입온보딩인연후보조회",
            missingCommunityTrack);

        Assert.Empty(missingCommunityTrack);
    }

    [Fact]
    public void CommunityFoundationApis_AreIntroducedInVersionZero()
    {
        var communityFoundationControllers = new[]
        {
            typeof(커뮤니티게시글Controller),
            typeof(커뮤니티게시판Controller),
            typeof(커뮤니티대화Controller),
            typeof(커뮤니티투표Controller),
            typeof(커뮤니티원장공유Controller),
            typeof(주문원장Controller)
        };

        var incorrectlyVersioned = communityFoundationControllers
            .Where(type => type.GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true)?.Version
                != HongdalProductVersion.V0_0)
            .Select(type => type.FullName)
            .Order()
            .ToArray();

        Assert.Empty(incorrectlyVersioned);
    }

    [Fact]
    public void DomesticTransportApis_RemainVersionOne()
    {
        var version = typeof(Hongdal.Controllers.Shipper.Request01.화주운송의뢰Controller)
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true)?.Version;

        Assert.Equal(HongdalProductVersion.V1_0, version);
    }

    private static IEnumerable<Type> GetControllerTypes()
    {
        return typeof(VersionFeatureFlagsController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .OrderBy(type => type.FullName);
    }

    private static IEnumerable<MethodInfo> GetActionMethods(Type controllerType)
    {
        return controllerType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any()
                || method.GetCustomAttributes<RouteAttribute>(inherit: true).Any());
    }

    private static string Get필요FeatureKey(RequireVersionFeatureAttribute attribute)
    {
        var featureKey = attribute.Arguments?.OfType<string>().FirstOrDefault();
        Assert.False(string.IsNullOrWhiteSpace(featureKey));
        return featureKey!;
    }

    private static void AddIfMissingCommunityTrack(Type controllerType, List<string> missingCommunityTrack)
    {
        var hasCommunityTrack = controllerType
            .GetCustomAttributes<HongdalApiGrowthTrackAttribute>(inherit: true)
            .Any(attribute => attribute.Track == HongdalApiGrowthTrack.Community);
        if (!hasCommunityTrack)
        {
            missingCommunityTrack.Add(controllerType.FullName ?? controllerType.Name);
        }
    }

    private static void AddIfMissingCommunityTrack(MethodInfo? action, string displayName, List<string> missingCommunityTrack)
    {
        Assert.NotNull(action);

        var hasCommunityTrack = action!
            .GetCustomAttributes<HongdalApiGrowthTrackAttribute>(inherit: true)
            .Any(attribute => attribute.Track == HongdalApiGrowthTrack.Community);
        if (!hasCommunityTrack)
        {
            missingCommunityTrack.Add(displayName);
        }
    }

    private static void AddIfMissingWorkflow(Type controllerType, HongdalWorkflow workflow, List<string> missingWorkflow)
    {
        var hasWorkflow = controllerType
            .GetCustomAttributes<HongdalApiWorkflowAttribute>(inherit: true)
            .Any(attribute => attribute.Workflow == workflow);
        if (!hasWorkflow)
        {
            missingWorkflow.Add($"{controllerType.FullName ?? controllerType.Name}: {workflow}");
        }
    }

    private static void AddIfMissingWorkflow(MethodInfo? action, string displayName, HongdalWorkflow workflow, List<string> missingWorkflow)
    {
        Assert.NotNull(action);

        var hasWorkflow = action!
            .GetCustomAttributes<HongdalApiWorkflowAttribute>(inherit: true)
            .Any(attribute => attribute.Workflow == workflow);
        if (!hasWorkflow)
        {
            missingWorkflow.Add($"{displayName}: {workflow}");
        }
    }

    private static void AssertUseCaseHasPrimaryActor(Type useCaseType, HongdalActor actor)
    {
        var attributes = useCaseType.GetCustomAttributes<HongdalUseCaseActorAttribute>(inherit: true);
        Assert.Contains(attributes, attribute =>
            attribute.Actor == actor &&
            attribute.Role == HongdalUseCaseActorRole.Primary &&
            !string.IsNullOrWhiteSpace(attribute.ActorLabel));
    }

    private static void AssertUseCaseHasWorkflow(Type useCaseType, HongdalWorkflow workflow)
    {
        var workflowAttributes = useCaseType.GetCustomAttributes<HongdalApiWorkflowAttribute>(inherit: true);
        var useCaseAttribute = useCaseType.GetCustomAttribute<HongdalUseCaseAttribute>(inherit: true);

        Assert.Contains(workflowAttributes, attribute => attribute.Workflow == workflow);
        Assert.NotNull(useCaseAttribute);
        Assert.False(string.IsNullOrWhiteSpace(useCaseAttribute!.Name));
    }

    private static void AssertUseCaseHasRelation(Type useCaseType, HongdalUseCaseRelationKind kind, string targetUseCaseCode)
    {
        var relationAttributes = useCaseType.GetCustomAttributes<HongdalUseCaseRelationAttribute>(inherit: true);
        Assert.Contains(relationAttributes, attribute =>
            attribute.Kind == kind &&
            attribute.TargetUseCaseCode == targetUseCaseCode &&
            !string.IsNullOrWhiteSpace(attribute.Summary));
    }

    private sealed class FakeVersionFeatureFlagService : IVersionFeatureFlagService
    {
        public bool IsEnabled(string featureKey)
            => GetAll().TryGetValue(featureKey, out var enabled) && enabled;

        public IReadOnlyDictionary<string, bool> GetAll()
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                [VersionFeatureFlagKeys.DomesticTransportWorkflow] = true,
                [VersionFeatureFlagKeys.WarehouseFulfillmentWorkflow] = false,
                [VersionFeatureFlagKeys.CustomsAndTradeDataWorkflow] = false,
                [VersionFeatureFlagKeys.GroupPurchaseImportWorkflow] = false,
                [VersionFeatureFlagKeys.SalesChannelFulfillmentWorkflow] = false,
                [VersionFeatureFlagKeys.CommunityTrustWorkflow] = true,
                [VersionFeatureFlagKeys.HrParticipationWorkflow] = false,
                [VersionFeatureFlagKeys.FoodDeliveryWorkflow] = false,
                [VersionFeatureFlagKeys.HongdalMartWorkflow] = false
            };
        }
    }
}
