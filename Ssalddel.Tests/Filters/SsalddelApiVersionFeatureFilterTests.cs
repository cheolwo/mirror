using System.Reflection;
using Ssalddel.ApiMetadata;
using Ssalddel.Extensions;
using Ssalddel.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using 살뜰.Services.Options;
using 살뜰.Services.Versioning;

namespace Ssalddel.Tests.Filters;

public sealed class SsalddelApiFeatureBoundaryFilterTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task 실제공통배차Controller의세행동은_음식또는화물게이트로열고_둘다OFF이면닫는다(
        bool food, bool cargo)
    {
        var flags = CreateOperationalDispatchFeatureService(food, cargo);
        var expected = food || cargo;
        var actions = new[] { "수신상태조회", "수신의사변경", "단기지표조회" };
        foreach (var action in actions)
        {
            var context = CreateContext<Ssalddel.Controllers.Driver.Settings05.기사운영배차공통Controller>(action);
            var nextCalled = false;
            await new SsalddelApiFeatureBoundaryFilter(flags).OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, [], new object()));
            });

            Assert.Equal(expected, nextCalled);
            if (expected)
            {
                Assert.Null(context.Result);
            }
            else
            {
                var result = Assert.IsType<ObjectResult>(context.Result);
                Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
                var problem = Assert.IsType<ProblemDetails>(result.Value);
                Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
                Assert.Equal(VersionFeatureFlagKeys.OperationalDispatchCore, problem.Extensions["featureKey"]);
            }
        }
    }

    [Fact]
    public async Task 음식전용공통배차Api를열어도_실제화물기사조회는계속닫혀있다()
    {
        var flags = CreateOperationalDispatchFeatureService(food: true, cargo: false);
        var context = CreateContext<Ssalddel.Controllers.Driver.Progress05.기사운송진행Controller>(
            nameof(Ssalddel.Controllers.Driver.Progress05.기사운송진행Controller.현재조회));
        var nextCalled = false;
        await new SsalddelApiFeatureBoundaryFilter(flags).OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(context.Result).Value);
        Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
        Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, problem.Extensions["featureKey"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 화물기사조회는_기존국내운송기능게이트만명시적으로소비한다(bool enabled)
    {
        var context = CreateContext<Ssalddel.Controllers.Driver.Progress05.기사운송진행Controller>(
            nameof(Ssalddel.Controllers.Driver.Progress05.기사운송진행Controller.현재조회));
        var flags = enabled
            ? new RecordingFeatureFlagService(VersionFeatureFlagKeys.DomesticTransportWorkflow)
            : new RecordingFeatureFlagService();
        var nextCalled = false;
        await new SsalddelApiFeatureBoundaryFilter(flags).OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.Equal(enabled, nextCalled);
        Assert.Equal([VersionFeatureFlagKeys.DomesticTransportWorkflow], flags.CheckedFeatureKeys);
        if (!enabled)
        {
            var problem = Assert.IsType<ProblemDetails>(Assert.IsType<ObjectResult>(context.Result).Value);
            Assert.Equal("FeatureDisabled", problem.Extensions["errorCode"]);
            Assert.Equal(VersionFeatureFlagKeys.DomesticTransportWorkflow, problem.Extensions["featureKey"]);
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_BlocksControllerFeatureMetadataWithoutRequireAttribute()
    {
        var context = CreateContext<FeatureMetadataController>(nameof(FeatureMetadataController.ControllerFeature));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new SsalddelApiFeatureBoundaryFilter(featureFlags);

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
        var filter = new SsalddelApiFeatureBoundaryFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        Assert.Equal([VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow], featureFlags.CheckedFeatureKeys);
    }

    [Fact]
    public async Task OnActionExecutionAsync_ActionVersionWithoutFeatureInheritsControllerFeature()
    {
        var context = CreateContext<FeatureMetadataController>(nameof(FeatureMetadataController.ActionVersionOnly));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new SsalddelApiFeatureBoundaryFilter(featureFlags);

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
        var filter = new SsalddelApiFeatureBoundaryFilter(featureFlags);

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
    public async Task OnActionExecutionAsync_BlocksPostV0EndpointWithoutFeatureMetadata()
    {
        var context = CreateContext<UnclassifiedFutureController>(
            nameof(UnclassifiedFutureController.Get));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new SsalddelApiFeatureBoundaryFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        Assert.Empty(featureFlags.CheckedFeatureKeys);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("FeatureBoundaryUnclassified", problem.Extensions["errorCode"]);
        Assert.Equal("2.5", problem.Extensions["productVersion"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_BlocksPostV0ActionOverrideWithoutFeatureMetadata()
    {
        var context = CreateContext<V0ControllerWithFutureAction>(
            nameof(V0ControllerWithFutureAction.Future));
        var nextCalled = false;
        var featureFlags = new RecordingFeatureFlagService();
        var filter = new SsalddelApiFeatureBoundaryFilter(featureFlags);

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(context, [], new object()));
        });

        Assert.False(nextCalled);
        Assert.Empty(featureFlags.CheckedFeatureKeys);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("FeatureBoundaryUnclassified", problem.Extensions["errorCode"]);
        Assert.Equal("2.5", problem.Extensions["productVersion"]);
    }

    [Fact]
    public void AddSsalddelPresentation_RegistersAutomaticFeatureBoundaryFilter()
    {
        var services = new ServiceCollection();
        services.AddSsalddelPresentation();
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<MvcOptions>>().Value;

        Assert.Contains(options.Filters.OfType<TypeFilterAttribute>(), filter =>
            filter.ImplementationType == typeof(SsalddelApiFeatureBoundaryFilter));
    }

    [Fact]
    public void AddSsalddelPresentation_RequiresCertificatePathWhenConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PersonalDataProtection:RequireCertificate"] = "true",
                ["PersonalDataProtection:CertificatePath"] = ""
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddSsalddelPresentation(configuration));

        Assert.Contains(
            "PersonalDataProtection:CertificatePath is required",
            exception.Message,
            StringComparison.Ordinal);
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

    [SsalddelApiIntroducedIn(SsalddelProductVersion.V3_0)]
    [SsalddelApiFeature(VersionFeatureFlagKeys.FoodDeliveryWorkflow)]
    private sealed class FeatureMetadataController
    {
        public void ControllerFeature()
        {
        }

        [SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
        [SsalddelApiFeature(VersionFeatureFlagKeys.GroupPurchaseDemandWorkflow)]
        public void ActionFeature()
        {
        }

        [SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
        public void ActionVersionOnly()
        {
        }
    }

    [SsalddelApiIntroducedIn(SsalddelProductVersion.V0_0)]
    [SsalddelApiCapability(SsalddelCapability.CommunityInformationDiscovery)]
    private sealed class CapabilityController
    {
        public void Get()
        {
        }
    }

    [SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
    private sealed class UnclassifiedFutureController
    {
        public void Get()
        {
        }
    }

    [SsalddelApiIntroducedIn(SsalddelProductVersion.V0_0)]
    private sealed class V0ControllerWithFutureAction
    {
        [SsalddelApiIntroducedIn(SsalddelProductVersion.V2_5)]
        public void Future()
        {
        }
    }

    private static VersionFeatureFlagService CreateOperationalDispatchFeatureService(bool food, bool cargo)
        => new(new StaticOptionsMonitor<VersionFeatureFlagsOptions>(new VersionFeatureFlagsOptions
        {
            FoodDeliveryWorkflow = food,
            DomesticTransportWorkflow = cargo,
            CommunityTrustWorkflow = cargo
        }));

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
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
