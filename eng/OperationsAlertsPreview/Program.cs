using OperationsAlertsPreview;
var builder=WebApplication.CreateBuilder(args);
if (!builder.Environment.IsDevelopment()) throw new InvalidOperationException("Local review host only.");
builder.WebHost.UseUrls("http://127.0.0.1:5417").UseStaticWebAssets();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
var app=builder.Build();
app.Use(async(context,next)=> { if(context.Connection.RemoteIpAddress is not {} ip || !System.Net.IPAddress.IsLoopback(ip)) { context.Response.StatusCode=404;return; } await next(); });
app.UseStaticFiles();app.UseAntiforgery();app.MapStaticAssets();app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
