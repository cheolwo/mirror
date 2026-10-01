namespace Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

internal sealed record AdminPageCatalogQuery(
    string AppKey,
    string AreaKey,
    string ReviewState,
    string ExecutionMode,
    string SearchText,
    bool NeedsAttentionOnly);

internal static class AdminPageCatalogProjection
{
    public static IReadOnlyList<AdminManagedPageSnapshot> Order(
        IEnumerable<AdminManagedPageSnapshot> pages)
        => pages
            .OrderBy(page => page.AppName, StringComparer.Ordinal)
            .ThenBy(page => page.AreaName, StringComparer.Ordinal)
            .ThenBy(page => page.Title, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<AdminPageCatalogOption> BuildAppOptions(
        IReadOnlyList<AdminManagedPageSnapshot> pages)
        => pages
            .GroupBy(page => page.AppKey, StringComparer.Ordinal)
            .Select(group => new AdminPageCatalogOption(group.Key, group.First().AppName))
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<AdminPageCatalogOption> BuildAreaOptions(
        IReadOnlyList<AdminManagedPageSnapshot> pages)
        => pages
            .GroupBy(page => page.AreaKey, StringComparer.Ordinal)
            .Select(group => new AdminPageCatalogOption(group.Key, group.First().AreaName))
            .OrderBy(option => option.Label, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<AdminManagedPageSnapshot> Filter(
        IReadOnlyList<AdminManagedPageSnapshot> pages,
        AdminPageCatalogQuery query)
    {
        IEnumerable<AdminManagedPageSnapshot> filtered = pages;
        if (!string.IsNullOrWhiteSpace(query.AppKey))
        {
            filtered = filtered.Where(page => page.AppKey == query.AppKey);
        }

        if (!string.IsNullOrWhiteSpace(query.AreaKey))
        {
            filtered = filtered.Where(page => page.AreaKey == query.AreaKey);
        }

        if (Enum.TryParse<AdminPageReviewState>(query.ReviewState, out var reviewState))
        {
            filtered = filtered.Where(page => page.ReviewState == reviewState);
        }

        if (Enum.TryParse<AdminPageExecutionMode>(query.ExecutionMode, out var executionMode))
        {
            filtered = filtered.Where(page => page.ExecutionMode == executionMode);
        }

        if (query.NeedsAttentionOnly)
        {
            filtered = filtered.Where(page => page.NeedsAttention);
        }

        var search = query.SearchText.Trim();
        if (search.Length > 0)
        {
            filtered = filtered.Where(page =>
                page.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || page.RouteTemplate.Contains(search, StringComparison.OrdinalIgnoreCase)
                || page.AppName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || page.AreaName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || page.OwnerRole.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToArray();
    }

    public static AdminPageCatalogSummary Summarize(
        IReadOnlyList<AdminManagedPageSnapshot> pages)
        => new(
            pages.Count,
            pages.Count(page => page.NavigationState == AdminPageNavigationState.Primary),
            pages.Count(page => page.ExecutionMode == AdminPageExecutionMode.Simulation),
            pages.Count(page => page.NeedsAttention),
            pages.Count(page => page.ReviewState == AdminPageReviewState.Verified
                                && page.DesktopVerified
                                && page.MobileVerified));
}
