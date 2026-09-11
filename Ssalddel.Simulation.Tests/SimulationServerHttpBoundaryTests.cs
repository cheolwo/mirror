using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationServerHttpBoundaryTests
{
    [Fact]
    public async Task 세션_API_경로와_HTTP방식은_호환기준을_유지한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var health = await client.GetAsync("/health/live");
        health.EnsureSuccessStatusCode();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>();
        var manifest = endpoints.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "api/simulation/v1/sessions",
                StringComparison.Ordinal) == true)
            .SelectMany(endpoint => endpoint.Metadata
                .GetMetadata<IHttpMethodMetadata>()?
                .HttpMethods
                .Select(method => $"{method} {endpoint.RoutePattern.RawText}")
                ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join("\n", manifest))))
            .ToLowerInvariant();
        Assert.Equal(180, manifest.Length);
        Assert.Contains(
            "GET api/simulation/v1/sessions/{sessionStableId}/nature-survival/observation",
            manifest);
        Assert.Contains(
            "GET api/simulation/v1/sessions/{sessionStableId}/learning-focus",
            manifest);
        Assert.Contains(
            "POST api/simulation/v1/sessions/{sessionStableId}/learning-focus/equip/preview",
            manifest);
        Assert.Contains(
            "POST api/simulation/v1/sessions/{sessionStableId}/learning-focus/equip/confirm",
            manifest);
        Assert.Contains(
            "POST api/simulation/v1/sessions/{sessionStableId}/restaurant-responses/confirm",
            manifest);
        Assert.Contains(
            "POST api/simulation/v1/sessions/{sessionStableId}/hexagram-campaign/fail",
            manifest);
        Assert.Equal(
            "da5bb95d1c0dbf59551781c986dd4a8f6aa9664649a68d07c8dfa79288cae9e1",
            hash);
    }

    [Fact]
    public async Task 단일_살뜰서버의_상태확인경로를_제공한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 준비상태는_운영과_Simulation_저장소를_합쳐_보고한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task 단일_살뜰서버가_Simulation경로를_항상_등록한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/simulation/v1/sessions/simulation-session:missing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();
        Assert.Equal("SimulationSessionNotFound", error!.ErrorCode);
    }

    [Fact]
    public async Task 존재하지_않는_세션은_오류코드와_함께_404를_반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/simulation/v1/sessions/simulation-session:missing");
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("SimulationSessionNotFound", error.ErrorCode);
    }

    [Fact]
    public async Task 분리된_턴Controller도_공통예외Filter로_404를_반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/simulation/v1/sessions/simulation-session:missing/turn-closing-context");
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("SimulationSessionNotFound", error.ErrorCode);
    }

    [Fact]
    public async Task 세션생성은_201과_비운영_Simulation상태사본을_반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateValidRequest();

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            request);
        var snapshot = await response.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(snapshot);
        Assert.Equal(
            "simulation-session:" + request.ClientRequestId.ToString("N"),
            snapshot.SessionStableId);
        Assert.Equal(SimulationModeCodes.Simulation, snapshot.ModeCode);
        Assert.False(snapshot.IsOperationalState);
        Assert.Equal(0, snapshot.Revision);
        Assert.Equal(0, snapshot.WorldContext.WorldRevision);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(
            $"/api/simulation/v1/sessions/{snapshot.SessionStableId}",
            Uri.UnescapeDataString(response.Headers.Location.AbsolutePath));
    }

    [Fact]
    public async Task 잘못된_생성요청은_오류코드와_함께_400을_반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.ClientRequestId = Guid.Empty;

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            request);
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("SimulationClientRequestIdMissing", error.ErrorCode);
    }

    [Fact]
    public async Task 같은_요청식별자의_다른_내용은_오류코드와_함께_409를_반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateValidRequest();

        using var created = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            request);
        created.EnsureSuccessStatusCode();

        request.ScenarioStableId = "scenario:http-boundary-conflict";
        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            request);
        var error = await response.Content.ReadFromJsonAsync<SimulationErrorResponse>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(error);
        Assert.Equal("SimulationCreateRequestPayloadConflict", error.ErrorCode);
    }

    [Fact]
    public async Task 상향식_H공간구성은_서버조회에서도_같은GraphHash를_반환한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = CreateValidRequest();
        request.NpcRoutineControlRevision =
            SimulationNpcRoutineControlRevisionCodes.R1;
        request.SpatialCompositionRuleRevision =
            SimulationSpatialCompositionCodes.RuleRevision;
        request.SpatialWorld =
            PyeongchangSimulation공간상호작용Fixture.Create();
        request.NpcWorkforce = PyeongchangSimulationNpcWorkforceFixture
            .CreateHubOutboundReadyFixture();

        using var createdResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions", request);
        createdResponse.EnsureSuccessStatusCode();
        var created = await createdResponse.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.NotNull(created);

        using var graphResponse = await client.GetAsync(
            $"/api/simulation/v1/sessions/{created!.SessionStableId}"
            + "/spatial-composition?areaCode=Hub");
        graphResponse.EnsureSuccessStatusCode();
        var graph = await graphResponse.Content
            .ReadFromJsonAsync<SimulationSpatialCompositionStateSnapshot>();

        Assert.NotNull(graph);
        Assert.Equal(64, graph!.GraphHashSha256.Length);
        Assert.Equal(SimulationSpatialCompositionCodes.Qualified,
            graph.Assessments.Single(value => value.TargetDefinitionStableId ==
                PyeongchangHubSpatialCompositionCodes.InternalWarehouseH2)
            .StateCode);
    }

    [Fact]
    public async Task 통합_Simulation_API는_익명요청을_거부한다()
    {
        using var factory = CreateAuthenticatedFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            CreateValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task 로그인사용자는_자기_Simulation_세션만_조회한다()
    {
        using var factory = CreateAuthenticatedFactory();
        var request = CreateValidRequest();
        using var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("user:simulation-owner"));

        using var created = await owner.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var snapshot = await created.Content
            .ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.NotNull(snapshot);

        using var other = factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            CreateToken("user:simulation-other"));
        using var denied = await other.GetAsync(
            "/api/simulation/v1/sessions/" + snapshot!.SessionStableId);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        using var allowed = await owner.GetAsync(
            "/api/simulation/v1/sessions/" + snapshot.SessionStableId);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelExecution:Mode"] = "Operational",
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                            ["SimulationSharedPublicData:Enabled"] = "false",
                        });
                });
            });

    private static WebApplicationFactory<Program> CreateAuthenticatedFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("TestingAuthenticated");
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "false",
                        }));
            });

    private static string CreateToken(string subjectId)
    {
        const string secret = "ssalddel-unified-host-test-secret-key-2026";
        var token = new JwtSecurityToken(
            issuer: "Ssalddel.Tests",
            audience: "Ssalddel.Client.Tests",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, subjectId),
                new Claim(JwtRegisteredClaimNames.Sub, subjectId),
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static 경영SimulationSession생성Request CreateValidRequest()
        => new()
        {
            ClientRequestId = Guid.NewGuid(),
            ScenarioStableId = "scenario:http-boundary",
            ScenarioDataRevision = "scenario-data-r1",
            ScenarioSeed = 1208,
            RuleRevision = "simulation-rule-r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:test",
                TerritoryStableId = "territory:test",
                SettlementStableId = "settlement:test",
                GameDateStartsOn = new DateTimeOffset(
                    2026,
                    8,
                    12,
                    0,
                    0,
                    0,
                    TimeSpan.Zero),
            },
        };
}
