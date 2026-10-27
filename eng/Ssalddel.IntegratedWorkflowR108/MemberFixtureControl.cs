using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Application.CommandProcessing;

// Only the explicit loopback fixture uses an AsyncLocal identity override.
// All product HTTP requests continue to read their real validated JWT claims.
internal sealed class HarnessActor(IHttpContextAccessor http) : ICurrentUserAccessor
{
    private readonly AsyncLocal<string?> current = new();
    public string? UserId { get => current.Value ?? http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.HttpContext?.User.FindFirstValue("sub"); set => current.Value = value; }
    public string? Role => current.Value is not null ? "커뮤니티회원" : http.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
}
internal sealed class HarnessFailure(string code) : Exception(code) { internal string Code { get; } = code; }
internal static class Entry
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static readonly AsyncLocal<string?> stage = new();
    internal static string? CurrentStage => stage.Value;
    internal static void Stage(string value) { stage.Value = value; }
    internal static Task Save(string path, object data) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(data, Json));
}
internal sealed class MemberFixtureControl(CargoJourneySettings settings) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Path != "/verification/member/prepare") { await nextMiddleware(context); return; }
            var supplied = context.Request.Headers["x-r108-key"].ToString();
            if (!HttpMethods.IsPost(context.Request.Method) || context.Connection.RemoteIpAddress is not { } remote || !IPAddress.IsLoopback(remote)
                || context.Request.Host.Port != CargoJourneySettings.HostPort || context.Request.ContentLength is > 1024
                || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(settings.AccessKey)))
            { context.Response.StatusCode = 401; return; }
            var input = await context.Request.ReadFromJsonAsync<PrepareInput>(context.RequestAborted);
            if (input is null || !System.Text.RegularExpressions.Regex.IsMatch(input.Case, "^[a-z0-9][a-z0-9-]{0,47}$"))
            { context.Response.StatusCode = 400; return; }
            var actor = context.RequestServices.GetRequiredService<HarnessActor>();
            try
            {
                await new MemberCaseCli(context.RequestServices, settings, actor).Prepare(input.Case, true, context.RequestAborted);
                var path = Path.Combine(settings.ArtifactRoot, ".private", "cases", input.Case, "prepared.json");
                using var prepared = JsonDocument.Parse(await File.ReadAllTextAsync(path, context.RequestAborted));
                await context.Response.WriteAsJsonAsync(prepared.RootElement, context.RequestAborted);
            }
            catch (Exception ex)
            {
                var diagnostics = new List<object>();
                for (Exception? item = ex; item is not null && diagnostics.Count < 6; item = item.InnerException)
                    diagnostics.Add(new { type = item.GetType().FullName,
                        socketError = item is System.Net.Sockets.SocketException socket ? socket.SocketErrorCode.ToString() : null,
                        methods = new System.Diagnostics.StackTrace(item, false).GetFrames()?.Select(frame => frame.GetMethod())
                            .Where(method => method is not null).Select(method => method!.DeclaringType?.FullName + "." + method.Name).Distinct().Take(16).ToArray() });
                await Entry.Save(Path.Combine(settings.ArtifactRoot, ".private", "cases", input.Case, "failure-safe.json"),
                    new { code = ex.GetType().Name, stage = Entry.CurrentStage, diagnostics, exceptionMessagesRecorded = false });
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { code = ex is HarnessFailure f ? f.Code : ex.GetType().Name, stage = Entry.CurrentStage ?? "PrepareMemberFixture", externalEffects = false }, context.RequestAborted);
            }
            finally { actor.UserId = null; }
        });
        next(app);
    };
    private sealed record PrepareInput(string Case);
}
