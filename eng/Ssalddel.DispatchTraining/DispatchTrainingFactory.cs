using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Ssalddel.Application.Shipper.Request;
using Ssalddel.Contracts.Admin.DispatchTraining;
using Ssalddel.Controllers.Shipper.Request01;
using 살뜰.Services.Dispatch.Queue;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.External.Google;
using 살뜰.Services.External.Opinet;
using 살뜰.도메인.공통;
using 살뜰.도메인.사용자;
using 살뜰.Data;

namespace Ssalddel.DispatchTraining;

internal sealed class DispatchTrainingFactory : WebApplicationFactory<화주운송의뢰Controller>
{
    private readonly DispatchTrainingSettings settings;
    internal DispatchTrainingFactory(DispatchTrainingSettings settings)
    {
        this.settings = settings;
        UseKestrel(o => o.Listen(IPAddress.Loopback, DispatchTrainingSettings.HostPort));
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development").UseContentRoot(Path.Combine(settings.ArtifactRoot, "host-content"));
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config.AddJsonFile(Path.Combine(settings.RepositoryRoot, "Ssalddel", "appsettings.json"));
            config.AddInMemoryCollection(settings.Configuration);
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.AddSingleton(settings);
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<DispatchTrainingReadiness>();
            services.AddSingleton<DispatchTrainingTraceCollector>();
            services.AddSingleton<I용달운송후보검토Observer>(s => s.GetRequiredService<DispatchTrainingTraceCollector>());
            services.AddSingleton(new DispatchTrainingSessionStore(Path.Combine(settings.ArtifactRoot, "session.json")));
            services.AddSingleton<IDispatchTrainingActions, DispatchTrainingActions>();
            services.AddSingleton<DispatchTrainingCoordinator>();
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, DispatchTrainingHttpGuard>();
            services.AddTransient<IStartupFilter, DispatchTrainingControl>();
            services.Replace(ServiceDescriptor.Scoped<IGeocodingService, DispatchTrainingGeocoding>());
            services.Replace(ServiceDescriptor.Scoped<I화주운송기준운임Service, DispatchTrainingFareService>());
            services.Replace(ServiceDescriptor.Scoped<I배차추천경로Service, DispatchTrainingFixtureRoutes>());
            services.Replace(ServiceDescriptor.Singleton<IOpinetAveragePriceService, DispatchTrainingFuelPrice>());
            services.AddHostedService<DispatchTrainingWorker>();
        });
    }
}

internal sealed class DispatchTrainingReadiness { internal bool Ready; }

/// <summary>추천 API의 비용 표시에만 사용하는 합성 유가. Opinet 인증·외부 호출·실제 시세와 분리한다.</summary>
internal sealed class DispatchTrainingFuelPrice : IOpinetAveragePriceService
{
    public Task<IReadOnlyList<OpinetAveragePriceItem>> GetAveragePricesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<OpinetAveragePriceItem>>(
            [new() { ProductCode = "DispatchTrainingSynthetic", ProductName = "합성 경유", Price = 1700m }]);
    }
}

internal sealed class DispatchTrainingWorker(IServiceProvider services, DispatchTrainingReadiness ready) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!ready.Ready) continue;
            try { await services.GetRequiredService<DispatchTrainingCoordinator>().TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e)
            {
                // Never log token-bearing HTTP payloads or connection strings.
                Console.Error.WriteLine("DispatchTrainingTickFailed:" + e.GetType().Name);
            }
        }
    }
}

internal sealed class DispatchTrainingTraceCollector : I용달운송후보검토Observer
{
    private readonly ConcurrentQueue<(string RequestId, 용달운송후보검토결과 Review)> traces = new();
    public void OnReviewed(string requestId, 용달운송후보검토결과 trace) => traces.Enqueue((requestId, trace));
    internal bool TryRead(out (string RequestId, 용달운송후보검토결과 Review) result) => traces.TryDequeue(out result);
}

