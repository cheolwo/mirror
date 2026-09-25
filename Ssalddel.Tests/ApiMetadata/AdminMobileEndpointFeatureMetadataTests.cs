using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Ssalddel.ApiMetadata;
using Ssalddel.Controllers.Admin;
using Ssalddel.Controllers.Admin.Home01;
using Ssalddel.Controllers.Admin.Operations;
using Ssalddel.Controllers.Admin.Progress03;
using Ssalddel.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.ApiMetadata;

public sealed class AdminMobileEndpointFeatureMetadataTests
{
    public static TheoryData<Type, string> FeatureCases => new()
    {
        { typeof(관리자대시보드Controller), VersionFeatureFlagKeys.DomesticTransportWorkflow },
        { typeof(운송진행관리Controller), VersionFeatureFlagKeys.DomesticTransportWorkflow },
        { typeof(기사운행현황Controller), VersionFeatureFlagKeys.DomesticTransportWorkflow },
        { typeof(음식배달요금정책Controller), VersionFeatureFlagKeys.FoodDeliveryWorkflow },
        { typeof(운영후속처리복구Controller), VersionFeatureFlagKeys.PlatformOperationsControl },
        { typeof(운영재무Controller), VersionFeatureFlagKeys.PlatformOperationsControl }
    };

    [Theory]
    [MemberData(nameof(FeatureCases))]
    public void 관리자모바일Api의_모든Http동작은_실행Feature를_해소한다(
        Type controllerType,
        string expectedFeatureKey)
    {
        var actions = controllerType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .ToArray();

        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            var descriptor = new ControllerActionDescriptor
            {
                ControllerName = controllerType.Name,
                ActionName = action.Name,
                ControllerTypeInfo = controllerType.GetTypeInfo(),
                MethodInfo = action
            };

            Assert.Equal(
                expectedFeatureKey,
                SsalddelApiFeatureBoundaryFilter.ResolveFeatureKey(descriptor));
        });
    }

    [Theory]
    [InlineData(typeof(관리자대시보드Controller), SsalddelWorkflow.DomesticTransport)]
    [InlineData(typeof(운송진행관리Controller), SsalddelWorkflow.DomesticTransport)]
    [InlineData(typeof(기사운행현황Controller), SsalddelWorkflow.DomesticTransport)]
    [InlineData(typeof(음식배달요금정책Controller), SsalddelWorkflow.FoodDelivery)]
    public void 단일업무흐름이_명확한Api는_기존Workflow를_표시한다(
        Type controllerType,
        SsalddelWorkflow expectedWorkflow)
    {
        var workflows = controllerType
            .GetCustomAttributes<SsalddelApiWorkflowAttribute>(inherit: true)
            .Select(attribute => attribute.Workflow)
            .ToArray();

        Assert.Contains(expectedWorkflow, workflows);
    }

    [Theory]
    [InlineData(typeof(운영후속처리복구Controller))]
    [InlineData(typeof(운영재무Controller))]
    public void 교차운영통제Api는_없는Workflow를_만들지_않는다(Type controllerType)
    {
        Assert.Empty(controllerType.GetCustomAttributes<SsalddelApiWorkflowAttribute>(inherit: true));
        Assert.Null(controllerType.GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true)?.WorkflowKey);
    }
}
