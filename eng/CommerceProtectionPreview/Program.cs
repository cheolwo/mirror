using CommerceProtectionPreview;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("개발 전용 화면 호스트입니다.");
builder.WebHost.UseUrls("http://127.0.0.1:5396").UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddScoped<CommercePreviewClient>();
var realApi = !string.IsNullOrWhiteSpace(builder.Configuration["COMMERCE_PREVIEW_REAL_ACCOUNTS"]);
builder.Services.AddSingleton<RealApiEvidence>();
builder.Services.AddScoped<RealApiSession>();
if (realApi)
{
    builder.Services.AddScoped<ISsalddelJsonApiClient>(sp => sp.GetRequiredService<RealApiSession>().JsonApi);
    builder.Services.AddScoped<ISsalddel현재사용자Context>(sp => sp.GetRequiredService<RealApiSession>());
    builder.Services.AddScoped<IRoleWorkspaceAccess>(sp => sp.GetRequiredService<RealApiSession>());
    builder.Services.AddScoped<IRoleWorkspaceApi>(sp => sp.GetRequiredService<RealApiSession>());
    builder.Services.AddScoped<IRoleWorkspaceLocationProvider, RealFixtureLocationProvider>();
    builder.Services.AddScoped<INeighborhoodMapHost, RealApiMapUnavailableHost>();
    builder.Services.AddScoped<IRoleWorkspaceAdapter, OrdererRoleWorkspaceAdapter>();
    builder.Services.AddScoped<IRoleWorkspaceAdapter, RestaurantRoleWorkspaceAdapter>();
    builder.Services.AddScoped<IRoleWorkspaceAdapter, FoodDriverRoleWorkspaceAdapter>();
    builder.Services.AddScoped<IRoleWorkspaceAdapter, OperatorRoleWorkspaceAdapter>();
    builder.Services.AddScoped<RoleWorkspaceState>();
    builder.Services.AddTransient<RoleWorkspaceViewModel>();
    builder.Services.Add보호지원관리Ui();
}
else
{
    builder.Services.AddScoped<I통신판매보호Client>(sp => sp.GetRequiredService<CommercePreviewClient>());
    builder.Services.AddScoped<ISsalddel현재사용자Context>(sp => sp.GetRequiredService<CommercePreviewClient>());
}
builder.Services.AddCommerceProtectionUi();
var app = builder.Build();
app.Use(async (context, next) => { if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip)) { context.Response.StatusCode = 404; return; } await next(); });
app.UseStaticFiles(); app.UseAntiforgery(); app.MapStaticAssets();
if (realApi)
{
    app.MapGet("/verification/http-evidence", (RealApiEvidence evidence) => Results.Json(evidence.Snapshot()));
    // 제품의 생성된 scoped CSS 복사본만 제공하며 별도 디자인 구현이나 소스 변경을 하지 않습니다.
    app.MapGet("/verification/shared-ui.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "verification-shared-ui.css"), "text/css"));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(); app.Run();
