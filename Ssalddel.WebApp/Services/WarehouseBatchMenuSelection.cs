using System.Net;
using Ssalddel.Contracts.Common.Inventory;

namespace Ssalddel.WebApp.Services;

/// <summary>같은 출고 경로의 공정 메뉴를 조회 조건의 순서와 관계없이 구분합니다.</summary>
public static class WarehouseBatchMenuSelection
{
    private const string CanonicalPath = "/warehouse/general/outbound-plan-review";

    public static bool IsBatchMenu(string? menuPath)
        => menuPath is not null && string.Equals(PathWithoutQuery(menuPath), CanonicalPath, StringComparison.OrdinalIgnoreCase);

    public static bool IsActive(string baseUri, string currentUri, string menuPath)
    {
        if (!IsBatchMenu(menuPath)
            || !Uri.TryCreate(baseUri, UriKind.Absolute, out var appBase)
            || !Uri.TryCreate(currentUri, UriKind.Absolute, out var current)
            || !appBase.IsBaseOf(current)) return false;

        // 역할 호스트의 /roles/05/를 제거한 업무 경로와 통합 웹의 / 경로를 같은 기준으로 봅니다.
        var relativePath = "/" + current.AbsolutePath[appBase.AbsolutePath.Length..];
        return string.Equals(relativePath, CanonicalPath, StringComparison.OrdinalIgnoreCase)
            && TryReadStage(current.Query, out var currentStage)
            && TryReadStage(QueryPart(menuPath), out var menuStage)
            && string.Equals(currentStage, menuStage, StringComparison.Ordinal);
    }

    public static string PathWithoutQuery(string path) => path.Split('?', '#')[0];

    private static string QueryPart(string path)
    {
        var withoutFragment = path.Split('#')[0];
        var index = withoutFragment.IndexOf('?');
        return index < 0 ? string.Empty : withoutFragment[index..];
    }

    private static bool TryReadStage(string query, out string? stage)
    {
        stage = null;
        var parameters = query.StartsWith('?') ? query[1..] : query;
        foreach (var parameter in parameters.Split('&'))
        {
            var separator = parameter.IndexOf('=');
            var name = WebUtility.UrlDecode(separator < 0 ? parameter : parameter[..separator]);
            if (!string.Equals(name, "stage", StringComparison.OrdinalIgnoreCase)) continue;
            if (stage is not null) return false;
            var value = separator < 0 ? string.Empty : WebUtility.UrlDecode(parameter[(separator + 1)..]);
            if (!OutboundWorkflowStageCodes.All.Contains(value, StringComparer.Ordinal)) return false;
            stage = value;
        }
        return true;
    }
}
