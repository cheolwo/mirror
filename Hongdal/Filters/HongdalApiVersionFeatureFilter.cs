using System.Reflection;
using Hongdal.ApiMetadata;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using 홍달.Services.Versioning;

namespace Hongdal.Filters;

/// <summary>
/// Enforces the feature key declared by <see cref="HongdalApiVersionAttribute"/>
/// for every MVC controller action.
/// </summary>
public sealed class HongdalApiVersionFeatureFilter : IAsyncActionFilter, IOrderedFilter
{
    private readonly IVersionFeatureFlagService _featureFlagService;

    public HongdalApiVersionFeatureFilter(IVersionFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public int Order => int.MinValue;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var featureKey = ResolveFeatureKey(context.ActionDescriptor);
        if (string.IsNullOrWhiteSpace(featureKey))
        {
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

        var actionFeatureKey = controllerAction.MethodInfo
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true)?
            .FeatureKey;
        if (!string.IsNullOrWhiteSpace(actionFeatureKey))
        {
            return actionFeatureKey;
        }

        return controllerAction.ControllerTypeInfo
            .GetCustomAttribute<HongdalApiVersionAttribute>(inherit: true)?
            .FeatureKey;
    }
}
