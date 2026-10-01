using HongdalApp.Options;
using HongdalApp.Services.Commerce.Coupang;
using HongdalApp.Services.Commerce.Naver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HongdalApp.Services;

internal static class ShipperExternalApiModule
{
    internal static IServiceCollection AddShipperExternalApiModule(this IServiceCollection services)
    {
        services.AddScoped<배차주소ApiService>();
        services.AddSingleton<INaverCommerceSignatureGenerator, BCryptNaverCommerceSignatureGenerator>();
        services.AddHttpClient<INaverCommerceTokenProvider, NaverCommerceTokenProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<NaverCommerceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddHttpClient<INaverSmartStoreProductClient, NaverSmartStoreProductClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<NaverCommerceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        services.AddSingleton<ICoupangWingSignatureGenerator, HmacCoupangWingSignatureGenerator>();
        services.AddHttpClient<ICoupangWingProductClient, CoupangWingProductClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<CoupangWingOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
        });
        return services;
    }
}
