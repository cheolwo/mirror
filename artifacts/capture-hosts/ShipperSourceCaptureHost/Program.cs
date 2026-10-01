using Hongdal.Ui.Common.Areas.App.Services;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using HongdalApp.Services;
using HongdalApp.Services.Samples;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHongdalAppServices(builder.Configuration);
builder.Services.AddScoped<IShipperOperationsService, SampleShipperOperationsService>();
builder.Services.AddHongdalUiCommonAppServices();
builder.Services.AddHongdalDocumentOutputServices();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7117/"),
    Timeout = TimeSpan.FromSeconds(2)
});
builder.Services.AddScoped<PlatformCommunityService>();
builder.Services.AddScoped<PlatformHomeModeStateService>();

var app = builder.Build();
var repositoryRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", ".."));
var appWebRoot = Path.Combine(repositoryRoot, "HongdalApp", "wwwroot");
var mudBlazorStaticAssets = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".nuget",
    "packages",
    "mudblazor",
    "9.5.0",
    "staticwebassets");
var aspNetCoreInternalAssetsRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".nuget",
    "packages",
    "microsoft.aspnetcore.app.internal.assets");
var blazorFrameworkAssets = Directory.Exists(aspNetCoreInternalAssetsRoot)
    ? Directory.GetDirectories(aspNetCoreInternalAssetsRoot)
        .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
        .Select(path => Path.Combine(path, "_framework"))
        .FirstOrDefault(Directory.Exists)
    : null;
var commonAppStaticAssets = Path.Combine(repositoryRoot, "Hongdal.Ui.Common", "Areas", "App", "wwwroot");
var commonAppScopedCss = Path.Combine(
    repositoryRoot,
    "Hongdal.Ui.Common",
    "obj",
    "Debug",
    "net10.0",
    "scopedcss",
    "projectbundle");
var commonAppScopedCssFile = Path.Combine(commonAppScopedCss, "Hongdal.Ui.Common.bundle.scp.css");
var hongdalAppCssFile = Path.Combine(appWebRoot, "app.css");
var communityDecorationStoreCssFile = Path.Combine(
    repositoryRoot,
    "HongdalApp",
    "Components",
    "Pages",
    "CommunityDecorationStorePage.razor.css");
var communityDecorationDetailCssFile = Path.Combine(
    repositoryRoot,
    "HongdalApp",
    "Components",
    "Pages",
    "CommunityDecorationDetailPage.razor.css");
var prajnaLectureLibraryCssFile = Path.Combine(
    repositoryRoot,
    "Hongdal.Ui.Common",
    "Areas",
    "App",
    "Components",
    "Community",
    "HongdalPrajnaLectureLibrary.razor.css");
var mudBlazorCssFile = Path.Combine(mudBlazorStaticAssets, "MudBlazor.min.css");
var hongdalAppScopedCssFile = Path.Combine(
    repositoryRoot,
    "HongdalApp",
    "obj",
    "Debug",
    "net10.0-windows10.0.19041.0",
    "win-x64",
    "scopedcss",
    "bundle",
    "HongdalApp.styles.css");

app.UseStatusCodePagesWithReExecute("/not-found");
if (Directory.Exists(mudBlazorStaticAssets))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(mudBlazorStaticAssets),
        RequestPath = "/_content/MudBlazor"
    });
}

if (blazorFrameworkAssets is not null)
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(blazorFrameworkAssets),
        RequestPath = "/_framework"
    });
}

if (Directory.Exists(commonAppStaticAssets))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(commonAppStaticAssets),
        RequestPath = "/_content/Hongdal.Ui.Common/Areas/App"
    });
}

if (Directory.Exists(commonAppScopedCss))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(commonAppScopedCss),
        RequestPath = "/_content/Hongdal.Ui.Common"
    });
}

if (Directory.Exists(appWebRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(appWebRoot)
    });
}

app.UseStaticFiles();
app.UseAntiforgery();
app.MapGet("/capture/app.css", () => Results.File(hongdalAppCssFile, "text/css"));
app.MapGet("/capture/mudblazor.css", () => Results.File(mudBlazorCssFile, "text/css"));
app.MapGet("/capture/common.css", () => Results.File(commonAppScopedCssFile, "text/css"));
app.MapGet("/capture/shipper.css", () => Results.File(hongdalAppScopedCssFile, "text/css"));
app.MapGet("/capture/community-decoration-store.css", () =>
    Results.File(communityDecorationStoreCssFile, "text/css"));
app.MapGet("/capture/community-decoration-detail.css", () =>
    Results.File(communityDecorationDetailCssFile, "text/css"));
app.MapGet("/capture/prajna-lecture-library.css", () =>
    Results.File(prajnaLectureLibraryCssFile, "text/css"));

app.MapRazorComponents<HongdalApp.App>()
    .AddInteractiveServerRenderMode();

app.Run();
