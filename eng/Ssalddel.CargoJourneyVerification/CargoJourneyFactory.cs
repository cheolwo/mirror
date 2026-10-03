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

internal sealed class CargoJourneyFactory : WebApplicationFactory<화주운송의뢰Controller>
{
    private readonly CargoJourneySettings _settings;

    internal CargoJourneyFactory(CargoJourneySettings settings)
    {
        _settings = settings;
        UseKestrel(options => options.Listen(IPAddress.Loopback, CargoJourneySettings.HostPort));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.Combine(_settings.ArtifactRoot, "host-content"));
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.Sources.Clear();
            configuration.AddJsonFile(Path.Combine(_settings.RepositoryRoot, "Ssalddel", "appsettings.json"), optional: false);
            configuration.AddInMemoryCollection(_settings.Configuration);
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            // Quartz, collection, payment and notification workers do not run in this host.
            services.RemoveAll<IHostedService>();
            services.AddSingleton(_settings);
            services.AddSingleton<IHttpMessageHandlerBuilderFilter, CargoExternalHttpBlocker>();
            services.AddTransient<IStartupFilter, CargoJourneyControlStartupFilter>();
        });
    }
}

internal sealed class CargoExternalHttpBlocker : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
        => builder => { next(builder); builder.AdditionalHandlers.Insert(0, new CargoExternalHttpBlockerHandler()); };

    private sealed class CargoExternalHttpBlockerHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { IsLoopback: true, Scheme: "http", Port: CargoJourneySettings.HostPort })
                throw new HttpRequestException("CargoJourneyExternalHttpDisabled");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
