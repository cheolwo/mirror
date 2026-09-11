using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ssalddel.Simulation.Application;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;
using Ssalddel.Simulation.Infrastructure;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "지식 습득 Preview·Confirm·멱등성·거부 경계와 Application 실행을 검증한다.",
    Boundary = "LocalProcess 단위시험이며 Save·RemoteHost·Unity 증거가 아니다.")]
public sealed class SimulationPlayerKnowledgeTests
{
    [Fact]
    public async Task QueryPreviewConfirm은_LocalProcess와_RemoteHost에서_같은_결과를_만든다()
    {
        var localService = new Simulation플레이어지식Service(
            new InMemorySimulation플레이어지식Store());
        var localLedgerId = "player-knowledge-ledger:parity:local:" +
            Guid.NewGuid().ToString("N");
        localService.Create(localLedgerId, CreateInitialState(true));
        using var local = new LocalSimulationRuntime(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(),
            new 사용하지않는LocalSaveSlotStore(),
            playerKnowledgeService: localService);

        using var factory = new SimulationWebApplicationFactory();
        var remoteLedgerId = "player-knowledge-ledger:parity:remote:" +
            Guid.NewGuid().ToString("N");
        factory.Services.GetRequiredService<Simulation플레이어지식Service>()
            .Create(remoteLedgerId, CreateInitialState(true));
        using var client = factory.CreateClient();

        var localBefore = await local.PlayerKnowledge
            .GetPlayerKnowledgeAsync(localLedgerId);
        var remoteBefore = await client.GetFromJsonAsync<
            Simulation플레이어지식LedgerSnapshot>(
            PlayerKnowledgeUrl(remoteLedgerId));
        Assert.NotNull(remoteBefore);
        AssertEquivalent(localBefore, remoteBefore!);

        var localPreview = await local.PlayerKnowledge
            .PreviewPlayerKnowledgeAsync(localLedgerId, PreviewRequest());
        var remotePreviewResponse = await client.PostAsJsonAsync(
            PlayerKnowledgeUrl(remoteLedgerId) + "/previews", PreviewRequest());
        Assert.Equal(HttpStatusCode.OK, remotePreviewResponse.StatusCode);
        var remotePreview = await remotePreviewResponse.Content.ReadFromJsonAsync<
            Simulation지식습득PreviewSnapshot>();
        Assert.NotNull(remotePreview);
        Assert.Equal(localPreview.CanConfirm, remotePreview!.CanConfirm);
        Assert.Equal(localPreview.AlreadyKnown, remotePreview.AlreadyKnown);
        Assert.Equal(localPreview.BlockReasonCodes, remotePreview.BlockReasonCodes);

        var localAfterPreview = await local.PlayerKnowledge
            .GetPlayerKnowledgeAsync(localLedgerId);
        var remoteAfterPreview = await client.GetFromJsonAsync<
            Simulation플레이어지식LedgerSnapshot>(
            PlayerKnowledgeUrl(remoteLedgerId));
        Assert.NotNull(remoteAfterPreview);
        AssertEquivalent(localBefore, localAfterPreview);
        AssertEquivalent(remoteBefore!, remoteAfterPreview!);

        var request = ConfirmRequest("command:parity:learn");
        var localConfirmed = await local.PlayerKnowledge
            .ConfirmPlayerKnowledgeAsync(localLedgerId, request);
        var remoteConfirmResponse = await client.PostAsJsonAsync(
            PlayerKnowledgeUrl(remoteLedgerId) + "/confirmations", request);
        Assert.Equal(HttpStatusCode.OK, remoteConfirmResponse.StatusCode);
        var remoteConfirmed = await remoteConfirmResponse.Content.ReadFromJsonAsync<
            Simulation지식습득ConfirmResult>();
        Assert.NotNull(remoteConfirmed);
        AssertEquivalent(localConfirmed, remoteConfirmed!);

        var localReused = await local.PlayerKnowledge
            .ConfirmPlayerKnowledgeAsync(localLedgerId, request);
        var remoteReusedResponse = await client.PostAsJsonAsync(
            PlayerKnowledgeUrl(remoteLedgerId) + "/confirmations", request);
        Assert.Equal(HttpStatusCode.OK, remoteReusedResponse.StatusCode);
        var remoteReused = await remoteReusedResponse.Content.ReadFromJsonAsync<
            Simulation지식습득ConfirmResult>();
        Assert.NotNull(remoteReused);
        Assert.True(localReused.Reused);
        AssertEquivalent(localReused, remoteReused!);
    }

