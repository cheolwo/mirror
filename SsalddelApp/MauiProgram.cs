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

		builder.Services.AddSsalddelOperationalApiHttpClient(
			SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
				builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
				builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
				new Uri(SsalddelServerEndpoint.LocalDevelopmentBaseAddress)));
		builder.Services.AddSsalddelAppServices(builder.Configuration);
		builder.Services.AddSingleton<IPlatformCommunityNodeNavigationResolver, SsalddelAppPlatformCommunityNodeNavigationResolver>();
		builder.Services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, SsalddelAppPlatformHomeWorkspaceNavigationResolver>();
		builder.Services.AddSsalddelUiCommonAppServices<IAuthSession>();
		builder.Services.AddSsalddelDocumentOutputServices();
		builder.Services.AddMudServices();
		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
