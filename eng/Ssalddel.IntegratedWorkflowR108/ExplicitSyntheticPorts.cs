using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using 살뜰.Services.Dispatch.Recommendation;
using 살뜰.Services.Storage.Local;
using 살뜰.Services.Notifications;
using 살뜰.도메인.기사;

internal static class ExplicitSyntheticPorts
{
    internal static void AddR108ExplicitPorts(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<I배차추천경로Service, R108MemberFixtureRoutes>());
        services.Replace(ServiceDescriptor.Singleton<IDriverPushTokenStore, R108SyntheticPushTokens>());
        services.Replace(ServiceDescriptor.Singleton<IFcmPushService, R108LocalPushTransport>());
    }
}

/// <summary>r46의 명시적 제공자 관문 fixture. IsActual=true는 합성 분기 입력이며 실제 도로 증거가 아니다.</summary>
internal sealed class R108MemberFixtureRoutes : I배차추천경로Service
{
    private readonly UsbR67SyntheticRoutes fallback = new();
    private static bool CargoPickup(배차경로좌표? p) => p is { Latitude: 37.588m, Longitude: 127.085m };
    private static bool CargoDropoff(배차경로좌표? p) => p is { Latitude: 37.590m, Longitude: 127.087m };
    private static bool DeclaredCargoPair(배차경로좌표? a, 배차경로좌표? b)
        => CargoPickup(a) && (CargoPickup(b) || CargoDropoff(b));
    private static 배차경로예상결과 CargoGuardFixture(배차경로좌표? destination)
        // IsActual=true는 이 전용 호스트의 제공자 관문 입력이다. 실제 도로/GPS 확인이 아니다.
        => new(CargoPickup(destination) ? 0m : 2.5m,
            CargoPickup(destination) ? TimeSpan.Zero : TimeSpan.FromMinutes(2), 0m,
            "R108DeclaredCargoProviderGuardEmulated_NotActualRoad", true) { RouteOption = "trafast" };
    private static bool Point(배차경로좌표? p) => p is not null &&
        ((p.Latitude == 37.571m && p.Longitude == 127.071m) || (p.Latitude == 37.58m && p.Longitude == 127.08m));
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string option, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (DeclaredCargoPair(origin, destination) && option == "trafast")
            return Task.FromResult<배차경로예상결과?>(CargoGuardFixture(destination));
        if (Point(origin) && Point(destination) && option == "traavoidcar")
            return Task.FromResult<배차경로예상결과?>(new(2.5m, TimeSpan.FromMinutes(2), 0m,
                "R108ExplicitSyntheticProviderGuard_NotActualRoad", true) { RouteOption = option });
        return fallback.EstimateRouteAsync(origin, destination, option, ct);
    }
    public Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? a, 배차경로좌표? b)
        => DeclaredCargoPair(a, b) ? Task.FromResult<배차경로예상결과?>(CargoGuardFixture(b)) : fallback.EstimateRouteAsync(a,b);
    public Task<배차경로좌표?> ResolveOriginLocationAsync(string id, 용달기사? d, DriverLocationSnapshot? l, 배차추천검색조건? c) => fallback.ResolveOriginLocationAsync(id,d,l,c);
    public Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string id, 용달기사? d, DriverLocationSnapshot? l) => fallback.ResolveRouteAnchorLocationAsync(id,d,l);
    public Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? a,IReadOnlyList<배차경로좌표> b,CancellationToken ct=default) => fallback.EstimateOrderedRouteAsync(a,b,ct);
    public Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? a,배차경로좌표? b,배차경로좌표? c,배차경로좌표? d) => fallback.EstimateInsertionDelayAsync(a,b,c,d);
    public decimal? CalculateDistanceKm(배차경로좌표 a,배차경로좌표 b) => fallback.CalculateDistanceKm(a,b);
}

internal sealed class R108SyntheticPushTokens : IDriverPushTokenStore
{
    public Task SetAsync(string id,string token,CancellationToken ct=default) => throw new NotSupportedException("NoRealDeviceRegistration");
    public Task ClearAsync(string id,CancellationToken ct=default) => Task.CompletedTask;
    public Task<string?> GetAsync(string id,CancellationToken ct=default) => Task.FromResult<string?>("r108-local-synthetic-token");
    public Task<string?> GetForAppAsync(string id,string app,CancellationToken ct=default) => GetAsync(id,ct);
}

/// <summary>제품 Outbox의 실제 HTTP 제공자 포트만 대체한다. Google 수신/단말/청취 증거가 아니다.</summary>
internal sealed class R108LocalPushTransport : IFcmPushService
{
    private readonly object gate = new();
    public Task<bool> SendAsync(FcmPushMessage message,CancellationToken ct=default) => Record(message.Data,ct);
    public Task<bool> SendToTokenAsync(string token,string title,string body,IReadOnlyDictionary<string,string> data,CancellationToken ct=default) => Record(data,ct);
    private Task<bool> Record(IReadOnlyDictionary<string,string> data,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var settings = CargoJourneySettings.Load();
        string? Field(string key) => data.TryGetValue(key,out var value) ? value : null;
        var row = System.Text.Json.JsonSerializer.Serialize(new { type=Field("type"),appKey=Field("appKey"),
            requestId=Field("requestId"),offerId=Field("offerId"),atUtc=DateTime.UtcNow,
            transport="ExplicitLocalFake",googleFcmSent=false,deviceReceived=false });
        lock(gate) File.AppendAllText(Path.Combine(settings.ArtifactRoot,"local-notification-observations.jsonl"),row+Environment.NewLine);
        return Task.FromResult(true);
    }
}
