using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ssalddel.Simulation.Contracts;
using Ssalddel.Simulation.Domain;

namespace Ssalddel.Simulation.Tests;

[Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceResponsibility(
    Ssalddel.Contracts.Common.Metadata.SsalddelEvidenceStage.E3,
    "Simulation·Unity 계약과 결정성 및 회귀 증거를 검증한다.",
    Boundary = "자동 시험 통과와 실제 Play Mode·Game View·E 승격 증거를 구분한다.")]
public sealed class SimulationNpcWorkforceTests
{
    [Fact]
    public async System.Threading.Tasks.Task LocalRuntime음식점포트는_접수와조리완료를같은Core에서실행한다()
    {
        using var runtime = new LocalSimulationRuntime(new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore(), new 카드시험SaveSlotStore());
        var current = await runtime.Sessions.CreateAsync(CreateSessionRequest());
        var food = new Simulation음식배달PreviewRequest
        {
            RestaurantPreparationOnly = true, FoodOrderStableId = "food-order:restaurant:local",
            MenuItemStableId = "menu-item:restaurant:sample", RestaurantFacilityStableId = "facility:restaurant:sample",
            DestinationFacilityStableId = "facility:residence:sample", DeliveryScopeStableId = "delivery-scope:sample",
            OrdererStableId = "participant:customer:sample", ActorStableId = "actor:restaurant:sample", Quantity = 1,
            SourceStableIds = new[] { "source:fixture:restaurant:local" },
        };
        ISimulationFoodOrderRuntime orders = runtime;
        var preview = await orders.PreviewFoodDeliveryAsync(current.SessionStableId, food);
        Assert.Contains("RestaurantPreparationOnly", preview.BoundaryCodes);
        Assert.Empty((await runtime.Sessions.GetAsync(current.SessionStableId)).FoodDeliveries);
        current = await orders.ConfirmFoodDeliveryAsync(current.SessionStableId,
            new Simulation음식배달ConfirmRequest
            { CommandId = "command:restaurant:local", ExpectedRevision = current.Revision, FoodDelivery = food });
        for (var i = 0; i < 4; i++) current = await runtime.AdvanceWorldTickAsync(current.SessionStableId,
            new 경영SimulationTick진행Request { CommandId = $"command:restaurant:tick:{i}", ExpectedRevision = current.Revision, TickCount = 1 });
        Assert.Equal("픽업대기", Assert.Single(current.FoodDeliveries).StateCode);
        Assert.Null(Assert.Single(current.FoodDeliveries).DeliveredTick);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async System.Threading.Tasks.Task 독립출고표본은_정책재개후_피킹포장을한번완료하고운송하지않는다(bool disableAfterStart)
    {
        var store = new InMemory경영SimulationSessionStore();
        var saves = new InMemorySimulationSessionSaveStore();
        using var runtime = new LocalSimulationRuntime(store, saves, new 카드시험SaveSlotStore());
        var creation = 창고출고관찰표본.Create(Guid.NewGuid());
        Assert.DoesNotContain(creation.SpatialWorld!.Definitions, value =>
            value.SpatialStableId == PyeongchangSimulation공간StableIds.HubTown운송회랑);
        var current = await runtime.Sessions.CreateAsync(creation);
        var inboundCards = new Ssalddel.Unity.Warehouse.창고정책카드Presenter(runtime,
            current.SessionStableId, PyeongchangSimulationWorldStableIds.진부Hub시설);
        await inboundCards.조회Async();
        Assert.Empty(inboundCards.카드목록);
        var cards = new Ssalddel.Unity.Warehouse.창고정책카드Presenter(runtime,
            current.SessionStableId, PyeongchangSimulationWorldStableIds.진부Hub시설, includeOutbound: true);
        await cards.조회Async();
        var policyId = Assert.Single(cards.카드목록);
        Assert.Equal("출고·피킹·포장", cards.제목(policyId));
        var draft = cards.편집시작(policyId);
        draft.AutomationEnabled = false;
        await cards.적용Async(draft, "command:outbound:pause");
        current = await runtime.GetPolicySessionAsync(current.SessionStableId);
        current = await runtime.AdvanceWorldTickAsync(current.SessionStableId,
            new 경영SimulationTick진행Request { CommandId = "command:outbound:paused", ExpectedRevision = current.Revision, TickCount = 1 });
        Assert.Empty(current.Tasks);
        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted, Assert.Single(current.NpcFacilityInventories).StateCode);
        await cards.조회Async();
        draft = cards.편집시작(policyId);
        draft.AutomationEnabled = true;
        draft.Priority = 333;
        await cards.적용Async(draft, "command:outbound:resume");
        current = await runtime.GetPolicySessionAsync(current.SessionStableId);
        var states = new System.Collections.Generic.List<string>();
        for (var i = 0; i < 10; i++)
        {
            current = await runtime.AdvanceWorldTickAsync(current.SessionStableId,
                new 경영SimulationTick진행Request { CommandId = $"command:outbound:tick:{i}", ExpectedRevision = current.Revision, TickCount = 1 });
            states.Add(Assert.Single(current.NpcFacilityInventories).StateCode);
            if (i == 0 && disableAfterStart)
            {
                await cards.조회Async();
                draft = cards.편집시작(policyId);
                draft.AutomationEnabled = false;
                await cards.적용Async(draft, "command:outbound:disable-running");
                current = await runtime.GetPolicySessionAsync(current.SessionStableId);
            }
        }
        Assert.Contains(SimulationNpcInventoryStateCodes.OutboundRequested, states);
        Assert.Contains(SimulationNpcInventoryStateCodes.Picked, states);
        Assert.Equal(SimulationNpcInventoryStateCodes.OutboundReady, states.Last());
        Assert.Single(current.Tasks);
        Assert.Equal(new[] { "WI-HUB-03", "WI-HUB-04", "WI-HUB-05" },
            current.NpcRoutineExecutions.Select(value => value.WorldInteractionId));
        Assert.DoesNotContain(current.Tasks, value => value.ActionCode == SimulationNpcActionCodes.FreightTransport);
        Assert.Equal(300m, Assert.Single(current.NpcFacilityInventories).Quantity);
        await cards.조회Async();
        Assert.Contains(cards.재고상태, value => value.Contains("포장 완료·출고 대기"));
        var service = new 경영SimulationSessionService(store, saves);
        var save = service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:outbound:card", ExpectedRevision = current.Revision });
        var restored = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), saves)
            .Restore(new SimulationSessionRestoreRequest { SaveStableId = save.SaveStableId });
        Assert.Equal(save.ReplayHash, restored.ReplayHash);
        Assert.Equal(333, Assert.Single(restored.Session.NpcWorkPolicies).Priority);
        Assert.Equal(!disableAfterStart, Assert.Single(restored.Session.NpcWorkPolicies).AutomationEnabled);
    }

    [Fact]
    public async System.Threading.Tasks.Task 독립관찰표본은_직접작업Confirm없이_검수적치후출고하지않는다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var saves = new InMemorySimulationSessionSaveStore();
        using var runtime = new LocalSimulationRuntime(store, saves, new 카드시험SaveSlotStore());
        var current = await runtime.Sessions.CreateAsync(창고입고관찰표본.Create(Guid.NewGuid()));
        var cards = new Ssalddel.Unity.Warehouse.창고정책카드Presenter(
            runtime, current.SessionStableId, PyeongchangSimulationWorldStableIds.진부Hub시설);
        await cards.조회Async();
        var draft = cards.편집시작(PyeongchangSimulationNpcStableIds.진부입고검수정책);
        draft.AutomationEnabled = false;
        await cards.적용Async(draft, "command:observer:pause");
        current = await runtime.GetPolicySessionAsync(current.SessionStableId);
        current = await runtime.AdvanceWorldTickAsync(current.SessionStableId,
            new 경영SimulationTick진행Request { CommandId = "command:observer:paused-tick", ExpectedRevision = current.Revision, TickCount = 1 });
        Assert.Empty(current.Tasks);
        await cards.조회Async();
        draft = cards.편집시작(draft.PolicyStableId);
        draft.AutomationEnabled = true;
        await cards.적용Async(draft, "command:observer:resume");
        current = await runtime.GetPolicySessionAsync(current.SessionStableId);
        for (var i = 0; i < 16; i++)
            current = await runtime.AdvanceWorldTickAsync(current.SessionStableId,
                new 경영SimulationTick진행Request { CommandId = $"command:observer:tick:{i}", ExpectedRevision = current.Revision, TickCount = 1 });
        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted, Assert.Single(current.NpcFacilityInventories).StateCode);
        Assert.Equal(new[] { "WI-001", "WI-002" }, current.NpcRoutineExecutions.Select(value => value.WorldInteractionId));
        Assert.Equal(300m, Assert.Single(current.NpcFacilityInventories).Quantity);
        await cards.조회Async();
        Assert.Contains(cards.재고상태, value => value.Contains("적치 완료"));
        Assert.Equal(2, current.Tasks.Length);
    }

    [Fact]
    public async System.Threading.Tasks.Task 정책카드는_검수차단과재개후_같은재고의적치완료를관찰한다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var saves = new InMemorySimulationSessionSaveStore();
        var context = new TestContext(new 경영SimulationSessionService(store, saves));
        var current = context.Service.Create(CreateSessionRequest());
        using var runtime = new LocalSimulationRuntime(store, saves, new 카드시험SaveSlotStore());
        var cards = new Ssalddel.Unity.Warehouse.창고정책카드Presenter(
            runtime, current.SessionStableId, PyeongchangSimulationWorldStableIds.진부Hub시설);
        await cards.조회Async();
        Assert.Equal(2, cards.카드목록.Length);
        var draft = cards.편집시작(PyeongchangSimulationNpcStableIds.진부입고검수정책);
        draft.AutomationEnabled = false;
        // 초안 편집은 권위 원장을 바꾸지 않는다.
        Assert.True(context.Service.Get(current.SessionStableId).NpcWorkPolicies.Single(
            value => value.PolicyStableId == draft.PolicyStableId).AutomationEnabled);
        await cards.적용Async(draft, "command:card:disable");
        current = context.Service.Get(current.SessionStableId);
        current = Confirm(context, current, InboundInspection("card"), "command:card:inbound");
        current = Advance(context, current, "command:card:blocked");
        await cards.조회Async();
        Assert.Contains(cards.작업상태, value => value.Contains("차단"));
        Assert.Empty(current.NpcFacilityInventories);

        draft = cards.편집시작(PyeongchangSimulationNpcStableIds.진부입고검수정책);
        draft.AutomationEnabled = true;
        draft.Priority = 321;
        await cards.적용Async(draft, "command:card:enable");
        current = context.Service.Get(current.SessionStableId);
        for (var i = 0; i < 4; i++) current = Advance(context, current, $"command:card:inspection:{i}");
        var inventory = Assert.Single(current.NpcFacilityInventories);
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible, inventory.StateCode);
        current = context.Service.ConfirmWarehousePutAway(current.SessionStableId,
            new SimulationWarehousePutAwayConfirmRequest
            {
                CommandId = "command:card:putaway", ExpectedRevision = current.Revision,
                PutAway = new SimulationWarehousePutAwayPreviewRequest
                {
                    InventoryStableId = inventory.InventoryStableId, InventoryRevision = inventory.Revision,
                    ActorStableId = PyeongchangSimulationNpcStableIds.진부적재담당,
                    PutAwayDurationTicks = 2, SourceStableIds = new[] { inventory.InventoryStableId },
                },
            });
        for (var i = 0; i < 3; i++) current = Advance(context, current, $"command:card:putaway:{i}");
        await cards.조회Async();
        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(current.NpcFacilityInventories).StateCode);
        Assert.Contains(cards.재고상태, value => value.Contains("100 KGM"));
        Assert.Contains(cards.작업상태, value => value.Contains("완료"));
        var save = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        { SaveStableId = "save:card:warehouse", ExpectedRevision = current.Revision });
        var restored = new 경영SimulationSessionService(new InMemory경영SimulationSessionStore(), saves)
            .Restore(new SimulationSessionRestoreRequest { SaveStableId = save.SaveStableId });
        Assert.Equal(save.ReplayHash, restored.ReplayHash);
        Assert.Equal(321, restored.Session.NpcWorkPolicies.Single(
            value => value.PolicyStableId == draft.PolicyStableId).Priority);
    }

    [Fact]
    public async System.Threading.Tasks.Task 오래된카드초안은_최신설정을덮지않고_재조회한다()
    {
        var store = new InMemory경영SimulationSessionStore();
        var saves = new InMemorySimulationSessionSaveStore();
        var service = new 경영SimulationSessionService(store, saves);
        var session = service.Create(CreateSessionRequest());
        using var runtime = new LocalSimulationRuntime(store, saves, new 카드시험SaveSlotStore());
        var cards = new Ssalddel.Unity.Warehouse.창고정책카드Presenter(
            runtime, session.SessionStableId, PyeongchangSimulationWorldStableIds.진부Hub시설);
        await cards.조회Async();
        var stale = cards.편집시작(PyeongchangSimulationNpcStableIds.진부입고검수정책);
        var fresh = cards.편집시작(stale.PolicyStableId);
        fresh.Priority = 250;
        await cards.적용Async(fresh, "command:card:fresh");
        stale.Priority = 999;
        await Assert.ThrowsAsync<SimulationConflictException>(() => cards.적용Async(stale, "command:card:stale"));
        Assert.Equal(250, cards.편집시작(stale.PolicyStableId).Priority);
        Assert.Contains("다시 선택", cards.마지막안내);
        var invalid = cards.편집시작(stale.PolicyStableId);
        invalid.Priority = 1001;
        await Assert.ThrowsAsync<SimulationContractException>(() => cards.적용Async(invalid, "command:card:invalid"));
        Assert.Equal(250, cards.편집시작(stale.PolicyStableId).Priority);
    }

    private sealed class 카드시험SaveSlotStore : ISimulationLocalSaveSlotStore
    {
        public void Write(string id, SimulationSessionSavePackage package) => throw new NotSupportedException();
        public SimulationLocalSaveSlotPackage Read(string id) => throw new NotSupportedException();
    }

    [Fact]
    public void 진부Hub입고검수는_이동과작업을거쳐_보관가능재고가된다()
    {
        var context = CreateContext();
        var created = context.Service.Create(CreateSessionRequest());
        var previewRequest = InboundInspection("1");

        var preview = context.Service.PreviewDecision(created.SessionStableId, previewRequest);
        Assert.Equal(3, preview.TaskPlan.DurationTicks);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부입고검수담당,
            preview.TaskPlan.AssignedActorStableId);

        var scheduled = context.Service.ConfirmDecision(
            created.SessionStableId,
            new SimulationDecisionConfirmRequest
            {
                CommandId = "command:npc-inbound-inspection:1",
                ExpectedRevision = created.Revision,
                Preview = previewRequest,
            });
        var assignment = Assert.Single(scheduled.NpcTaskAssignments);
        Assert.Equal(SimulationNpcActionPhaseCodes.Scheduled, assignment.PhaseCode);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부입고검수담당,
            assignment.ActorStableId);
        Assert.Equal(SimulationNpcInventoryStateCodes.PendingInspection,
            Assert.Single(scheduled.NpcFacilityInventories).StateCode);

        var navigating = Advance(context, scheduled, "command:npc-tick:1");
        Assert.Equal(SimulationNpcActionPhaseCodes.Navigating,
            Assert.Single(navigating.NpcActionProjections).PhaseCode);

        var working = Advance(context, navigating, "command:npc-tick:2");
        var workingProjection = Assert.Single(working.NpcActionProjections);
        Assert.Equal(SimulationNpcActionPhaseCodes.Working, workingProjection.PhaseCode);
        Assert.Equal(0.5m, workingProjection.ProgressRate);

        var completed = Advance(context, working, "command:npc-tick:3");
        Assert.Equal(SimulationTaskStateCodes.Completed, Assert.Single(completed.Tasks).StateCode);
        Assert.Equal(SimulationNpcActionPhaseCodes.Completed,
            Assert.Single(completed.NpcActionProjections).PhaseCode);
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible,
            Assert.Single(completed.NpcFacilityInventories).StateCode);
        var workRecord = Assert.Single(completed.NpcWorkRecords);
        Assert.Contains(SimulationNpcInventoryStateCodes.StorageEligible, workRecord.ResultCodes);
    }

    [Fact]
    public void 검수완료된_같은입고재고는_적재Npc작업을거쳐야만_적재완료가된다()
    {
        var saveStore = new InMemorySimulationSessionSaveStore();
        var context = new TestContext(new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(),
            saveStore));
        var current = context.Service.Create(CreateSessionRequest());
        current = Confirm(context, current, InboundInspection("put-away"), "command:npc-put-away:inspection");
        current = Advance(context, current, "command:npc-put-away:inspection-tick-1");
        current = Advance(context, current, "command:npc-put-away:inspection-tick-2");
        current = Advance(context, current, "command:npc-put-away:inspection-tick-3");
        var inventory = Assert.Single(current.NpcFacilityInventories);
        var request = new SimulationWarehousePutAwayPreviewRequest
        {
            InventoryStableId = inventory.InventoryStableId,
            InventoryRevision = inventory.Revision,
            ActorStableId = PyeongchangSimulationNpcStableIds.진부적재담당,
            PutAwayDurationTicks = 2,
            SourceStableIds = new[]
            {
                inventory.InventoryStableId,
                PyeongchangSimulationWorldStableIds.창고적재규칙,
            },
        };

        var preview = context.Service.PreviewWarehousePutAway(current.SessionStableId, request);
        Assert.Empty(preview.Decision.BlockReasonCodes);
        Assert.Equal(3, preview.TaskPlan.DurationTicks);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부적재담당,
            preview.TaskPlan.AssignedActorStableId);

        var confirmRequest = new SimulationWarehousePutAwayConfirmRequest
        {
            CommandId = "command:npc-put-away:confirm",
            ExpectedRevision = current.Revision,
            PutAway = request,
        };
        current = context.Service.ConfirmWarehousePutAway(current.SessionStableId, confirmRequest);
        var retried = context.Service.ConfirmWarehousePutAway(current.SessionStableId, confirmRequest);
        Assert.Equal(current.Revision, retried.Revision);
        var assignment = Assert.Single(current.NpcTaskAssignments, value =>
            value.ActionCode == SimulationNpcActionCodes.WarehouseStorageMove);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부적재담당, assignment.ActorStableId);
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible,
            Assert.Single(current.NpcFacilityInventories).StateCode);

        current = Advance(context, current, "command:npc-put-away:tick-1");
        current = Advance(context, current, "command:npc-put-away:tick-2");
        current = Advance(context, current, "command:npc-put-away:tick-3");

        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(current.NpcFacilityInventories).StateCode);
        Assert.Contains(current.NpcWorkRecords, value =>
            value.ActionCode == SimulationNpcActionCodes.WarehouseStorageMove
            && value.ResultCodes.Contains(SimulationNpcInventoryStateCodes.PutAwayCompleted));
        Assert.False(current.IsOperationalState);

        var saved = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:sim:npc-put-away:1",
            ExpectedRevision = current.Revision,
        });
        var restored = new 경영SimulationSessionService(
                new InMemory경영SimulationSessionStore(),
                saveStore)
            .Restore(new SimulationSessionRestoreRequest { SaveStableId = saved.SaveStableId });
        Assert.Equal(SimulationNpcInventoryStateCodes.PutAwayCompleted,
            Assert.Single(restored.Session.NpcFacilityInventories).StateCode);
        Assert.Equal(2, restored.Session.NpcWorkRecords.Length);
        Assert.Equal(saved.ReplayHash, restored.ReplayHash);
    }

    [Fact]
    public void 두번째검수작업은_관리자초기권한으로_보조Npc에게동적위임된다()
    {
        var context = CreateContext();
        var current = context.Service.Create(CreateSessionRequest(inspectionSlots: 2m));
        current = Confirm(context, current, InboundInspection("1"), "command:npc-backlog:1");
        current = Confirm(context, current, InboundInspection("2"), "command:npc-backlog:2");

        Assert.Equal(2, current.NpcTaskAssignments.Length);
        Assert.Contains(current.NpcTaskAssignments, value =>
            value.ActorStableId == PyeongchangSimulationNpcStableIds.진부입고검수담당);
        Assert.Contains(current.NpcTaskAssignments, value =>
            value.ActorStableId == PyeongchangSimulationNpcStableIds.진부물류보조);
        var delegated = Assert.Single(current.NpcCapabilityGrants, value =>
            value.GrantKindCode == SimulationNpcGrantKindCodes.Delegated);
        Assert.Equal(PyeongchangSimulationNpcStableIds.진부Hub관리자,
            delegated.GrantedByActorStableId);
        Assert.False(delegated.CanDelegate);
        Assert.Equal(PyeongchangSimulationWorldStableIds.진부Hub시설,
            delegated.FacilityStableId);
    }

    [Fact]
    public void 자동화정책을끄면_작업은완료되지않고_명시적차단상태로남는다()
    {
        var context = CreateContext();
        var created = context.Service.Create(CreateSessionRequest());
        var policyRequest = new SimulationNpcPolicyChangeRequest
        {
            CommandId = "command:npc-policy:disable",
            ExpectedRevision = created.Revision,
            PolicyStableId = PyeongchangSimulationNpcStableIds.진부입고검수정책,
            AutomationEnabled = false,
            Priority = 100,
            PreferredActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
            AutoDelegationEnabled = false,
        };

        var changed = context.Service.UpdateNpcPolicy(created.SessionStableId, policyRequest);
        var retried = context.Service.UpdateNpcPolicy(created.SessionStableId, policyRequest);
        Assert.Equal(changed.Revision, retried.Revision);

        var blocked = Confirm(
            context,
            changed,
            InboundInspection("blocked"),
            "command:npc-blocked:1");
        Assert.Equal(SimulationTaskStateCodes.Blocked, Assert.Single(blocked.Tasks).StateCode);
        Assert.Contains("SimulationNpcAutomationDisabled",
            Assert.Single(blocked.NpcTaskAssignments).BlockReasonCodes);

        var advanced = Advance(context, blocked, "command:npc-blocked:tick");
        Assert.Equal(SimulationTaskStateCodes.Blocked, Assert.Single(advanced.Tasks).StateCode);
        Assert.Empty(advanced.NpcWorkRecords);
        Assert.Empty(advanced.NpcFacilityInventories);
    }

    [Fact]
    public void 저장복원은_Npc배정과정책과작업이력과Hash를동일하게보존한다()
    {
        var saveStore = new InMemorySimulationSessionSaveStore();
        var context = new TestContext(new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(),
            saveStore));
        var current = context.Service.Create(CreateSessionRequest());
        current = Confirm(context, current, InboundInspection("save"), "command:npc-save:confirm");
        current = Advance(context, current, "command:npc-save:tick-1");
        current = Advance(context, current, "command:npc-save:tick-2");
        current = Advance(context, current, "command:npc-save:tick-3");
        var saved = context.Service.Save(current.SessionStableId, new SimulationSessionSaveRequest
        {
            SaveStableId = "save:sim:npc-workforce:1",
            ExpectedRevision = current.Revision,
        });

        var restoreService = new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(),
            saveStore);
        var restored = restoreService.Restore(new SimulationSessionRestoreRequest
        {
            SaveStableId = saved.SaveStableId,
        });
        var savedAgain = restoreService.Save(restored.Session.SessionStableId,
            new SimulationSessionSaveRequest
            {
                SaveStableId = "save:sim:npc-workforce:2",
                ExpectedRevision = restored.Session.Revision,
            });

        Assert.Equal(saved.ReplayHash, savedAgain.ReplayHash);
        Assert.Equal(current.NpcTaskAssignments[0].ActorStableId,
            restored.Session.NpcTaskAssignments[0].ActorStableId);
        Assert.Equal(current.NpcWorkRecords[0].CompletedTick,
            restored.Session.NpcWorkRecords[0].CompletedTick);
        Assert.Equal(SimulationNpcInventoryStateCodes.StorageEligible,
            restored.Session.NpcFacilityInventories[0].StateCode);
    }

    [Fact]
    public void 같은입력과Seed는_같은Npc배정을만든다()
    {
        var first = RunDeterministicFixture();
        var second = RunDeterministicFixture();

        Assert.Equal(
            first.NpcTaskAssignments.Select(value => value.ActorStableId),
            second.NpcTaskAssignments.Select(value => value.ActorStableId));
        Assert.Equal(
            first.NpcCapabilityGrants.Select(value => value.GrantStableId),
            second.NpcCapabilityGrants.Select(value => value.GrantStableId));
    }

    [Fact]
    public void Npc규칙계층은_운영사용자와HrAssembly를참조하지않는다()
    {
        var properties = typeof(SimulationNpcActorSnapshot).GetProperties()
            .Concat(typeof(SimulationNpcCapabilityGrantSnapshot).GetProperties())
            .Select(value => value.Name)
            .ToArray();
        Assert.DoesNotContain(properties, value => value.Contains("UserId", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(경영SimulationSessionAggregate).Assembly.GetReferencedAssemblies(),
            value => value.Name != null
                && (value.Name.Contains("Infrastructure", StringComparison.Ordinal)
                    || value.Name.Contains("Identity", StringComparison.Ordinal)
                    || value.Name.Contains("Hr", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Npc정책API는_로그인없이_Simulation세션정책만변경한다()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var createRequest = CreateSessionRequest();
        using var createResponse = await client.PostAsJsonAsync(
            "/api/simulation/v1/sessions",
            createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<경영SimulationSessionSnapshot>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        using var response = await client.PostAsJsonAsync(
            $"/api/simulation/v1/sessions/{created.SessionStableId}/npc-policies",
            new SimulationNpcPolicyChangeRequest
            {
                CommandId = "command:http:npc-policy:1",
                ExpectedRevision = created.Revision,
                PolicyStableId = PyeongchangSimulationNpcStableIds.진부입고검수정책,
                AutomationEnabled = true,
                Priority = 120,
                PreferredActorStableId = PyeongchangSimulationNpcStableIds.진부입고검수담당,
                AutoDelegationEnabled = true,
            });
        var changed = await response.Content.ReadFromJsonAsync<경영SimulationSessionSnapshot>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(changed);
        Assert.False(changed.IsOperationalState);
        Assert.Equal(120, Assert.Single(changed.NpcWorkPolicies, value =>
            value.PolicyStableId == PyeongchangSimulationNpcStableIds.진부입고검수정책).Priority);
    }

    private static 경영SimulationSessionSnapshot RunDeterministicFixture()
    {
        var context = CreateContext();
        var current = context.Service.Create(CreateSessionRequest(inspectionSlots: 2m));
        current = Confirm(context, current, InboundInspection("1"), "command:npc-deterministic:1");
        return Confirm(context, current, InboundInspection("2"), "command:npc-deterministic:2");
    }

    private static 경영SimulationSessionSnapshot Confirm(
        TestContext context,
        경영SimulationSessionSnapshot current,
        SimulationDecisionPreviewRequest preview,
        string commandId)
        => context.Service.ConfirmDecision(current.SessionStableId,
            new SimulationDecisionConfirmRequest
            {
                CommandId = commandId,
                ExpectedRevision = current.Revision,
                Preview = preview,
            });

    private static 경영SimulationSessionSnapshot Advance(
        TestContext context,
        경영SimulationSessionSnapshot current,
        string commandId)
        => context.Service.Advance(current.SessionStableId, new 경영SimulationTick진행Request
        {
            CommandId = commandId,
            ExpectedRevision = current.Revision,
            TickCount = 1,
        });

    private static SimulationDecisionPreviewRequest InboundInspection(string suffix)
        => new SimulationDecisionPreviewRequest
        {
            DecisionStableId = "decision:npc-inbound:" + suffix,
            DecisionTypeCode = "WarehouseInboundInspection",
            ActorStableId = PyeongchangSimulationNpcStableIds.진부Hub관리자,
            TargetStableIds = new[] { "cargo:sim:potato:" + suffix },
            ExpectedEffects = new[]
            {
                new SimulationValueProjection
                {
                    ValueTypeCode = "StorageEligibleQuantity",
                    TargetLedgerStableId = "inventory:sim:potato:" + suffix,
                    BeforeValue = 0m,
                    Delta = 100m,
                    AfterValue = 100m,
                    UnitCode = "KGM",
                    SourceStableIds = new[] { "source:fixture:npc-inbound:" + suffix },
                },
            },
            SourceStableIds = new[] { "source:fixture:npc-inbound:" + suffix },
            Task = new SimulationTaskPlanRequest
            {
                TaskStableId = "task:npc-inbound:" + suffix,
                TaskTypeCode = "FreightReceiptConfirmation",
                FacilityStableId = PyeongchangSimulationWorldStableIds.진부Hub시설,
                ActionCode = SimulationNpcActionCodes.WarehouseInboundInspection,
                AssignedCapacity = 100m,
                AssignedCapacityUnitCode = "KGM",
                DurationTicks = 1,
                InputLotStableIds = new[] { "cargo:sim:potato:" + suffix },
                OutputCandidateCodes = new[] { SimulationNpcInventoryStateCodes.StorageEligible },
                SourceStableIds = new[] { "source:fixture:npc-inbound:" + suffix },
            },
        };

    private static 경영SimulationSession생성Request CreateSessionRequest(decimal inspectionSlots = 1m)
    {
        var spatialWorld = PyeongchangSimulation공간상호작용Fixture.Create();
        spatialWorld.Definitions.Single(value => value.CapabilityCodes.Contains(
            Simulation공간능력Codes.InspectionWorkArea)).BaseCapacities.Single().Quantity
            = inspectionSlots;
        return new 경영SimulationSession생성Request
        {
            ClientRequestId = Guid.Parse("2731b15d-1d1f-4f4f-88d4-89ae8013790a"),
            ScenarioStableId = "scenario:pyeongchang-farm-hub-town:npc-workforce",
            ScenarioDataRevision = "scenario-data:pyeongchang:npc-workforce:r1",
            ScenarioSeed = 240813,
            RuleRevision = "simulation-npc-workforce:r1",
            DurationTicks = 28,
            WorldContext = new SimulationWorldContext생성Request
            {
                FactionStableId = "faction:sim:pyeongchang",
                TerritoryStableId = "territory:sim:pyeongchang",
                SettlementStableId = "settlement:sim:pyeongchang",
                GameDateStartsOn = new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero),
            },
            NpcWorkforce = PyeongchangSimulationNpcWorkforceFixture.Create(),
            SpatialWorld = spatialWorld,
        };
    }

    private static TestContext CreateContext()
        => new(new 경영SimulationSessionService(
            new InMemory경영SimulationSessionStore(),
            new InMemorySimulationSessionSaveStore()));

    private static WebApplicationFactory<Program> CreateFactory()
        => new SimulationWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["SsalddelExecution:Mode"] = "Operational",
                        ["SsalddelSimulation:AllowUnauthenticatedTesting"] = "true",
                        ["SimulationSharedPublicData:Enabled"] = "false",
                    });
                });
            });

    private sealed record TestContext(경영SimulationSessionService Service);
}