    [Theory]
    [InlineData("unknown", true, 7,
        Simulation플레이어지식Codes.RecipeUnknown)]
    [InlineData("known", false, 7,
        Simulation플레이어지식Codes.KnowledgeSourceUnavailable)]
    [InlineData("known", true, 6,
        Simulation플레이어지식Codes.ExpectedRevisionMismatch)]
    public async Task 거부경계도_LocalProcess와_RemoteHost에서_같고_상태는_변하지_않는다(
        string recipeMode, bool accessible, long revision,
        string expectedError)
    {
        var initial = CreateInitialState(accessible);
        var localService = new Simulation플레이어지식Service(
            new InMemorySimulation플레이어지식Store());
        var localLedgerId = "player-knowledge-ledger:reject:local:" +
            Guid.NewGuid().ToString("N");
        localService.Create(localLedgerId, initial);
        using var local = new LocalSimulationRuntime(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(),
            new 사용하지않는LocalSaveSlotStore(),
            playerKnowledgeService: localService);

        using var factory = new SimulationWebApplicationFactory();
        var remoteLedgerId = "player-knowledge-ledger:reject:remote:" +
            Guid.NewGuid().ToString("N");
        factory.Services.GetRequiredService<Simulation플레이어지식Service>()
            .Create(remoteLedgerId, CreateInitialState(accessible));
        using var client = factory.CreateClient();

        var localBefore = await local.PlayerKnowledge
            .GetPlayerKnowledgeAsync(localLedgerId);
        var remoteBefore = await client.GetFromJsonAsync<
            Simulation플레이어지식LedgerSnapshot>(
            PlayerKnowledgeUrl(remoteLedgerId));
        Assert.NotNull(remoteBefore);

        var request = ConfirmRequest("command:parity:reject:" + expectedError);
        request.ExpectedWorldRevision = revision;
        if (recipeMode == "unknown")
            request.RecipeStableId = "recipe:nature:unknown.v1";

        var localError = await Assert.ThrowsAsync<SimulationConflictException>(
            async () => await local.PlayerKnowledge
                .ConfirmPlayerKnowledgeAsync(localLedgerId, request));
        Assert.Equal(expectedError, localError.ErrorCode);

        var remoteResponse = await client.PostAsJsonAsync(
            PlayerKnowledgeUrl(remoteLedgerId) + "/confirmations", request);
        Assert.Equal(HttpStatusCode.Conflict, remoteResponse.StatusCode);
        var remoteError = await remoteResponse.Content.ReadFromJsonAsync<
            SimulationErrorResponse>();
        Assert.NotNull(remoteError);
        Assert.Equal(expectedError, remoteError!.ErrorCode);

        var localAfter = await local.PlayerKnowledge
            .GetPlayerKnowledgeAsync(localLedgerId);
        var remoteAfter = await client.GetFromJsonAsync<
            Simulation플레이어지식LedgerSnapshot>(
            PlayerKnowledgeUrl(remoteLedgerId));
        Assert.NotNull(remoteAfter);
        AssertEquivalent(localBefore, localAfter);
        AssertEquivalent(remoteBefore!, remoteAfter!);
    }

    [Fact]
    public void Preview는_지식원장과_WorldRevision을_변경하지_않는다()
    {
        var aggregate = CreateAggregate();
        var before = aggregate.Snapshot();

        var preview = aggregate.Preview(PreviewRequest());
        var after = aggregate.Snapshot();

        Assert.True(preview.CanConfirm);
        Assert.False(preview.AlreadyKnown);
        Assert.Equal(before.WorldRevision, after.WorldRevision);
        Assert.Equal(before.StateHashSha256, after.StateHashSha256);
        Assert.Empty(after.KnownRecipeStableIds);
        Assert.Empty(after.ActionLedger.TailRecords);
    }

