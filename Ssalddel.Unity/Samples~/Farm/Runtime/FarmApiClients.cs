using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Ssalddel.Unity.Farm;
using Ssalddel.Unity.Npcs;
using Ssalddel.Unity.OperationalTransport;
using Ssalddel.Unity.WorldProjection;
using UnityEngine;

namespace Ssalddel.Unity.Samples.Farm
{
    public sealed class FarmApiOptions
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 15;
    }

    public sealed class SimulatedFarmProducerApiClient : IFarmProducerPerspectiveApiClient
    {
        public Task<FarmProducerPerspectiveApiModel> GetAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new FarmProducerPerspectiveApiModel
            {
                StableId = "role-perspective:farm.producer",
                Revision = 7,
                AuthorizedRoleCode = FarmProducerRoleCodes.Producer,
                WorldZoneCode = "farm",
                ViewerScopeCode = "AuthorizedParty",
                SourceTypeCode = "SimulatedFixture",
                AuthorizationDecisionId = "simulation:farm-producer",
                GeneratedAt = DateTimeOffset.Parse("2026-08-08T16:00:00+09:00"),
                Farms = new[]
                {
                    new FarmApiModel
                    {
                        StableId = "farm:a",
                        Revision = 4,
                        FarmName = "살뜰 실증 농장",
                        StatusCode = "Operating",
                        Plots = new[]
                        {
                            new FarmPlotApiModel
                            {
                                StableId = "farm-plot:a.1",
                                Revision = 5,
                                PlotName = "감자 1번 밭",
                                SoilManagementProfileCode = "LoamWellDrained",
                                Cultivations = new[]
                                {
                                    new FarmCultivationApiModel
                                    {
                                        StableId = "cultivation:a.potato.2026",
                                        Revision = 6,
                                        CropName = "감자",
                                        CropReferenceStableId = "crop-reference-category:fc01",
                                        CropReferenceSourceKey = "nongsaro:crop-ebook",
                                        GrowthStatusCode = "Growing",
                                        PlantedOn = "2026-04-10",
                                        ExpectedHarvestOn = "2026-08-25",
                                    },
                                },
                                Sensors = new[]
                                {
                                    new FarmSensorApiModel
                                    {
                                        StableId = "sensor:a.soil-moisture.1",
                                        Revision = 7,
                                        SensorTypeCode = "SoilMoisture",
                                        StatusCode = "Online",
                                        LatestObservation = new FarmSensorObservationApiModel
                                        {
                                            Value = 18.5m,
                                            UnitCode = "Percent",
                                            ObservedAt = DateTimeOffset.Parse("2026-08-08T15:55:00+09:00"),
                                            FreshnessStatusCode = "Current",
                                            ConditionCode = FarmSensorConditionCodes.Dry,
                                            AssessmentRuleRevision = "soil-water-rule:3",
                                            EvidenceCardId = "SOIL-WATER-001",
                                            ConfidenceCode = "Medium",
                                            Limitation = "토성과 생육 단계에 따라 해석 범위가 달라집니다.",
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                Workers = new[]
                {
                    new NpcMovementApiModel
                    {
                        StableId = "npc-movement:simulation.farm-worker.a.1",
                        Revision = 7,
                        NpcStableId = "farm-worker:a.1",
                        ActorRoleCode = "Producer",
                        WorldZoneCode = "farm",
                        RouteCode = "farm-producer-round",
                        CurrentWaypointKey = "farm.field-a",
                        DestinationWaypointKey = "farm.sensor-a",
                        MovementStateCode = NpcMovementStateCodes.Moving,
                        ArrivalActionCode = "InspectSensor",
                        SourceTypeCode = NpcMovementSourceTypeCodes.SimulatedFixture,
                        CanonicalTaskStableId = string.Empty,
                        GeneratedAt = DateTimeOffset.Parse("2026-08-08T16:00:00+09:00"),
                    },
                },
            });
        }
    }

    public sealed class OperationalFarmProducerApiClient : IFarmProducerPerspectiveApiClient
    {
        private readonly IOperationalWorldProjectionTransport transport;

        public OperationalFarmProducerApiClient(
            FarmApiOptions apiOptions,
            FarmSessionTokenProvider sessionProvider)
        {
            if (apiOptions == null) throw new ArgumentNullException(nameof(apiOptions));
            if (sessionProvider == null) throw new ArgumentNullException(nameof(sessionProvider));
            transport = new UnityWebRequestOperationalWorldProjectionTransport(
                new OperationalWorldProjectionEndpoint(
                    apiOptions.BaseUrl,
                    Math.Max(1, apiOptions.TimeoutSeconds)),
                sessionProvider);
        }

        public async Task<FarmProducerPerspectiveApiModel> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var json = await transport.GetAsync(
                FarmProducerApiRoutes.Producer,
                false,
                true,
                cancellationToken);
            var wire = JsonUtility.FromJson<FarmPerspectiveWireModel>(json);
            return wire?.ToApiModel()
                ?? throw new InvalidOperationException("FarmApiJsonInvalid");
        }
    }

    [Serializable]
    internal sealed class FarmPerspectiveWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string authorizedRoleCode = string.Empty;
        public string worldZoneCode = string.Empty;
        public string viewerScopeCode = string.Empty;
        public string sourceTypeCode = string.Empty;
        public string authorizationDecisionId = string.Empty;
        public string generatedAt = string.Empty;
        public FarmWireModel[] farms = Array.Empty<FarmWireModel>();
        public FarmWorkerWireModel[] workers = Array.Empty<FarmWorkerWireModel>();

        public FarmProducerPerspectiveApiModel ToApiModel()
            => new FarmProducerPerspectiveApiModel
            {
                StableId = stableId,
                Revision = revision,
                AuthorizedRoleCode = authorizedRoleCode,
                WorldZoneCode = worldZoneCode,
                ViewerScopeCode = viewerScopeCode,
                SourceTypeCode = sourceTypeCode,
                AuthorizationDecisionId = authorizationDecisionId,
                GeneratedAt = ParseTimestamp(generatedAt),
                Farms = Array.ConvertAll(farms ?? Array.Empty<FarmWireModel>(), item => item.ToApiModel()),
                Workers = Array.ConvertAll(workers ?? Array.Empty<FarmWorkerWireModel>(), item => item.ToApiModel()),
            };

        public static DateTimeOffset ParseTimestamp(string value)
            => DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var parsed)
                ? parsed
                : throw new InvalidOperationException("FarmTimestampInvalid");
    }

    [Serializable]
    internal sealed class FarmWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string farmName = string.Empty;
        public string statusCode = string.Empty;
        public FarmPlotWireModel[] plots = Array.Empty<FarmPlotWireModel>();

        public FarmApiModel ToApiModel() => new FarmApiModel
        {
            StableId = stableId,
            Revision = revision,
            FarmName = farmName,
            StatusCode = statusCode,
            Plots = Array.ConvertAll(plots ?? Array.Empty<FarmPlotWireModel>(), item => item.ToApiModel()),
        };
    }

    [Serializable]
    internal sealed class FarmPlotWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string plotName = string.Empty;
        public string soilManagementProfileCode = string.Empty;
        public FarmCultivationWireModel[] cultivations = Array.Empty<FarmCultivationWireModel>();
        public FarmSensorWireModel[] sensors = Array.Empty<FarmSensorWireModel>();

        public FarmPlotApiModel ToApiModel() => new FarmPlotApiModel
        {
            StableId = stableId,
            Revision = revision,
            PlotName = plotName,
            SoilManagementProfileCode = soilManagementProfileCode,
            Cultivations = Array.ConvertAll(cultivations ?? Array.Empty<FarmCultivationWireModel>(), item => item.ToApiModel()),
            Sensors = Array.ConvertAll(sensors ?? Array.Empty<FarmSensorWireModel>(), item => item.ToApiModel()),
        };
    }

    [Serializable]
    internal sealed class FarmCultivationWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string cropName = string.Empty;
        public string cropReferenceStableId = string.Empty;
        public string cropReferenceSourceKey = string.Empty;
        public string growthStatusCode = string.Empty;
        public string plantedOn = string.Empty;
        public string expectedHarvestOn = string.Empty;

        public FarmCultivationApiModel ToApiModel() => new FarmCultivationApiModel
        {
            StableId = stableId,
            Revision = revision,
            CropName = cropName,
            CropReferenceStableId = EmptyToNull(cropReferenceStableId),
            CropReferenceSourceKey = EmptyToNull(cropReferenceSourceKey),
            GrowthStatusCode = growthStatusCode,
            PlantedOn = EmptyToNull(plantedOn),
            ExpectedHarvestOn = EmptyToNull(expectedHarvestOn),
        };

        private static string? EmptyToNull(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [Serializable]
    internal sealed class FarmSensorWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string sensorTypeCode = string.Empty;
        public string statusCode = string.Empty;
        public FarmObservationWireModel? latestObservation;

        public FarmSensorApiModel ToApiModel() => new FarmSensorApiModel
        {
            StableId = stableId,
            Revision = revision,
            SensorTypeCode = sensorTypeCode,
            StatusCode = statusCode,
            LatestObservation = latestObservation?.ToApiModel(),
        };
    }

    [Serializable]
    internal sealed class FarmObservationWireModel
    {
        public double value;
        public string unitCode = string.Empty;
        public string observedAt = string.Empty;
        public string freshnessStatusCode = string.Empty;
        public string conditionCode = string.Empty;
        public string assessmentRuleRevision = string.Empty;
        public string evidenceCardId = string.Empty;
        public string confidenceCode = string.Empty;
        public string limitation = string.Empty;

        public FarmSensorObservationApiModel ToApiModel() => new FarmSensorObservationApiModel
        {
            Value = Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            UnitCode = unitCode,
            ObservedAt = FarmPerspectiveWireModel.ParseTimestamp(observedAt),
            FreshnessStatusCode = freshnessStatusCode,
            ConditionCode = conditionCode,
            AssessmentRuleRevision = assessmentRuleRevision,
            EvidenceCardId = string.IsNullOrWhiteSpace(evidenceCardId) ? null : evidenceCardId,
            ConfidenceCode = string.IsNullOrWhiteSpace(confidenceCode) ? null : confidenceCode,
            Limitation = string.IsNullOrWhiteSpace(limitation) ? null : limitation,
        };
    }

    [Serializable]
    internal sealed class FarmWorkerWireModel
    {
        public string stableId = string.Empty;
        public long revision;
        public string npcStableId = string.Empty;
        public string actorRoleCode = string.Empty;
        public string worldZoneCode = string.Empty;
        public string routeCode = string.Empty;
        public string currentWaypointKey = string.Empty;
        public string destinationWaypointKey = string.Empty;
        public string movementStateCode = string.Empty;
        public string arrivalActionCode = string.Empty;
        public string sourceTypeCode = string.Empty;
        public string canonicalTaskStableId = string.Empty;
        public string generatedAt = string.Empty;

        public NpcMovementApiModel ToApiModel() => new NpcMovementApiModel
        {
            StableId = stableId,
            Revision = revision,
            NpcStableId = npcStableId,
            ActorRoleCode = actorRoleCode,
            WorldZoneCode = worldZoneCode,
            RouteCode = routeCode,
            CurrentWaypointKey = currentWaypointKey,
            DestinationWaypointKey = destinationWaypointKey,
            MovementStateCode = movementStateCode,
            ArrivalActionCode = arrivalActionCode,
            SourceTypeCode = sourceTypeCode,
            CanonicalTaskStableId = canonicalTaskStableId,
            GeneratedAt = FarmPerspectiveWireModel.ParseTimestamp(generatedAt),
        };
    }
}
