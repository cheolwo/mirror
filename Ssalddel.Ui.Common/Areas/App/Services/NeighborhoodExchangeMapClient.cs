using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Contracts.Common.PublicData;

namespace Ssalddel.Ui.Common.Areas.App.Services;

public sealed record NeighborhoodMapDeliverySummary(string RequestId, string CargoName, string Stage, DateTime CreatedAtUtc,
    NeighborhoodMapPoint? Pickup = null, NeighborhoodMapPoint? Dropoff = null);
public sealed record NeighborhoodMapDeliveryPage(IReadOnlyList<NeighborhoodMapDeliverySummary> Items, bool HasNext);
public interface INeighborhoodExchangeMapClient
{
    Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct);
    Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct);
    Task<PlatformCommunityPostListResponse> PostsAsync(string? regionKey, string? intent, int page, CancellationToken ct);
    Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct);
    Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string requestId, bool includeRoute, CancellationToken ct);
    Task<RegionalAgriculturalMapMarkerListResponse?> PublicDataAsync(CancellationToken ct) => Task.FromResult<RegionalAgriculturalMapMarkerListResponse?>(null);
    Task<NeighborhoodStorageMapResponse?> StorageAsync(CancellationToken ct) => Task.FromResult<NeighborhoodStorageMapResponse?>(null);
}

public sealed class NeighborhoodExchangeMapClient(ISsalddelJsonApiClient api, INeighborhoodDeliveryClient delivery)
    : INeighborhoodExchangeMapClient
{
    public async Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct)
        => await api.GetAsync<NeighborhoodExchangeRegionListResponse>(NeighborhoodExchangeMapRoutes.RegionsApi,
            "동네 목록", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException("지역 응답 없음");
    public async Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct)
        => await api.GetAsync<NeighborhoodExchangeMapResponse>(NeighborhoodExchangeMapRoutes.Api + Query(null, intent, null),
            "생활 지도", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException("지도 응답 없음");
    public async Task<PlatformCommunityPostListResponse> PostsAsync(string? regionKey, string? intent, int page, CancellationToken ct)
        => await api.GetAsync<PlatformCommunityPostListResponse>(NeighborhoodExchangeMapRoutes.PostsApi + Query(regionKey, intent, page),
            "동네 글 목록", allowNotFound: false, cancellationToken: ct) ?? throw new InvalidOperationException("글 목록 응답 없음");
    public async Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct)
    {
        var result = await delivery.MineAsync(page, ct);
        return new(result.Where(value => value.ProposalStateCode != NeighborhoodDeliveryProposalStates.Closed)
            .Select(value => new NeighborhoodMapDeliverySummary(value.RequestId, value.CargoName,
                ViewModels.NeighborhoodDeliveryPresentation.Stage(value), value.CreatedAtUtc,
                Point(value.Request.픽업위도 ?? value.Request.픽업?.주소.위도, value.Request.픽업경도 ?? value.Request.픽업?.주소.경도),
                Point(value.Request.하차위도 ?? value.Request.하차?.주소.위도, value.Request.하차경도 ?? value.Request.하차?.주소.경도))).ToArray(), result.Count == 20);
    }
    private static NeighborhoodMapPoint? Point(decimal? latitude, decimal? longitude)
        => latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180 ? new((double)latitude.Value, (double)longitude.Value) : null;
    public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string requestId, bool includeRoute, CancellationToken ct)
        => api.GetAsync<NeighborhoodDeliveryMapResponse>($"{NeighborhoodDeliveryRoutes.Api}/{Uri.EscapeDataString(requestId)}/map?includeRoute={includeRoute.ToString().ToLowerInvariant()}",
            "내 배송 지도", cancellationToken: ct);
    public Task<RegionalAgriculturalMapMarkerListResponse?> PublicDataAsync(CancellationToken ct)
        => api.GetAsync<RegionalAgriculturalMapMarkerListResponse>(RegionalAgriculturalMapRoutes.MarkerApi + "?countryCode=KR&maxItems=200", "공공자료 지도", allowNotFound: false, cancellationToken: ct);
    public Task<NeighborhoodStorageMapResponse?> StorageAsync(CancellationToken ct)
        => api.GetAsync<NeighborhoodStorageMapResponse>(NeighborhoodStorageRoutes.MapApi, "동네 보관 지도", allowNotFound: false, cancellationToken: ct);
    private static string Query(string? region, string? intent, int? page)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(region)) values.Add("publicNeighborhoodRegionKey=" + Uri.EscapeDataString(region));
        if (NeighborhoodExchange.IsIntent(intent)) values.Add("intent=" + Uri.EscapeDataString(intent!));
        if (page.HasValue) values.Add("page=" + Math.Clamp(page.Value, 1, 10000));
        return values.Count == 0 ? "" : "?" + string.Join('&', values);
    }
}
