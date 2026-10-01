using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using HongdalApp.Options;
using HongdalApp.Services.CommonContents;
using HongdalApp.Services;
using HongdalApp.Services.Localization;
using HongdalApp.Services.Samples;
using Hongdal.Ui.Common.Areas.App.Services;

namespace HongdalApp;

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

		builder.Services.AddHongdalApiHttpClient(HongdalApiEndpoint.ResolveBaseAddress(
			builder.Configuration[HongdalApiEndpoint.ConfigurationKey],
			new Uri(HongdalApiEndpoint.LocalDevelopmentBaseAddress)));
		builder.Services.AddHongdalAppServices(builder.Configuration);
		builder.Services.AddHongdalUiCommonAppServices<IAuthSession>();
		builder.Services.AddHongdalDocumentOutputServices();
		builder.Services.AddMudServices();
		builder.Services.AddMauiBlazorWebView();

#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
