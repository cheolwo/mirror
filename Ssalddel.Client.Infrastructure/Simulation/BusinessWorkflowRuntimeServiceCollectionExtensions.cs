using System.Linq;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.BusinessWorkflow;
using Ssalddel.Simulation.Infrastructure;
using Ssalddel.WorkflowRules;

namespace Ssalddel.Client.Infrastructure.Simulation;

/// <summary>
/// 웹·모바일의 DI와 개발용 로컬 호스트가 같은 업무 흐름 포트를 선택하도록 한다.
/// Local과 Remote를 동시에 등록하거나 실패 시 자동 전환하지 않는다.
/// </summary>
public static class BusinessWorkflowRuntimeServiceCollectionExtensions
{
    public const string RemoteSimulationHttpClientName = "Ssalddel.Simulation.Api";

    /// <summary>
    /// 단일 살뜰 서버 주소에 Simulation 논리 client를 등록합니다.
    /// </summary>
    public static IServiceCollection AddSsalddelSimulationApiHttpClient(
        this IServiceCollection services,
        Uri simulationApiBaseAddress,
        TimeSpan? timeout = null)
        => AddSsalddelSimulationApiHttpClientCore(
            services,
            simulationApiBaseAddress,
            accessTokenFactory: null,
            timeout);

    /// <summary>
    /// 단일 살뜰 서버 주소와 같은 로그인 토큰을 사용하는 Simulation 논리
    /// client를 등록합니다. 토큰은 요청마다 새 scope에서 다시 조회합니다.
    /// </summary>
    public static IServiceCollection AddSsalddelSimulationApiHttpClient(
        this IServiceCollection services,
        Uri simulationApiBaseAddress,
        Func<IServiceProvider, string?> accessTokenFactory,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(accessTokenFactory);
        return AddSsalddelSimulationApiHttpClientCore(
            services,
            simulationApiBaseAddress,
            accessTokenFactory,
            timeout);
    }

    private static IServiceCollection AddSsalddelSimulationApiHttpClientCore(
        IServiceCollection services,
        Uri simulationApiBaseAddress,
        Func<IServiceProvider, string?>? accessTokenFactory,
        TimeSpan? timeout)
    {
        ArgumentNullException.ThrowIfNull(services);
        var normalized = SsalddelSimulationApiEndpoint.NormalizeBaseAddress(
            simulationApiBaseAddress);

        if (timeout is { } value
            && value != Timeout.InfiniteTimeSpan
            && value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(RemoteSimulationApiRegistration)))
        {
            throw new InvalidOperationException(
                "원격 Simulation API 주소가 이미 등록되어 있습니다.");
        }

