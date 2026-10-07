using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using SsalddelApp.Options;
using SsalddelApp.Services.CommonContents;
using SsalddelApp.Services;
using SsalddelApp.Services.Localization;
using SsalddelApp.Services.Samples;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace SsalddelApp;

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
		builder.Services.AddSsalddelAppServices(builder.Configuration);
		builder.Services.AddSingleton<IPlatformCommunityNodeNavigationResolver, SsalddelAppPlatformCommunityNodeNavigationResolver>();
		builder.Services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, SsalddelAppPlatformHomeWorkspaceNavigationResolver>();
		builder.Services.AddSsalddelUiCommonAppServices<IAuthSession>();
		builder.Services.AddSsalddelDocumentOutputServices();
		builder.Services.AddMudServices();
		builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<NeighborhoodNativeMapBridge>();
        builder.Services.AddScoped<INeighborhoodMapHost, MauiNeighborhoodMapHost>();
        builder.Services.AddScoped<INeighborhoodMapPreferenceStore, NeighborhoodMapPreferenceStore>();
        builder.Services.AddScoped<Ssalddel.Client.RoleWorkspace.IRoleWorkspacePrimaryAuth, MauiRoleWorkspacePrimaryAuth>();
        builder.Services.AddScoped<Ssalddel.Client.RoleWorkspace.IRoleWorkspaceTokenStoreFactory, MauiRoleWorkspaceTokenStoreFactory>();
        builder.Services.AddScoped<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core.IRoleWorkspaceLocationProvider, MauiRoleWorkspaceLocationProvider>();
        Ssalddel.Client.RoleWorkspace.RoleWorkspaceRegistration.AddUnifiedRoleWorkspaces(builder.Services);
#if ANDROID
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<Controls.NeighborhoodNativeMapView, Handlers.NeighborhoodNativeMapViewHandler>());
#endif

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
