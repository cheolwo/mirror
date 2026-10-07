using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using SsalddelAdminApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MudBlazor.Services;

namespace SsalddelAdminApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json"),
            optional: true,
            reloadOnChange: false);

        builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        builder.Services.AddSingleton<AdminAuthSession>();
        builder.Services.AddSingleton<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>();
        builder.Services.AddSsalddelUiCommonAppServices<AdminAuthSession>();
        builder.Services.Add보호지원관리Ui();
        builder.Services.AddTransient<관리자Controller기능모음ViewModel>();
        builder.Services.AddTransient<관리자전체Api기능모음ViewModel>();
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
        builder.Services.AddScoped<AdminAuthService>();
        builder.Services.AddSingleton(provider =>
        {
            var httpClient = provider.GetRequiredService<HttpClient>();
            var session = provider.GetRequiredService<AdminAuthSession>();
            return new TransportRequestLedgerRealtimeClient(
                httpClient.BaseAddress
                ?? throw new InvalidOperationException("Ssalddel API BaseAddress가 설정되어 있지 않습니다."),
                _ => Task.FromResult(session.AccessToken),
                provider.GetRequiredService<ITransportRequestLedgerObserver>());
        });
        builder.Services.AddScoped<AdminAuthenticatedApiClient>();
        builder.Services.AddScoped<AdminDashboardService>();
        builder.Services.AddScoped<AdminOperationsService>();
        builder.Services.AddScoped<AdminFoodOperationsService>();
        builder.Services.AddScoped<AdminFinanceService>();
        builder.Services.AddScoped<FollowUpRecoveryMobileService>();
        builder.Services.AddScoped<CommunityManagementAdminService>();
        builder.Services.AddScoped<HongikHakdangAdminService>();
        builder.Services.AddScoped<CommunityInformationAdminService>();
        builder.Services.AddScoped<I같이수입준비관리Client, 같이수입준비관리Client>();
        builder.Services.AddTransient<같이수입준비관리ViewModel>();
        builder.Services.AddScoped<ICommunityInformationReviewClient>(services =>
            services.GetRequiredService<CommunityInformationAdminService>());
        builder.Services.AddScoped<ICommunityAuthoringImageClient>(services =>
            services.GetRequiredService<CommunityInformationAdminService>());
        builder.Services.AddTransient<CommunityAuthoringSocialResearchViewModel>();
        builder.Services.AddTransient<CommunityAuthoringPeriodStatisticsViewModel>();
        builder.Services.AddTransient<CommunityAuthoringAiDraftViewModel>();
        builder.Services.AddTransient<CommunityAuthoringImageGeneratorViewModel>();
        builder.Services.AddTransient<CommunityInformationReviewPageViewModel>();
        builder.Services.AddSingleton<IAdminPageCatalogClient, AdminPageCatalogSampleService>();
        builder.Services.AddTransient<AdminPageCatalogListViewModel>();
        builder.Services.AddTransient<AdminPageCatalogDetailViewModel>();
        builder.Services.AddTransient<AdminPageCatalogPageViewModel>();
        builder.Services.AddMudServices();
        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