        services.AddSingleton(new RemoteSimulationApiRegistration(normalized));
        var clientBuilder = services.AddHttpClient(
            RemoteSimulationHttpClientName,
            client =>
            {
                client.BaseAddress = normalized;
                if (timeout is { } configuredTimeout)
                {
                    client.Timeout = configuredTimeout;
                }
            });
        if (accessTokenFactory is not null)
        {
            clientBuilder.AddHttpMessageHandler(provider =>
                new SsalddelSimulationBearerTokenHandler(
                    provider.GetRequiredService<IServiceScopeFactory>(),
                    accessTokenFactory));
        }
        return services;
    }

    public static IServiceCollection AddRemoteSimulationBusinessWorkflowRuntime(
        this IServiceCollection services,
        Uri simulationApiBaseAddress,
        TimeSpan? timeout = null)
    {
        services.AddSsalddelSimulationApiHttpClient(
            simulationApiBaseAddress,
            timeout);
        return services.AddRemoteSimulationBusinessWorkflowRuntime();
    }

    public static IServiceCollection AddRemoteSimulationBusinessWorkflowRuntime(
        this IServiceCollection services,
        Uri simulationApiBaseAddress,
        Func<IServiceProvider, string?> accessTokenFactory,
        TimeSpan? timeout = null)
    {
        services.AddSsalddelSimulationApiHttpClient(
            simulationApiBaseAddress,
            accessTokenFactory,
            timeout);
        return services.AddRemoteSimulationBusinessWorkflowRuntime();
    }

    /// <summary>
    /// 앞서 명시적으로 등록한 원격 Simulation Client만 사용합니다.
    /// </summary>
    public static IServiceCollection AddRemoteSimulationBusinessWorkflowRuntime(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureNotRegistered(services);
        EnsureRemoteSimulationClientRegistered(services);

        services.AddScoped<IBusinessWorkflowRuntime>(provider =>
            RemoteBusinessWorkflowRuntimeFactory.Create(
                provider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(RemoteSimulationHttpClientName)));
        return AddPorts(services);
    }

    /// <summary>
    /// 시험 또는 호스트별 전송 정책처럼 조립자가 Simulation 전용 Client를
    /// 직접 제공해야 하는 경우에 사용합니다.
    /// </summary>
    public static IServiceCollection AddRemoteSimulationBusinessWorkflowRuntime(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient> simulationClientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(simulationClientFactory);
        EnsureNotRegistered(services);

        services.AddScoped<IBusinessWorkflowRuntime>(provider =>
            RemoteBusinessWorkflowRuntimeFactory.Create(
                ValidateSimulationClient(simulationClientFactory(provider))));
        return AddPorts(services);
    }

    [Obsolete(
        "Use AddRemoteSimulationBusinessWorkflowRuntime after registering the Simulation API explicitly.")]
    public static IServiceCollection AddRemoteBusinessWorkflowRuntime(
        this IServiceCollection services)
        => services.AddRemoteSimulationBusinessWorkflowRuntime();

    public static IServiceCollection AddLocalBusinessWorkflowRuntime(
        this IServiceCollection services,
        Func<IServiceProvider, LocalSimulationRuntime> runtimeFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(runtimeFactory);
        EnsureNotRegistered(services);
        if (services.Any(descriptor =>
                descriptor.ServiceType == typeof(LocalSimulationRuntime)))
            throw new InvalidOperationException(
                "LocalSimulationRuntime이 이미 등록되어 있습니다.");

        services.Add(ServiceDescriptor.Scoped(
            typeof(LocalSimulationRuntime),
            provider => runtimeFactory(provider)));
        services.AddScoped<IBusinessWorkflowRuntime>(provider =>
            LocalBusinessWorkflowRuntimeFactory.Create(
                provider.GetRequiredService<LocalSimulationRuntime>(),
                BusinessWorkflowRuleEngine.기본));
        return AddPorts(services);
    }

    private static IServiceCollection AddPorts(IServiceCollection services)
    {
        services.AddScoped<I주문업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Orders);
        services.AddScoped<I음식점업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Restaurants);
        services.AddScoped<I배차업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Dispatch);
        services.AddScoped<I배송업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Delivery);
        services.AddScoped<I창고업무Runtime>(provider =>
            provider.GetRequiredService<IBusinessWorkflowRuntime>().Warehouse);
        return services;
    }

    private static void EnsureNotRegistered(IServiceCollection services)
    {
        var serviceTypes = new[]
        {
            typeof(IBusinessWorkflowRuntime),
            typeof(I주문업무Runtime),
            typeof(I음식점업무Runtime),
            typeof(I배차업무Runtime),
            typeof(I배송업무Runtime),
            typeof(I창고업무Runtime),
        };
        var duplicate = services.FirstOrDefault(descriptor =>
            serviceTypes.Contains(descriptor.ServiceType));
        if (duplicate != null)
            throw new InvalidOperationException(
                $"업무 흐름 Runtime 서비스가 이미 등록되어 있습니다: "
                + duplicate.ServiceType.Name);
    }

    private static void EnsureRemoteSimulationClientRegistered(
        IServiceCollection services)
    {
        if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(RemoteSimulationApiRegistration)))
        {
            throw new InvalidOperationException(
                "원격 Simulation 업무 Runtime을 사용하려면 "
                + "AddSsalddelSimulationApiHttpClient로 Simulation 주소를 먼저 등록해야 합니다.");
        }
    }

    private static HttpClient ValidateSimulationClient(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (client.BaseAddress is null)
        {
            throw new InvalidOperationException(
                "원격 Simulation HttpClient에는 BaseAddress가 필요합니다.");
        }

        SsalddelSimulationApiEndpoint.NormalizeBaseAddress(client.BaseAddress);
        return client;
    }

    private sealed record RemoteSimulationApiRegistration(Uri BaseAddress);

    private sealed class SsalddelSimulationBearerTokenHandler(
        IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, string?> accessTokenFactory)
        : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var scope = scopeFactory.CreateScope();
            var token = accessTokenFactory(scope.ServiceProvider)?.Trim();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
