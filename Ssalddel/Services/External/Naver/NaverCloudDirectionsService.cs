using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace 살뜰.Services.External.Naver
{
    public interface INaverCloudDirectionsService
    {
        Task<NaverCloudDrivingRoute?> GetDrivingRouteAsync(
            decimal startLat,
            decimal startLng,
            decimal goalLat,
            decimal goalLng,
            string? option = null,
            CancellationToken cancellationToken = default);

        Task<NaverCloudDrivingRoute?> GetDrivingRouteAsync(
            decimal startLat,
            decimal startLng,
            decimal goalLat,
            decimal goalLng,
            IReadOnlyList<NaverCloudRouteWaypoint> waypoints,
            string? option = null,
            CancellationToken cancellationToken = default);
    }

    public sealed class NaverCloudDirectionsService : INaverCloudDirectionsService
    {
        private readonly HttpClient _httpClient;
        private readonly NaverCloudDirectionsOptions _options;

        public NaverCloudDirectionsService(HttpClient httpClient, IOptions<NaverCloudDirectionsOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<NaverCloudDrivingRoute?> GetDrivingRouteAsync(
            decimal startLat,
            decimal startLng,
            decimal goalLat,
            decimal goalLng,
            string? option = null,
            CancellationToken cancellationToken = default)
        {
            return await GetDrivingRouteAsync(
                startLat,
                startLng,
                goalLat,
                goalLng,
                [],
                option,
                cancellationToken);
        }

        public async Task<NaverCloudDrivingRoute?> GetDrivingRouteAsync(
            decimal startLat,
            decimal startLng,
            decimal goalLat,
            decimal goalLng,
            IReadOnlyList<NaverCloudRouteWaypoint> waypoints,
            string? option = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKeyId) || string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                return null;
            }

            if (waypoints.Count > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(waypoints), waypoints.Count, "Directions5는 경유지를 최대 5개까지 허용합니다.");
            }

            var start = FormatPosition(startLat, startLng);
            var goal = FormatPosition(goalLat, goalLng);
            var routeOption = string.IsNullOrWhiteSpace(option) ? _options.DefaultOption : option.Trim();

            var query = new List<string>
            {
                $"start={Uri.EscapeDataString(start)}",
                $"goal={Uri.EscapeDataString(goal)}",
                $"option={Uri.EscapeDataString(routeOption)}"
            };
            if (waypoints.Count > 0)
            {
                var waypointValue = string.Join("|", waypoints.Select(x => FormatPosition(x.Latitude, x.Longitude)));
                query.Add($"waypoints={Uri.EscapeDataString(waypointValue)}");
            }

            var requestUrl = $"{_options.Path.TrimStart('/')}?{string.Join("&", query)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            request.Headers.TryAddWithoutValidation("x-ncp-apigw-api-key-id", _options.ApiKeyId);
            request.Headers.TryAddWithoutValidation("x-ncp-apigw-api-key", _options.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(content);

            if (!TryGetRouteSummary(document.RootElement, routeOption, out var route))
            {
                return null;
            }

            return route;
        }

        private static string FormatPosition(decimal latitude, decimal longitude)
            => FormattableString.Invariant($"{longitude},{latitude}");

        private static bool TryGetRouteSummary(JsonElement root, string requestedOptions, out NaverCloudDrivingRoute route)
        {
            route = default!;

            if (root.TryGetProperty("code", out var code)
                && (code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var resultCode) || resultCode != 0))
                return false;

            if (!root.TryGetProperty("route", out var routeElement) || routeElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var routeType in requestedOptions.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!routeElement.TryGetProperty(routeType, out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
                {
                    continue;
                }

                var firstCandidate = candidates[0];
                if (firstCandidate.ValueKind != JsonValueKind.Object
                    || !firstCandidate.TryGetProperty("summary", out var summary)
                    || summary.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var distance = GetDecimal(summary, "distance");
                if (distance is null or < 0m)
                    continue;

                route = new NaverCloudDrivingRoute
                {
                    RouteType = routeType,
                    DistanceMeters = distance,
                    DurationMilliseconds = GetDecimal(summary, "duration"),
                    TollFare = GetDecimal(summary, "tollFare"),
                    FuelPrice = GetDecimal(summary, "fuelPrice"),
                    DepartureName = GetString(summary, "departureName"),
                    GoalName = GetString(summary, "goalName"),
                    Path = GetPath(firstCandidate)
                };

                return true;
            }

            return false;
        }

        private static decimal? GetDecimal(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var valueElement))
            {
                return null;
            }

            return valueElement.ValueKind switch
            {
                JsonValueKind.Number when valueElement.TryGetDecimal(out var value) => value,
                JsonValueKind.String when decimal.TryParse(valueElement.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        private static string? GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var valueElement) ? valueElement.GetString() : null;
        }

        private static IReadOnlyList<NaverCloudRoutePathPoint> GetPath(JsonElement candidate)
        {
            if (!candidate.TryGetProperty("path", out var pathElement)
                || pathElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<NaverCloudRoutePathPoint>();
            foreach (var coordinate in pathElement.EnumerateArray())
            {
                if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() < 2)
                {
                    continue;
                }

                var longitudeElement = coordinate[0];
                var latitudeElement = coordinate[1];
                if (longitudeElement.TryGetDecimal(out var longitude)
                    && latitudeElement.TryGetDecimal(out var latitude))
                {
                    result.Add(new NaverCloudRoutePathPoint(latitude, longitude));
                }
            }

            return result;
        }
    }

    public sealed record NaverCloudRouteWaypoint(decimal Latitude, decimal Longitude);

    public sealed record NaverCloudRoutePathPoint(decimal Latitude, decimal Longitude);

    public sealed class NaverCloudDrivingRoute
    {
        public string RouteType { get; set; } = string.Empty;
        public decimal? DistanceMeters { get; set; }
        public decimal? DurationMilliseconds { get; set; }
        public decimal? TollFare { get; set; }
        public decimal? FuelPrice { get; set; }
        public string? DepartureName { get; set; }
        public string? GoalName { get; set; }
        public IReadOnlyList<NaverCloudRoutePathPoint> Path { get; set; } = [];

        public decimal? DistanceKm => DistanceMeters.HasValue ? DistanceMeters.Value / 1000m : null;
        public TimeSpan? Duration => DurationMilliseconds.HasValue ? TimeSpan.FromMilliseconds((double)DurationMilliseconds.Value) : null;
    }
}


