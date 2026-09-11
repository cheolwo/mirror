using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Web.UnityReviewApp;
using Ssalddel.Web.UnityReviewApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = SsalddelServerEndpoint.ResolveBrowserBaseAddress(
    builder.Configuration[SsalddelServerEndpoint.ConfigurationKey],
    builder.Configuration[SsalddelServerEndpoint.LegacyConfigurationKey],
    new Uri(builder.HostEnvironment.BaseAddress, UriKind.Absolute));

builder.Services.AddSsalddelOperationalApiHttpClient(apiBaseAddress);
builder.Services.AddSingleton<IClientSessionGuard, ClientSessionGuard>();
builder.Services.AddScoped<UnityReviewAuthSessionService>();
builder.Services.AddScoped<Synty공간조립오프라인검토Store>();
builder.Services.AddScoped<ISynty공간조립모바일검토Client, Synty공간조립모바일검토Client>();
builder.Services.AddScoped<Synty공간조립검토Workspace>();

await builder.Build().RunAsync();
