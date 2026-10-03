using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 살뜰.Data;
using 살뜰.도메인.기사;
using 살뜰.Services.External.Google;
using 살뜰.Services.External.Naver;
using 살뜰.Services.Options;
using 살뜰.Services.Storage.Local;

namespace 살뜰.Services.Dispatch.Recommendation
{
    public interface I배차추천경로Service
    {
        Task<배차경로좌표?> ResolveOriginLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation, 배차추천검색조건? criteria);
        Task<배차경로좌표?> ResolveRouteAnchorLocationAsync(string driverId, 용달기사? driver, DriverLocationSnapshot? currentLocation);
        Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination);
        Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, CancellationToken cancellationToken)
            => EstimateRouteAsync(origin, destination).WaitAsync(cancellationToken);
        Task<배차경로예상결과?> EstimateRouteAsync(배차경로좌표? origin, 배차경로좌표? destination, string routeOption, CancellationToken cancellationToken)
            => throw new ArgumentException("FoodPricingVehicleRouteUnsupported");
        Task<배차경로예상결과?> EstimateOrderedRouteAsync(배차경로좌표? origin, IReadOnlyList<배차경로좌표> orderedStops, CancellationToken cancellationToken = default);
        Task<배차삽입경로예상결과?> EstimateInsertionDelayAsync(배차경로좌표? origin, 배차경로좌표? routeAnchor, 배차경로좌표? pickup, 배차경로좌표? dropoff);
        decimal? CalculateDistanceKm(배차경로좌표 source, 배차경로좌표 target);
    }

    public sealed partial class 배차추천경로Service : I배차추천경로Service
    {
        private readonly SsalddelContext _db;
        private readonly IDriverWorkQueueStore _driverWorkQueueStore;
        private readonly IGeocodingService _geocodingService;
        private readonly INaverCloudDirectionsService _routeService;
        private readonly NaverCloudDirectionsOptions _routeOptions;
        private readonly ILogger<배차추천경로Service> _logger;
        private readonly Dictionary<배차경로CacheKey, Task<배차경로예상결과?>> _routeEstimateCache = [];

        public 배차추천경로Service(
            SsalddelContext db,
            IDriverWorkQueueStore driverWorkQueueStore,
            IGeocodingService geocodingService,
            INaverCloudDirectionsService routeService,
            IOptions<NaverCloudDirectionsOptions> routeOptions,
            ILogger<배차추천경로Service> logger)
        {
            _db = db;
            _driverWorkQueueStore = driverWorkQueueStore;
            _geocodingService = geocodingService;
            _routeService = routeService;
            _routeOptions = routeOptions.Value;
            _logger = logger;
        }

    }
}
