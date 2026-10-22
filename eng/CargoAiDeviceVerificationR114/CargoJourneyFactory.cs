using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Ssalddel.Controllers.Shipper.Request01;

// 기존 r114의 수동 호스트 경계를 유지하며 새 명시 프로필의 모델 HTTP만 추가한다.
internal sealed class CargoJourneyFactory(CargoJourneySettings settings) : WebApplicationFactory<화주운송의뢰Controller>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.Combine(settings.ArtifactRoot, "host-content"));
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddJsonFile(Path.Combine(settings.RepositoryRoot, "Ssalddel", "appsettings.json"), optional: false);
            configuration.AddInMemoryCollection(settings.Configuration);
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.AddSingleton(settings);
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, CargoExternalHttpBlocker>();
            services.AddTransient<IStartupFilter, CargoJourneyControlStartupFilter>();
        });
    }
}

internal sealed class CargoExternalHttpBlocker(CargoJourneySettings settings) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        if (builder.PrimaryHandler is HttpClientHandler client) { client.AllowAutoRedirect = false; client.UseProxy = false; }
        if (builder.PrimaryHandler is SocketsHttpHandler sockets) { sockets.AllowAutoRedirect = false; sockets.UseProxy = false; }
        builder.AdditionalHandlers.Insert(0, new Guard(settings.CargoPrimaryAI));
    };

    internal static bool Allowed(HttpRequestMessage request, bool primaryAI)
    {
        if (request.RequestUri is not { IsAbsoluteUri: true } uri) return false;
        return uri is { IsLoopback: true, Scheme: "http", Port: CargoJourneySettings.HostPort }
            || primaryAI && request.Method == HttpMethod.Post
                && uri is { Scheme: "http", Host: "127.0.0.1", Port: 12681, AbsolutePath: "/api/chat", Query: "", Fragment: "", UserInfo: "" };
    }

    private sealed class Guard(bool primaryAI) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!Allowed(request, primaryAI)) throw new HttpRequestException("CargoJourneyExternalHttpDisabled");
            return base.SendAsync(request, ct);
        }
    }
}
