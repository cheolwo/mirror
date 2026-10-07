using FluentResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;
using 살뜰.Data;
using NeighborhoodExchangePreview;

var builder = WebApplication.CreateBuilder(args);
var flexiblePreview = builder.Configuration.GetValue<bool>("FlexibleDeliveryPreview");
var mapPreview = builder.Configuration.GetValue<bool>("MapPreview");
var collaborationPreview = builder.Configuration.GetValue<bool>("CollaborationPreview");
var previewOrigin = flexiblePreview ? "http://127.0.0.1:" + (builder.Configuration["FlexiblePort"] ?? "5392") : collaborationPreview ? "http://127.0.0.1:5391" : mapPreview ? "http://127.0.0.1:5390" : "http://127.0.0.1:5389";
if (mapPreview || collaborationPreview) builder.Configuration.AddUserSecrets("47766019-f542-4cfc-9d4a-14b2fbfeac0e");
builder.WebHost.UseUrls(previewOrigin).UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var evidence = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../artifacts/local/", flexiblePreview ? "neighborhood-flexible-delivery-r22" : collaborationPreview ? "neighborhood-collaboration-r1" : mapPreview ? "community-map-home-r1" : "neighborhood-exchange-mvp-r1"));
Directory.CreateDirectory(evidence);
if (flexiblePreview) builder.AddFlexibleDatabase(evidence);
else builder.Services.AddDbContext<SsalddelContext>(options => options.UseSqlite($"Data Source={Path.Combine(evidence, "preview.sqlite")}"));
builder.Services.AddSingleton<살뜰.Infrastructure.Security.IPersonalDataEncryptionService, PreviewDataProtection>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AnonymousPreviewUser>();
builder.Services.AddScoped<Ssalddel.Application.CommandProcessing.ICurrentUserAccessor>(sp => sp.GetRequiredService<AnonymousPreviewUser>());
builder.Services.AddSingleton<I생활교류공개지역Source, Official생활교류공개지역Source>();
builder.Services.AddScoped<I생활교류지도조회UseCase, 생활교류지도조회UseCase>();
builder.Services.AddScoped<ICommunityBoardWritePolicy, CommunityBoardWritePolicy>();
builder.Services.AddScoped<I게시글원장표시ContextService, EmptyLedgerContext>();
builder.Services.AddScoped(sp => new 커뮤니티게시글생성Service(sp.GetRequiredService<SsalddelContext>(),
    new 커뮤니티게시글음성작업예약Service(), new CommunityKeywordNotificationQueue(), null!,
    sp.GetRequiredService<I게시글원장표시ContextService>(), sp.GetRequiredService<ICommunityBoardWritePolicy>(),
    sp.GetRequiredService<AnonymousPreviewUser>(), new PreviewPublisher(), sp.GetRequiredService<ILogger<커뮤니티게시글생성Service>>(), sp.GetRequiredService<I생활교류공개지역Source>()));
builder.Services.AddScoped(sp => new 커뮤니티게시글발행UseCase(sp.GetRequiredService<커뮤니티게시글생성Service>(), sp.GetRequiredService<SsalddelContext>(), null!,
    sp.GetRequiredService<I게시글원장표시ContextService>(), sp.GetRequiredService<ICommunityBoardWritePolicy>(), sp.GetRequiredService<AnonymousPreviewUser>(), sp.GetRequiredService<I생활교류공개지역Source>()));
builder.Services.AddScoped(sp => new 커뮤니티게시글조회UseCase(sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<I게시글원장표시ContextService>(), sp.GetRequiredService<AnonymousPreviewUser>()));
builder.Services.AddScoped(sp => new 커뮤니티게시글참여UseCase(sp.GetRequiredService<SsalddelContext>(), sp.GetRequiredService<AnonymousPreviewUser>()));
builder.Services.AddScoped<커뮤니티게시글운영UseCase>();
builder.Services.AddScoped(sp => { var client = new HttpClient { BaseAddress = new Uri(previewOrigin + "/") }; client.DefaultRequestHeaders.Add("Origin", previewOrigin);
    if (collaborationPreview && sp.GetRequiredService<AnonymousPreviewUser>().FixtureAccount is { } account) client.DefaultRequestHeaders.Add("Cookie", "preview-account=" + account); return client; });
