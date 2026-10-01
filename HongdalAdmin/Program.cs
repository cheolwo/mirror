using HongdalAdmin.Components;
using HongdalAdmin.Options;
using HongdalAdmin.Services;
using Hongdal.Client.Infrastructure.Transport;
using Hongdal.Ui.Common.Areas.App.Services;
using Hongdal.Ui.Common.Areas.App.ViewModels;
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
builder.Services.Configure<관리자ApiOptions>(builder.Configuration.GetSection(관리자ApiOptions.SectionName));
builder.Services.Configure<FoodApiOptions>(builder.Configuration.GetSection(FoodApiOptions.SectionName));
builder.Services.AddScoped<ITransportRequestLedgerObserver, TransportRequestLedgerObserver>();
builder.Services.AddHongdalUiCommonAppServices<관리자인증세션Service>();
builder.Services.AddTransient<관리자Controller기능모음ViewModel>();
builder.Services.AddTransient<관리자전체Api기능모음ViewModel>();
builder.Services.AddScoped(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    var useMemory = sp.GetRequiredService<IConfiguration>().GetValue("AdminData:UseMemory", false);
    return new HttpClient
    {
        BaseAddress = new Uri(options.BaseUrl),
        Timeout = useMemory ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(100)
    };
});
builder.Services.AddScoped<관리자인증세션Service>();
builder.Services.AddHttpClient<ViewPolicyService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<관리자ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
});
builder.Services.AddScoped<ActivityLogService>();
builder.Services.AddHttpClient<PlatformCommunityService>((sp, client) =>
    {
        var configuration = sp.GetRequiredService<IConfiguration>();
        var baseUrl = configuration["AdminApi:BaseUrl"] ?? "https://localhost:7117/";
        client.BaseAddress = new Uri(baseUrl);
        if (configuration.GetValue("AdminData:UseMemory", false))
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
builder.Services.AddSingleton<백오피스메모리Service>();
builder.Services.AddSingleton<문서관리메모리Service>();
builder.Services.AddScoped<차량관리Service>();
builder.Services.AddScoped<음식운영Service>();
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
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FoodApiOptions>>().Value;
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
