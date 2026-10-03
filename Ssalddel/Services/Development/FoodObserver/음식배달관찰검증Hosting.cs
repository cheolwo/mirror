using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Ssalddel.Services.Food;
using 살뜰.Services.Dispatch.Recommendation;

namespace Ssalddel.Services.Development.FoodObserver;

public static class 음식배달관찰검증Hosting
{
    public static void Add음식배달관찰검증(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(음식배달관찰검증Options.Section).Get<음식배달관찰검증Options>() ?? new();
        if (!options.Enabled) return;
        options.Validate(builder.Configuration, builder.Environment);
        // Quartz/수집/지급/외부 알림 작업을 검증 호스트에 올리지 않는다.
        builder.Services.RemoveAll<IHostedService>();
        builder.Services.AddSingleton(options);
        builder.Services.Replace(ServiceDescriptor.Scoped<IKakao좌표변환Service, 검증표본좌표Service>());
        // 실제 경로 API 실패의 대체가 아니다. 검증 경계 통과 후 알려진 합성 경로만 명시적으로 주입한다.
        builder.Services.Replace(ServiceDescriptor.Scoped<I음식배달기사제안요금Service, 검증표본기사제안요금Service>());
        builder.Services.AddSingleton<IHttpMessageHandlerBuilderFilter, 외부요청차단Filter>();
        builder.Services.AddSingleton<음식배달관찰검증Runner>();
        builder.Services.AddHostedService<음식배달관찰배차작업자>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<음식배달관찰검증Runner>());
    }

    public static void Map음식배달관찰검증(this WebApplication app)
    {
        var options = app.Services.GetService<음식배달관찰검증Options>();
        if (options is null) return;
        bool Authorized(HttpContext context) => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(context.Request.Headers["X-Verification-Key"].ToString()),
            Encoding.UTF8.GetBytes(options.AccessKey));
        // 검증 표본 준비만 판정한다. 운영 전체 migration 준비 상태의 대체가 아니다.
        app.MapGet("/verification/health", (음식배달관찰검증Runner runner) =>
            runner.IsReady ? Results.NoContent() : Results.StatusCode(503));
        app.MapGet("/verification/food-delivery", (HttpContext context, 음식배달관찰검증Runner runner) =>
            Authorized(context) ? Results.Ok(runner.Read()) : Results.Unauthorized());
        app.MapPost("/verification/food-delivery/start", (HttpContext context, 음식배달관찰검증Runner runner) =>
            !Authorized(context) ? Results.Unauthorized() : runner.Start() ? Results.Accepted() : Results.Conflict());
    }

    private sealed class 외부요청차단Filter : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => builder =>
        {
            next(builder);
            builder.AdditionalHandlers.Insert(0, new 외부요청차단Handler());
        };
    }

    private sealed class 외부요청차단Handler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { IsLoopback: true, Scheme: "http", Port: 8080 })
                throw new HttpRequestException("External HTTP is disabled in food observer verification.");
            return base.SendAsync(request, cancellationToken);
        }
    }
}

/// <summary>실제 지도 API의 실패 대체가 아니라 검증 시 명시적으로 주입하는 합성 주소 대장.</summary>
public sealed class 검증표본좌표Service : IKakao좌표변환Service
{
    public const string RestaurantAddress = "검증 표본 음식점";
    public const string CustomerAddress = "검증 표본 주택";
    public Task<Kakao주소정보?> 주소정보조회Async(string 주소, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (주소 != RestaurantAddress && 주소 != CustomerAddress)
            throw new ArgumentException("검증 대장에 없는 주소입니다.");
        return Task.FromResult<Kakao주소정보?>(new(주소, 주소, "서울특별시", "중랑구", "면목동",
            37.5880m, 주소 == RestaurantAddress ? 127.0850m : 127.0870m));
    }
    public async Task<(double 위도, double 경도)?> 도로명주소좌표변환Async(string 주소, CancellationToken cancellationToken = default)
    {
        var value = await 주소정보조회Async(주소, cancellationToken);
        return ((double)value!.위도!.Value, (double)value.경도!.Value);
    }
    public Task<Kakao지역정보?> 좌표지역정보조회Async(decimal 위도, decimal 경도, CancellationToken cancellationToken = default)
        => Task.FromResult<Kakao지역정보?>(new("서울특별시", "중랑구", "면목동", "검증 합성 좌표"));
}
