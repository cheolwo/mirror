using MudBlazor.Services;
using RestaurantDeskApp;
using RestaurantDeskApp.Options;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.Services;

var builder = WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment())
    throw new InvalidOperationException("음식점 POS 미리보기는 Development에서만 실행합니다.");
builder.WebHost.UseUrls("http://127.0.0.1:5371").UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddSsalddelDocumentOutputServices();
builder.Services.AddHttpClient();
builder.Services.AddScoped(_ =>
{
    var state = new 역할앱생명주기State();
    state.초기화("브라우저", connected: true);
    state.전환(역할앱생명주기단계.활성);
    return state;
});
builder.Services.AddScoped<ISsalddelJsonApiClient, Pos미리보기미연결Api>();
builder.Services.Configure<RestaurantDeskOptions>(options =>
{
    options.RestaurantId = 66001;
    options.RestaurantName = "살뜰 식당";
    options.RestaurantAddress = "화면 검토용 장소";
});
builder.Services.AddScoped(_ => Pos미리보기인증.CreateSession());
builder.Services.AddScoped(sp => Pos미리보기인증.CreateAuth(sp.GetRequiredService<ClientAuthSession>()));
builder.Services.AddScoped<Pos미리보기Realtime>();
builder.Services.AddScoped<I음식점주문SignalRClientService>(sp => sp.GetRequiredService<Pos미리보기Realtime>());
builder.Services.AddScoped<Pos미리보기자료>();
builder.Services.AddScoped<I음식점주문DeskService, Pos미리보기Desk>();
builder.Services.AddScoped<I음식주문ApiClient, Pos미리보기Api>();
builder.Services.AddScoped<IRestaurantOrderNotificationPlatform, NoRestaurantOrderNotifications>();
builder.Services.AddScoped<I주문알림Service, Pos미리보기무음>();
builder.Services.AddScoped<IRestaurantReceiptPrinter, Pos미리보기프린터>();
builder.Services.AddScoped<RestaurantOrderNotificationCoordinator>();
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is not { } ip || !System.Net.IPAddress.IsLoopback(ip))
    { context.Response.StatusCode = 404; return; }
    await next();
});
app.UseStaticFiles();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/app.css", () => Results.File(Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath,
    "../../RestaurantDeskApp/wwwroot/app.css")), "text/css"));
app.MapGet("/preview-source", () => Results.Json(new
{
    synthetic = true,
    externalEffects = false,
    componentSources = new[] { "RestaurantDeskApp/Components/Pages/OrderInbox.razor",
        "RestaurantDeskApp/Components/Pages/OrderDetail.razor", "RestaurantDeskApp/Components/Pages/OrderDetail.razor.cs",
        "RestaurantDeskApp/Services/RestaurantPosPresentation.cs" }
}));
// IAB 창 크기를 바꾸지 않고 실제 좁은 viewport의 CSS를 확인하는 합성 검토 shell.
// 임의 폭·URL·원본 경로는 받지 않고 이 호스트의 /orders만 연다.
app.MapGet("/viewport/{width:int}", (int width) => width is 320 or 390
    ? Results.Content($"""
        <!DOCTYPE html><html lang="ko"><head><meta charset="utf-8"><title>음식점 {width}px 화면 검토</title></head>
        <body style="margin:0;padding:16px;background:#e8edf2;font-family:system-ui,sans-serif">
        <p style="margin:0 0 12px;color:#344454">합성 화면 검토 · {width}px · 실제 휴대폰 아님</p>
        <iframe title="{width}px 음식점 주문 화면" src="/orders" width="{width}" height="840"
        style="display:block;border:1px solid #71838e;background:white"></iframe></body></html>
        """, "text/html; charset=utf-8")
    : Results.NotFound());
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
