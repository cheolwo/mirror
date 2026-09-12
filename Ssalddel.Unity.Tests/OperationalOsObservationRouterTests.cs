using Ssalddel.Unity.Data.WorldProjection;
using Ssalddel.Unity.WorldProjection;
using Ssalddel.WorkflowRules.Contracts;

namespace Ssalddel.Unity.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "FoodDeliveryOS의 인증 GET부터 Unity OS별 메모리 관찰까지의 읽기 전용 경계를 자동 검증한다.",
    Boundary = "순수 C# 시험이며 실제 Unity import, Scene, Play Mode 또는 Game View 증거가 아니다.")]
public sealed class OperationalOsObservationRouterTests
{
    private static readonly DateTime Now = new(2026, 9, 12, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FoodDeliveryOS완료사본을_읽기전용메모리관찰상태로분배한다()
    {
        var module = new OperationalOsObservationModule(new FoodDeliveryOsObservationAdapter());
        var router = new OperationalOsObservationRouter(new OperationalOsModuleRegistry([module]));
        var item = CompletedFoodItem();

        var result = router.Route(Accepted(item));

        Assert.True(result.Accepted);
        Assert.Empty(result.Diagnostics);
        var state = Assert.Single(result.CurrentStates);
        Assert.Equal(OperationalWorldOperatingSystemIds.FoodDelivery, state.OperatingSystemId);
        Assert.Equal(item.SnapshotStableId, state.WorkStableId);
        Assert.Equal(item.Revision, state.Revision);
        Assert.Equal(음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed, state.LifecycleStageId);
        Assert.Equal(OperationalOsAttentionStateCodes.Completed, state.AttentionStateCode);
        Assert.Equal(item.RepresentationDataJson, state.RepresentationDataJson);
        Assert.Single(module.CurrentStates);
    }

    [Fact]
    public void Interpreter현재사본에서사라진업무는_OS메모리에서도제거한다()
    {
        var module = new OperationalOsObservationModule(new FoodDeliveryOsObservationAdapter());
        var router = new OperationalOsObservationRouter(new OperationalOsModuleRegistry([module]));
        router.Route(Accepted(CompletedFoodItem()));

        var result = router.Route(Accepted());

        Assert.True(result.Accepted);
        Assert.Empty(result.CurrentStates);
        Assert.Empty(module.CurrentStates);
    }

    [Fact]
    public void 미지원OS와_로컬저장을허용한항목은_추측하지않고진단으로보존한다()
    {
        var module = new OperationalOsObservationModule(new FoodDeliveryOsObservationAdapter());
        var router = new OperationalOsObservationRouter(new OperationalOsModuleRegistry([module]));
        var unsafeFood = CompletedFoodItem("food:unsafe");
        unsafeFood.LocalStorageAllowed = true;
        var unknown = CompletedFoodItem("unknown:1");
        unknown.OperatingSystemId = "UnknownOS";

        var result = router.Route(Accepted(unsafeFood, unknown));

        Assert.True(result.Accepted);
        Assert.Empty(result.CurrentStates);
        Assert.Contains(result.Diagnostics, item =>
            item.SnapshotStableId == unsafeFood.SnapshotStableId
            && item.Code == OperationalOsObservationDiagnosticCodes.LocalPersistenceForbidden);
        Assert.Contains(result.Diagnostics, item =>
            item.SnapshotStableId == unknown.SnapshotStableId
            && item.Code == OperationalOsObservationDiagnosticCodes.OperatingSystemUnsupported);
    }

    [Fact]
    public void 거부된Interpreter결과는_기존OS메모리를변경하지않는다()
    {
        var module = new OperationalOsObservationModule(new FoodDeliveryOsObservationAdapter());
        var router = new OperationalOsObservationRouter(new OperationalOsModuleRegistry([module]));
        router.Route(Accepted(CompletedFoodItem()));

        var result = router.Route(new OperationalWorldSceneApplyResult
        {
            Accepted = false,
            ErrorCode = "SchemaVersionUnsupported",
            CurrentItems = []
        });

        Assert.False(result.Accepted);
        Assert.Single(result.CurrentStates);
        Assert.Single(module.CurrentStates);
        Assert.Equal(OperationalOsObservationDiagnosticCodes.ApplyResultRejected, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task 인증GET부터_Interpreter와_FoodDeliveryOS메모리까지_한갱신으로잇는다()
    {
        var item = CompletedFoodItem();
        var transport = new RecordingTransport();
        var client = new OperationalWorldSceneClient(
            transport,
            new StaticDecoder(new OperationalWorldSceneResponse
            {
                AreaStableId = item.AreaStableId,
                Cursor = 9,
                IsFullSnapshot = true,
                AsOfUtc = Now,
                Items = [item]
            }),
            new OperationalWorldSceneInterpreter());
        var module = new OperationalOsObservationModule(new FoodDeliveryOsObservationAdapter());
        var session = new OperationalOsWorldObservationSession(
            client,
            new OperationalOsObservationRouter(new OperationalOsModuleRegistry([module])));

        var refreshed = await session.RefreshAsync(item.AreaStableId, 8, Now);
        var expired = session.Expire(Now.AddHours(2));

        Assert.True(refreshed.SceneResult.Accepted);
        Assert.True(refreshed.OsObservationResult.Accepted);
        Assert.Single(refreshed.OsObservationResult.CurrentStates);
        Assert.Contains("cursor=8", transport.Route, StringComparison.Ordinal);
        Assert.True(transport.RequiresAuthentication);
        Assert.Empty(expired.SceneResult.CurrentItems);
        Assert.Empty(expired.OsObservationResult.CurrentStates);
    }

    private static OperationalWorldSceneApplyResult Accepted(params OperationalWorldSceneItem[] items)
        => new()
        {
            Accepted = true,
            Cursor = 7,
            CurrentItems = items
        };

    private static OperationalWorldSceneItem CompletedFoodItem(string id = "food-delivery-completed:public-1")
        => new()
        {
            SnapshotStableId = id,
            AreaStableId = "region:kr:bjd:1126010100",
            OperatingSystemId = OperationalWorldOperatingSystemIds.FoodDelivery,
            ItemKind = OperationalWorldSceneItemKinds.CompletedLifecycle,
            RoleCode = "FoodDeliveryTeam",
            ActivityCode = 음식배달완료WorldSnapshot정책.OutcomeReceiptConfirmed,
            Revision = 7,
            OccurredAtUtc = Now,
            PublishedAtUtc = Now,
            ExpiresAtUtc = Now.AddHours(1),
            DataPolicyCode = OperationalWorldScenePolicy.OnlineEphemeral,
            LocalStorageAllowed = false,
            ReplayAllowed = false,
            RepresentationDataJson = "{\"actors\":{},\"milestones\":[]}"
        };

    private sealed class RecordingTransport : IOperationalWorldProjectionTransport
    {
        public string Route { get; private set; } = string.Empty;
        public bool RequiresAuthentication { get; private set; }

        public Task<string?> GetAsync(
            string relativeRoute,
            bool allowNotFound,
            bool requiresAuthentication,
            CancellationToken cancellationToken = default)
        {
            Route = relativeRoute;
            RequiresAuthentication = requiresAuthentication;
            return Task.FromResult<string?>("{}");
        }
    }

    private sealed class StaticDecoder(OperationalWorldSceneResponse response) : IOperationalWorldSceneDecoder
    {
        public OperationalWorldSceneResponse Decode(string json) => response;
    }
}
