using Hongdal.Contracts.Common.Community;
using Hongdal.Contracts.Common.Versioning;
using MudBlazor;

namespace Hongdal.Ui.Common.Areas.App.Components.Community;

public partial class PlatformCommunityHome
{
    private string Get원장Api경로변수값(string parameterName)
        => 원장Api경로변수값.TryGetValue(parameterName, out var value) ? value : string.Empty;

    private IReadOnlyList<ApiRouteParameter> GetApiRouteParameters(CommunityLedgerProcessingSurfaceResponse surface)
        => GetApiRouteParameters(ResolveSurfaceRoutePattern(surface));

    private static IReadOnlyList<ApiRouteParameter> GetApiRouteParameters(string routePattern)
    {
        if (string.IsNullOrWhiteSpace(routePattern))
        {
            return [];
        }

        var parameters = new List<ApiRouteParameter>();
        var startIndex = 0;
        while (startIndex < routePattern.Length)
        {
            var openIndex = routePattern.IndexOf('{', startIndex);
            if (openIndex < 0)
            {
                break;
            }

            var closeIndex = routePattern.IndexOf('}', openIndex + 1);
            if (closeIndex < 0)
            {
                break;
            }

            var token = routePattern[openIndex..(closeIndex + 1)];
            var name = token.Trim('{', '}');
            var constraintIndex = name.IndexOf(':', StringComparison.Ordinal);
            if (constraintIndex >= 0)
            {
                name = name[..constraintIndex];
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                parameters.Add(new ApiRouteParameter(name.Trim(), token));
            }

            startIndex = closeIndex + 1;
        }

        return parameters;
    }

    private string BuildResolvedApiRoute(CommunityLedgerProcessingSurfaceResponse surface)
    {
        var route = ResolveSurfaceRoutePattern(surface);
        foreach (var parameter in GetApiRouteParameters(surface))
        {
            var value = Get원장Api경로변수값(parameter.Name);
            if (!string.IsNullOrWhiteSpace(value))
            {
                route = route.Replace(parameter.Token, Uri.EscapeDataString(value.Trim()), StringComparison.OrdinalIgnoreCase);
            }
        }

        return route;
    }

    private bool HasMissingApiRouteParameters(CommunityLedgerProcessingSurfaceResponse surface)
        => GetApiRouteParameters(surface)
            .Any(parameter => string.IsNullOrWhiteSpace(Get원장Api경로변수값(parameter.Name)));

    private bool HasUnresolvedApiMetadata(CommunityLedgerProcessingSurfaceResponse surface)
        => surface.IsExistingSurface &&
           !string.IsNullOrWhiteSpace(surface.ApiEndpointKey) &&
           ResolveApiEndpoint(surface) is null;

