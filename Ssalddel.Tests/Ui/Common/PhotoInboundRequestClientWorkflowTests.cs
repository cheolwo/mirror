using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Common.Inbound;
using Ssalddel.Contracts.Common.Versioning;
using Ssalddel.Contracts.Common.Workflow;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class PhotoInboundRequestClientWorkflowTests
{
    private static readonly byte[] Jpeg = [255, 216, 255, 224, 1, 2];

    [Fact]
    public async Task 사진만으로접수하고_동일번호정본을조회한뒤_준비목록에표시한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        await model.SelectPhotoAsync(new PhotoFile(Jpeg));
        Assert.True(model.CanSubmit);
        await model.SubmitAsync();
        var call = Assert.Single(f.Client.Created);
        Assert.Equal(string.Empty, call.Name);
        Assert.Equal(call.Id, model.Current?.Id);
        Assert.Contains(call.Id, f.Client.Reads);
        Assert.False(model.HasPendingRequest);
        Assert.Empty(f.Store.Records);
        Assert.Single(model.List.Items);
        Assert.True(model.CanStartFormal);
        Assert.False(model.CanSubmit);
    }

    [Fact]
    public async Task 저장후응답유실은_재진입해서같은번호GET으로만복구한다()
    {
        var f = new Fixture(); using var first = await f.Ready();
        f.Client.LoseCreateResponse = true;
        await first.SelectPhotoAsync(new PhotoFile(Jpeg)); await first.SubmitAsync();
        var pending = Assert.Single(f.Store.Records.Values);
        Assert.Null(pending.PayloadJson);
        Assert.Equal("create-photo", pending.Target);
        using var reopened = await f.Ready();
        Assert.Equal(pending.RequestId, reopened.Current?.Id);
        Assert.False(reopened.HasPendingRequest);
        Assert.Single(f.Client.Created);
        Assert.Empty(f.Store.Records);
    }

    [Fact]
    public async Task 미확인404는_재신청을해제하지않고_계속같은번호를조회한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.LoseCreateResponse = true; f.Client.HideReads = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        await model.RefreshAsync(); await model.SubmitAsync();
        Assert.True(model.HasPendingRequest); Assert.False(model.CanSubmit);
        Assert.Single(f.Client.Created); Assert.Single(f.Store.Records);
    }

    [Fact]
    public async Task 같은업무의명시미적용거절은_확인기록을조건부정리해_사진을다시선택할수있다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected();
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.False(model.HasPendingRequest); Assert.False(model.RecoveryBlocked);
        Assert.True(model.HasSelectedPhoto); Assert.True(model.CanSubmit); Assert.Empty(f.Store.Records);
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); f.Client.BeforeCreateFailure = null;
        await model.SubmitAsync(); Assert.NotNull(model.Current); Assert.Equal(2, f.Client.Created.Count);
        Assert.NotEqual(f.Client.Created[0].Id, f.Client.Created[1].Id);
    }

    [Fact]
    public async Task 숫자400만있는거절은_저장실패로추측하지않고_접수번호를보존한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = new SsalddelApiException("unknown", 400, "photo", string.Empty, null);
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync(); await model.RefreshAsync();
        Assert.True(model.HasPendingRequest); Assert.False(model.CanSubmit); Assert.Single(f.Store.Records);
        Assert.Single(f.Client.Created);
    }

    [Fact]
    public async Task 명시미적용이어도_확인기록이남으면_재진입이나404조회로새신청을허용하지않는다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected(); f.Store.IgnoreRemove = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.True(model.RecoveryBlocked); Assert.True(model.HasPendingRequest);
        using var reopened = await f.Ready(); await reopened.SubmitAsync();
        Assert.True(reopened.HasPendingRequest); Assert.False(reopened.CanSubmit); Assert.Single(f.Client.Created);
    }

    [Fact]
    public async Task 명시거절뒤삭제실패는_재진입에서거절표식을정리하고_새POST없이복구한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected(); f.Store.IgnoreRemove = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); model.Name = "비공개 준비 이름"; await model.SubmitAsync();
        var rejected = Assert.Single(f.Store.Records.Values);
        Assert.Equal("create-photo-rejected", rejected.Target); Assert.Null(rejected.PayloadJson);
        Assert.DoesNotContain("비공개", System.Text.Json.JsonSerializer.Serialize(rejected), StringComparison.Ordinal);
        f.Store.IgnoreRemove = false;
        using var reopened = await f.Ready();
        Assert.False(reopened.HasPendingRequest); Assert.False(reopened.RecoveryBlocked); Assert.False(reopened.CanSubmit);
        Assert.Null(reopened.Current); Assert.Empty(f.Store.Records); Assert.Empty(f.Client.Reads); Assert.Single(f.Client.Created);
    }

    [Fact]
    public async Task 거절표식은_최초시각과번호를유지하고_만료뒤에도삭제복구에사용한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected(); f.Store.IgnoreRemove = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        var original = f.Store.Saved[0]; var rejected = f.Store.Saved[1];
        Assert.Equal(original.RequestId, rejected.RequestId); Assert.Equal(original.CreatedAtUtc, rejected.CreatedAtUtc);
        var expired = WorkflowRecoveryPolicy.Expire(rejected with { CreatedAtUtc = DateTime.UtcNow.AddDays(-4) }, DateTime.UtcNow);
        Assert.Equal("create-photo-rejected", expired.Target); Assert.Null(expired.PayloadJson);
        f.Store.Records[(expired.Owner, expired.Scope)] = expired; f.Store.IgnoreRemove = false;
        using var reopened = await f.Ready();
        Assert.False(reopened.HasPendingRequest); Assert.Empty(f.Client.Reads); Assert.Single(f.Client.Created);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 거절표식의저장실패나쓰기누락은_기존미확정기록으로남겨재신청하지않는다(bool fail)
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected(); f.Store.FailRejectedWrite = fail; f.Store.DropRejectedWrite = !fail;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.True(model.RecoveryBlocked); Assert.Equal("create-photo", Assert.Single(f.Store.Records.Values).Target);
        using var reopened = await f.Ready(); await reopened.SubmitAsync();
        Assert.True(reopened.HasPendingRequest); Assert.False(reopened.CanSubmit); Assert.Single(f.Client.Created);
    }

    [Fact]
    public async Task 거절표식저장은됐지만응답이끊긴경우_저장된표식으로삭제를재개한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected(); f.Store.ThrowAfterRejectedWrite = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.True(model.RecoveryBlocked); Assert.Equal("create-photo-rejected", Assert.Single(f.Store.Records.Values).Target);
        using var reopened = await f.Ready();
        Assert.False(reopened.HasPendingRequest); Assert.False(reopened.RecoveryBlocked);
        Assert.Empty(f.Store.Records); Assert.Empty(f.Client.Reads); Assert.Single(f.Client.Created);
    }

    [Fact]
    public async Task 거절기록삭제뒤확인조회만실패했으면_새로고침에서부재확인으로마무리한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = Rejected(); f.Store.FailReadAfterRemove = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.True(model.RecoveryBlocked); Assert.True(model.HasPendingRequest); Assert.Empty(f.Store.Records);
        await model.RefreshAsync();
        Assert.False(model.RecoveryBlocked); Assert.False(model.HasPendingRequest); Assert.True(model.CanSubmit);
        Assert.True(model.HasSelectedPhoto); Assert.Empty(f.Client.Reads); Assert.Single(f.Client.Created);
    }

    private static SsalddelApiException Rejected() => new("invalid image", 400, "photo", string.Empty, null,
        workflowContext: new(OperatingSystemIds.WarehouseCommerceFulfillment, OperatingSystemLifecycleStageIds.WarehouseInboundPlan, 업무처리결과Codes.미적용));

    [Fact]
    public async Task 다른단계의미적용은_사진접수가거절됐다는근거로사용하지않는다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.BeforeCreateFailure = new SsalddelApiException("other stage", 400, "photo", string.Empty, null,
            workflowContext: new(OperatingSystemIds.WarehouseCommerceFulfillment, OperatingSystemLifecycleStageIds.WarehouseReceiving, 업무처리결과Codes.미적용));
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.True(model.HasPendingRequest); Assert.False(model.CanSubmit); Assert.Single(f.Store.Records);
    }

    [Fact]
    public async Task 다른요청의응답은_현재접수확인이나잠금해제에사용하지않는다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.LoseCreateResponse = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        f.Client.WrongReadId = true; await model.RefreshAsync();
        Assert.True(model.HasPendingRequest); Assert.Null(model.Current);
        Assert.Single(f.Store.Records); Assert.Single(f.Client.Created);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 보호저장실패또는쓰기누락이면_사진을전송하지않는다(bool fail)
    {
        var f = new Fixture(); using var model = await f.Ready();
        await model.SelectPhotoAsync(new PhotoFile(Jpeg));
        f.Store.FailWrite = fail; f.Store.DropWrite = !fail;
        await model.SubmitAsync(); Assert.Empty(f.Client.Created); Assert.True(model.RecoveryBlocked);
    }

    [Fact]
    public async Task 확인기록삭제가반영되지않으면_정본을찾아도잠금을유지한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Store.IgnoreRemove = true;
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.True(model.RecoveryBlocked); Assert.True(model.HasPendingRequest);
        Assert.Null(model.Current); Assert.Single(f.Store.Records);
    }

    [Fact]
    public async Task 계정이바뀐뒤늦게도착한응답은_사진과결과를현재화면에남기지않는다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        f.Client.AfterCreate = () => f.Context.Current = new("other-owner", 2, new string('b', 32));
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.Null(model.Current); Assert.Null(model.PreviewUrl); Assert.Empty(model.List.Items);
        var record = Assert.Single(f.Store.Records.Values);
        Assert.Equal("photo-owner", record.Owner);
    }

    [Fact]
    public async Task 유휴상태에서계정이바뀌면_이전사진과이름과준비목록을즉시비운다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        Assert.NotNull(model.Current); Assert.NotNull(model.PreviewUrl); Assert.Single(model.List.Items);
        f.Context.Current = new("new-owner", 2, new string('c', 32)); f.Access.Notify();
        Assert.Null(model.Current); Assert.Null(model.PreviewUrl); Assert.Empty(model.Name); Assert.Empty(model.Rename);
        Assert.Empty(model.List.Items); Assert.True(model.RecoveryBlocked);
    }

    [Fact]
    public async Task 첫진입은_역할로그인을초기화한뒤_계정문맥을캡처한다()
    {
        var f = new Fixture(); f.Context.Ready = false; f.Access.Initialize = () => f.Context.Ready = true;
        using var model = await f.Ready();
        Assert.False(model.HasError); Assert.False(model.RecoveryBlocked); Assert.Equal(1, f.Access.InitializeCount);
    }

    [Fact]
    public async Task 잘못된새사진이나촬영취소는_기존선택사진을지우지않는다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); var preview = model.PreviewUrl;
        await model.SelectPhotoAsync(new PhotoFile([1, 2, 3]));
        Assert.Equal(preview, model.PreviewUrl); Assert.True(model.HasSelectedPhoto);
        await model.CaptureAsync();
        Assert.Equal(preview, model.PreviewUrl); Assert.True(model.HasSelectedPhoto);
    }

    [Fact]
    public async Task 이름수정은_확인한개정으로요청하고_같은신청을다시조회한다()
    {
        var f = new Fixture(); using var model = await f.Ready();
        await model.SelectPhotoAsync(new PhotoFile(Jpeg)); await model.SubmitAsync();
        var original = model.Current!; model.Rename = "다음 주 물품";
        await model.RenameAsync();
        var rename = Assert.Single(f.Client.Renames);
        Assert.Equal(original.Id, rename.Id); Assert.Equal(1, rename.Revision);
        Assert.Equal(2, model.Current?.Revision); Assert.Equal("다음 주 물품", model.Current?.이름);
    }

    [Fact]
    public async Task 연결된신청은_이름수정과정식입고중복전환을제공하지않는다()
    {
        var f = new Fixture(); var id = Guid.NewGuid();
        f.Client.Items[id] = new() { Id = id, Revision = 2, 입고요청Id = 77 };
        using var model = f.Model(); await model.InitializeAsync(id);
        Assert.False(model.CanRename); Assert.False(model.CanStartFormal);
        await model.RenameAsync(); Assert.Empty(f.Client.Renames);
    }

    [Fact]
    public async Task 준비목록은_다음페이지를서버에요청하고_전체목록처럼합치지않는다()
    {
        var f = new Fixture();
        for (var index = 0; index < 12; index++)
        { var id = Guid.NewGuid(); f.Client.Items[id] = new() { Id = id, Revision = 1, 이름 = "준비 " + index }; }
        using var model = await f.Ready(); Assert.Equal(10, model.List.Items.Count);
        await model.LoadPageAsync(1);
        Assert.Equal(1, model.List.Page); Assert.Equal(12, model.List.TotalCount); Assert.Equal(2, model.List.Items.Count);
    }

    private sealed class Fixture
    {
        public FakeClient Client { get; } = new(); public FakeStore Store { get; } = new(); public FakeContext Context { get; } = new(); public Access Access { get; } = new();
        public 가입고사진신청ViewModel Model() => new(Client, Store, Context, new Camera(), Access);
        public async Task<가입고사진신청ViewModel> Ready() { var model = Model(); await model.InitializeAsync(); return model; }
    }
    private sealed class FakeContext : IWorkflowRecoveryContext
    {
        public WorkflowRecoverySnapshot Current { get; set; } = new("photo-owner", 1, new string('a', 32));
        public bool Ready = true;
        public WorkflowRecoverySnapshot Capture() => Ready ? Current : throw new InvalidOperationException("not initialized");
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => snapshot == Current;
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public event Action? Changed; public Action? Initialize; public int InitializeCount;
        public void Notify() => Changed?.Invoke();
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => new("photo-owner", 1, true);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default)
        { Assert.Equal(RoleWorkspaceCatalog.Shipper, roleKey); InitializeCount++; Initialize?.Invoke(); return Task.CompletedTask; }
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
    private sealed class FakeStore : IWorkflowRecoveryStore
    {
        public Dictionary<(string Owner, string Scope), WorkflowRecoveryRecord> Records { get; } = [];
        public List<WorkflowRecoveryRecord> Saved { get; } = [];
        public bool FailWrite, DropWrite, IgnoreRemove;
        public bool FailRejectedWrite, DropRejectedWrite, ThrowAfterRejectedWrite, FailReadAfterRemove;
        private bool failNextRead;
        public Task<WorkflowRecoveryRecord?> LoadAsync(string owner, string scope, CancellationToken ct)
        {
            if (failNextRead) { failNextRead = false; throw new IOException("read unavailable"); }
            return Task.FromResult(Records.GetValueOrDefault((owner, scope)));
        }
        public Task SaveAsync(WorkflowRecoveryRecord record, CancellationToken ct)
        {
            WorkflowRecoveryPolicy.Validate(record);
            Saved.Add(record);
            if (FailWrite || (FailRejectedWrite && record.Target == "create-photo-rejected")) throw new IOException("write unavailable");
            if (!DropWrite && !(DropRejectedWrite && record.Target == "create-photo-rejected")) Records[(record.Owner, record.Scope)] = record;
            if (ThrowAfterRejectedWrite && record.Target == "create-photo-rejected") throw new IOException("write response unavailable");
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string owner, string scope, Guid expectedRequestId, CancellationToken ct)
        {
            if (!IgnoreRemove && Records.GetValueOrDefault((owner, scope))?.RequestId == expectedRequestId)
            { Records.Remove((owner, scope)); if (FailReadAfterRemove) { failNextRead = true; FailReadAfterRemove = false; } }
            return Task.CompletedTask;
        }
    }
    private sealed class Camera : I업무사진촬영Adapter
    { public bool IsSupported => true; public Task<업무촬영사진?> CaptureAsync(string title, CancellationToken ct) => Task.FromResult<업무촬영사진?>(null); }
    private sealed class PhotoFile(byte[] bytes) : IBrowserFile
    {
        public string Name => "private-original-name.jpg"; public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => bytes.LongLength; public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream(bytes, false);
    }
    private sealed class FakeClient : I가입고사진신청Client
    {
        public Dictionary<Guid, 가입고사진신청응답> Items { get; } = [];
        public List<(Guid Id, string? Name)> Created { get; } = [];
        public List<Guid> Reads { get; } = [];
        public List<(Guid Id, long Revision)> Renames { get; } = [];
        public bool LoseCreateResponse, HideReads, WrongReadId; public Action? AfterCreate; public Exception? BeforeCreateFailure;
        public Task<가입고사진신청응답?> CreateAsync(Guid requestId, string? name, string contentType, byte[] bytes, CancellationToken ct)
        {
            Created.Add((requestId, name)); if (BeforeCreateFailure is not null) throw BeforeCreateFailure;
            Items[requestId] = new() { Id = requestId, Revision = 1, 이름 = name ?? string.Empty };
            AfterCreate?.Invoke(); if (LoseCreateResponse) throw new HttpRequestException("response lost");
            return Task.FromResult<가입고사진신청응답?>(Copy(Items[requestId]));
        }
        public Task<가입고사진신청응답?> GetAsync(Guid id, CancellationToken ct)
        {
            Reads.Add(id); var result = HideReads ? null : Items.GetValueOrDefault(id);
            if (result is null) return Task.FromResult<가입고사진신청응답?>(null);
            var copied = Copy(result); if (WrongReadId) copied.Id = Guid.NewGuid();
            return Task.FromResult<가입고사진신청응답?>(copied);
        }
        public Task<가입고사진신청목록응답> ListAsync(int page, int pageSize, CancellationToken ct)
            => Task.FromResult(new 가입고사진신청목록응답 { Page = page, PageSize = pageSize, TotalCount = Items.Count, Items = Items.Values.Skip(page * pageSize).Take(pageSize).Select(Copy).ToArray() });
        public Task<가입고사진내용응답> PhotoAsync(Guid id, CancellationToken ct) => Task.FromResult(new 가입고사진내용응답 { ContentType = "image/jpeg", Bytes = Jpeg });
        public Task<가입고사진신청응답?> RenameAsync(Guid id, 가입고사진이름변경요청 request, CancellationToken ct)
        {
            Renames.Add((id, request.Revision)); var item = Items[id];
            if (item.Revision != request.Revision) throw new InvalidOperationException("revision changed");
            item.Revision++; item.이름 = request.이름;
            return Task.FromResult<가입고사진신청응답?>(Copy(item));
        }
        private static 가입고사진신청응답 Copy(가입고사진신청응답 item) => new() { Id = item.Id, 이름 = item.이름, Revision = item.Revision, 입고요청Id = item.입고요청Id, CreatedAtUtc = item.CreatedAtUtc, UpdatedAtUtc = item.UpdatedAtUtc };
    }
}
