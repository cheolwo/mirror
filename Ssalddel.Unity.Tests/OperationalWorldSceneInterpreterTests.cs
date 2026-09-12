using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "운영 지역 장면의 판본, 만료, 부분 실패 격리를 순수 해석기에서 검증한다.",
    Boundary = "Unity 메모리 해석 시험이며 실제 Scene 배치, Play Mode, Game View 증거가 아니다.")]
public sealed class OperationalWorldSceneInterpreterTests
{
    private static readonly DateTime Now = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Client는_인증GET전용경계로_cursor를보내고_응답을즉시해석한다()
    {
        var transport = new RecordingTransport();
        var client = new OperationalWorldSceneClient(
            transport,
            new StaticDecoder(Response(Item("actor:1", 1, Now.AddMinutes(5)), cursor: 8)),
            new OperationalWorldSceneInterpreter());

        var result = await client.RefreshAsync("region:kr:bjd:1126010100", 7, Now);

        Assert.True(result.Accepted);
        Assert.Equal(8, result.Cursor);
        Assert.Contains("cursor=7", transport.Route);
        Assert.True(transport.RequiresAuthentication);
        Assert.False(transport.AllowNotFound);
    }

    [Fact]
    public async Task v2Client는_명시적_schemaVersion만추가한다()
    {
        var transport = new RecordingTransport();
        var response = Response(V2Item("actor:1", 1, Now.AddMinutes(5)), cursor: 8);
        response.SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2;
        var client = new OperationalWorldSceneClient(
            transport,
            new StaticDecoder(response),
            new OperationalWorldSceneInterpreter(),
            OperationalWorldScenePolicy.SchemaVersionV2);

        var result = await client.RefreshAsync("region:kr:bjd:1126010100", 7, Now);

        Assert.True(result.Accepted);
        Assert.Contains("schemaVersion=operational-world-scene.v2", transport.Route);
    }

    [Fact]
    public void 최신판본만적용하고_만료된객체는제거한다()
    {
        var interpreter = new OperationalWorldSceneInterpreter();
        var first = interpreter.Apply(Response(Item("actor:1", 2, Now.AddMinutes(1))), Now);
        var stale = interpreter.Apply(Response(Item("actor:1", 1, Now.AddMinutes(2)), full: false, cursor: 2), Now);
        var expired = interpreter.Expire(Now.AddMinutes(3));

        Assert.True(first.Accepted);
        Assert.Equal(1, first.AddedCount);
        Assert.Equal(2, Assert.Single(stale.CurrentItems).Revision);
        Assert.Equal(1, expired.RemovedCount);
        Assert.Empty(expired.CurrentItems);
    }

    [Fact]
    public void 전체사본에서누락되어도_실패한자료원의기존객체는유지한다()
    {
        var interpreter = new OperationalWorldSceneInterpreter();
        interpreter.Apply(Response(Item("food:1", 1, Now.AddMinutes(5), "FoodDeliveryOS")), Now);

        var response = Response(full: true, cursor: 2);
        response.SourceFailures =
        [
            new OperationalWorldSceneSourceFailure { SourceCode = "FoodDeliveryOS", ErrorCode = "SourceReadFailed" }
        ];
        var result = interpreter.Apply(response, Now.AddSeconds(30));

        Assert.True(result.Accepted);
        Assert.Single(result.CurrentItems);
        Assert.Equal(0, result.RemovedCount);
    }

    [Fact]
    public void 알수없는Schema는_현재장면을바꾸지않는다()
    {
        var interpreter = new OperationalWorldSceneInterpreter();
        interpreter.Apply(Response(Item("actor:1", 1, Now.AddMinutes(5))), Now);
        var incompatible = Response(full: true, cursor: 9);
        incompatible.SchemaVersion = "operational-world-scene.v99";

        var result = interpreter.Apply(incompatible, Now);

        Assert.False(result.Accepted);
        Assert.Equal("SchemaVersionUnsupported", result.ErrorCode);
        Assert.Single(result.CurrentItems);
        Assert.Equal(1, result.Cursor);
    }

