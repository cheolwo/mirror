using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Models;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace NeighborhoodExchangePreview;

// 공개 동네와 글은 실제 HTTP/SQLite, 본인 배송만 명시적으로 구분한 화면 검토용 자료입니다.
internal sealed class PreviewNeighborhoodMapClient(NeighborhoodExchangeMapClient actual) : INeighborhoodExchangeMapClient
{
    public Task<NeighborhoodExchangeRegionListResponse> RegionsAsync(CancellationToken ct) => actual.RegionsAsync(ct);
    public Task<NeighborhoodExchangeMapResponse> MapAsync(string? intent, CancellationToken ct) => actual.MapAsync(intent, ct);
    public Task<PlatformCommunityPostListResponse> PostsAsync(string? region, string? intent, int page, CancellationToken ct) => actual.PostsAsync(region, intent, page, ct);
    public Task<NeighborhoodStorageMapResponse?> StorageAsync(CancellationToken ct) => actual.StorageAsync(ct);
    public Task<NeighborhoodMapDeliveryPage> MineAsync(int page, CancellationToken ct) => Task.FromResult(new NeighborhoodMapDeliveryPage([
        new("preview-assigned", "책 전달 · 화면 검토용", "픽업지 이동 중", DateTime.UtcNow, new(37.5844,127.0983), new(37.5892,127.0778)),
        new("preview-waiting", "생활 물품 · 화면 검토용", "기사 배정 대기", DateTime.UtcNow, new(37.5943,127.084), new(37.5991,127.075))], false));
    public Task<NeighborhoodDeliveryMapResponse?> DeliveryMapAsync(string id, bool includeRoute, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var pickup = new NeighborhoodDeliveryMapPoint { Latitude=37.5844m, Longitude=127.0983m };
        var dropoff = new NeighborhoodDeliveryMapPoint { Latitude=37.5892m, Longitude=127.0778m };
        return Task.FromResult<NeighborhoodDeliveryMapResponse?>(new() {
            RequestId=id, StageCode=NeighborhoodDeliveryMapStages.Pickup, ObservedAtUtc=now, RouteRevision="preview:pickup:round1",
            Pickup=pickup, Dropoff=dropoff, RouteStateCode=NeighborhoodDeliveryMapStates.NotRequested,
            RouteNotice="예정 경로는 실제 배송 API 연결 후 확인합니다. 이 화면의 배송 정보는 검토용입니다.",
            DriverLocationStateCode=NeighborhoodDeliveryMapStates.Available,
            DriverLocationNotice="화면 검토용 최근 위치 · 실제 기사 위치 아님",
            DriverLocation=new() { Latitude=37.582m, Longitude=127.096m, MeasuredAtUtc=now, ReceivedAtUtc=now }
        });
    }
}
