using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using SsalddelAdminApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Client.Infrastructure.Transport;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace SsalddelAdminApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
        builder.Services.AddSingleton<AdminAuthSession>();
        builder.Services.AddSingleton<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>();
        builder.Services.AddSsalddelUiCommonAppServices<AdminAuthSession>();
        builder.Services.AddTransient<관리자Controller기능모음ViewModel>();
        builder.Services.AddTransient<관리자전체Api기능모음ViewModel>();
        builder.Services.AddSsalddelOperationalApiHttpClient(
            SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
                builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
                builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey]));
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