builder.Services.AddScoped<SsalddelIsmsPClientEncryptionService>();
builder.Services.AddScoped(sp => new SsalddelProtectedApiClient(sp.GetRequiredService<HttpClient>(), sp.GetRequiredService<SsalddelIsmsPClientEncryptionService>(), sp.GetRequiredService<AnonymousPreviewUser>()));
builder.Services.AddScoped<ICommunityPostClient, CommunityPlatformClient>();
builder.Services.AddScoped<ISsalddelJsonApiClient, SsalddelJsonApiClient>();
builder.Services.AddScoped<NeighborhoodExchangeMapClient>();
builder.Services.AddScoped<INeighborhoodExchangeMapClient, PreviewNeighborhoodMapClient>();
builder.Services.AddTransient<NeighborhoodExchangeMapViewModel>();
builder.Services.AddScoped<NeighborhoodMapWorkspaceSession>();
builder.Services.AddScoped<Ssalddel.WebApp.Services.GoogleMapsBrowserRuntimeClient>();
builder.Services.AddScoped<INeighborhoodMapHost, Ssalddel.WebApp.Services.NeighborhoodGoogleMapHost>();
builder.Services.AddScoped<INeighborhoodMapPreferenceStore, Ssalddel.WebApp.Services.NeighborhoodMapPreferenceStore>();
builder.Services.AddScoped<INeighborhoodExchangeClient, NeighborhoodExchangeClient>();
builder.Services.AddTransient<NeighborhoodExchangeViewModel>();
builder.Services.AddSingleton<ISsalddel현재사용자Context, PreviewDeliveryUser>();
builder.Services.AddSingleton<INeighborhoodDeliveryClient, PreviewDeliveryClient>();
builder.Services.AddTransient<NeighborhoodDeliveryAuthoringViewModel>();
builder.Services.AddTransient<NeighborhoodDeliveryQueryViewModel>();
if (collaborationPreview) builder.AddCollaborationPreview(evidence);
if (flexiblePreview) builder.AddFlexibleDelivery();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SsalddelContext>();
    await db.Database.EnsureCreatedAsync();
    if (flexiblePreview && builder.Configuration.GetValue<bool>("FlexibleMigrationProbe")) await FlexibleMigrationProbe.VerifyAsync(db, evidence);
}
// 선택한 로컬 publish 출력으로 독립 01 Web의 실제 /roles/01/ 진입도 함께 확인합니다.
var communityWebRoot = Environment.GetEnvironmentVariable("SSALDDEL_PREVIEW_COMMUNITY_WEB_ROOT");
if (!string.IsNullOrWhiteSpace(communityWebRoot))
{
    var root = Path.GetFullPath(communityWebRoot);
    var allowed = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../artifacts/local/map-workspace-r1")) + Path.DirectorySeparatorChar;
    if (!root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || !File.Exists(Path.Combine(root, "index.html")))
        throw new InvalidOperationException("The Community Web preview requires this task's local publish output.");
    var contentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
    contentTypes.Mappings[".wasm"] = "application/wasm";
    app.MapGet("/roles/01/{**path}", (HttpContext context) =>
    {
        if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip)) return Results.NotFound();
        var relative = context.Request.RouteValues["path"]?.ToString() ?? "";
        var file = Path.GetFullPath(Path.Combine(root, relative));
        if (!file.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && file != root) return Results.NotFound();
        if (File.Exists(file))
            return Results.File(file, contentTypes.TryGetContentType(file, out var contentType) ? contentType : "application/octet-stream");
        // 누락된 정적 자산을 HTML 성공으로 숨기지 않습니다. 확장자 없는 앱 route만 진입 HTML로 복귀합니다.
        if (Path.HasExtension(relative)) return Results.NotFound();
        return Results.File(Path.Combine(root, "index.html"), "text/html");
    });
}
if (flexiblePreview) app.Use(async (context, next) => { if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip)) { context.Response.StatusCode = 404; return; } await next(context); });
app.UseStaticFiles(); app.UseAntiforgery(); app.MapStaticAssets();
if (collaborationPreview) app.UseMiddleware<Ssalddel.Middleware.IsmsPEncryptedTransportMiddleware>();
foreach (var script in new[] { "community-world-google-map.js", "neighborhood-life-google-map.js" })
{
    var scriptPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../Ssalddel.WebApp/wwwroot/js", script));
    app.MapGet("/js/" + script, () => Results.File(scriptPath, "text/javascript"));
}
app.MapGet("/", () => Results.Redirect(mapPreview || collaborationPreview ? "/community/map" : NeighborhoodExchange.Home));
app.MapGet("/" + NeighborhoodExchangeMapRoutes.RegionsApi, async (I생활교류지도조회UseCase useCase, CancellationToken ct) => Results.Json(await useCase.공개지역Async(ct)));
app.MapGet("/" + NeighborhoodExchangeMapRoutes.Api, async (string? intent, I생활교류지도조회UseCase useCase, CancellationToken ct) => Json(await useCase.지도Async(new() { Intent = intent }, ct)));
app.MapGet("/" + NeighborhoodExchangeMapRoutes.PostsApi, async (string? intent, string? publicNeighborhoodRegionKey, int? page, I생활교류지도조회UseCase useCase, CancellationToken ct) => Json(await useCase.글목록Async(new() { Intent = intent, PublicNeighborhoodRegionKey = publicNeighborhoodRegionKey }, page ?? 1, NeighborhoodExchange.PageSize, ct)));
app.MapGet("/api/v1/platform/runtime/google-maps", (HttpContext context) => {
    // 같은 루프백 origin에서 제공한 WASM의 GET에는 Origin이 없을 수 있습니다.
    // 검토 서버에서만 해당 페이지의 same-origin Referer를 허용하며 운영 controller 정책은 바꾸지 않습니다.
    var localBrowser = context.Request.Headers["Sec-Fetch-Site"] == "same-origin"
        && Uri.TryCreate(context.Request.Headers.Referer, UriKind.Absolute, out var referer)
        && referer.GetLeftPart(UriPartial.Authority) == previewOrigin;
    if (!(mapPreview || collaborationPreview) || context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip)
        || (context.Request.Headers.Origin != previewOrigin && !localBrowser)) return Results.NotFound();
    context.Response.Headers.CacheControl = "no-store";
    var key = builder.Configuration["GoogleMaps:BrowserApiKey"];
    return string.IsNullOrWhiteSpace(key) ? Results.NoContent() : Results.Json(new Ssalddel.Contracts.Common.Platform.GoogleMapsBrowserRuntimeResponse { BrowserApiKey = key, AllowedOrigins = [previewOrigin] });
});
app.MapGet("/api/v1/community/posts", async (string? roleTag, int? page, 커뮤니티게시글조회UseCase useCase, CancellationToken ct)
    => Json(await useCase.목록Async("platform", PlatformCommunityPostCategories.General, null, NeighborhoodExchange.WorkflowTag,
        NeighborhoodExchange.IsIntent(roleTag) ? roleTag : null, page ?? 1, NeighborhoodExchange.PageSize, ct)));
