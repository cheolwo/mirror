using SsalddelAdmin.Components;
using SsalddelAdmin.Options;
using SsalddelAdmin.Services;
using Ssalddel.Client.Infrastructure.Transport;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
using Ssalddel.Ui.Common.Areas.BackOffice.ViewModels;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (!isRunningInContainer)
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddCommandLine(args);
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
var operationalApiBaseAddress =
    SsalddelServerEndpoint.ResolveConfiguredBaseAddress(
        builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
        builder.Configuration[관리자ApiOptions.SectionName + ":BaseUrl"],
        SsalddelServerEndpoint.CreateDefaultBaseAddress());
var useMemoryData = builder.Configuration.GetValue("AdminData:UseMemory", false);
builder.Services.Configure<관리자ApiOptions>(builder.Configuration.GetSection(관리자ApiOptions.SectionName));
builder.Services.PostConfigure<관리자ApiOptions>(options =>
    options.BaseUrl = operationalApiBaseAddress.AbsoluteUri);
builder.Services.AddScoped<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>();
builder.Services.AddSsalddelUiCommonAppServices<관리자인증세션Service>();
builder.Services.AddTransient<관리자Controller기능모음ViewModel>();
builder.Services.AddTransient<관리자전체Api기능모음ViewModel>();
builder.Services.AddScoped<I같이수입준비관리Client, 같이수입준비관리Client>();
builder.Services.AddTransient<같이수입준비관리ViewModel>();
builder.Services.AddSsalddelOperationalApiHttpClient(
    operationalApiBaseAddress,
    ServiceLifetime.Scoped,
    useMemoryData ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(100));
builder.Services.AddScoped<관리자인증세션Service>();
builder.Services.AddHttpClient<공간자료CatalogAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddHttpClient<ViewPolicyService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddHttpClient<PlatformCommunityService>((sp, client) =>
    {
        client.BaseAddress = operationalApiBaseAddress;
        if (useMemoryData)
        {
            client.Timeout = TimeSpan.FromSeconds(2);
        }
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        return new SocketsHttpHandler
        {
            ConnectTimeout = useMemoryData ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(10)
        };
    });
builder.Services.AddHttpClient<CommunityManagementService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddHttpClient<지도신청운송취소검토AdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddSingleton<백오피스메모리Service>();
builder.Services.AddSingleton<문서관리메모리Service>();
builder.Services.AddScoped<차량관리Service>();
builder.Services.AddScoped<탐색캠페인샘플Service>();
builder.Services.AddSingleton<FakePaymentSettlementSimulationService>();

builder.Services.AddHttpClient<관리자인증Service>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<기사운행현황Service>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<백오피스조회Service>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddScoped<I백오피스Service>(sp =>
{
    var useMemory = sp.GetRequiredService<IConfiguration>().GetValue("AdminData:UseMemory", false);
    return useMemory
        ? sp.GetRequiredService<백오피스메모리Service>()
        : sp.GetRequiredService<백오피스조회Service>();
});

builder.Services.AddHttpClient<차량관리Service>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<HsCodeOperationsService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<AuxiliaryFeatureSettingsService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<음식운영Service>((sp, client) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
        var useMemory = sp.GetRequiredService<IConfiguration>().GetValue("AdminData:UseMemory", false);
        client.BaseAddress = new Uri(options.BaseUrl);
        if (useMemory)
        {
            client.Timeout = TimeSpan.FromSeconds(2);
        }
    })
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var useMemory = sp.GetRequiredService<IConfiguration>().GetValue("AdminData:UseMemory", false);
        return new SocketsHttpHandler
        {
            ConnectTimeout = useMemory ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(10)
        };
    });

builder.Services.AddHttpClient<RestaurantSearchPolicyAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<DispatchAIJudgmentCaseAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<DomesticCargoDispatchAIReviewAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<FoodDeliveryDispatchAIReviewAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<FoodOrderOperationsTraceAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

builder.Services.AddHttpClient<FollowUpRecoveryAdminService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
