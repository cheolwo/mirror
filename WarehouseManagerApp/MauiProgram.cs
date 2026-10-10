using Microsoft.Extensions.Logging;
using Ssalddel.Ui.Common.Areas.App.Services;
using MudBlazor.Services;
using WarehouseManagerApp.Services;

namespace WarehouseManagerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment.I업무사진촬영Adapter, Ssalddel.Client.Infrastructure.Native.Maui업무사진촬영Adapter>();
        builder.Services.AddMudServices();
        builder.Services.AddWarehouseManagerApplication();
        builder.Services.AddSingleton<IPlatformCommunityNodeNavigationResolver, WarehousePlatformCommunityNodeNavigationResolver>();
        builder.Services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, WarehousePlatformHomeWorkspaceNavigationResolver>();
        builder.Services.AddSsalddelUiCommonAppServices<WarehouseAccessTokenProvider>();
        builder.Services.AddScoped<IWorkflowRecoveryStore>(services =>
        {
            var session = services.GetRequiredService<Ssalddel.Client.Infrastructure.Security.ClientAuthSession>();
            return new Ssalddel.Client.Infrastructure.Native.MauiRoleWorkflowRecoveryStore(() =>
            {
                var current = session.CaptureIdentity();
                return new Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.RoleWorkspaceIdentity(current.OwnerId, current.Revision, session.IsAuthenticated);
            }, "warehouse");
        });
#if DEBUG || SSALDDEL_USB_FIELD_TEST
        const bool allowInsecureDebugEndpoint = true;
#else
        const bool allowInsecureDebugEndpoint = false;
#endif
        builder.Services.AddSsalddelOperationalApiHttpClient(
            SsalddelServerEndpoint.ResolveMobileBaseAddress(
                typeof(MauiProgram).Assembly,
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
                allowInsecureDebugEndpoint));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
