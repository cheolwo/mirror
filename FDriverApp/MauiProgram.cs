using CommunityToolkit.Maui;
using FDriverApp.Controls;
using FDriverApp.Handlers;
using FDriverApp.Pages;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Syncfusion.Maui.Toolkit.Hosting;

using FDriverApp.Services;
using FDriverApp.ViewModels;
using Ssalddel.Client.Infrastructure.Security;

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
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
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

            builder.Configuration.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
                optional: true,
                reloadOnChange: false);

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
            builder.Services.AddSingleton<IFDriverPendingOperationStore, FDriverSecurePendingOperationStore>();
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
                    allowInsecureDebugEndpoint),
                ServiceLifetime.Singleton,
                TimeSpan.FromSeconds(20));
            builder.Services.AddSingleton<FDriverAuthApiService>();
            builder.Services.AddSingleton<IFoodDeliveryDriverApiService, FoodDeliveryDriverApiService>();
            builder.Services.AddSingleton<IFDriverLocationService, FDriverLocationService>();
            builder.Services.AddSingleton<IFDriverFoodNotificationService, FDriverFoodNotificationService>();
            builder.Services.AddSingleton<IFDriverPushDeviceStore, FDriverSecurePushDeviceStore>();
            builder.Services.AddSingleton<FDriverPushRegistrationService>();
            builder.Services.AddSingleton<FDriverFoodPushReceiver>();
            builder.Services.AddSingleton<IFDriverWorkspaceNavigator, FDriverWorkspaceNavigator>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddSingleton<IFDriverCompletedDeliveryNavigator, FDriverCompletedDeliveryNavigator>();
            builder.Services.AddTransient<FDriverCompletedDeliveryListPageModel>();
            builder.Services.AddTransient<FDriverCompletedDeliveryDetailPageModel>();
            builder.Services.AddTransient<CompletedDeliveryListPage>();
            builder.Services.AddTransient<CompletedDeliveryDetailPage>();
            builder.Services.AddSingleton<IFDriverProtectionSupportNavigator, FDriverProtectionSupportNavigator>();
            builder.Services.AddSingleton<FDriverSupportReturnContext>();
            builder.Services.AddTransient<ProtectionSupportPage>();
            builder.Services.AddSingleton<ProjectListPageModel>();
            builder.Services.AddSingleton<ManageMetaPageModel>();

            builder.Services.AddSingleton<IPlatformCommunityNodeNavigationResolver, FDriverPlatformCommunityNodeNavigationResolver>();
            builder.Services.AddSingleton<IPlatformHomeWorkspaceNavigationResolver, FDriverPlatformHomeWorkspaceNavigationResolver>();
            builder.Services.AddSsalddelUiCommonAppServices<IFDriverAuthSession>();
            builder.Services.AddTransient<음식배달기사Controller기능모음ViewModel>();
            builder.Services.AddTransient<기사Controller기능모음ViewModel>();
            builder.Services.AddTransient<음식배달기사운행ViewModel>();
            builder.Services.AddTransient<음식배달수락ViewModel>();
            builder.Services.AddTransient<음식배달이행ViewModel>();
            builder.Services.AddTransient<음식배달경로ViewModel>();
            builder.Services.AddTransient<음식배달기사업무기능ViewModel>();
            builder.Services.AddTransient<음식배달기사Api기능모음ViewModel>();
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
