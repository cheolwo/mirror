using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
using HumanResourcesManagerApp.ViewModels;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace HumanResourcesManagerApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddHongdalUiCommonAppServices();
        builder.Services.AddTransient<인사Controller기능모음ViewModel>();
        builder.Services.AddTransient<고용계약기능ViewModel>();
        builder.Services.AddTransient<참여혜택기능ViewModel>();
        builder.Services.AddTransient<인사역할기능ViewModel>();
        builder.Services.AddTransient<사회보험신고기능ViewModel>();
        builder.Services.AddTransient<인사Api기능모음ViewModel>();
        builder.Services.AddHongdalApiHttpClient(HongdalApiEndpoint.ResolveBaseAddress(
            builder.Configuration[HongdalApiEndpoint.ConfigurationKey],
            new Uri(HongdalApiEndpoint.LocalDevelopmentBaseAddress)));

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
