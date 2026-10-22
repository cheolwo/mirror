using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class InboundRequestRecoveryTests
{
    [Fact]
    public async Task 재실행후_접수번호없는미확정은_목록에비슷한행이있어도재제출하지않는다()
    {
        var f = new Fixture();
        var first = await f.Ready();
        f.Service.CreateFailureAfterSave = new HttpRequestException("response lost");
        Assert.Null(await first.CreateInboundAsync());

        var reopened = await f.Ready();
        await reopened.LoadListAsync();
        Assert.Null(await reopened.CreateInboundAsync());

        Assert.Equal(1, f.Service.CreateCount);
        Assert.True(reopened.State.RegistrationResultPending);
        Assert.Null(reopened.State.SavedInboundId);
        Assert.Equal(업무처리결과Codes.미확정, reopened.State.Failure?.WorkflowContext?.OutcomeCode);
        Assert.Single(reopened.State.Items);
        Assert.Single(f.Store.Records);
    }

    [Fact]
    public async Task 저장후HTTP400도_같은신청을새POST로반복하지않는다()
    {
        var f = new Fixture();
        var first = await f.Ready();
        f.Service.CreateFailureAfterSave = new SsalddelApiException("후속 기록 실패", 400, "입고 신청", string.Empty, null);
        Assert.Null(await first.CreateInboundAsync());

        var reopened = await f.Ready();
        Assert.Null(await reopened.CreateInboundAsync());

        Assert.Equal(1, f.Service.CreateCount);
        Assert.True(reopened.State.HasPendingMutation);
    }

    [Fact]
    public async Task 응답ID있는신청은_재실행후같은ID조회로만확인하고보호번호해제도검증한다()
    {
        var f = new Fixture();
        var first = await f.Ready();
        f.Service.DetailFailure = new HttpRequestException("read unavailable");
        Assert.Equal(77, await first.CreateInboundAsync());
        var record = Assert.Single(f.Store.Records.Values);
        Assert.Equal("create-request:77", record.Target);

        f.Service.DetailFailure = null;
        var reopened = await f.Ready();
        Assert.Equal(77, reopened.State.SavedInboundId);
        Assert.Equal(77, await reopened.CreateInboundAsync());

        Assert.Equal(1, f.Service.CreateCount);
        Assert.Equal(77, reopened.State.Current?.Id);
        Assert.Empty(f.Store.Records);
        Assert.False(reopened.State.HasPendingMutation);
    }

    [Fact]
    public async Task 완료응답유실은_재실행후완료상태GET만으로해제한다()
    {
        var f = new Fixture();
        f.Service.Inbounds.Add(new() { Id = 41, 상태 = 입고상태코드.예정, 예정상품명 = "상품", 예정수량 = 2 });
        var first = f.Model();
        await first.LoadCompletionAsync(41);
        f.Service.CompleteFailureAfterSave = new HttpRequestException("response lost");
        Assert.False(await first.CompleteInboundAsync(41));

        var reopened = f.Model();
        await reopened.LoadCompletionAsync(41);

        Assert.Equal(1, f.Service.CompleteCount);
        Assert.Equal(입고상태코드.완료, reopened.State.Current?.상태);
        Assert.False(reopened.CanComplete);
        Assert.Null(reopened.State.Failure);
        Assert.Empty(f.Store.Records);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 기록쓰기실패나쓰기확인누락이면_POST를보내지않는다(bool failWrite)
    {
        var f = new Fixture(); var model = await f.Ready();
        f.Store.FailWrite = failWrite; f.Store.DropWrite = !failWrite;

        Assert.Null(await model.CreateInboundAsync());
        Assert.Null(await model.CreateInboundAsync());

        Assert.Equal(0, f.Service.CreateCount);
        Assert.True(model.State.RecoveryBlocked);
    }

    [Fact]
    public async Task 시작시기록조회실패는_기록없음으로취급하지않는다()
    {
        var f = new Fixture(); f.Store.FailRead = true;
        var model = await f.Ready();

        Assert.Null(await model.CreateInboundAsync());

        Assert.Equal(0, f.Service.CreateCount);
        Assert.True(model.State.RecoveryBlocked);
        Assert.Empty(model.State.Warehouses);
    }

    [Fact]
    public async Task 해제후에도기록이남으면_현재결과를확인해도새요청을잠근다()
    {
        var f = new Fixture(); var first = await f.Ready(); f.Store.IgnoreRemove = true;
        Assert.Equal(77, await first.CreateInboundAsync());

        var reopened = f.Model(); await reopened.LoadDetailAsync(77);
        Assert.True(reopened.State.HasPendingMutation);
        Assert.Equal(1, f.Service.CreateCount);
        Assert.Single(f.Store.Records);

        f.Store.IgnoreRemove = false;
        await reopened.LoadDetailAsync(77);
        Assert.False(reopened.State.HasPendingMutation);
        Assert.Empty(f.Store.Records);
    }

    [Fact]
    public async Task 만료되어원문이없어도_접수번호없는미확정잠금은남긴다()
    {
        var f = new Fixture(); var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow.AddHours(-73), "create");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        var reopened = await f.Ready();

        Assert.Null(await reopened.CreateInboundAsync());
        Assert.Equal(0, f.Service.CreateCount);
        Assert.True(reopened.State.RegistrationResultPending);
        Assert.Single(f.Store.Records);
        Assert.Equal(0, f.Store.RemoveCount);
    }

    [Fact]
    public async Task 다른입고의성공조회는_앞선미확정기록을해제하지않는다()
    {
        var f = new Fixture(); var first = await f.Ready();
        f.Service.CreateFailureAfterSave = new HttpRequestException("response lost");
        await first.CreateInboundAsync();
        f.Service.Inbounds.Add(new() { Id = 99, 상태 = 입고상태코드.예정, 공급처명 = "다른 입고" });
        var reopened = f.Model(); await reopened.LoadDetailAsync(99);

        Assert.Equal(99, reopened.State.Current?.Id);
        Assert.True(reopened.State.RegistrationResultPending);
        Assert.False(reopened.CanComplete);
        Assert.Single(f.Store.Records);
        Assert.Equal(0, f.Store.RemoveCount);
    }

    [Fact]
    public async Task 최소기록에는_상품과초안과동의원문을저장하지않는다()
    {
        var f = new Fixture(); var model = await f.Ready();
        model.CreateDraft.SupplierName = "개인 공급처 원문";
        model.CreateDraft.Notes = "private address and memo";
        model.CreateDraft.ApplicationPrivacyConsentEvidenceId = Guid.NewGuid();
        f.Service.CreateFailureAfterSave = new HttpRequestException("response lost");
        await model.CreateInboundAsync();

        var json = System.Text.Json.JsonSerializer.Serialize(Assert.Single(f.Store.Records.Values));
        Assert.DoesNotContain("개인 공급처", json);
        Assert.DoesNotContain("private address", json);
        Assert.DoesNotContain(model.CreateDraft.ApplicationPrivacyConsentEvidenceId.Value.ToString(), json);
        Assert.DoesNotContain("Supplier", json);
        Assert.Equal("create-request", Assert.Single(f.Store.Records.Values).Target);
    }

    [Fact]
    public async Task POST응답전계정판본변경은_새계정화면에결과를남기지않고원계정기록은보존한다()
    {
        var f = new Fixture(); var model = await f.Ready();
        f.Service.DeferredCreate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var saving = model.CreateInboundAsync();
        f.Context.Snapshot = f.Context.Snapshot with { Owner = "owner-b", SessionRevision = 2 };
        f.Service.DeferredCreate.SetResult(new() { Id = 77, 공급처명 = "원계정 결과" });
        Assert.Null(await saving);

        Assert.Null(model.State.Current);
        Assert.Empty(model.State.Warehouses);
        Assert.Empty(model.CreateDraft.SupplierName);
        Assert.Empty(model.CreateDraft.Notes);
        Assert.Equal("owner-a", Assert.Single(f.Store.Records.Values).Owner);
    }

    [Fact]
    public async Task 저장직후서버가변경되면_원서버기록을남기고POST는보내지않는다()
    {
        var f = new Fixture(); var model = await f.Ready();
        var originalScope = f.Context.Capture().Scope("warehouse-inbound-mutation.v1");
        f.Store.AfterWrite = () => f.Context.Snapshot = f.Context.Snapshot with { EndpointKey = new string('b', 32) };

        Assert.Null(await model.CreateInboundAsync());

        Assert.Equal(0, f.Service.CreateCount);
        Assert.Equal(originalScope, Assert.Single(f.Store.Records.Values).Scope);
        Assert.Null(model.State.Current);
        Assert.Empty(model.State.Warehouses);
    }

    [Fact]
    public async Task 잘못된업무기록은_삭제나재POST로자동복구하지않는다()
    {
        var f = new Fixture(); var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), "{\"Version\":1,\"OutcomeCode\":\"Unknown\"}", DateTime.UtcNow, "complete:not-an-id");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        var model = await f.Ready();

        Assert.Null(await model.CreateInboundAsync());

        Assert.True(model.State.RecoveryBlocked);
        Assert.Equal(0, f.Service.CreateCount);
        Assert.Equal(0, f.Store.RemoveCount);
        Assert.Single(f.Store.Records);
    }

    [Fact]
    public async Task 기존보호저장adapter를새로만들어도_원계정의미확정만복원한다()
    {
        var context = new Context(); var service = new WarehouseService(); var storage = new Dictionary<string, string>();
        ProtectedWorkflowRecoveryStore Store() => new(_ => new(context.Snapshot.Owner, context.Snapshot.SessionRevision, true),
            key => Task.FromResult(storage.GetValueOrDefault(key)),
            (key, value) => { storage[key] = value; return Task.CompletedTask; }, key => storage.Remove(key));
        var first = new InboundRequestPageViewModel(service, Store(), context);
        await first.LoadCreateAsync(new() { WarehouseId = 5, SupplierName = "공급처" });
        service.CreateFailureAfterSave = new HttpRequestException("response lost");
        Assert.Null(await first.CreateInboundAsync());
        first.Dispose();

        var reopened = new InboundRequestPageViewModel(service, Store(), context);
        await reopened.LoadCreateAsync(new() { WarehouseId = 5, SupplierName = "공급처" });
        Assert.Null(await reopened.CreateInboundAsync());
        Assert.True(reopened.State.RegistrationResultPending);
        Assert.Equal(1, service.CreateCount);
        Assert.Single(storage);

        context.Snapshot = context.Snapshot with { Owner = "owner-b", SessionRevision = 2 };
        var other = new InboundRequestPageViewModel(service, Store(), context);
        await other.LoadCreateAsync(new());
        Assert.False(other.State.HasPendingMutation);
        Assert.Empty(other.CreateDraft.SupplierName);
        Assert.Single(storage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WAL쓰기실패후저장소가회복되면_다시확인하고새요청을한번만보낸다(bool failWrite)
    {
        var f = new Fixture(); var model = await f.Ready();
        f.Store.FailWrite = failWrite; f.Store.DropWrite = !failWrite;
        Assert.Null(await model.CreateInboundAsync());
        Assert.True(model.State.RecoveryBlocked);
        Assert.Equal(0, f.Service.CreateCount);

        f.Store.FailWrite = f.Store.DropWrite = false;
        await model.RefreshWarehousesAsync();
        Assert.False(model.State.RecoveryBlocked);
        Assert.Equal("공급처", model.CreateDraft.SupplierName);
        Assert.Equal(77, await model.CreateInboundAsync());

        Assert.Equal(1, f.Service.CreateCount);
        Assert.Empty(f.Store.Records);
    }

    [Fact]
    public async Task 명시적새로고침은_기록없음캐시대신다른화면의미확정기록을확인한다()
    {
        var f = new Fixture(); var model = await f.Ready(); var snapshot = f.Context.Capture();
        var pending = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow, "create");
        f.Store.Records[(pending.Owner, pending.Scope)] = pending;

        await model.RefreshWarehousesAsync();
        Assert.Null(await model.CreateInboundAsync());

        Assert.True(model.State.RegistrationResultPending);
        Assert.Equal(0, f.Service.CreateCount);
        Assert.Equal(pending.RequestId, Assert.Single(f.Store.Records.Values).RequestId);
    }

    [Fact]
    public async Task 조회저장소장애가회복되면_같은VM의차단상태도다시확인한다()
    {
        var f = new Fixture(); var model = await f.Ready(); f.Store.FailRead = true;
        await model.RefreshWarehousesAsync();
        Assert.True(model.State.RecoveryBlocked);
        f.Store.FailRead = false;
        await model.RefreshWarehousesAsync();

        Assert.False(model.State.RecoveryBlocked);
        Assert.Equal(77, await model.CreateInboundAsync());
        Assert.Equal(1, f.Service.CreateCount);
    }

    [Fact]
    public async Task 응답ID전유실후_새화면은서버에전달한원요청GET으로만복구한다()
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true;
        var first = await f.Ready();
        f.Service.CreateFailureAfterSave = new HttpRequestException("response lost");
        Assert.Null(await first.CreateInboundAsync());
        var record = Assert.Single(f.Store.Records.Values);
        Assert.Equal(record.RequestId, f.Service.LastCreateRequestId);
        first.Dispose();

        var reopened = await f.Ready();
        Assert.Equal(77, reopened.State.SavedInboundId);
        Assert.True(reopened.State.Created);
        Assert.False(reopened.State.HasPendingMutation);
        Assert.False(reopened.State.CanQueryRegistrationResult);
        Assert.Null(reopened.State.Failure);
        Assert.Empty(f.Store.Records);
        Assert.Equal(77, await reopened.CreateInboundAsync());
        Assert.Equal(1, f.Service.CreateCount);
        Assert.Contains(record.RequestId, f.Service.RequestLookups);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 원요청조회미확인이나404는_늦은저장까지잠금을유지한다(bool explicit404)
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true;
        var first = await f.Ready(); f.Service.CreateFailureAfterSave = new HttpRequestException("lost");
        await first.CreateInboundAsync();
        var saved = Assert.Single(f.Service.Inbounds); f.Service.Inbounds.Clear();
        if (explicit404) f.Service.LookupFailure = new SsalddelApiException("조회 대기", 404, "입고 결과", string.Empty, null);
        var reopened = await f.Ready();
        Assert.True(reopened.State.HasPendingMutation);
        Assert.True(reopened.State.CanQueryRegistrationResult);
        Assert.Null(await reopened.CreateInboundAsync());
        Assert.Equal(1, f.Service.CreateCount);

        f.Service.LookupFailure = null; f.Service.Inbounds.Add(saved);
        await reopened.RefreshWarehousesAsync();
        Assert.False(reopened.State.HasPendingMutation);
        Assert.Equal(77, reopened.State.SavedInboundId);
        Assert.Empty(f.Store.Records);
        Assert.Equal(1, f.Service.CreateCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 구판단말번호는_서버원요청으로소급조회하지않는다(bool expired)
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true; var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), expired ? null : "{\"Version\":1,\"OutcomeCode\":\"Unknown\"}",
            DateTime.UtcNow.AddHours(expired ? -73 : -1), "create");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        f.Service.Inbounds.Add(new() { Id = 77, ClientRequestId = record.RequestId, 상태 = 입고상태코드.예정 });
        var reopened = await f.Ready();
        Assert.True(reopened.State.HasPendingMutation);
        Assert.False(reopened.State.CanQueryRegistrationResult);
        Assert.Null(await reopened.CreateInboundAsync());
        Assert.Empty(f.Service.RequestLookups);
        Assert.Single(f.Store.Records);
        Assert.Equal(0, f.Service.CreateCount);
    }

    [Fact]
    public async Task 새원요청표식은_72시간뒤원문이없어도정확한결과를조회한다()
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true; var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow.AddHours(-73), "create-request");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        f.Service.Inbounds.Add(new() { Id = 88, ClientRequestId = record.RequestId, 상태 = 입고상태코드.예정 });
        var reopened = await f.Ready();
        Assert.Equal(88, reopened.State.SavedInboundId);
        Assert.False(reopened.State.HasPendingMutation);
        Assert.Empty(f.Store.Records);
        Assert.Equal(0, f.Service.CreateCount);
        Assert.Contains(record.RequestId, f.Service.RequestLookups);
    }

    [Fact]
    public async Task 보호저장adapter도다시만든뒤_저장한원요청번호로접수결과를복구한다()
    {
        var context = new Context(); var service = new WarehouseService(); var storage = new Dictionary<string, string>();
        ProtectedWorkflowRecoveryStore Store() => new(_ => new(context.Snapshot.Owner, context.Snapshot.SessionRevision, true),
            key => Task.FromResult(storage.GetValueOrDefault(key)),
            (key, value) => { storage[key] = value; return Task.CompletedTask; }, key => storage.Remove(key));
        var first = new InboundRequestPageViewModel(service, Store(), context);
        await first.LoadCreateAsync(new() { WarehouseId = 5, SupplierName = "공급처" });
        service.CreateFailureAfterSave = new HttpRequestException("response lost");
        Assert.Null(await first.CreateInboundAsync());
        var requestId = service.LastCreateRequestId;
        first.Dispose();

        service.EnableRequestLookup = true;
        var reopened = new InboundRequestPageViewModel(service, Store(), context);
        await reopened.LoadCreateAsync(new());
        Assert.Equal(77, reopened.State.SavedInboundId);
        Assert.False(reopened.State.HasPendingMutation);
        Assert.True(reopened.State.Created);
        Assert.Empty(storage);
        Assert.Equal(1, service.CreateCount);
        Assert.Contains(requestId!.Value, service.RequestLookups);
    }

    [Fact]
    public async Task 다른입고를선택하는동안이전신청이확인되어도_현재대상은그대로조회한다()
    {
        var f = new Fixture(); var first = await f.Ready();
        f.Service.CreateFailureAfterSave = new HttpRequestException("response lost");
        await first.CreateInboundAsync();
        f.Service.EnableRequestLookup = true;
        f.Service.Inbounds.Add(new() { Id = 99, 상태 = 입고상태코드.예정, 공급처명 = "다른 대상" });
        var reopened = f.Model();
        await reopened.LoadDetailAsync(99);
        Assert.Equal(99, reopened.State.Current?.Id);
        Assert.Equal(77, reopened.State.SavedInboundId);
        Assert.False(reopened.State.HasPendingMutation);
        Assert.Empty(f.Store.Records);
        Assert.Equal(1, f.Service.CreateCount);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(77, false)]
    [InlineData(77, true)]
    public async Task 원요청조회응답의잘못된ID나요청번호는_기록해제로사용하지않는다(long responseId, bool missingRequest)
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true; var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow, "create-request");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        f.Service.LookupOverride = new() { Id = responseId, ClientRequestId = missingRequest ? null : Guid.NewGuid() };
        var model = await f.Ready();
        Assert.True(model.State.HasPendingMutation);
        Assert.Null(model.State.SavedInboundId);
        Assert.Null(await model.CreateInboundAsync());
        Assert.Single(f.Store.Records);
        Assert.Equal(0, f.Store.RemoveCount);
        Assert.Equal(0, f.Service.CreateCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 원요청조회중계정이나서버가바뀌면_늦은결과를표시하거나삭제하지않는다(bool changeServer)
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true; var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow, "create-request");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        f.Service.DeferredLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = f.Model(); var loading = model.RefreshWarehousesAsync();
        f.Context.Snapshot = changeServer ? snapshot with { EndpointKey = new string('b', 32) }
            : snapshot with { Owner = "owner-b", SessionRevision = 2 };
        f.Service.DeferredLookup.SetResult(new() { Id = 77, ClientRequestId = record.RequestId });
        await loading;
        Assert.Null(model.State.SavedInboundId);
        Assert.Null(model.State.Current);
        Assert.Empty(model.State.Warehouses);
        Assert.Single(f.Store.Records);
        Assert.Equal(0, f.Store.RemoveCount);
        Assert.Equal(0, f.Service.CreateCount);
    }

    [Fact]
    public async Task 원요청GET중다른요청기록이생기면_그기록을덮거나삭제하지않는다()
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true; var snapshot = f.Context.Capture();
        var original = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow, "create-request");
        f.Store.Records[(original.Owner, original.Scope)] = original;
        f.Service.DeferredLookup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = f.Model(); var loading = model.RefreshWarehousesAsync();
        var next = original with { RequestId = Guid.NewGuid() };
        f.Store.Records[(original.Owner, original.Scope)] = next;
        f.Service.DeferredLookup.SetResult(new() { Id = 77, ClientRequestId = original.RequestId });
        await loading;
        Assert.Null(model.State.SavedInboundId);
        Assert.True(model.State.HasPendingMutation);
        Assert.Equal(next.RequestId, Assert.Single(f.Store.Records.Values).RequestId);
        Assert.Equal(0, f.Store.RemoveCount);
        Assert.Equal(0, f.Service.CreateCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 정확한원요청GET이어도_저장확인이나삭제확인이실패하면잠금을유지한다(bool failSave)
    {
        var f = new Fixture(); f.Service.EnableRequestLookup = true; var snapshot = f.Context.Capture();
        var record = new WorkflowRecoveryRecord(snapshot.Owner, snapshot.Scope("warehouse-inbound-mutation.v1"),
            Guid.NewGuid(), null, DateTime.UtcNow, "create-request");
        f.Store.Records[(record.Owner, record.Scope)] = record;
        f.Service.Inbounds.Add(new() { Id = 77, ClientRequestId = record.RequestId, 상태 = 입고상태코드.예정 });
        f.Store.FailWrite = failSave; f.Store.IgnoreRemove = !failSave;
        var model = await f.Ready();
        Assert.True(model.State.HasPendingMutation);
        await model.CreateInboundAsync();
        Assert.True(model.State.HasPendingMutation);
        Assert.Single(f.Store.Records);
        Assert.Equal(0, f.Service.CreateCount);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Context Context { get; } = new();
        public WarehouseService Service { get; } = new();
        public InboundRequestPageViewModel Model() => new(Service, Store, Context);
        public async Task<InboundRequestPageViewModel> Ready()
        {
            var model = Model();
            await model.LoadCreateAsync(new() { WarehouseId = 5, SupplierName = "공급처" });
            return model;
        }
    }

    private sealed class Context : IWorkflowRecoveryContext
    {
        public WorkflowRecoverySnapshot Snapshot { get; set; } = new("owner-a", 1, new string('a', 32));
        public WorkflowRecoverySnapshot Capture() => Snapshot;
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Snapshot;
    }

    private sealed class Store : IWorkflowRecoveryStore
    {
        public Dictionary<(string Owner, string Scope), WorkflowRecoveryRecord> Records { get; } = [];
        public bool FailRead { get; set; }
        public bool FailWrite { get; set; }
        public bool DropWrite { get; set; }
        public bool IgnoreRemove { get; set; }
        public Action? AfterWrite { get; set; }
        public int RemoveCount { get; private set; }
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
        {
            if (FailRead) throw new IOException("private storage read failure");
            return Task.FromResult(Records.GetValueOrDefault((owner, scope)));
        }
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
        {
            if (FailWrite) throw new IOException("private storage write failure");
            if (Records.TryGetValue((record.Owner, record.Scope), out var old) && old.RequestId != record.RequestId)
                throw new InvalidOperationException("pending conflict");
            if (!DropWrite) Records[(record.Owner, record.Scope)] = record;
            AfterWrite?.Invoke();
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
        {
            RemoveCount++;
            if (!IgnoreRemove && Records.GetValueOrDefault((owner, scope))?.RequestId == expectedRequestId)
                Records.Remove((owner, scope));
            return Task.CompletedTask;
        }
    }

    private sealed class WarehouseService : IWarehouseWorkspaceService
    {
        public List<입고요청항목응답> Inbounds { get; } = [];
        public int CreateCount { get; private set; }
        public int CompleteCount { get; private set; }
        public Exception? CreateFailureAfterSave { get; set; }
        public Exception? CompleteFailureAfterSave { get; set; }
        public Exception? DetailFailure { get; set; }
        public bool EnableRequestLookup { get; set; }
        public Exception? LookupFailure { get; set; }
        public 입고요청항목응답? LookupOverride { get; set; }
        public Guid? LastCreateRequestId { get; private set; }
        public List<Guid> RequestLookups { get; } = [];
        public TaskCompletionSource<입고요청항목응답?>? DeferredLookup { get; set; }
        public TaskCompletionSource<입고요청항목응답?>? DeferredCreate { get; set; }
        public Task<창고목록응답?> GetWarehousesAsync(CancellationToken ct = default)
            => Task.FromResult<창고목록응답?>(new() { Items = [new() { Id = 5, 창고명 = "테스트 창고", IsActive = true }] });
        public Task<창고요약응답?> CreateWarehouseAsync(창고저장요청 payload, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<입고요청목록응답?> GetInboundsAsync(CancellationToken ct = default)
            => Task.FromResult<입고요청목록응답?>(new() { Items = Inbounds.ToArray() });
        public Task<입고요청항목응답?> GetInboundAsync(long id, CancellationToken ct = default)
        {
            if (DetailFailure is not null) throw DetailFailure;
            var value = Inbounds.FirstOrDefault(item => item.Id == id);
            return Task.FromResult(value is null ? null : new 입고요청항목응답
            {
                Id = value.Id, 상태 = value.상태, 창고Id = value.창고Id, 공급처명 = value.공급처명,
                예정상품명 = value.예정상품명, 예정수량 = value.예정수량
            });
        }
        public Task<입고요청항목응답?> CreateInboundAsync(입고요청저장요청 payload, CancellationToken ct = default)
        {
            CreateCount++;
            LastCreateRequestId = payload.ClientRequestId;
            if (DeferredCreate is not null) return DeferredCreate.Task;
            var item = new 입고요청항목응답 { Id = 77, ClientRequestId = payload.ClientRequestId,
                창고Id = payload.창고Id, 공급처명 = payload.공급처명, 상태 = 입고상태코드.예정 };
            Inbounds.Add(item);
            if (CreateFailureAfterSave is not null) throw CreateFailureAfterSave;
            return Task.FromResult<입고요청항목응답?>(item);
        }
        public Task<입고요청항목응답?> GetInboundByClientRequestAsync(Guid id, CancellationToken ct = default)
        {
            RequestLookups.Add(id);
            if (!EnableRequestLookup) return Task.FromResult<입고요청항목응답?>(null);
            if (LookupFailure is not null) throw LookupFailure;
            if (DeferredLookup is not null) return DeferredLookup.Task;
            return Task.FromResult(LookupOverride ?? Inbounds.SingleOrDefault(item => item.ClientRequestId == id));
        }
        public Task<입고상품목록응답?> CompleteInboundAsync(long id, 입고완료요청 payload, CancellationToken ct = default)
        {
            CompleteCount++;
            Inbounds.Single(item => item.Id == id).상태 = 입고상태코드.완료;
            if (CompleteFailureAfterSave is not null) throw CompleteFailureAfterSave;
            return Task.FromResult<입고상품목록응답?>(new()
            { Items = [new() { Id = 501, 입고요청Id = id, 상품명 = payload.Items[0].상품명, 입고수량 = payload.Items[0].입고수량 }] });
        }
        public Task<재고목록응답?> GetInventoryAsync(CancellationToken ct = default)
            => Task.FromResult<재고목록응답?>(new());
    }
}