    [Fact]
    public void Confirm은_처방을_한번_추가하고_같은_revision의_행위기록을_남긴다()
    {
        var aggregate = CreateAggregate();

        var result = aggregate.Confirm(ConfirmRequest("command:learn:1"));

        Assert.True(result.Added);
        Assert.False(result.Reused);
        Assert.Equal(8, result.KnowledgeLedger.WorldRevision);
        Assert.Equal(Simulation플레이어지식Codes.기초약초차RecipeStableId,
            Assert.Single(result.KnowledgeLedger.KnownRecipeStableIds));
        var action = Assert.IsType<Simulation행위발현Record>(result.ActionRecord);
        Assert.Equal(7, action.BeforeWorldRevision);
        Assert.Equal(8, action.AfterWorldRevision);
        Assert.Equal(Simulation플레이어지식Codes.지식습득WorldInteractionId,
            action.WorldInteractionId);
        Assert.Contains(Simulation행위변화의미Codes.플레이어진척변경,
            action.변화의미Codes);
        Assert.Equal(action.행위기록StableId,
            Assert.Single(result.KnowledgeLedger.ActionLedger.TailRecords)
                .행위기록StableId);
    }

    [Fact]
    public void 같은_Command는_결과를_재사용하고_중복_지식과_행위기록을_만들지_않는다()
    {
        var aggregate = CreateAggregate();
        var request = ConfirmRequest("command:learn:idem");
        var first = aggregate.Confirm(request);

        var second = aggregate.Confirm(request);

        Assert.True(second.Reused);
        Assert.True(second.Added);
        Assert.Equal(first.KnowledgeLedger.StateHashSha256,
            second.KnowledgeLedger.StateHashSha256);
        Assert.Single(second.KnowledgeLedger.KnownRecipeStableIds);
        Assert.Single(second.KnowledgeLedger.ActionLedger.TailRecords);
    }

    [Fact]
    public void 이미_아는_처방의_새_Command는_무변경으로_재사용된다()
    {
        var aggregate = CreateAggregate();
        aggregate.Confirm(ConfirmRequest("command:learn:first"));
        var before = aggregate.Snapshot();
        var request = ConfirmRequest("command:learn:again");
        request.ExpectedWorldRevision = before.WorldRevision;

        var result = aggregate.Confirm(request);

        Assert.False(result.Added);
        Assert.True(result.Reused);
        Assert.Null(result.ActionRecord);
        Assert.Equal(before.StateHashSha256,
            result.KnowledgeLedger.StateHashSha256);

        request.KnowledgeSourceStableId = "knowledge-source:nature:note:other";
        var conflict = Assert.Throws<SimulationConflictException>(
            () => aggregate.Confirm(request));
        Assert.Equal(Simulation플레이어지식Codes.CommandPayloadConflict,
            conflict.ErrorCode);
        Assert.Equal(before.StateHashSha256, aggregate.Snapshot().StateHashSha256);
    }

    [Theory]
    [InlineData("unknown", true, 7, "PlayerKnowledgeRecipeUnknown")]
    [InlineData("known", false, 7, "PlayerKnowledgeSourceUnavailable")]
    [InlineData("known", true, 6, "PlayerKnowledgeExpectedRevisionMismatch")]
    public void 알수없는처방_접근불가_revision불일치는_상태를_바꾸지_않는다(
        string recipeMode, bool accessible, long expectedRevision,
        string expectedError)
    {
        var aggregate = CreateAggregate(accessible);
        var before = aggregate.Snapshot();
        var request = ConfirmRequest("command:blocked:" + expectedError);
        request.ExpectedWorldRevision = expectedRevision;
        if (recipeMode == "unknown")
            request.RecipeStableId = "recipe:nature:unknown.v1";

        var error = Assert.Throws<SimulationConflictException>(
            () => aggregate.Confirm(request));

        Assert.Equal(expectedError, error.ErrorCode);
        Assert.Equal(before.StateHashSha256, aggregate.Snapshot().StateHashSha256);
    }

    [Fact]
    public void Application서비스는_저장소를_통해_같은_순수_계약을_실행한다()
    {
        const string ledgerId = "player-knowledge-ledger:solo";
        var service = new Simulation플레이어지식Service(
            new InMemorySimulation플레이어지식Store());
        service.Create(ledgerId, CreateInitialState(true));

        Assert.True(service.Preview(ledgerId, PreviewRequest()).CanConfirm);
        var result = service.Confirm(ledgerId,
            ConfirmRequest("command:service:learn"));

        Assert.True(result.Added);
        Assert.Single(service.Get(ledgerId).KnownRecipeStableIds);
    }

