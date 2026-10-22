using Microsoft.AspNetCore.WebUtilities;
using Ssalddel.Contracts.Admin.BusinessPackages;
using Ssalddel.Contracts.Common.Community;

namespace SsalddelAdmin.Services;

/// <summary>운송 목록의 분야·조회 조건만 전달합니다. 역할 자격이나 운송 분류를 만들지 않습니다.</summary>
public sealed record 운송관제NavigationContext
{
    public string ListPath { get; private init; } = "/transports";
    public string? Status { get; private init; }
    public string? EventRequestId { get; private init; }
    public bool AdminOnly { get; private init; }
    // 주문·창고 별칭만으로 화물 유형을 단정하지 않습니다.
    public string? BusinessCode => ListPath == 업무패키지관리Routes.FreightDeliveryTransports
        ? BusinessPackageCatalog.FreightDelivery : null;

    public string ListHref
    {
        get
        {
            var query = new Dictionary<string, string?>();
            if (Status is not null) query["status"] = Status;
            if (EventRequestId is not null) query["eventRequest"] = EventRequestId;
            if (AdminOnly) query["adminOnly"] = "true";
            return QueryHelpers.AddQueryString(ListPath, query);
        }
    }

    public static 운송관제NavigationContext ForList(string path, string? status = null,
        string? eventRequestId = null, bool adminOnly = false)
        => new()
        {
            ListPath = CanonicalList(path) ?? "/transports",
            Status = Filter(status), EventRequestId = Filter(eventRequestId), AdminOnly = adminOnly
        };

    public string DetailHref(string requestId) => WorkflowHref(requestId, null, ListHref);

    public static string ResolveListHref(string? returnPath)
    {
        var local = PageNavigationContext.NormalizeReturnPath(returnPath);
        if (local is null || local.Contains('#')) return "/transports";
        var parts = local.Split('?', 2);
        var listPath = CanonicalList(parts[0]);
        if (listPath is null) return "/transports";
        var query = QueryHelpers.ParseQuery(parts.Length == 2 ? parts[1] : "");
        if (query.Any(pair => pair.Value.Count != 1 || pair.Key is not ("status" or "eventRequest" or "adminOnly")))
            return "/transports";
        var status = query.GetValueOrDefault("status").ToString();
        var eventRequest = query.GetValueOrDefault("eventRequest").ToString();
        var only = query.GetValueOrDefault("adminOnly").ToString();
        if (status.Length > 256 || eventRequest.Length > 256
            || status.Any(char.IsControl) || eventRequest.Any(char.IsControl)
            || only.Length > 0 && !bool.TryParse(only, out _)) return "/transports";
        return ForList(listPath, status, eventRequest, bool.TryParse(only, out var value) && value).ListHref;
    }

    public static string WorkflowHref(string requestId, string? segment, string? listReturnPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (segment is not (null or "events" or "proofs" or "settlement"))
            throw new ArgumentException("지원하지 않는 운송 조회 화면입니다.", nameof(segment));
        var route = "/transports/" + Uri.EscapeDataString(requestId)
            + (segment is null ? "" : "/" + segment);
        return PageNavigationContext.WithReturnPath(route, ResolveListHref(listReturnPath));
    }

    private static string? CanonicalList(string path)
    {
        var candidate = path.TrimEnd('/');
        return new[] { "/transports", 업무패키지관리Routes.FreightDeliveryTransports, 업무패키지관리Routes.OrderWarehouseOutboundTransports }
            .FirstOrDefault(allowed => string.Equals(allowed, candidate, StringComparison.OrdinalIgnoreCase));
    }
    private static string? Filter(string? value)
        => string.IsNullOrWhiteSpace(value) || value.Length > 256 || value.Any(char.IsControl) ? null : value.Trim();
}
