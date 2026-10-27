using Microsoft.AspNetCore.SignalR;
using Ssalddel.Hubs;
using 살뜰.도메인.기사;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public interface I배차추천Service
    {
        Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string driverId);
        Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string driverId, 배차추천검색조건? criteria);
        Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null);
        Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null);
        Task SendToDriverAsync(string driverId);
    }

    public sealed record 배차추천검색조건(decimal Latitude, decimal Longitude, decimal RadiusKm);

    public abstract class 배차추천Service
    {
        protected readonly SsalddelContext _db;
        protected readonly IDriverLocationStore _driverLocationStore;
        protected readonly IDriverRejectedRequestStore _rejectedRequestStore;
        protected readonly IDriverRecommendationPushService _pushService;
        protected readonly IDispatchRecommendationLogStore _logStore;
        protected readonly IHubContext<DispatchRecommendationHub> _hubContext;
        protected readonly I배차추천경로Service _routeService;
        protected readonly I배차추천판정Service _판정Service;
        protected readonly I배차추천평가Service _평가Service;
        protected readonly I기사운송일정구성Service _기사운송일정구성Service;
        protected readonly I운송일정삽입평가Service _운송일정삽입평가Service;
        protected readonly IOpinetAveragePriceService _averagePriceService;
        private readonly 배차추천복귀지Reader _복귀지Reader;

        protected const string 복귀지출처_오늘복귀지 = 배차추천복귀지Reader.오늘복귀지출처;
        protected const string 복귀지출처_기본복귀지 = 배차추천복귀지Reader.기본복귀지출처;
        protected const string 복귀지출처_없음 = 배차추천복귀지Reader.복귀지없음출처;

        protected 배차추천Service(
            SsalddelContext db,
            IDriverLocationStore driverLocationStore,
            IDriverRejectedRequestStore rejectedRequestStore,
            IDriverRecommendationPushService pushService,
            IDispatchRecommendationLogStore logStore,
            IHubContext<DispatchRecommendationHub> hubContext,
            I배차추천경로Service routeService,
            I배차추천판정Service 판정Service,
            I배차추천평가Service 평가Service,
            I기사운송일정구성Service 기사운송일정구성Service,
            I운송일정삽입평가Service 운송일정삽입평가Service,
            IOpinetAveragePriceService averagePriceService)
        {
            _db = db;
            _driverLocationStore = driverLocationStore;
            _rejectedRequestStore = rejectedRequestStore;
            _pushService = pushService;
            _logStore = logStore;
            _hubContext = hubContext;
            _routeService = routeService;
            _판정Service = 판정Service;
            _평가Service = 평가Service;
            _기사운송일정구성Service = 기사운송일정구성Service;
            _운송일정삽입평가Service = 운송일정삽입평가Service;
            _averagePriceService = averagePriceService;
            _복귀지Reader = new 배차추천복귀지Reader(db);
        }

        public async Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string driverId)
        {
            return await GetRecommendationsAsync(driverId, null);
        }

        public async Task<IReadOnlyList<DispatchRecommendationDto>> GetRecommendationsAsync(string driverId, 배차추천검색조건? criteria)
        {
            var isDriving = await IsDrivingAsync(driverId);
            return isDriving
                ? await GetDrivingRecommendationsAsync(driverId, criteria)
                : await GetIdleRecommendationsAsync(driverId, criteria);
        }

        protected abstract Task<bool> IsDrivingAsync(string driverId);
        public abstract Task<IReadOnlyList<DispatchRecommendationDto>> GetDrivingRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null);
        public abstract Task<IReadOnlyList<DispatchRecommendationDto>> GetIdleRecommendationsAsync(string driverId, 배차추천검색조건? criteria = null);

        public virtual async Task SendToDriverAsync(string driverId)
        {
            var recommendations = await GetRecommendationsAsync(driverId);
            await 배차추천발송Coordinator.SendAsync(
                driverId, recommendations, _hubContext, _pushService, _logStore, BuildDriverGroup);
        }

        protected async Task<decimal?> ResolveFuelPricePerLiterAsync()
        {
            var items = await _averagePriceService.GetAveragePricesAsync();
            return 배차추천연료비Policy.ResolvePricePerLiter(items);
        }

        protected static decimal ResolveFuelEfficiencyKmPerLiter(string? vehicle)
        {
            return 배차추천연료비Policy.ResolveEfficiencyKmPerLiter(vehicle);
        }

        protected static decimal? EstimateFuelCost(decimal? totalDistanceKm, decimal fuelEfficiencyKmPerLiter, decimal? fuelPricePerLiter)
        {
            return 배차추천연료비Policy.EstimateCost(totalDistanceKm, fuelEfficiencyKmPerLiter, fuelPricePerLiter);
        }

        protected static decimal? SumDistance(params decimal?[] distances)
        {
            var values = distances.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            return values.Count == 0 ? null : values.Sum();
        }

        protected static decimal? SumMoney(params decimal?[] values)
        {
            var list = values.Where(x => x.HasValue).Select(x => x!.Value).ToList();
            return list.Count == 0 ? null : list.Sum();
        }

        protected static decimal? SumMinutes(params TimeSpan?[] durations)
        {
            var values = durations.Where(x => x.HasValue).Select(x => (decimal)x!.Value.TotalMinutes).ToList();
            return values.Count == 0 ? null : values.Sum();
        }

        protected static 배차경로좌표? CreatePoint(decimal? latitude, decimal? longitude)
        {
            return latitude.HasValue && longitude.HasValue ? new 배차경로좌표(latitude.Value, longitude.Value) : null;
        }

        protected Task<복귀지결정결과> ResolveReturnDestinationAsync(string driverId, 용달기사? driver)
        {
            return _복귀지Reader.ResolveAsync(driverId, driver);
        }

        protected virtual string BuildDriverGroup(string driverId) => $"driver-{driverId}";
    }
}



