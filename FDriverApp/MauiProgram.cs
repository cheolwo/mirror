using CommunityToolkit.Maui;
using FDriverApp.Controls;
using FDriverApp.Handlers;
using FDriverApp.Pages;
using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Syncfusion.Maui.Toolkit.Hosting;

using FDriverApp.Services;
using Hongdal.Client.Infrastructure.Security;

namespace FDriverApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                })
                .ConfigureSyncfusionToolkit()
                .ConfigureMauiHandlers(handlers =>
                {
                    handlers.AddHandler<FDriverNativeMapView, FDriverNativeMapViewHandler>();
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});

    				Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
    				{
    					if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
    					{
    						contentPanel.IsTabStop = true;
    					}
    				});
#endif
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<FDriverAppProfile>();
            builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
            builder.Services.AddSingleton<IFDriverAuthSession, FDriverAuthSession>();
            builder.Services.AddHongdalApiHttpClient(
                HongdalApiEndpoint.CreateDefaultBaseAddress(),
                ServiceLifetime.Singleton,
                TimeSpan.FromSeconds(20));
            builder.Services.AddSingleton<FDriverAuthApiService>();
            builder.Services.AddSingleton<IFoodDeliveryDriverApiService, FoodDeliveryDriverApiService>();
            builder.Services.AddSingleton<IFDriverLocationService, FDriverLocationService>();
            builder.Services.AddSingleton<IFDriverWorkspaceNavigator, FDriverWorkspaceNavigator>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddSingleton<ProjectListPageModel>();
            builder.Services.AddSingleton<ManageMetaPageModel>();

            builder.Services.AddHongdalUiCommonAppServices<IFDriverAuthSession>();
            builder.Services.AddMudServices();
            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif

            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");

            return builder.Build();
        }
    }
}
