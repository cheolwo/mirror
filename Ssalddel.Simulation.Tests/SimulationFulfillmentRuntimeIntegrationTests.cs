using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

/// <summary>전송 계약·공유 세션·접근 경계 회귀다. 실제 Hosted/Unity 실행 증거가 아니다.</summary>
[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "출고 실행 포트의 동일 세션·HTTP 계약·접근 경계와 재조회 회귀를 검증한다.",
    Boundary = "Local 및 테스트 서버 검증이며 운영 Hosted 서버·실제 창고·Unity 실행과 구분한다.")]
public sealed class SimulationFulfillmentRuntimeIntegrationTests
{
    [Fact]
    public async Task RemoteAdapter는_정확한_세션경로와_응답을_사용한다()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://simulation.invalid/") };
        var runtime = new RemoteSimulationFulfillmentRuntime(client);
        handler.Responses.Enqueue((HttpStatusCode.OK, "null"));
        handler.Responses.Enqueue((HttpStatusCode.OK, "{}"));
        handler.Responses.Enqueue((HttpStatusCode.OK, "{\"sessionStableId\":\"session:one\",\"revision\":27}"));

        Assert.Null(await runtime.GetFulfillmentStateAsync(" session:one "));
        Assert.NotNull(await runtime.PreviewFulfillmentAsync("session:one", new SimulationFulfillmentOrderRequest()));
        var confirmed = await runtime.ConfirmFulfillmentAsync("session:one", new SimulationFulfillmentCommand());

