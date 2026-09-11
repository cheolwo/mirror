using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationRealityContextTests
{
    [Fact]
    public void 승인된관측은_같은입력에서_같은의미신호로_동결된다()
    {
        var path = WriteCatalog(available: true);
        try
        {
            var reader = new FileSimulationRealityContextCatalogReader(path);
            var frozenAt = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

            Assert.True(reader.TryFreeze(ProfileId, AreaSetId, "context:test:1", frozenAt,
                out var first, out var firstError), firstError);
            Assert.True(reader.TryFreeze(ProfileId, AreaSetId, "context:test:1", frozenAt,
                out var second, out var secondError), secondError);

            Assert.Equal(first.InputHashSha256, second.InputHashSha256);
            Assert.Equal(SimulationRealityContextCodes.Available, first.AvailabilityCode);
            Assert.Equal(new[]
            {
                SimulationRealityContextCodes.ColdStressContext,
                SimulationRealityContextCodes.CropHealthAttentionContext,
                SimulationRealityContextCodes.WetWorkContext,
            }, first.SemanticSignals.Select(value => value.SignalCode).ToArray());
            Assert.False(first.ChangesSimulationRules);
            Assert.False(first.MovesSpatialDefinitions);
            Assert.False(first.CreatesIncidentOrEffect);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 누락된현실자료는_시나리오값으로대체하지않고_명시적으로남긴다()
    {
        var path = WriteCatalog(available: false);
        try
        {
            var reader = new FileSimulationRealityContextCatalogReader(path);

            Assert.True(reader.TryFreeze(ProfileId, AreaSetId, "context:test:missing",
                new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                out var snapshot, out var errorCode), errorCode);

            Assert.Equal(SimulationRealityContextCodes.Unavailable,
                snapshot.AvailabilityCode);
            Assert.Empty(snapshot.SemanticSignals);
            Assert.All(snapshot.SourceEvidence, value => Assert.Equal(
                SimulationRealityContextCodes.Unavailable, value.AvailabilityCode));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 오래된관측은_신호를만들지않고_Stale근거로남긴다()
    {
        var path = WriteCatalog(available: true);
        try
        {
            var reader = new FileSimulationRealityContextCatalogReader(path);

            Assert.True(reader.TryFreeze(ProfileId, AreaSetId, "context:test:stale",
                new DateTimeOffset(2026, 8, 22, 12, 0, 1, TimeSpan.Zero),
                out var snapshot, out var errorCode), errorCode);

            Assert.Equal(SimulationRealityContextCodes.Unavailable,
                snapshot.AvailabilityCode);
            Assert.Empty(snapshot.SemanticSignals);
            Assert.Equal(SimulationRealityContextCodes.Stale,
                Assert.Single(snapshot.SourceEvidence).FreshnessCode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void 세션시작동결문맥은_v8저장과재생에서_같은해시로복원된다()
    {
        var path = WriteCatalog(available: true);
        try
        {
            var reader = new FileSimulationRealityContextCatalogReader(path);
            Assert.True(reader.TryFreeze(ProfileId, AreaSetId, "context:test:save",
                new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
                out var reality, out var errorCode), errorCode);
            var request = CreateSessionRequest(ProfileId);
            var session = new 경영SimulationSessionAggregate(request, reality);

            var package = session.CreateSavePackage(new SimulationSessionSaveRequest
            {
                SaveStableId = "simulation-save:reality-context-1",
                ExpectedRevision = session.Revision,
            });
            var restored = SimulationSessionReplay.Restore(package);

            Assert.Equal(SimulationSaveSchemaVersions.V8, package.SchemaVersion);
            Assert.Equal(reality.InputHashSha256,
                package.RealityContext!.InputHashSha256);
            Assert.Equal(package.ReplayHash,
                restored.CreateSavePackage(new SimulationSessionSaveRequest
                {
                    SaveStableId = package.SaveStableId,
                    ExpectedRevision = restored.Revision,
                }).ReplayHash);
            Assert.Equal(reality.InputHashSha256,
                restored.RealityContextSnapshot()!.InputHashSha256);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task 실제E5세션은_프로필만받고_기본조회에서는출처상세를숨긴다()
    {
        using var factory = new SimulationWebApplicationFactory();
        using var client = factory.CreateClient();
        const string layoutId = "world-layout:sim:pyeongchang:nature-farm-hub-town.v1";
        var layout = await client.GetFromJsonAsync<SimulationWorldLayoutDefinitionResponse>(
            "/api/simulation/v1/world-stream/world-layouts/" + Uri.EscapeDataString(layoutId));

        var request = new SimulationActualE5SessionCreateRequest
        {
            AreaSetNetworkStableId = PyeongchangAreaSetStableIds.ActualNetwork,
            AreaSetStableId = AreaSetId,
            WorldLayoutStableId = layoutId,
            ExpectedWorldLayoutRevision = layout!.WorldLayoutRevision,
            ExpectedWorldLayoutHashSha256 = layout.WorldLayoutHashSha256,
            WorldInteractionIds = new[] { "WI-FARM-04" },
            Session = CreateSessionRequest(ProfileId),
        };
        var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/actual-e5", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content
            .ReadFromJsonAsync<SimulationActualE5SessionCreateResponse>();
        Assert.NotNull(created);
        Assert.Equal(SimulationRealityContextCodes.Unavailable,
            created!.RealityContextAvailabilityCode);
        Assert.NotEmpty(created.RealityContextSnapshotStableId);

        var repeatedResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions/actual-e5", request);
        Assert.Equal(HttpStatusCode.Created, repeatedResponse.StatusCode);
        var repeated = await repeatedResponse.Content
            .ReadFromJsonAsync<SimulationActualE5SessionCreateResponse>();
        Assert.Equal(created.RealityContextSnapshotStableId,
            repeated!.RealityContextSnapshotStableId);

        var hidden = await client.GetFromJsonAsync<
            SimulationRealityContextPlayerProjectionResponse>(
            $"/api/simulation/v1/sessions/{Uri.EscapeDataString(created.Session.SessionStableId)}/reality-context");
        Assert.NotNull(hidden);
        Assert.True(hidden!.PresentationOnly);
        Assert.False(hidden.SourceDetailsIncluded);
        Assert.Empty(hidden.SourceInformation);

        var detailed = await client.GetFromJsonAsync<
            SimulationRealityContextPlayerProjectionResponse>(
            $"/api/simulation/v1/sessions/{Uri.EscapeDataString(created.Session.SessionStableId)}/reality-context?includeSourceDetails=true");
        Assert.NotNull(detailed);
        Assert.True(detailed!.SourceDetailsIncluded);
        Assert.Equal(3, detailed.SourceInformation.Length);
        Assert.All(detailed.SourceInformation, value =>
        {
            Assert.NotEmpty(value.SourceName);
            Assert.NotEmpty(value.SpatialPrecisionCode);
            Assert.NotEmpty(value.LimitationCodes);
        });
    }

    private const string ProfileId =
        "reality-context-profile:sim:pyeongchang:farm-production.v1";
    private const string AreaSetId = "area-set:sim:pyeongchang:farm-production.v1";

    private static 경영SimulationSession생성Request CreateSessionRequest(
        string profileStableId) => new()
    {
        ClientRequestId = Guid.NewGuid(),
        ScenarioStableId = "scenario:sim.reality-context-test-1",
        ScenarioDataRevision = "scenario-data:r1",
        ScenarioSeed = 20260820,
        RuleRevision = "rule:reality-context-test.r1",
        RealityContextProfileStableId = profileStableId,
        DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:sim.farmers-1",
            TerritoryStableId = "territory:sim.farm-production-1",
            SettlementStableId = "settlement:sim.farm-home-1",
            GameDateStartsOn = new DateTimeOffset(2026, 4, 1, 0, 0, 0,
                TimeSpan.Zero),
        },
    };

    private static string WriteCatalog(bool available)
    {
        var path = Path.Combine(Path.GetTempPath(),
            "simulation-reality-context-" + Guid.NewGuid().ToString("N") + ".json");
        var status = available ? "Available" : "Unavailable";
        var quality = available ? "Valid" : "Unavailable";
        var observed = available ? "\"2026-08-19T12:00:00Z\"" : "null";
        var retrieved = available ? "\"2026-08-19T13:00:00Z\"" : "null";
        var hash = available ? new string('a', 64) : string.Empty;
        var measurements = available
            ? """
              [
                { "measurementCode": "DailyPrecipitationMm", "value": 12.5, "unitCode": "mm" },
                { "measurementCode": "MinimumTemperatureCelsius", "value": 2.0, "unitCode": "Celsius" },
                { "measurementCode": "RelativeHumidityPercent", "value": 85.0, "unitCode": "Percent" }
              ]
              """
            : "[]";
        File.WriteAllText(path, $$"""
        {
          "schemaVersion": "simulation-reality-context-catalog.v1",
          "profiles": [
            {
              "profileStableId": "{{ProfileId}}",
              "profileRevision": 1,
              "signalRuleRevision": "reality-signal-rule:farm-advisory.r1",
              "areaSetStableId": "{{AreaSetId}}",
              "maxAgeHours": 48,
              "h3StableIds": ["h3-candidate:highland-farm"],
              "sourceSnapshots": [
                {
                  "sourceEvidenceStableId": "reality-source:test:kma-asos",
                  "sourceName": "기상청 시험 관측",
                  "datasetCode": "kma-asos-daily",
                  "availabilityCode": "{{status}}",
                  "qualityCode": "{{quality}}",
                  "observedAtUtc": {{observed}},
                  "retrievedAtUtc": {{retrieved}},
                  "spatialPrecisionCode": "StationObservation",
                  "sourceHashSha256": "{{hash}}",
                  "licenseCode": "PublicDataPortalType1",
                  "sourceHref": "https://www.data.go.kr/data/15059093/openapi.do",
                  "limitationCodes": ["StationObservationIsNotParcelObservation"],
                  "measurements": {{measurements}}
                }
              ],
              "publicDataChangesSimulationRules": false,
              "publicDataMovesSpatialDefinitions": false,
              "contextProposalCreatesIncidentOrEffect": false
            }
          ]
        }
        """);
        return path;
    }
}
