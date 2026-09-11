using Microsoft.Extensions.Logging;
using DriverApp.Services.CommonContents;
using DriverApp.Services;
using DriverApp.Services.Samples;
using DriverApp.Handlers;
using Ssalddel.Ui.Common.Areas.App.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace DriverApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureMauiHandlers(handlers =>
			{
				handlers.AddHandler<Controls.DriverNativeMapView, DriverNativeMapViewHandler>();
			})
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddDriverAppServices(builder.Configuration);
		builder.Services.AddSsalddelUiCommonAppServices<IAuthSession>();
		builder.Services.AddSsalddelOperationalApiHttpClient(
			SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
				builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
				builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
				SsalddelServerEndpoint.CreateDefaultBaseAddress()),
			ServiceLifetime.Singleton);
		builder.Services.AddSsalddelDocumentOutputServices();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<NativeDriverHomePage>();
		builder.Services.AddMudServices();
		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		DriverAppServiceProvider.Initialize(app.Services);
		return app;
	}
}
