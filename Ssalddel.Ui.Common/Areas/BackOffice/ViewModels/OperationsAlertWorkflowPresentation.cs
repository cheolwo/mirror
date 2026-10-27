using System.Globalization;
using Ssalddel.Contracts.Admin.Operations;
using Ssalddel.Contracts.Common.Community;

namespace Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;

/// <summary>대응 안내와 지원되는 조회 링크만 표현합니다. 사건 해결·고객 응답·기한을 판정하지 않습니다.</summary>
public sealed class OperationsAlertWorkflowPresentation(OperationsAlertDto source)
{
    public string State => source.Status switch
    {
        "Resolved" => "해결됨", "Acknowledged" => "확인됨 · 미해결", _ => "미해결"
    };
    public string ResponsibleParty => source.Handling?.ResponsibleParty ?? "담당 확인 필요";
    public string NextAction => source.Handling?.NextAction ?? source.Action;
    public string OperatorReview => Time(source.Handling?.NextOperatorReviewAtUtc, "미설정");
    public string SourceRevalidation => Time(source.Handling?.NextRevalidationAtUtc, "미확인");
    public string? ServiceTarget => source.Handling?.ServiceTargetSummary;
    public bool ServiceClockUnconfigured => source.Handling?.ServiceClockCode == "WorkingHoursNotConfigured";
    public string? SourceHref => SupportedSource(source.SourceHref);
    public string SourceLabel => SourceHref switch
    {
        { } href when href.StartsWith("/privacy-support/", StringComparison.Ordinal) => "사건 보기",
        { } href when href.StartsWith("/food-operations/orders/", StringComparison.Ordinal) => "주문 보기",
        "/operations" => "운영 현황", "/operations-alerts" => "운영 알림", _ => "업무 연결 확인 필요"
    };

    public string? SourceHrefWithReturn(string alertHref)
        => SourceHref is { } href ? PageNavigationContext.WithReturnPath(href, alertHref) : null;

    public static string Status(string? status)
        => status is "Open" or "Acknowledged" or "Resolved" ? status : "Unresolved";
    public static string ListHref(string? status = null, int page = 1)
        => "/operations-alerts?status=" + Status(status) + "&page="
            + Math.Clamp(page, 1, 10000).ToString(CultureInfo.InvariantCulture);
    public static string DetailHref(string id, string? listReturnHref = null)
        => PageNavigationContext.WithReturnPath("/operations-alerts/" + Uri.EscapeDataString(id), ResolveListHref(listReturnHref));
    public static string ResolveListHref(string? returnHref)
    {
        var local = PageNavigationContext.NormalizeReturnPath(returnHref);
        if (local is null || local.Contains('#')) return ListHref();
        var parts = local.Split('?', 2);
        if (parts[0] != "/operations-alerts") return ListHref();
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parts.Length == 2 && parts[1].Length > 0)
            foreach (var entry in parts[1].Split('&'))
            {
                var pair = entry.Split('=', 2);
                if (pair.Length != 2) return ListHref();
                var key = Uri.UnescapeDataString(pair[0].Replace('+', ' '));
                var queryValue = Uri.UnescapeDataString(pair[1].Replace('+', ' '));
                if (key is not ("status" or "page") || !query.TryAdd(key, queryValue)) return ListHref();
            }
        var status = query.GetValueOrDefault("status") ?? string.Empty;
        if (status.Length > 0 && status is not ("Open" or "Acknowledged" or "Resolved" or "Unresolved")) return ListHref();
        var pageText = query.GetValueOrDefault("page") ?? string.Empty;
        if (pageText.Length > 0 && (!int.TryParse(pageText, out var value) || value is < 1 or > 10000)) return ListHref();
        return ListHref(status, int.TryParse(pageText, out var page) ? page : 1);
    }

    private static string? SupportedSource(string? href)
    {
        var local = PageNavigationContext.NormalizeReturnPath(href);
        if (local is null || local.IndexOfAny(['?', '#']) >= 0) return null;
        if (local is "/operations" or "/operations-alerts") return local;
        foreach (var prefix in new[] { "/privacy-support/", "/food-operations/orders/" })
        {
            if (!local.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var id = Uri.UnescapeDataString(local[prefix.Length..]);
            return id.Length > 0 && !id.Contains('/') && id is not ("." or "..") ? local : null;
        }
        return null;
    }
    private static string Time(DateTime? utc, string missing)
        => utc is { } value ? DateTime.SpecifyKind(value, DateTimeKind.Utc).AddHours(9).ToString("MM.dd HH:mm", CultureInfo.InvariantCulture) : missing;
}
