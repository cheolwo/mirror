using System.Globalization;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;

public static class FoodWorkspacePresentation
{
    public static string Money(decimal amount) => amount.ToString("N0", CultureInfo.GetCultureInfo("ko-KR")) + "원";
    public static string Time(DateTime? value) => value is null || value == default(DateTime)
        ? "미확인" : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).AddHours(9).ToString("M/d HH:mm", CultureInfo.InvariantCulture) + " (한국)";
    public static string Value(string? value, string fallback = "미등록") => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    public static string ActionRoute(string role, string action, string item)
        => $"/workspace/{Uri.EscapeDataString(role)}/action?actionKey={Uri.EscapeDataString(action)}&itemId={Uri.EscapeDataString(item)}";
    public static string OrdererReturnHref(string? href)
    {
        const string root = "/workspace/orderer";
        const string selectedPrefix = root + "?selected=";
        if (href == root) return root;
        if (href is null || !href.StartsWith(selectedPrefix, StringComparison.Ordinal)
            || href.Contains('&') || href.Contains('#') || href.Contains('\\') || href.Any(char.IsControl)) return root;
        var selected = Uri.UnescapeDataString(href[selectedPrefix.Length..]);
        return string.IsNullOrWhiteSpace(selected) ? root : root + "?selected=" + Uri.EscapeDataString(selected);
    }
    public static bool Coordinates(decimal? latitude, decimal? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 && (latitude != 0 || longitude != 0);
    public static IReadOnlyList<NeighborhoodMapMarker> Marker(string id, string label, string kind, decimal? latitude, decimal? longitude)
        => Coordinates(latitude, longitude) ? [new(id, label, kind, (double)latitude!.Value, (double)longitude!.Value)] : [];

    public static RoleWorkspaceAction? Available(IReadOnlyList<업무가능행동Dto> actions, string key, string label,
        bool primary = false, string? route = null)
    {
        var action = actions.FirstOrDefault(value => value.ActionId == key);
        if (action is null) return null;
        var current = !action.ExpiresAtUtc.HasValue || action.ExpiresAtUtc.Value > DateTime.UtcNow;
        return new(key, label, route, primary, current, current ? null : "가능 행동의 확인 시각이 지났습니다. 새로고침해 주세요.");
    }

    public static 업무가능행동Dto Require(IReadOnlyList<업무가능행동Dto> actions, string key)
    {
        var action = actions.FirstOrDefault(value => value.ActionId == key);
        if (action is null || action.ExpiresAtUtc is { } expiry && expiry <= DateTime.UtcNow)
            throw new InvalidOperationException("현재 할 수 없는 행동입니다. 새로고침으로 확인해 주세요.");
        return action;
    }

    public static string MaskPhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "미등록";
        var clean = value.Trim();
        return clean.Length <= 4 ? "••••" : "•••• " + clean[^4..];
    }

    public static RoleWorkspaceSection Section(string title, params (string Label, string? Value)[] fields)
        => new(title, fields.Select(value => new RoleWorkspaceField(value.Label, Value(value.Value))).ToArray());
}