    private static Simulation플레이어지식Aggregate CreateAggregate(
        bool accessible = true)
        => new Simulation플레이어지식Aggregate(
            CreateInitialState(accessible));

    private static Simulation플레이어지식InitialStateRequest CreateInitialState(
        bool accessible)
        => new()
        {
            WorldStableId = "world:nature:knowledge-fixture",
            SessionStableId = "simulation-session:knowledge-fixture",
            PlayerStableId = "player:solo",
            InitialWorldRevision = 7,
            KnowledgeSources = new[]
            {
                new Simulation처방지식SourceDefinition
                {
                    KnowledgeSourceStableId = "knowledge-source:nature:note:01",
                    IsAccessible = accessible,
                    ApprovedRecipeStableIds = new[]
                    {
                        Simulation플레이어지식Codes.기초약초차RecipeStableId,
                    },
                },
            },
        };

    private static Simulation지식습득PreviewRequest PreviewRequest()
        => new()
        {
            ObservedWorldRevision = 7,
            PlayerStableId = "player:solo",
            RecipeStableId =
                Simulation플레이어지식Codes.기초약초차RecipeStableId,
            KnowledgeSourceStableId = "knowledge-source:nature:note:01",
        };

    private static Simulation지식습득ConfirmRequest ConfirmRequest(
        string commandId)
        => new()
        {
            CommandId = commandId,
            ExpectedWorldRevision = 7,
            PlayerStableId = "player:solo",
            RecipeStableId =
                Simulation플레이어지식Codes.기초약초차RecipeStableId,
            KnowledgeSourceStableId = "knowledge-source:nature:note:01",
        };

    private static string PlayerKnowledgeUrl(string ledgerStableId)
        => "/api/simulation/v1/player-knowledge-ledgers/" + ledgerStableId;

    private static void AssertEquivalent(
        Simulation플레이어지식LedgerSnapshot expected,
        Simulation플레이어지식LedgerSnapshot actual)
    {
        Assert.Equal(expected.RuleRevision, actual.RuleRevision);
        Assert.Equal(expected.WorldStableId, actual.WorldStableId);
        Assert.Equal(expected.SessionStableId, actual.SessionStableId);
        Assert.Equal(expected.PlayerStableId, actual.PlayerStableId);
        Assert.Equal(expected.WorldRevision, actual.WorldRevision);
        Assert.Equal(expected.KnownRecipeStableIds,
            actual.KnownRecipeStableIds);
        Assert.Equal(expected.StateHashSha256, actual.StateHashSha256);
        Assert.Equal(expected.ActionLedger.StateHashSha256,
            actual.ActionLedger.StateHashSha256);
        Assert.Equal(
            expected.ActionLedger.TailRecords.Select(value =>
                value.행위기록StableId),
            actual.ActionLedger.TailRecords.Select(value =>
                value.행위기록StableId));
    }

    private static void AssertEquivalent(
        Simulation지식습득ConfirmResult expected,
        Simulation지식습득ConfirmResult actual)
    {
        Assert.Equal(expected.Added, actual.Added);
        Assert.Equal(expected.Reused, actual.Reused);
        AssertEquivalent(expected.KnowledgeLedger, actual.KnowledgeLedger);
        Assert.Equal(expected.ActionRecord?.행위기록StableId,
            actual.ActionRecord?.행위기록StableId);
        Assert.Equal(expected.ActionRecord?.BeforeWorldRevision,
            actual.ActionRecord?.BeforeWorldRevision);
        Assert.Equal(expected.ActionRecord?.AfterWorldRevision,
            actual.ActionRecord?.AfterWorldRevision);
    }

    private sealed class 사용하지않는LocalSaveSlotStore :
        ISimulationLocalSaveSlotStore
    {
        public void Write(string slotStableId,
            SimulationSessionSavePackage package)
            => throw new NotSupportedException();

        public SimulationLocalSaveSlotPackage Read(string slotStableId)
            => throw new NotSupportedException();
    }
}