    [Fact]
    public void v2의_낮은revision은_그객체만동결하고_다른객체는갱신한다()
    {
        var interpreter = new OperationalWorldSceneInterpreter();
        var first = Response(full: true, cursor: 1);
        first.SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2;
        first.Items =
        [
            V2Item("actor:freeze", 2, Now.AddMinutes(5)),
            V2Item("actor:continue", 2, Now.AddMinutes(5))
        ];
        interpreter.Apply(first, Now);

        var second = Response(full: false, cursor: 2);
        second.SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2;
        second.Items =
        [
            V2Item("actor:freeze", 1, Now.AddMinutes(5)),
            V2Item("actor:continue", 3, Now.AddMinutes(5))
        ];
        var result = interpreter.Apply(second, Now.AddSeconds(1));

        Assert.True(result.Accepted);
        Assert.Equal(2, result.CurrentItems.Single(x => x.SnapshotStableId == "actor:freeze").Revision);
        Assert.Equal(3, result.CurrentItems.Single(x => x.SnapshotStableId == "actor:continue").Revision);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(OperationalWorldSceneApplyDiagnosticCodes.LowerRevision, diagnostic.ErrorCode);
        Assert.True(diagnostic.ExistingObjectFrozen);
    }

    [Fact]
    public void v2의_민감필드오류뒤_더높은정상revision으로_그객체만회복한다()
    {
        var interpreter = new OperationalWorldSceneInterpreter();
        var first = Response(V2Item("actor:1", 1, Now.AddMinutes(5)));
        first.SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2;
        interpreter.Apply(first, Now);

        var invalid = Response(V2Item("actor:1", 2, Now.AddMinutes(5)), full: false, cursor: 2);
        invalid.SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2;
        invalid.Items[0].RepresentationDataJson = "{\"address\":\"private\"}";
        var frozen = interpreter.Apply(invalid, Now.AddSeconds(1));
        Assert.Equal(1, Assert.Single(frozen.CurrentItems).Revision);
        Assert.Equal(OperationalWorldSceneApplyDiagnosticCodes.SensitiveFieldForbidden,
            Assert.Single(frozen.Diagnostics).ErrorCode);

        var recovered = Response(V2Item("actor:1", 3, Now.AddMinutes(5)), full: false, cursor: 3);
        recovered.SchemaVersion = OperationalWorldScenePolicy.SchemaVersionV2;
        var result = interpreter.Apply(recovered, Now.AddSeconds(2));

        Assert.Equal(3, Assert.Single(result.CurrentItems).Revision);
        Assert.Empty(result.Diagnostics);
    }

    private static OperationalWorldSceneResponse Response(
        OperationalWorldSceneItem? item = null,
        bool full = true,
        long cursor = 1)
        => new()
        {
            AreaStableId = "region:kr:bjd:1126010100",
            Cursor = cursor,
            IsFullSnapshot = full,
            AsOfUtc = Now,
            Items = item is null ? [] : [item]
        };

    private static OperationalWorldSceneItem Item(
        string id,
        long revision,
        DateTime expiresAt,
        string operatingSystemId = "WarehouseCommerceFulfillmentOS")
        => new()
        {
            SnapshotStableId = id,
            AreaStableId = "region:kr:bjd:1126010100",
            OperatingSystemId = operatingSystemId,
            ItemKind = OperationalWorldSceneItemKinds.WarehouseActor,
            Revision = revision,
            PublishedAtUtc = Now.AddSeconds(revision),
            ExpiresAtUtc = expiresAt
        };

    private static OperationalWorldSceneItem V2Item(
        string id,
        long revision,
        DateTime expiresAt)
    {
        var item = Item(id, revision, expiresAt);
        item.WorkStableId = "work:" + id;
        item.LifecycleStageCode = "Completed";
        item.AttentionStateCode = OperationalWorldAttentionStateCodes.Completed;
        item.ObjectKindCode = "WarehouseOperation";
        item.SemanticPlaceStableId = "synthetic-place:warehouse-a";
        item.SourceKindCode = OperationalWorldSceneSourceKinds.VerificationSample;
        item.ScenarioRunStableId = "observable-operations-run:test";
        return item;
    }

    private sealed class RecordingTransport : IOperationalWorldProjectionTransport
    {
        public string Route { get; private set; } = string.Empty;
        public bool AllowNotFound { get; private set; }
        public bool RequiresAuthentication { get; private set; }

        public Task<string?> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            Route = relativeRoute;
            AllowNotFound = allowNotFound;
            RequiresAuthentication = requiresAuthentication;
            return Task.FromResult<string?>("{}");
        }
    }

    private sealed class StaticDecoder(OperationalWorldSceneResponse response) : IOperationalWorldSceneDecoder
    {
        public OperationalWorldSceneResponse Decode(string json) => response;
    }
}
