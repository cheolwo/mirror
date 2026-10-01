using Hongdal.Ui.Common.Areas.BackOffice.ViewModels;

namespace HongdalAdminApp.Services;

/// <summary>
/// 실제 route 배포와 기능 플래그를 변경하지 않고 Admin 페이지 카탈로그의 검토 메타데이터만 보관합니다.
/// </summary>
public sealed class AdminPageCatalogSampleService : IAdminPageCatalogClient
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AdminManagedPageSnapshot> _pages =
        AdminPageCatalogSeed.Create()
            .ToDictionary(page => page.PageKey, StringComparer.Ordinal);

    public Task<IReadOnlyList<AdminManagedPageSnapshot>> GetPagesAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AdminManagedPageSnapshot>>(_pages.Values.ToArray());
        }
    }

    public Task<AdminManagedPageSnapshot> UpdatePageAsync(
        AdminPageCatalogUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        lock (_gate)
        {
            if (!_pages.TryGetValue(request.PageKey, out var current))
            {
                throw new InvalidOperationException("관리 대상 페이지를 찾지 못했습니다.");
            }

            var updated = current with
            {
                ReviewState = request.ReviewState,
                NavigationState = request.NavigationState,
                DesktopVerified = request.DesktopVerified,
                MobileVerified = request.MobileVerified,
                LastReviewedAt = DateTimeOffset.Now,
                LastReviewer = request.Reviewer,
                AdminNote = request.AdminNote
            };
            _pages[updated.PageKey] = updated;
            return Task.FromResult(updated);
        }
    }
}
