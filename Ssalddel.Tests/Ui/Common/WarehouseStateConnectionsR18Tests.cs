using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Shipper.Request;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WarehouseStateConnectionsR18Tests
{
    [Fact]
    public async Task 수령_완료는_새_재고_ID를_재조회해_검수로_연결한다()
    {
        var fixture = new Fixture();
        using var model = new WarehouseTaskActionViewModel(new(fixture.Api), new Access());
        await model.LoadAsync("inbound", "11"); Confirm(model);
        await model.SubmitAsync();
        Assert.True(model.Saved); Assert.Equal("inspection:81", model.NextItemId);
        Assert.EndsWith("/inspection/81", Assert.Single(model.NextActions).Route);
        Assert.Contains("inspection%3A81", model.ReturnHref);
        Assert.Contains(fixture.Api.Reads, path => path.EndsWith("inventory/81/inspection-target", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Api.Reads, path => path.EndsWith("inventory/11/inspection-target", StringComparison.Ordinal));
        Assert.Single(fixture.Api.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 수령_후_검수_재고가_다른_요청이거나_조회실패면_다음_연결을_노출하지_않는다(bool failed)
    {
        var fixture = new Fixture();
        fixture.Api.ReadOverride = path => path.EndsWith("inspection-target", StringComparison.Ordinal)
            ? failed ? throw new InvalidOperationException("조회 실패") : fixture.Inspection(parentId: 99) : null;
        using var model = new WarehouseTaskActionViewModel(new(fixture.Api), new Access());
        await model.LoadAsync("inbound", "11"); Confirm(model); await model.SubmitAsync();
        Assert.False(model.Saved); Assert.True(model.Lifetime.HasError); Assert.Empty(model.NextActions); Assert.Null(model.NextItemId);
        Assert.Single(fixture.Api.Commands);
    }

    [Fact]
    public async Task 인계_준비는_새_출고예정의_재고_관계를_조회해_검토로_연결하며_의뢰를_만들지_않는다()
    {
        var fixture = new Fixture();
        using var model = new WarehouseTaskActionViewModel(new(fixture.Api), new Access());
        await model.LoadAsync("handoff", "81"); Confirm(model); await model.SubmitAsync();
        Assert.True(model.Saved); Assert.Equal("출고 인계 준비", model.Title); Assert.Equal("outbound:302", model.NextItemId);
        Assert.EndsWith("/outbound/302", Assert.Single(model.NextActions).Route);
        Assert.Contains(fixture.Api.Reads, path => path.EndsWith("outbound-plan-reviews/302", StringComparison.Ordinal));
        Assert.Single(fixture.Api.Commands);
        Assert.DoesNotContain(fixture.Api.Commands, command => command.Path.Contains("reconsignment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 준비_결과와_다른_재고의_출고예정은_후속_연결에_사용하지_않는다()
    {
        var fixture = new Fixture();
        fixture.Api.ReadOverride = path => path.EndsWith("outbound-plan-reviews/302", StringComparison.Ordinal)
            ? fixture.Plan(inventoryId: 99) : null;
        using var model = new WarehouseTaskActionViewModel(new(fixture.Api), new Access());
        await model.LoadAsync("handoff", "81"); Confirm(model); await model.SubmitAsync();
        Assert.False(model.Saved); Assert.True(model.Lifetime.HasError); Assert.Empty(model.NextActions);
    }

    [Fact]
    public async Task 완료한_수령의_후속_검수가_무관한_입고_요청보다_우선한다()
    {
        var fixture = new Fixture { Received = true, IncludeInspection = true };
        var result = await new WarehouseWorkspaceAdapter(new(fixture.Api)).LoadAsync("inbound:11", default);
        Assert.Equal("inspection:81", result.SelectedId);
        Assert.Contains(result.Items, item => item.Id == "inbound:11" && !item.IsCurrent);
        var current = Assert.Single(result.Items, item => item.IsCurrent);
        Assert.Equal("inspection:81", current.Id);
        Assert.Contains(current.Summary!.Metrics!, field => field.Label == "수령 수량" && field.Value == "4개");
        Assert.Equal("보관 조건", current.Summary.Request!.Label);
        Assert.DoesNotContain(fixture.Api.Commands, command => command.Body is not 창고작업진입확인요청);
    }

    [Fact]
    public async Task 완료한_인계_준비는_목록에_없어도_정확한_출고예정을_조회한다()
    {
        var fixture = new Fixture { Prepared = true };
        var result = await new WarehouseWorkspaceAdapter(new(fixture.Api)).LoadAsync("handoff:81", default);
        Assert.Equal("outbound:302", result.SelectedId);
        var current = Assert.Single(result.Items, item => item.IsCurrent);
        Assert.Equal("출고예정 검토", Assert.Single(current.Actions!).Label);
        Assert.EndsWith("/outbound/302", Assert.Single(current.Actions!).Route);
        Assert.Contains(result.Items, item => item.Id == "handoff:81" && !item.IsCurrent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 오래된_준비_링크의_실물_인계도_완료됐다면_같은_조회에서_활성_작업을_선택한다(bool hasCurrent)
    {
        var fixture = new Fixture { Prepared = true, Released = true, RequestId = "cargo-request", Accepted = true };
        if (!hasCurrent) fixture.Api.ReadOverride = path => path.EndsWith("inbounds", StringComparison.Ordinal) ? new 입고요청목록응답() : null;
        var result = await new WarehouseWorkspaceAdapter(new(fixture.Api)).LoadAsync("handoff:81", default);
        Assert.Equal(hasCurrent ? "inbound:12" : "outbound:302", result.SelectedId);
        Assert.Contains(result.Items, item => item.Id == "handoff:81" && !item.IsCurrent);
        Assert.Contains(result.Items, item => item.Id == "outbound:302");
        if (hasCurrent) Assert.Contains(fixture.Api.Reads, path => path.EndsWith("inbounds/12", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.Api.Commands, command => command.Body is not 창고작업진입확인요청);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 운송이_연결된_출고예정은_대기_조회와_실제_인계를_서버_상태로_구분한다(bool accepted)
    {
        var fixture = new Fixture { Prepared = true, RequestId = "cargo-request", Accepted = accepted };
        var result = await new WarehouseWorkspaceAdapter(new(fixture.Api)).LoadAsync("outbound:302", default);
        var action = Assert.Single(result.Items.Single(item => item.Id == "outbound:302").Actions!);
        Assert.Equal(accepted ? "기사에게 실제 인계" : "기사 인계 상태 확인", action.Label);
        Assert.EndsWith("/transport-draft/302", action.Route); Assert.True(action.Enabled);
        Assert.DoesNotContain(fixture.Api.Commands, command => command.Body is not 창고작업진입확인요청);
    }

    [Fact]
    public async Task 전문_검수는_창고_세션으로_저장하고_같은_재고의_검수완료를_조회한다()
    {
        var fixture = new Fixture { Received = true };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("inspection", "81");
        var writer = model.Inspection!.작성;
        writer.검수수량 = 3; writer.불량수량 = 1;
        writer.수량대조확인 = writer.포장파손확인 = writer.품질기한확인 = writer.보관조건확인 = true;
        Assert.True(await model.Inspection.검수후재조회Async());
        Assert.Equal(2, model.Inspection.상세.항목!.AvailableQuantity);
        Assert.EndsWith("/put-away/81", model.PutAwayHref);
        Assert.Contains("inspection%3A81", model.ReturnHref);
        Assert.All(fixture.Api.Roles, role => Assert.Equal(RoleWorkspaceCatalog.Warehouse, role));
        Assert.EndsWith("inventory/81/inspect", Assert.Single(fixture.Api.Commands).Path);
    }

    [Fact]
    public async Task 검수_POST만_성공하고_재조회가_미완료면_적재_진입을_제공하지_않는다()
    {
        var fixture = new Fixture { Received = true, StoreInspection = false };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("inspection", "81");
        var writer = model.Inspection!.작성;
        writer.수량대조확인 = writer.포장파손확인 = writer.품질기한확인 = writer.보관조건확인 = true;
        Assert.False(await model.Inspection.검수후재조회Async());
        Assert.True(writer.오류발생); Assert.Null(model.PutAwayHref);
    }

    [Fact]
    public async Task 운송_의뢰는_기존_폼의_명시_검토_저장으로만_생기고_같은_출고예정에_연결된다()
    {
        var fixture = new Fixture { Prepared = true };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("transport-draft", "302");
        Assert.Empty(fixture.Api.Commands);
        var draft = model.Draft!.초안;
        draft.하차지주소 = "서울시 확인된 하차지 1"; draft.차량유형 = "1톤 카고"; draft.상품수량확인 = true;
        Assert.True(draft.입력값검토());
        Assert.True(await model.Draft.서버저장Async());
        var command = Assert.Single(fixture.Api.Commands);
        var body = Assert.IsType<재고운송의뢰생성요청>(command.Body);
        Assert.Equal(302, body.출고예정Id); Assert.Equal(81, body.입고상품Id); Assert.Equal(4, body.요청수량);
        Assert.Equal("cargo-request", model.Draft.원장.원장!.TransportRequestId);
        Assert.Contains("outbound%3A302", model.ReturnHref);
    }

    [Fact]
    public async Task 기사_수락_전에는_실물_인계_명령을_보내지_않는다()
    {
        var fixture = new Fixture { Prepared = true, RequestId = "cargo-request" };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("transport-draft", "302"); ConfirmHandoff(model);
        Assert.False(await model.Draft!.서버인계완료Async()); Assert.Empty(fixture.Api.Commands);
    }

    [Fact]
    public async Task 독립_출고예정_검토는_같은_원장을_새로_조회해_변경된_운송_연결을_확인한다()
    {
        var fixture = new Fixture { Prepared = true };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("outbound", "302");
        Assert.True(model.Review!.상세.항목!.CanStartTransportRequestDraft);
        fixture.RequestId = "cargo-request";
        Assert.True(await model.Review.대상선택Async(302));
        Assert.Equal(302, model.Review.상세.항목!.OutboundPlanId);
        Assert.Equal("cargo-request", model.Review.상세.항목.TransportRequestId);
        Assert.False(model.Review.상세.항목.CanStartTransportRequestDraft);
        Assert.Empty(fixture.Api.Commands);
    }

    [Fact]
    public async Task 실제_기사_인계는_세_현장확인과_서버_재조회가_모두_필요하다()
    {
        var fixture = new Fixture { Prepared = true, RequestId = "cargo-request", Accepted = true };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("transport-draft", "302"); ConfirmHandoff(model);
        Assert.True(await model.Draft!.서버인계완료Async());
        var command = Assert.Single(fixture.Api.Commands);
        Assert.EndsWith("outbound-plan-reviews/302/handoff-complete", command.Path);
        Assert.NotNull(model.Draft.원장.원장!.HandoffCompletedAtUtc); Assert.False(model.Draft.원장.원장.CanCompleteHandoff);
    }

    [Fact]
    public async Task 확인한_기사와_저장_직전_기사가_달라지면_실물_인계를_차단한다()
    {
        var fixture = new Fixture { Prepared = true, RequestId = "cargo-request", Accepted = true };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("transport-draft", "302"); ConfirmHandoff(model);
        fixture.DriverId = "changed-driver";
        Assert.False(await model.Draft!.서버인계완료Async()); Assert.Empty(fixture.Api.Commands);
    }

    [Fact]
    public async Task 창고_계정_변경_후_늦은_전문_조회는_원장과_입력을_복원하지_않는다()
    {
        var fixture = new Fixture(); var access = new Access();
        var late = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Api.ReadAsyncOverride = _ => late.Task;
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, access);
        var load = model.LoadAsync("inspection", "81");
        access.Switch("new-owner"); late.SetResult(fixture.Inspection()); await load;
        Assert.False(model.Loaded); Assert.Null(model.Inspection); Assert.Null(model.Draft); Assert.Null(model.Review);
        Assert.Null(model.PutAwayHref);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task 전문_조회_권한이_사라지면_원장을_지우고_동일_입력의_로그인_복귀만_유지한다(int status)
    {
        var fixture = new Fixture { Prepared = true };
        using var model = new WarehouseSpecialistTaskViewModel(fixture.Api, new Access());
        await model.LoadAsync("transport-draft", "302");
        fixture.Api.ReadAsyncOverride = _ => Task.FromException<object>(new RoleWorkspaceAccessException(status, "서버 내부 내용"));
        await model.Draft!.초기화Async(302);
        Assert.False(model.Loaded); Assert.Null(model.Draft); Assert.Equal(status, model.DeniedStatus);
        Assert.Equal(status == 401, model.RequiresLogin);
        Assert.Contains("transport-draft%2F302", CargoWorkspaceRoutes.WarehouseLogin("transport-draft", "302"));
    }

    private static void Confirm(WarehouseTaskActionViewModel model)
    { model.StorageLocation = "A-1"; model.FirstConfirmed = model.SecondConfirmed = model.SaveConfirmed = true; }
    private static void ConfirmHandoff(WarehouseSpecialistTaskViewModel model)
    { var ledger = model.Draft!.원장; ledger.기사신원확인 = ledger.등록차량확인 = ledger.상품인계확인 = true; }

    private sealed class Fixture
    {
        public Api Api { get; }
        public bool Received { get; set; }
        public bool Prepared { get; set; }
        public bool IncludeInspection { get; set; }
        public bool Inspected { get; set; }
        public bool StoreInspection { get; set; } = true;
        public int ReceivedQuantity { get; set; } = 4;
        public int Defects { get; set; }
        public string? RequestId { get; set; }
        public bool Accepted { get; set; }
        public bool Released { get; set; }
        public string DriverId { get; set; } = "driver-1";
        private readonly DateTime _now = DateTime.UtcNow;
        public Fixture() { Api = new() { Read = Read, Write = Write }; }
        public 입고검수대상상세응답 Inspection(long parentId = 11) => new()
        { InboundItemId = 81, InboundId = parentId, WarehouseId = 9, ProductName = "상자", Sku = "BOX",
            CanInspect = !Inspected, InventoryStatus = Inspected ? "검수완료" : "보관중", ReceivedQuantity = ReceivedQuantity,
            DefectiveQuantity = Defects, AvailableQuantity = ReceivedQuantity - Defects, StorageLocation = "A-1", StorageCondition = "상온",
            ReceivedAtUtc = _now, InspectedAtUtc = Inspected ? _now : null };
        public 출고예정검토상세응답 Plan(long inventoryId = 81) => new()
        { OutboundPlanId = 302, InboundItemId = inventoryId, WarehouseId = 9, WarehouseName = "동네 창고", WarehouseActive = true,
            PickupAddressConfigured = true, ProductName = "상자", Sku = "BOX", Quantity = 4, AvailableQuantity = 4,
            StorageLocation = "A-1", StorageCondition = "상온", PackagingType = "일반포장", HandoffReadyAtUtc = _now,
            OutboundStatus = Released ? "출고완료" : "준비중", CanStartTransportRequestDraft = string.IsNullOrWhiteSpace(RequestId),
            TransportRequestId = RequestId, DriverAccepted = Accepted, AssignedDriverId = Accepted ? DriverId : null,
            AssignedDriverVehicle = Accepted ? "1톤 카고" : "", CanCompleteHandoff = Accepted && !Released,
            HandoffCompletedAtUtc = Released ? _now : null, HandoffStatus = Released ? "기사 인계 완료" : Accepted ? "기사 인계 대기" : "배차 대기",
            NextStep = "현재 원장을 확인해 주세요." };
        private object Read(string path)
        {
            if (path.EndsWith("warehouses", StringComparison.Ordinal)) return new 창고목록응답 { Items = [new() { Id = 9, IsActive = true, 창고명 = "동네 창고" }] };
            if (path.EndsWith("inbounds", StringComparison.Ordinal)) return new 입고요청목록응답 { Items = [Inbound(12)] };
            if (path.EndsWith("inbounds/11", StringComparison.Ordinal)) return Inbound(11);
            if (path.EndsWith("inbounds/12", StringComparison.Ordinal)) return Inbound(12);
            if (path.Contains("inspection-targets?", StringComparison.Ordinal)) return new 입고검수대상페이지응답
            { Items = IncludeInspection ? [new() { InboundId = 11, InboundItemId = 81, WarehouseId = 9, ReceivedQuantity = 4, CanInspect = !Inspected }] : [] };
            if (path.EndsWith("inventory/81/inspection-target", StringComparison.Ordinal)) return Inspection();
            if (path.Contains("put-away-tasks?", StringComparison.Ordinal)) return new 적재작업목록페이지응답();
            if (path.Contains("picking-tasks?", StringComparison.Ordinal)) return new 피킹작업목록페이지응답();
            if (path.Contains("packing-tasks?", StringComparison.Ordinal)) return new 포장작업목록페이지응답();
            if (path.Contains("outbound-handoff-tasks?", StringComparison.Ordinal)) return new 출고인계준비목록페이지응답();
            if (path.Contains("outbound-plan-reviews?", StringComparison.Ordinal)) return new 출고예정검토목록페이지응답();
            if (path.EndsWith("outbound-plan-reviews/302", StringComparison.Ordinal)) return Plan();
            if (path.EndsWith("outbound-handoff-tasks/81", StringComparison.Ordinal)) return new 출고인계준비상세응답
            { InboundItemId = 81, WarehouseId = 9, CanConfirmHandoff = !Prepared, ProductName = "상자", AvailableQuantity = 4,
                OutboundPlanId = Prepared ? 302 : null, HandoffReadyAtUtc = Prepared ? _now : null, OutboundStatus = "준비중" };
            throw new InvalidOperationException("예상하지 않은 조회: " + path);
        }
        private 입고요청항목응답 Inbound(long id) => new()
        { Id = id, 창고Id = 9, 상태 = id == 11 && Received ? 입고상태코드.완료 : 입고상태코드.예정, 예정상품명 = "상자", 예정SKU = "BOX", 예정수량 = 4 };
        private object Write(string path, object body)
        {
            if (body is 창고작업진입확인요청) return new 창고작업진입확인응답 { IsAllowed = true };
            if (body is 입고완료요청)
            { Received = true; return new 입고상품목록응답 { Items = [new() { Id = 81, 입고요청Id = 11, 창고Id = 9, 입고수량 = 4, 입고완료일시 = _now }] }; }
            if (body is 출고인계준비완료요청)
            { Prepared = true; return new 출고인계준비결과응답 { InboundItemId = 81, OutboundPlanId = 302, HandoffQuantity = 4, OutboundStatus = "준비중", HandoffReadyAtUtc = _now }; }
            if (body is 입고검수요청 inspection)
            { if (StoreInspection) { Inspected = true; ReceivedQuantity = inspection.검수수량; Defects = inspection.불량수량; }
                return new 창고작업결과응답 { 입고상품Id = 81, 창고Id = 9, 처리일시 = _now }; }
            if (body is 재고운송의뢰생성요청)
            { RequestId = "cargo-request"; return new 화주운송의뢰응답 { 의뢰Id = RequestId }; }
            if (body is 출고운송인계완료요청)
            { Released = true; return new 출고운송인계완료응답 { OutboundPlanId = 302, TransportRequestId = RequestId!, HandoffCompletedAtUtc = _now }; }
            throw new InvalidOperationException("예상하지 않은 변경: " + path);
        }
    }
    private sealed class Api : IRoleWorkspaceApi
    {
        public Func<string, object> Read { get; set; } = _ => throw new InvalidOperationException();
        public Func<string, object?>? ReadOverride { get; set; }
        public Func<string, Task<object>>? ReadAsyncOverride { get; set; }
        public Func<string, object, object> Write { get; set; } = (_, _) => throw new InvalidOperationException();
        public List<string> Reads { get; } = [];
        public List<string> Roles { get; } = [];
        public List<(string Path, object Body)> Commands { get; } = [];
        public async Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        { Reads.Add(path); Roles.Add(role); return (T)(ReadAsyncOverride is not null ? await ReadAsyncOverride(path) : ReadOverride?.Invoke(path) ?? Read(path)); }
        public Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct)
        { Roles.Add(role); Commands.Add((path, body)); return Task.FromResult((T?)(object)Write(path, body)); }
        public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => throw new NotSupportedException();
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity _identity = new("warehouse-owner", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string role) => _identity;
        public Task EnsureInitializedAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string role, CancellationToken ct = default) { Switch(null); return Task.CompletedTask; }
        public void Switch(string? owner) { _identity = new(owner, _identity.Revision + 1, owner is not null); Changed?.Invoke(); }
    }
}
