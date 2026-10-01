using MudBlazor.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using RestaurantDeskApp;
using RestaurantDeskApp.Services;

if (args.Contains("--verify"))
{
    await 메뉴화면검증.RunAsync();
    await 메뉴화면검증.Api안전검증Async();
    return;
}

var options = 메뉴미리보기Options.FromArguments(args);
var builder = WebApplication.CreateBuilder(args.Where(x => x != "--api").ToArray());
builder.WebHost.UseUrls("http://127.0.0.1:5387");
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddSingleton(options);
if (options.Api연결)
{
    builder.Services.AddScoped<메뉴Api연결>();
    builder.Services.AddScoped<I음식점메뉴ApiClient>(sp => sp.GetRequiredService<메뉴Api연결>());
    builder.Services.AddScoped<CircuitHandler, 메뉴ApiCircuitHandler>();
}
else
{
    builder.Services.AddScoped<I음식점메뉴ApiClient, 메뉴미리보기Client>();
}
var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/app.css", () => Results.File(Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "../../RestaurantDeskApp/wwwroot/app.css")), "text/css"));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