    private WorkflowApiEndpointDto? ResolveApiEndpoint(CommunityLedgerProcessingSurfaceResponse surface)
    {
        if (string.IsNullOrWhiteSpace(surface.ApiEndpointKey))
        {
            return null;
        }

        if (apiEndpointMetadata.TryGetValue(surface.ApiEndpointKey, out var endpoint))
        {
            return endpoint;
        }

        return apiEndpointMetadata.Values.FirstOrDefault(endpoint =>
            string.Equals(endpoint.ControllerName, surface.ControllerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(endpoint.ActionName, surface.ActionName, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(surface.Method) ||
             string.Equals(endpoint.Method, surface.Method, StringComparison.OrdinalIgnoreCase)));
    }

    private string ResolveSurfaceMethod(CommunityLedgerProcessingSurfaceResponse surface)
        => ResolveApiEndpoint(surface)?.Method
           ?? surface.Method
           ?? string.Empty;

    private string ResolveSurfaceRoutePattern(CommunityLedgerProcessingSurfaceResponse surface)
        => ResolveApiEndpoint(surface)?.RoutePattern
           ?? surface.RoutePattern
           ?? string.Empty;

    private string ResolveSurfaceStatusLabel(CommunityLedgerProcessingSurfaceResponse surface)
    {
        if (!surface.IsExistingSurface)
        {
            return "계획 API";
        }

        if (isApiEndpointMetadataLoading)
        {
            return "메타데이터 확인 중";
        }

        return ResolveApiEndpoint(surface) is null ? "메타데이터 대기" : "기존 API 메타데이터";
    }

    private Color ResolveSurfaceStatusColor(CommunityLedgerProcessingSurfaceResponse surface)
    {
        if (!surface.IsExistingSurface)
        {
            return Color.Info;
        }

        if (isApiEndpointMetadataLoading)
        {
            return Color.Secondary;
        }

        return ResolveApiEndpoint(surface) is null ? Color.Warning : Color.Success;
    }

    private IReadOnlyList<PlatformCommunityLedgerApiSurfacePresentation> BuildLedgerApiSurfacePresentations()
        => SelectedLedgerTemplate.ProcessingSurfaces
            .Select(surface => new PlatformCommunityLedgerApiSurfacePresentation(
                surface,
                ResolveSurfaceMethod(surface),
                ResolveSurfaceStatusColor(surface),
                ResolveSurfaceStatusLabel(surface),
                BuildResolvedApiRoute(surface),
                HasUnresolvedApiMetadata(surface),
                HasMissingApiRouteParameters(surface)))
            .ToArray();

    private void ClearLedgerMetadataResult()
        => 원장전송결과메시지 = null;

    private void 원장Api경로준비(CommunityLedgerProcessingSurfaceResponse surface)
    {
        if (HasUnresolvedApiMetadata(surface))
        {
            원장전송결과Severity = Severity.Warning;
            원장전송결과메시지 = "기존 API 메타데이터를 아직 불러오지 못했습니다. API 서버 연결 뒤 다시 확인하세요.";
            return;
        }

        if (HasMissingApiRouteParameters(surface))
        {
            원장전송결과Severity = Severity.Warning;
            원장전송결과메시지 = "API 경로에 필요한 값을 먼저 입력하세요.";
            return;
        }

        var template = SelectedLedgerTemplate;
        var apiLines = new List<string>
        {
            string.Empty,
            "선택한 API 경로 메타데이터:",
            $"- 처리 지점: {surface.ApiEndpointKey}",
            $"- 호출 방식: {ResolveSurfaceMethod(surface)}",
            $"- 경로: {BuildResolvedApiRoute(surface)}",
            $"- 목적: {surface.Purpose}"
        };

        var endpoint = ResolveApiEndpoint(surface);
        if (endpoint is not null)
        {
            if (endpoint.WorkflowNames.Count > 0)
            {
                apiLines.Add($"- 업무 흐름: {string.Join(", ", endpoint.WorkflowNames)}");
            }

            if (!string.IsNullOrWhiteSpace(endpoint.AuthorizationPolicy) ||
                !string.IsNullOrWhiteSpace(endpoint.AuthorizationRoles))
            {
                apiLines.Add($"- 권한: {endpoint.AuthorizationPolicy} {endpoint.AuthorizationRoles}".Trim());
            }
        }

        foreach (var block in template.LedgerBlocks)
        {
            var value = Get원장블록입력값(block.Code);
            if (!string.IsNullOrWhiteSpace(value))
            {
                apiLines.Add($"- 블록 {block.DisplayName}: {value.Trim()}");
            }
        }

        form.Body = Build원함포함원장본문(template) + string.Join(Environment.NewLine, apiLines);
        원장전송결과Severity = Severity.Info;
        원장전송결과메시지 = $"{ResolveSurfaceMethod(surface)} {BuildResolvedApiRoute(surface)} 경로 메타데이터를 원장 초안에 반영했습니다. 실제 호출은 해당 API 입력값이 채워진 뒤 수행합니다.";
    }

    private sealed record ApiRouteParameter(string Name, string Token);
}
