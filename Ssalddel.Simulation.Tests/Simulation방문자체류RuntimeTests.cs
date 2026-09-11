using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Contracts.Common.Metadata;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[SsalddelEvidenceResponsibility(
    SsalddelEvidenceStage.E3,
    "방문자 Local Runtime과 HTTP Host의 같은 규칙·멱등·거부 결과를 검증한다.",
    Boundary = "TestServer와 독립 원장 시험이며 Session·Save·실제 네트워크·Scene 증거가 아니다.",
    WorldInteractionIds = new[] { Simulation공동체방문자체류Codes.WorldInteractionId })]
public sealed class Simulation방문자체류RuntimeTests
{
    [Theory]
    [InlineData(Simulation공동체방문자체류Codes.임시체류수용)]
    [InlineData(Simulation공동체방문자체류Codes.거절선택)]
    public async Task 조회_미리보기_확정_재조회가_같은_상태와_행위기록을_반환한다(string decision)
    {
        using var context = new Context();
        var before = await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id);
        Equivalent(before, await context.GetRemote());
        var preview = new Simulation공동체방문자체류PreviewRequest
        {
            ObservedWorldRevision = 7, VisitorStableId = "visitor:one", DecisionCode = decision,
        };
        Equivalent(await context.Local.CommunityVisitors.PreviewVisitorStayAsync(context.Id, preview),
            await context.Post<Simulation공동체방문자체류PreviewSnapshot>("previews", preview));
        Equivalent(before, await context.GetRemote());
        Equivalent(before, await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id));

        var request = Request(decision);
        var local = await context.Local.CommunityVisitors.ConfirmVisitorStayAsync(context.Id, request);
        Equivalent(local, await context.Post<Simulation공동체방문자체류ConfirmResult>("confirmations", request));
        Assert.Equal(8, local.Ledger.WorldRevision);
        Assert.Single(local.Ledger.ActionLedger.TailRecords);
        Assert.Equal(decision == Simulation공동체방문자체류Codes.임시체류수용 ? 2 : 1,
            local.Ledger.OccupiedGuestCapacity);

        var duplicate = await context.Local.CommunityVisitors.ConfirmVisitorStayAsync(context.Id, request);
        Assert.True(duplicate.Reused);
        Equivalent(duplicate, await context.Post<Simulation공동체방문자체류ConfirmResult>("confirmations", request));
        Equivalent(local.Ledger, await context.GetRemote());
        Equivalent(local.Ledger, await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id));
    }

    [Theory]
    [InlineData("revision", Simulation공동체방문자체류Codes.ExpectedRevisionMismatch)]
    [InlineData("unknown", Simulation공동체방문자체류Codes.VisitorUnknown)]
    [InlineData("decision", Simulation공동체방문자체류Codes.DecisionInvalid)]
    [InlineData("capacity", Simulation공동체방문자체류Codes.CapacityUnavailable)]
    [InlineData("decided", Simulation공동체방문자체류Codes.VisitorAlreadyDecided)]
    [InlineData("payload", Simulation공동체방문자체류Codes.CommandPayloadConflict)]
    public async Task 거부_사유가_같고_상태는_변하지_않는다(string mode, string errorCode)
    {
        using var context = new Context(mode == "capacity");
        if (mode is "decided" or "payload")
        {
            var initial = Request(Simulation공동체방문자체류Codes.거절선택);
            await context.Local.CommunityVisitors.ConfirmVisitorStayAsync(context.Id, initial);
            await context.Post<Simulation공동체방문자체류ConfirmResult>("confirmations", initial);
        }
        var before = await context.GetRemote();
        var request = Request(Simulation공동체방문자체류Codes.임시체류수용);
        if (mode == "revision") request.ExpectedWorldRevision = 6;
        if (mode == "unknown") request.VisitorStableId = "visitor:unknown";
        if (mode == "decision") request.DecisionCode = "JoinPermanently";
        if (mode == "decided")
        {
            request.CommandId = "command:second";
            request.ExpectedWorldRevision = 8;
        }
        var error = await Assert.ThrowsAsync<SimulationConflictException>(async () =>
            await context.Local.CommunityVisitors.ConfirmVisitorStayAsync(context.Id, request));
        Assert.Equal(errorCode, error.ErrorCode);
        using var response = await context.Client.PostAsJsonAsync(context.Url + "/confirmations", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(errorCode, (await response.Content.ReadFromJsonAsync<SimulationErrorResponse>())!.ErrorCode);
        Equivalent(before, await context.GetRemote());
        Equivalent(before, await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id));
    }

    [Fact]
    public async Task 취소된_Local_요청은_확정하지_않는다()
    {
        using var context = new Context();
        var before = await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await context.Local.CommunityVisitors.ConfirmVisitorStayAsync(context.Id,
                Request(Simulation공동체방문자체류Codes.임시체류수용), cancellation.Token));
        Equivalent(before, await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id));
    }

    [Fact]
    public async Task 미등록원장과_잘못된명령은_404와_400으로_매핑되고_원장은_보존된다()
    {
        using var context = new Context();
        using var missing = await context.Client.GetAsync(context.Url + "-missing");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("CommunityVisitorStayLedgerNotFound",
            (await missing.Content.ReadFromJsonAsync<SimulationErrorResponse>())!.ErrorCode);
        await Assert.ThrowsAsync<SimulationNotFoundException>(async () =>
            await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id + "-missing"));
        var before = await context.GetRemote();
        var request = Request(Simulation공동체방문자체류Codes.임시체류수용);
        request.CommandId = " ";
        var localError = await Assert.ThrowsAsync<SimulationContractException>(async () =>
            await context.Local.CommunityVisitors.ConfirmVisitorStayAsync(context.Id, request));
        using var invalid = await context.Client.PostAsJsonAsync(context.Url + "/confirmations", request);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(localError.ErrorCode,
            (await invalid.Content.ReadFromJsonAsync<SimulationErrorResponse>())!.ErrorCode);
        Equivalent(before, await context.GetRemote());
        Equivalent(before, await context.Local.CommunityVisitors.GetVisitorsAsync(context.Id));
    }

    private static void Equivalent<T>(T expected, T actual)
        => Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));

    private static Simulation공동체방문자체류ConfirmRequest Request(string decision) => new()
    {
        CommandId = "command:visitor:decision", ExpectedWorldRevision = 7,
        VisitorStableId = "visitor:one", DecisionCode = decision,
    };

    private sealed class Context : IDisposable
    {
        private readonly WebApplicationFactory<Program> factory = new SimulationWebApplicationFactory();
        public string Id { get; } = "visitor-ledger:" + Guid.NewGuid().ToString("N");
        public string Url => "/api/simulation/v1/community-visitor-stay-ledgers/" + Uri.EscapeDataString(Id);
        public HttpClient Client { get; }
        public LocalSimulationRuntime Local { get; }

        public Context(bool full = false)
        {
            var service = new Simulation공동체방문자체류Service(new InMemorySimulation공동체방문자체류Store());
            Simulation공동체방문자체류InitialStateRequest Initial() => new()
            {
                WorldStableId = "world:camp", SessionStableId = "session:visitor-fixture",
                HostPlayerStableId = "player:host", InitialWorldRevision = 7,
                GuestCapacity = 3, OccupiedGuestCapacity = full ? 3 : 1,
                Visitors = new[] { new Simulation공동체방문자Definition { VisitorStableId = "visitor:one" } },
            };
            service.Create(Id, Initial());
            Local = new LocalSimulationRuntime(new InMemory경영SimulationSessionStore(),
                new InMemorySimulationSessionSaveStore(), new 사용금지SaveStore(), visitorStayService: service);
            factory.Services.GetRequiredService<Simulation공동체방문자체류Service>().Create(Id, Initial());
            Client = factory.CreateClient();
        }

        public async Task<Simulation공동체방문자체류LedgerSnapshot> GetRemote()
            => (await Client.GetFromJsonAsync<Simulation공동체방문자체류LedgerSnapshot>(Url))!;

        public async Task<T> Post<T>(string suffix, object request)
        {
            using var response = await Client.PostAsJsonAsync(Url + "/" + suffix, request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<T>())!;
        }

        public void Dispose() { Local.Dispose(); Client.Dispose(); factory.Dispose(); }
    }

    private sealed class 사용금지SaveStore : ISimulationLocalSaveSlotStore
    {
        public void Write(string slotStableId, SimulationSessionSavePackage package) => throw new NotSupportedException();
        public SimulationLocalSaveSlotPackage Read(string slotStableId) => throw new NotSupportedException();
    }
}