internal sealed class DispatchTrainingControl(DispatchTrainingReadiness ready, DispatchTrainingSettings settings) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            const string root = "/verification/dispatch-training";
            if (!context.Request.Path.StartsWithSegments(root)) { await nextMiddleware(context); return; }
            context.Response.Headers.CacheControl = "private, no-store";
            if (context.Connection.RemoteIpAddress is not { } ip || !IPAddress.IsLoopback(ip))
            { context.Response.StatusCode = 403; return; }
            if (context.Request.Path == root + "/capability" && HttpMethods.IsGet(context.Request.Method))
            { await context.Response.WriteAsJsonAsync(new DispatchTrainingCapabilityDto { Enabled = ready.Ready }); return; }
            var authentication = await context.AuthenticateAsync("Bearer");
            if (!authentication.Succeeded) { context.Response.StatusCode = 401; return; }
            context.User = authentication.Principal!;
            if (!context.User.IsInRole(역할명.서버관리자)
                || context.User.FindFirstValue(ClaimTypes.NameIdentifier) != DispatchTrainingSeed.AdminId)
            { context.Response.StatusCode = 403; return; }
            if (!ready.Ready) { context.Response.StatusCode = 503; return; }
            using var scope = context.RequestServices.CreateScope();
            settings.Validate(scope.ServiceProvider.GetRequiredService<IConfiguration>(), scope.ServiceProvider.GetRequiredService<IHostEnvironment>());
            var coordinator = context.RequestServices.GetRequiredService<DispatchTrainingCoordinator>();
            var path = context.Request.Path.Value![root.Length..];
            try
            {
                object? result = HttpMethods.IsGet(context.Request.Method) ? path switch
                {
                    "/current" => await coordinator.ReadAsync(context.RequestAborted),
                    "/decisions" => await coordinator.DecisionsAsync(context.RequestAborted),
                    "/incidents" => await coordinator.IncidentsAsync(context.RequestAborted),
                    _ => null
                } : null;
                if (HttpMethods.IsPost(context.Request.Method))
                {
                    if (context.Request.ContentLength is > 8192) { context.Response.StatusCode = 413; return; }
                    if (path == "/support-drivers")
                    {
                        var input = await context.Request.ReadFromJsonAsync<DispatchTrainingSupportDriversRequest>(context.RequestAborted);
                        if (input is null) { context.Response.StatusCode = 400; return; }
                        result = await coordinator.SupportDriversAsync(input, context.RequestAborted);
                    }
                    else if (path is "/start" or "/pause" or "/resume" or "/stop")
                    {
                        var input = await context.Request.ReadFromJsonAsync<DispatchTrainingCommandRequest>(context.RequestAborted);
                        if (input is null) { context.Response.StatusCode = 400; return; }
                        result = await coordinator.CommandAsync(path[1..], input, context.RequestAborted);
                    }
                }
                if (result is null) { context.Response.StatusCode = 404; return; }
                await context.Response.WriteAsJsonAsync(result, context.RequestAborted);
            }
            catch (System.Text.Json.JsonException) { context.Response.StatusCode = 400; }
            catch (DispatchTrainingControlException e)
            {
                context.Response.StatusCode = e.StatusCode;
                await context.Response.WriteAsJsonAsync(new { code = e.Code, message = "현재 세션과 요청 번호를 다시 확인해 주세요." });
            }
            catch (InvalidOperationException)
            {
                context.Response.StatusCode = 409;
                await context.Response.WriteAsJsonAsync(new { code = "DispatchTraining.Conflict", message = "현재 세션을 다시 확인해 주세요." });
            }
        });
        next(app);
    };
}

internal sealed class DispatchTrainingHttpGuard(DispatchTrainingSettings settings) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        if (builder.PrimaryHandler is HttpClientHandler client) { client.AllowAutoRedirect = false; client.UseProxy = false; }
        if (builder.PrimaryHandler is SocketsHttpHandler sockets) { sockets.AllowAutoRedirect = false; sockets.UseProxy = false; }
        builder.AdditionalHandlers.Insert(0, new Guard(settings.Configuration.GetValueOrDefault("CargoDispatchOllama:Enabled") == "true"));
    };
    internal static bool Allowed(HttpRequestMessage request, bool cargoCandidateAI)
    {
        if (request.RequestUri is not { IsAbsoluteUri: true }) return false;
        return request.RequestUri is { Scheme: "http", IsLoopback: true, Port: DispatchTrainingSettings.HostPort }
            || cargoCandidateAI && request.Method == HttpMethod.Post
                && request.RequestUri is { Scheme: "http", Host: "127.0.0.1", Port: 12681,
                    AbsolutePath: "/api/chat", Query: "", Fragment: "", UserInfo: "" };
    }

    private sealed class Guard(bool cargoCandidateAI) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!Allowed(request, cargoCandidateAI))
                throw new HttpRequestException("DispatchTrainingExternalHttpDisabled");
            return base.SendAsync(request, ct);
        }
    }
}
