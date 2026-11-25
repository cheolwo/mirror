using Microsoft.Extensions.DependencyInjection.Extensions;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;

namespace SsalddelAdmin.Services;

public static class 관리자플랫폼ServiceCollectionExtensions
{
    public static IServiceCollection Add관리자플랫폼Services(this IServiceCollection services)
    {
        services.TryAddScoped<IRoleWorkspaceLocationProvider>(_ => new 관리자미지원위치Provider());
        services.TryAddScoped<I업무사진촬영Adapter, 미지원업무사진촬영Adapter>();
        return services;
    }

    private sealed class 관리자미지원위치Provider : IRoleWorkspaceLocationProvider
    {
        public Task<RoleWorkspaceLocation?> GetCurrentAsync(CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<RoleWorkspaceLocation?>(cancellationToken)
                : Task.FromResult<RoleWorkspaceLocation?>(null);

        public Task<RoleWorkspaceLocation?> GetCurrentWithoutPermissionPromptAsync(CancellationToken cancellationToken)
            => GetCurrentAsync(cancellationToken);
    }
}