        Assert.Equal(27, confirmed.Revision);
        Assert.Equal("session:one", confirmed.SessionStableId);
        Assert.Equal(new[]
        {
            "GET /api/simulation/v1/sessions/session%3Aone/fulfillment",
            "POST /api/simulation/v1/sessions/session%3Aone/fulfillment/previews",
            "POST /api/simulation/v1/sessions/session%3Aone/fulfillment/confirm",
        }, handler.Requests);
        Assert.All(handler.Bodies.Skip(1), body => Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(body).RootElement.ValueKind));
        Assert.All(handler.ContentTypes.Skip(1), value => Assert.Equal("application/json", value));
    }

    [Fact]
    public async Task Remote조회는_미구성204와_없는세션404를_구분한다()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://simulation.invalid/") };
        var runtime = new RemoteSimulationFulfillmentRuntime(client);
        handler.Responses.Enqueue((HttpStatusCode.NoContent, ""));
        handler.Responses.Enqueue((HttpStatusCode.NotFound, "{\"errorCode\":\"SimulationSessionNotFound\"}"));

        Assert.Null(await runtime.GetFulfillmentStateAsync("session:legacy"));
        await Assert.ThrowsAsync<HttpRequestException>(async () => await runtime.GetFulfillmentStateAsync("session:missing"));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Remote확정실패는_자동재시도하지않는다()
    {
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://simulation.invalid/") };
        var runtime = new RemoteSimulationFulfillmentRuntime(client);
        handler.Responses.Enqueue((HttpStatusCode.Conflict, "{\"errorCode\":\"SimulationExpectedRevisionMismatch\"}"));

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await runtime.ConfirmFulfillmentAsync("session:one", new SimulationFulfillmentCommand()));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task 취소된_Local과Remote호출은_세션조회나HTTP에_진입하지않는다()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var local = new LocalSimulationFulfillmentRuntime(
            new 경영SimulationSessionAccessor(new InMemory경영SimulationSessionStore()));
        var handler = new RecordingHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://simulation.invalid/") };
        var remote = new RemoteSimulationFulfillmentRuntime(client);

        foreach (ISimulationFulfillmentRuntime runtime in new ISimulationFulfillmentRuntime[] { local, remote })
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await runtime.GetFulfillmentStateAsync("session:missing", cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await runtime.PreviewFulfillmentAsync("session:missing", new SimulationFulfillmentOrderRequest(), cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await runtime.ConfirmFulfillmentAsync("session:missing", new SimulationFulfillmentCommand(), cancellation.Token));
        }
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task LocalAdapter는_주입된_기존세션만_읽고_새세션을_만들지않는다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var accessor = new 경영SimulationSessionAccessor(store);
        var session = accessor.CreateOrGet(CreateRequest());
        var local = new LocalSimulationFulfillmentRuntime(accessor);
        var before = session.Snapshot();

        Assert.Null(await local.GetFulfillmentStateAsync(before.SessionStableId));
        Assert.Equal(before.Revision, session.Snapshot().Revision);
        await Assert.ThrowsAsync<SimulationNotFoundException>(async () =>
            await local.GetFulfillmentStateAsync("session:uncreated"));
        Assert.Null(store.Find("session:uncreated"));
    }

    [Fact]
    public async Task LocalAdapter는_같은Aggregate의_예약과부분피킹을_기존서비스와공유한다()
    {
        var accessor = new 경영SimulationSessionAccessor(new InMemory경영SimulationSessionStore());
        var aggregate = accessor.CreateOrGet(다중센터출고표본.Create(Guid.NewGuid()));
        var session = aggregate.Snapshot();
        var local = new LocalSimulationFulfillmentRuntime(accessor);
        var service = new 경영Simulation물류창고Service(accessor);
        session = await local.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:local:configure", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Configure, InitialState = 다중센터출고표본.InitialState(),
        });
        var before = JsonSerializer.Serialize(service.GetFulfillmentState(session.SessionStableId));
        var plan = await local.PreviewFulfillmentAsync(session.SessionStableId, 다중센터출고표본.Order());
        Assert.True(plan.IsFeasible);
        Assert.Equal(before, JsonSerializer.Serialize(aggregate.GetFulfillmentState()));
        session = await local.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:local:reserve", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Reserve, Order = 다중센터출고표본.Order(),
        });
        var reservation = service.GetFulfillmentState(session.SessionStableId)!;
        var order = Assert.Single(reservation.Orders);
        var allocation = Assert.Single(order.Allocations);
        Assert.Equal(다중센터출고표본.ReadyCenter, allocation.CenterStableId);
        var revisionBeforeRejected = aggregate.Snapshot().Revision;
        var rejected = await Assert.ThrowsAsync<SimulationContractException>(async () =>
            await local.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
            {
                CommandId = "fulfillment:local:pack-too-early", ExpectedRevision = revisionBeforeRejected,
                ActionCode = SimulationFulfillmentActionCodes.PackReady, OrderStableId = order.OrderStableId,
            }));
        Assert.Equal("SimulationFulfillmentPickingIncomplete", rejected.ErrorCode);
        Assert.Equal(revisionBeforeRejected, aggregate.Snapshot().Revision);
        Assert.Equal(JsonSerializer.Serialize(reservation), JsonSerializer.Serialize(aggregate.GetFulfillmentState()));
        session = await local.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:local:pick-part", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Pick, OrderStableId = order.OrderStableId,
            AllocationStableId = allocation.AllocationStableId, LocationBarcode = allocation.LocationBarcode,
            ProductBarcode = allocation.ProductBarcode, Quantity = 1m,
        });
        var current = (await local.GetFulfillmentStateAsync(session.SessionStableId))!;
        Assert.Equal(1m, Assert.Single(Assert.Single(current.Orders).Allocations).PickedQuantity);
        var stock = current.Stocks.Single(value => value.StockStableId == allocation.StockStableId);
        Assert.Equal(9m, stock.OnHandQuantity);
        Assert.Equal(2m, stock.ReservedQuantity);
        Assert.Equal(JsonSerializer.Serialize(service.GetFulfillmentState(session.SessionStableId)), JsonSerializer.Serialize(current));

    }

    [Fact]
    public async Task HTTP와RemoteAdapter는_동일세션의_구성부터출고준비까지_왕복한다()
    {
        using var factory = new SimulationWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                    ["SimulationSharedPublicData:Enabled"] = "false",
                })));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/simulation/v1/sessions", 다중센터출고표본.Create(Guid.NewGuid()));
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<경영SimulationSessionSnapshot>())!;
        var remote = new RemoteSimulationFulfillmentRuntime(client);
        session = await remote.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:http:configure", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Configure, InitialState = 다중센터출고표본.InitialState(),
        });
        var before = (await remote.GetFulfillmentStateAsync(session.SessionStableId))!;
        Assert.Empty(before.Orders);
        var plan = await remote.PreviewFulfillmentAsync(session.SessionStableId, 다중센터출고표본.Order());
        Assert.True(plan.IsFeasible);
        Assert.Empty((await remote.GetFulfillmentStateAsync(session.SessionStableId))!.Orders);
        session = await remote.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:http:reserve", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Reserve, Order = 다중센터출고표본.Order(),
        });
        var allocated = Assert.Single(session.Fulfillment!.Orders);
        var allocation = Assert.Single(allocated.Allocations);
        session = await remote.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:http:pick", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.Pick, OrderStableId = allocated.OrderStableId,
            AllocationStableId = allocation.AllocationStableId, LocationBarcode = allocation.LocationBarcode,
            ProductBarcode = allocation.ProductBarcode, Quantity = allocation.Quantity,
        });
        session = await remote.ConfirmFulfillmentAsync(session.SessionStableId, new SimulationFulfillmentCommand
        {
            CommandId = "fulfillment:http:pack", ExpectedRevision = session.Revision,
            ActionCode = SimulationFulfillmentActionCodes.PackReady, OrderStableId = allocated.OrderStableId,
        });
        var final = (await remote.GetFulfillmentStateAsync(session.SessionStableId))!;
        Assert.Equal(SimulationFulfillmentStateCodes.PackReady, Assert.Single(final.Orders).StateCode);
        Assert.Equal(JsonSerializer.Serialize(session.Fulfillment), JsonSerializer.Serialize(final));

    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "/previews")]
    [InlineData("POST", "/confirm")]
    public async Task HTTP는_새출고경로에도_기존인증관문을_적용한다(string method, string suffix)
    {
        using var factory = new SimulationWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("TestingAuthenticated");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "false" }));
        });
        using var client = factory.CreateClient();
        using var message = new HttpRequestMessage(new HttpMethod(method),
            "/api/simulation/v1/sessions/session:unknown/fulfillment" + suffix);
        if (method == "POST") message.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HTTP의_기존미구성세션은_204이며_상태를_변경하지않는다()
    {
        using var factory = new SimulationWebApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                    ["SimulationSharedPublicData:Enabled"] = "false",
                })));
        using var client = factory.CreateClient();
        using var createdResponse = await client.PostAsJsonAsync("/api/simulation/v1/sessions", CreateRequest());
        createdResponse.EnsureSuccessStatusCode();
        var created = (await createdResponse.Content.ReadFromJsonAsync<경영SimulationSessionSnapshot>())!;
        var path = "/api/simulation/v1/sessions/" + Uri.EscapeDataString(created.SessionStableId);

        using var response = await client.GetAsync(path + "/fulfillment");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await client.GetFromJsonAsync<경영SimulationSessionSnapshot>(path);
        Assert.Equal(created.Revision, after!.Revision);
        using var missing = await client.GetAsync("/api/simulation/v1/sessions/session:uncreated/fulfillment");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static 경영SimulationSession생성Request CreateRequest() => new()
    {
        ClientRequestId = Guid.NewGuid(), ScenarioStableId = "scenario:fulfillment-adapter-test",
        ScenarioDataRevision = "scenario-data-r1", ScenarioSeed = 1208,
        RuleRevision = "simulation-rule-r1", DurationTicks = 28,
        WorldContext = new SimulationWorldContext생성Request
        {
            FactionStableId = "faction:test", TerritoryStableId = "territory:test",
            SettlementStableId = "settlement:test",
            GameDateStartsOn = new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero),
        },
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Queue<(HttpStatusCode Status, string Body)> Responses { get; } = new();
        public List<string> Requests { get; } = new();
        public List<string> Bodies { get; } = new();
        public List<string?> ContentTypes { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request.Method + " " + request.RequestUri!.AbsolutePath);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            ContentTypes.Add(request.Content?.Headers.ContentType?.MediaType);
            var response = Responses.Dequeue();
            return new HttpResponseMessage(response.Status)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, "application/json"),
            };
        }
    }
}
