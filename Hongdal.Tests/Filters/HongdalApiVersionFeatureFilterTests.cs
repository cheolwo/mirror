using System.Reflection;
using Hongdal.ApiMetadata;
using Hongdal.Extensions;
using Hongdal.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using 홍달.Services.Versioning;

namespace Hongdal.Tests.Filters;

public sealed class HongdalApiVersionFeatureFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_BlocksControllerFeatureMetadataWithoutRequireAttribute()
    {
        var context = CreateContext<FeatureMetadataController>(nameof(FeatureMetadataController.ControllerFeature));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new HongdalApiVersionFeatureFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        Assert.Equal([VersionFeatureFlagKeys.FoodDeliveryWorkflow], featureFlags.CheckedFeatureKeys);
        var result = Assert.IsType<ObjectResult>(context.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
        Assert.Equal(VersionFeatureFlagKeys.FoodDeliveryWorkflow, problem.Extensions["featureKey"]);
        Assert.Empty(typeof(FeatureMetadataController)
            .GetCustomAttributes<RequireVersionFeatureAttribute>(inherit: true));
    }

    [Fact]
    public async Task OnActionExecutionAsync_ActionFeatureMetadataOverridesControllerFeature()
    {
        var context = CreateContext<FeatureMetadataController>(nameof(FeatureMetadataController.ActionFeature));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService(VersionFeatureFlagKeys.FoodDeliveryWorkflow);
        var filter = new HongdalApiVersionFeatureFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        Assert.Equal([VersionFeatureFlagKeys.GroupPurchaseImportWorkflow], featureFlags.CheckedFeatureKeys);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ActionVersionWithoutFeatureInheritsControllerFeature()
    {
        var context = CreateContext<FeatureMetadataController>(nameof(FeatureMetadataController.ActionVersionOnly));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new HongdalApiVersionFeatureFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        Assert.Equal([VersionFeatureFlagKeys.FoodDeliveryWorkflow], featureFlags.CheckedFeatureKeys);
    }

    [Fact]
    public async Task OnActionExecutionAsync_AllowsCapabilityEndpointWithoutFeatureMetadata()
    {
        var context = CreateContext<CapabilityController>(nameof(CapabilityController.Get));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new HongdalApiVersionFeatureFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.True(nextCalled);
        Assert.Empty(featureFlags.CheckedFeatureKeys);
        Assert.Null(context.Result);
    }

    [Fact]
    public void AddHongdalPresentation_RegistersAutomaticVersionFeatureFilter()
    {
        var services = new ServiceCollection();
        services.AddHongdalPresentation();
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.Contains(options.Filters.OfType<TypeFilterAttribute>(), filter =>
            filter.ImplementationType == typeof(HongdalApiVersionFeatureFilter));
    }

    private static ActionExecutingContext CreateContext<TController>(string actionName)
    {
        var controllerType = typeof(TController);
        var method = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)!;
        var descriptor = new ControllerActionDescriptor
        {
            ControllerName = controllerType.Name,
            ActionName = method.Name,
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            MethodInfo = method
        };
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace"
        };
        httpContext.Request.Path = "/api/test";
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);

        return new ActionExecutingContext(
            actionContext,
            [],
            new Dictionary<string, object?>(),
            new object());
    }

    [HongdalApiVersion(
        HongdalProductVersion.V3_0,
        FeatureKey = VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
    private sealed class FeatureMetadataController
    {
        public void ControllerFeature()
        {
        }

        [HongdalApiVersion(
            HongdalProductVersion.V2_5,
            FeatureKey = VersionFeatureFlagKeys.GroupPurchaseImportWorkflow)]
        public void ActionFeature()
        {
        }

        [HongdalApiVersion(HongdalProductVersion.V2_5)]
        public void ActionVersionOnly()
        {
        }
    }

    [HongdalApiVersion(HongdalProductVersion.V0_0)]
    private sealed class CapabilityController
    {
        public void Get()
        {
        }
    }

    private sealed class RecordingFeatureFlagService : IVersionFeatureFlagService
    {
        private readonly HashSet<string> _enabledFeatureKeys;

        public RecordingFeatureFlagService(params string[] enabledFeatureKeys)
        {
            _enabledFeatureKeys = new HashSet<string>(enabledFeatureKeys, StringComparer.Ordinal);
        }

        public List<string> CheckedFeatureKeys { get; } = [];

        public bool IsEnabled(string featureKey)
        {
            CheckedFeatureKeys.Add(featureKey);
            return _enabledFeatureKeys.Contains(featureKey);
        }

        public IReadOnlyDictionary<string, bool> GetAll()
        {
            return CheckedFeatureKeys
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                    featureKey => featureKey,
                    featureKey => _enabledFeatureKeys.Contains(featureKey),
                    StringComparer.Ordinal);
        }
    }
}
