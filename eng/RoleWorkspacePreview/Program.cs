using RoleWorkspacePreview;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.Services;
using MudBlazor.Services;
using Ssalddel.Contracts.Common.Platform;
using Ssalddel.WebApp.Services;
using System.Net;
using System.Text.RegularExpressions;
using Ssalddel.Ui.Common.Areas.App.Services.Commerce;
var builder = WebApplication.CreateBuilder(args);
var previewPort = builder.Configuration.GetValue<int?>("LifeFacilitationPreviewPort") ?? 5392;
if (previewPort is < 1024 or > 65535) throw new InvalidOperationException("The preview requires a valid loopback port.");
var previewOrigin = $"http://127.0.0.1:{previewPort}";
builder.WebHost.UseUrls(previewOrigin).UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddScoped<I통신판매보호Client, CommerceProtectionPreview.CommercePreviewClient>();
builder.Services.AddCommerceProtectionUi();
builder.Services.AddScoped<IRoleWorkspaceAccess, PreviewAccess>();
builder.Services.AddScoped<PreviewActionApi>();
builder.Services.AddScoped<PreviewInputApi>();
builder.Services.AddScoped<RefactoringPreviewFixture>();
builder.Services.AddScoped<Ssalddel.Ui.Common.Areas.BackOffice.Services.I보호지원관리Client>(sp => sp.GetRequiredService<RefactoringPreviewFixture>());
builder.Services.AddScoped<IRoleWorkspaceApi>(sp => new Uri(sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).AbsolutePath.StartsWith("/exception-refactoring-preview/", StringComparison.Ordinal)
    ? sp.GetRequiredService<RefactoringPreviewFixture>()
    : sp.GetRequiredService<PreviewInputApi>());
builder.Services.AddScoped<ISsalddel현재사용자Context, PreviewNeighborhoodUser>();
builder.Services.AddScoped<INeighborhoodDeliveryClient, PreviewNeighborhoodDeliveryClient>();
builder.Services.AddTransient<Ssalddel.Ui.Common.Areas.App.ViewModels.NeighborhoodDeliveryQueryViewModel>();
builder.Services.AddCommunityToolsPreview();
builder.Services.AddLifeFacilitationPreview();
builder.Services.AddNeighborhoodFulfillmentPreview();
builder.Services.AddAutoFulfillmentPreview();
builder.Services.AddNeighborhoodMemberDeliveryPreview();
builder.Services.AddScoped<OrderLedgerRolePreviewFixture>();
builder.Services.AddScoped<Ssalddel.Ui.Common.Areas.App.Services.I주문원장Service>(sp => sp.GetRequiredService<OrderLedgerRolePreviewFixture>());
builder.Services.AddScoped<Ssalddel.Ui.Common.Areas.App.ViewModels.주문업무상태ViewModel>();
builder.Services.AddTransient<Ssalddel.Ui.Common.Areas.App.ViewModels.주문조회ViewModel>();
builder.Services.AddScoped<DeliveryCostSharingPreviewFixture>();
builder.Services.AddScoped<DeliveryExperiencePreviewFixture>();
builder.Services.AddScoped<HistoricalDeliveryPreviewFixture>();
builder.Services.AddSingleton<RecordedMenuCatalogPreviewFixture>();
// 이 전용 loopback 호스트에서만 격리 표본을 조립합니다. 운영 API/DbContext DI는 등록하지 않습니다.
builder.Services.AddScoped<SsalddelMartSampleInput>();
builder.Services.AddScoped<SsalddelMartSampleRuntime>();
builder.Services.AddScoped<FoodDriverControlsPreviewFixture>();
builder.Services.AddScoped<WarehouseWorkspaceApiClient>();
builder.Services.AddScoped<CargoDriverWorkspaceApiClient>();
builder.Services.AddScoped<RoleWorkspaceState>();
builder.Services.AddTransient(sp => new Uri(sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).AbsolutePath.StartsWith("/delivery-cost-sharing-preview/role-", StringComparison.Ordinal)
    ? sp.GetRequiredService<DeliveryCostSharingPreviewFixture>().RoleModel
    : new Uri(sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).AbsolutePath.StartsWith("/delivery-experience-preview/", StringComparison.Ordinal)
        ? sp.GetRequiredService<DeliveryExperiencePreviewFixture>().Model
    : new Uri(sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri).AbsolutePath.StartsWith("/food-driver-controls-preview/", StringComparison.Ordinal)
        ? sp.GetRequiredService<FoodDriverControlsPreviewFixture>().Model
    : ActivatorUtilities.CreateInstance<RoleWorkspaceViewModel>(sp));
builder.Services.AddScoped(_ =>
{
    var http = new HttpClient { BaseAddress = new Uri(previewOrigin + "/") };
    http.DefaultRequestHeaders.Add("Origin", previewOrigin);
    return http;
});
builder.Services.AddScoped<GoogleMapsBrowserRuntimeClient>();
builder.Services.AddScoped<INeighborhoodMapHost, NeighborhoodGoogleMapHost>();
foreach (var role in RoleWorkspaceCatalog.Roles)
{
    var key = role.Key;
    builder.Services.AddScoped<IRoleWorkspaceAdapter>(sp => new PreviewAdapter(key, sp.GetRequiredService<IRoleWorkspaceAccess>(), sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>(), sp.GetRequiredService<PreviewInputApi>().For(key)));
}
var app = builder.Build();
// 검토용 예시 계정과 키 공급은 이 컴퓨터의 루프백에만 제공합니다.
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.MapGet(GoogleMapsBrowserRuntimeRoutes.LocalDevelopment, (HttpContext context, IConfiguration configuration, IHostEnvironment environment) =>
{
    if (!environment.IsDevelopment() || context.Request.Headers.Origin != previewOrigin)
        return Results.NotFound();
    var key = configuration["GoogleMaps:BrowserApiKey"]?.Trim();
    if (key is null || !Regex.IsMatch(key, "^AIza[0-9A-Za-z_-]{35}$", RegexOptions.CultureInvariant))
        return Results.NoContent();
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers.Vary = "Origin";
    return Results.Ok(new GoogleMapsBrowserRuntimeResponse { BrowserApiKey = key, AllowedOrigins = [previewOrigin] });
});
app.MapGet(RecordedMenuCatalogPreviewFixture.ImageRoutePrefix + "{assetId}", (string assetId, HttpContext context, RecordedMenuCatalogPreviewFixture fixture) =>
{
    if (context.Connection.RemoteIpAddress is not { } address || !IPAddress.IsLoopback(address)
        || context.Request.Headers["Sec-Fetch-Site"] == "cross-site"
        || (context.Request.Headers.Origin.Count > 0 && context.Request.Headers.Origin != previewOrigin)
        || !fixture.TryReadImage(assetId, out var bytes, out var mimeType))
        return Results.NotFound();
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
    return Results.File(bytes, mimeType);
});
app.UseStaticFiles(); app.UseAntiforgery(); app.MapStaticAssets();
// 프로젝트 밖에서 링크한 제품 모듈은 출력 사본만 제공하며 별도 구현을 만들지 않습니다.
foreach (var module in new[] { "neighborhood-life-google-map.js", "community-world-google-map.js" })
{
    var modulePath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "js", module);
    app.MapGet($"/js/{module}", () => Results.File(modulePath, "text/javascript"));
}
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
