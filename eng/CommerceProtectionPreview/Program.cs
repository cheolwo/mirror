using CommerceProtectionPreview;
using MudBlazor.Services;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Food;
using Ssalddel.Ui.Common.Areas.BackOffice.Services;
var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("개발 전용 화면 호스트입니다.");
var fourRoleR27 = builder.Configuration["COMMERCE_PREVIEW_FOUR_ROLE_R27"] == "1";
builder.WebHost.UseUrls(fourRoleR27 ? "http://127.0.0.1:5397" : "http://127.0.0.1:5396").UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddScoped<CommercePreviewClient>();
var realApi = !string.IsNullOrWhiteSpace(builder.Configuration["COMMERCE_PREVIEW_REAL_ACCOUNTS"]);
builder.Services.AddSingleton<RealApiEvidence>();
builder.Services.AddSingleton<RealUiTransportFaults>();
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
    if (fourRoleR27)
    {
        builder.Services.AddScoped<I주문자앱인증Service, RealOrdererAuthentication>();
        builder.Services.AddSsalddelUiCommonAppServices();
        builder.Services.AddScoped(sp => sp.GetRequiredService<RealApiSession>().HttpClient);
        builder.Services.AddScoped<SsalddelAdminApp.Services.AdminAuthSession>();
        builder.Services.AddScoped<SsalddelAdminApp.Services.AdminAuthService>();
        builder.Services.AddScoped<SsalddelAdminApp.Services.AdminAuthenticatedApiClient>();
        builder.Services.AddScoped<SsalddelAdminApp.Services.AdminFoodOperationsService>();
    }
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
    if (fourRoleR27)
    {
        app.MapPost("/verification/fail-next-admin-trace", (RealUiTransportFaults faults) => { faults.FailNextAdminTrace(); return Results.NoContent(); });
        app.MapGet("/verification/admin-app.css", () => Results.File(Path.Combine(AppContext.BaseDirectory, "verification-admin-app.css"), "text/css"));
    }
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(); app.Run();
