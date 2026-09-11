using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.PublicData;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.PublicDataHall
{
    public sealed class PublicDataHallApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
    }

    public sealed class SimulatedPublicWorldMapApiClient : IPublicWorldMapApiClient
    {
        public Task<PublicWorldMapSnapshotApiModel> GetAsync(
            PublicWorldMapQuery query,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PublicWorldMapSnapshotApiModel
            {
                DatasetCode = query.DatasetCode,
                Revision = "simulation-public-data-hall-1",
                GeneratedAtUtc = DateTimeOffset.Parse("2026-08-08T15:00:00+09:00"),
                Layers = new[]
                {
                    new PublicWorldMapLayerApiModel
                    {
                        Code = "public-price",
                        DatasetCode = query.DatasetCode,
                        DisplayName = "가격·시장",
                        Color = "#ef8f3c",
                        MarkerShape = "diamond",
                    },
                },
                Observations = new[]
                {
                    Observation("public-data:seoul", "서울 공개 관측", 37.5665, 126.9780),
                    Observation("public-data:busan", "부산 공개 관측", 35.1796, 129.0756),
                    Observation("public-data:jeju", "제주 공개 관측", 33.4996, 126.5312),
                },
            });
        }

        private static PublicWorldMapObservationApiModel Observation(
            string stableId,
            string title,
            double latitude,
            double longitude)
        {
            return new PublicWorldMapObservationApiModel
            {
                StableId = stableId,
                DatasetCode = PublicWorldMapDatasetCodes.DayWork,
                LayerCode = "public-price",
                CountryCode = "KR",
                CountryName = "대한민국",
                Latitude = latitude,
                Longitude = longitude,
                Title = title,
                Summary = "SIMULATED public observation",
                SourceName = "SIMULATED",
                EvidenceAsOfUtc = DateTimeOffset.Parse("2026-08-08T00:00:00Z"),
                EvidenceStatusCode = "Simulated",
                DetailHref = "/community/information-map",
                LocationPrecisionCode = "administrative-region-representative",
                FreshnessCode = "Fixture",
                BoundaryNotice = "실제 관측이 아닌 primitive fixture",
            };
        }
    }

    public sealed class OperationalPublicWorldMapApiClient : IPublicWorldMapApiClient
    {
        private readonly IOperationalWorldProjectionTransport transport;

        public OperationalPublicWorldMapApiClient(PublicDataHallApiOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            transport = new UnityWebRequestOperationalWorldProjectionTransport(
                new OperationalWorldProjectionEndpoint(
                    options.BaseUrl,
                    Math.Max(1, options.TimeoutSeconds)));
        }

        public async Task<PublicWorldMapSnapshotApiModel> GetAsync(
            PublicWorldMapQuery query,
            CancellationToken cancellationToken = default)
        {
            var route = PublicWorldMapApiRoutes.Observations
                + "?dataset=" + Uri.EscapeDataString(query.DatasetCode);
            var json = await transport.GetAsync(
                route,
                false,
                false,
                cancellationToken);
            var wire = JsonUtility.FromJson<PublicWorldMapSnapshotWire>(json);
            return wire?.ToApiModel()
                ?? throw new InvalidOperationException("PublicWorldMapJsonInvalid");
        }
    }

    [Serializable]
    internal sealed class PublicWorldMapSnapshotWire
    {
        public string datasetCode = string.Empty;
        public string revision = string.Empty;
        public string generatedAtUtc = string.Empty;
        public PublicWorldMapLayerWire[] layers = Array.Empty<PublicWorldMapLayerWire>();
        public PublicWorldMapObservationWire[] observations = Array.Empty<PublicWorldMapObservationWire>();

        public PublicWorldMapSnapshotApiModel ToApiModel() => new PublicWorldMapSnapshotApiModel
        {
            DatasetCode = datasetCode,
            Revision = revision,
            GeneratedAtUtc = ParseDate(generatedAtUtc),
            Layers = Array.ConvertAll(layers, item => item.ToApiModel()),
            Observations = Array.ConvertAll(observations, item => item.ToApiModel()),
        };

        internal static DateTimeOffset ParseDate(string value)
        {
            if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var result))
            {
                throw new InvalidOperationException("PublicWorldMapDateInvalid");
            }

            return result;
        }
    }

    [Serializable]
    internal sealed class PublicWorldMapLayerWire
    {
        public string code = string.Empty;
        public string datasetCode = string.Empty;
        public string displayName = string.Empty;
        public string description = string.Empty;
        public string color = string.Empty;
        public string markerShape = string.Empty;

        public PublicWorldMapLayerApiModel ToApiModel() => new PublicWorldMapLayerApiModel
        {
            Code = code,
            DatasetCode = datasetCode,
            DisplayName = displayName,
            Description = description,
            Color = color,
            MarkerShape = markerShape,
        };
    }

    [Serializable]
    internal sealed class PublicWorldMapObservationWire
    {
        public string stableId = string.Empty;
        public string datasetCode = string.Empty;
        public string layerCode = string.Empty;
        public string countryCode = string.Empty;
        public string countryName = string.Empty;
        public double latitude;
        public double longitude;
        public string title = string.Empty;
        public string summary = string.Empty;
        public string sourceName = string.Empty;
        public string evidenceAsOfUtc = string.Empty;
        public string evidenceStatusCode = string.Empty;
        public string detailHref = string.Empty;
        public string sourceHref = string.Empty;
        public string locationPrecisionCode = string.Empty;
        public string markerStatusCode = string.Empty;
        public string freshnessCode = string.Empty;
        public string boundaryNotice = string.Empty;
        public string sourceVersion = string.Empty;

        public PublicWorldMapObservationApiModel ToApiModel() => new PublicWorldMapObservationApiModel
        {
            StableId = stableId,
            DatasetCode = datasetCode,
            LayerCode = layerCode,
            CountryCode = countryCode,
            CountryName = countryName,
            Latitude = latitude,
            Longitude = longitude,
            Title = title,
            Summary = summary,
            SourceName = sourceName,
            EvidenceAsOfUtc = string.IsNullOrWhiteSpace(evidenceAsOfUtc)
                ? (DateTimeOffset?)null
                : PublicWorldMapSnapshotWire.ParseDate(evidenceAsOfUtc),
            EvidenceStatusCode = evidenceStatusCode,
            DetailHref = detailHref,
            SourceHref = sourceHref,
            LocationPrecisionCode = locationPrecisionCode,
            MarkerStatusCode = markerStatusCode,
            FreshnessCode = freshnessCode,
            BoundaryNotice = boundaryNotice,
            SourceVersion = sourceVersion,
        };
    }
}