app.MapGet("/api/v1/community/posts/{id:long}", async (long id, 커뮤니티게시글조회UseCase useCase, CancellationToken ct) => Json(await useCase.상세Async(id, ct)));
app.MapPost("/api/v1/community/posts", async (PlatformCommunityPostCreateRequest request, 커뮤니티게시글발행UseCase useCase, CancellationToken ct)
    => request.WorkflowTag == NeighborhoodExchange.WorkflowTag && NeighborhoodExchange.IsIntent(request.RoleTag)
        && request.Category == PlatformCommunityPostCategories.General && request.커뮤니티원장Id is null
        && request.SalesOffer is null && !request.IsReportBoardPost
        ? Json(await useCase.생성Async(request, ct)) : Results.BadRequest());
app.MapDelete("/api/v1/community/posts/{id:long}", async (long id, [FromBody] PlatformCommunityPostPasswordRequest request, 커뮤니티게시글발행UseCase useCase, CancellationToken ct) => Empty(await useCase.삭제Async(id, request, ct)));
app.MapGet("/api/v1/community/posts/{id:long}/comments", async (long id, 커뮤니티게시글참여UseCase useCase, CancellationToken ct) => Json(await useCase.댓글목록Async(id, ct)));
app.MapPost("/api/v1/community/posts/{id:long}/comments", async (long id, PlatformCommunityPostCommentCreateRequest request, 커뮤니티게시글참여UseCase useCase, CancellationToken ct) => Json(await useCase.댓글작성Async(id, request, ct)));
app.MapDelete("/api/v1/community/posts/{id:long}/comments/{commentId:long}", async (long id, long commentId, [FromBody] PlatformCommunityPostPasswordRequest request, 커뮤니티게시글참여UseCase useCase, CancellationToken ct) => Empty(await useCase.댓글삭제Async(id, commentId, request, ct)));
app.MapPost("/api/v1/community/posts/comments/{commentId:long}/reports", async (long commentId, 커뮤니티게시글운영UseCase useCase, CancellationToken ct) => Empty(await useCase.댓글신고Async(commentId, ct)));
if (collaborationPreview) app.MapCollaborationPreview();
if (flexiblePreview) app.MapFlexibleDelivery();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static IResult Json<T>(Result<T> result) => result.IsSuccess ? Results.Json(result.Value) : Failure(result.Errors);
static IResult Empty(Result result) => result.IsSuccess ? Results.NoContent() : Failure(result.Errors);
static IResult Failure(IReadOnlyList<IError> errors)
{
    var status = errors.FirstOrDefault()?.Metadata.GetValueOrDefault("StatusCode") as int? ?? 400;
    return Results.Problem(statusCode: status, detail: errors.FirstOrDefault()?.Message ?? "요청을 처리하지 못했습니다.");
}
