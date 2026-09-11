using System.Reflection;
using Ssalddel.ApiMetadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using 살뜰.Services.Versioning;

namespace Ssalddel.Filters;

/// <summary>
/// 제품 버전과 분리된 실행 Feature 경계를 모든 MVC Controller action에 적용합니다.
/// 기존 Version metadata의 Feature Key는 전환 기간에만 호환 경로로 읽습니다.
/// </summary>
public class SsalddelApiFeatureBoundaryFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IVersionFeatureFlagService _featureFlagService;

    public SsalddelApiFeatureBoundaryFilter(IVersionFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public int Order => int.MinValue;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor simulationAction
            && string.Equals(
                simulationAction.ControllerTypeInfo.Assembly.GetName().Name,
                "Ssalddel.Simulation.Hosting",
                StringComparison.Ordinal))
        {
            // Simulation API는 제품 판본 Feature가 아니라 로그인·세션 소유권
            // 관문으로 통제되는 단일 서버 내부 모듈이다.
            await next();
            return;
        }

        var featureKey = ResolveFeatureKey(context.ActionDescriptor);
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            var version = ResolveVersion(context.ActionDescriptor);
            if (version is null || version > SsalddelProductVersion.V0_0)
            {
                context.Result = CreateUnclassifiedBoundaryResult(context, version);
                return;
            }

            await next();
            return;
        }

        var featureFilter = new RequireVersionFeatureFilter(featureKey, _featureFlagService);
        await featureFilter.OnActionExecutionAsync(context, next);
    }

    internal static string? ResolveFeatureKey(ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor is not ControllerActionDescriptor controllerAction)
        {
            return null;
        }

        var actionFeature = controllerAction.MethodInfo
            .GetCustomAttribute<SsalddelApiFeatureAttribute>(inherit: true)?
            .FeatureKey;
        if (!string.IsNullOrWhiteSpace(actionFeature))
        {
            return actionFeature;
        }

        var controllerFeature = controllerAction.ControllerTypeInfo
            .GetCustomAttribute<SsalddelApiFeatureAttribute>(inherit: true)?
            .FeatureKey;
        if (!string.IsNullOrWhiteSpace(controllerFeature))
        {
            return controllerFeature;
        }

        // 기존 Controller를 순차적으로 전환하기 위한 임시 호환 경로입니다.
        var actionFeatureKey = controllerAction.MethodInfo
            .GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true)?
            .FeatureKey;
        if (!string.IsNullOrWhiteSpace(actionFeatureKey))
        {
            return actionFeatureKey;
        }

        return controllerAction.ControllerTypeInfo
            .GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true)?
            .FeatureKey;
    }

    internal static SsalddelProductVersion? ResolveVersion(ActionDescriptor actionDescriptor)
    {
        if (actionDescriptor is not ControllerActionDescriptor controllerAction)
        {
            return null;
        }

        return controllerAction.MethodInfo
                   .GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true)?
                   .Version
               ?? controllerAction.ControllerTypeInfo
                   .GetCustomAttribute<SsalddelApiVersionAttribute>(inherit: true)?
                   .Version;
    }

    private static ObjectResult CreateUnclassifiedBoundaryResult(
        ActionExecutingContext context,
        SsalddelProductVersion? version)
    {
        var versionLabel = version.HasValue
            ? SsalddelProductVersionLabels.GetLabel(version.Value)
            : "unclassified";
        var problem = new ProblemDetails
        {
            Title = "배포 범위가 명시되지 않은 기능입니다.",
            Status = StatusCodes.Status404NotFound,
            Type = "https://httpstatuses.com/404",
            Detail = $"{versionLabel} API must declare an execution feature key before it can be exposed.",
            Instance = context.HttpContext.Request.Path.Value
        };
        problem.Extensions["errors"] = new[] { problem.Title };
        problem.Extensions["errorCode"] = "FeatureBoundaryUnclassified";
        problem.Extensions["productVersion"] = versionLabel;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status404NotFound
        };
    }
}

/// <summary>
/// 전환 기간 동안 기존 DI와 테스트가 참조할 수 있도록 남긴 호환 이름입니다.
/// 새 코드는 <see cref="SsalddelApiFeatureBoundaryFilter"/>를 사용합니다.
/// </summary>
public sealed class SsalddelApiVersionFeatureFilter : SsalddelApiFeatureBoundaryFilter
{
    public SsalddelApiVersionFeatureFilter(IVersionFeatureFlagService featureFlagService)
        : base(featureFlagService)
    {
    }
}
