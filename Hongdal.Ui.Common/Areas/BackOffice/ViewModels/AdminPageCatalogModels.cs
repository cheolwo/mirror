namespace Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

public enum AdminPageLifecycle
{
    Active,
    Preview,
    Internal
}

public enum AdminPageExecutionMode
{
    ReadOnly,
    Simulation,
    Operational
}

public enum AdminPageReviewState
{
    Verified,
    NeedsReview,
    Blocked
}

public enum AdminPageNavigationState
{
    Primary,
    Contextual,
    Hidden
}

public enum AdminPageCatalogMessageKind
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record AdminManagedPageSnapshot(
    string PageKey,
    string AppKey,
    string AppName,
    string AreaKey,
    string AreaName,
    string Title,
    string RouteTemplate,
    string PreviewPath,
    string SourcePath,
    string Purpose,
    string OwnerRole,
    IReadOnlyList<string> AudienceRoles,
    AdminPageLifecycle Lifecycle,
    AdminPageExecutionMode ExecutionMode,
    AdminPageReviewState ReviewState,
    AdminPageNavigationState NavigationState,
    bool RouteDeclared,
    bool DesktopVerified,
    bool MobileVerified,
    bool RequiresAuthentication,
    bool HasExternalEffects,
    DateTimeOffset? LastReviewedAt,
    string? LastReviewer,
    string AdminNote)
{
    public bool NeedsAttention
        => !RouteDeclared
           || ReviewState != AdminPageReviewState.Verified
           || !DesktopVerified
           || !MobileVerified;
}

public sealed record AdminPageCatalogUpdateRequest(
    string PageKey,
    AdminPageReviewState ReviewState,
    AdminPageNavigationState NavigationState,
    bool DesktopVerified,
    bool MobileVerified,
    string AdminNote,
    string Reviewer);

public sealed record AdminPageCatalogOption(string Key, string Label);

public sealed record AdminPageCatalogSummary(
    int TotalCount,
    int PrimaryNavigationCount,
    int SimulationCount,
    int NeedsAttentionCount,
    int FullyVerifiedCount);

public interface IAdminPageCatalogClient
{
    Task<IReadOnlyList<AdminManagedPageSnapshot>> GetPagesAsync(
        CancellationToken cancellationToken = default);

    Task<AdminManagedPageSnapshot> UpdatePageAsync(
        AdminPageCatalogUpdateRequest request,
        CancellationToken cancellationToken = default);
}
