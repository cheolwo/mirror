using Microsoft.AspNetCore.Components.Forms;
using Ssalddel.Contracts.Common.Inventory;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Contracts.Driver.Transport;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Cargo;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkflowCompletionCargoTests
{
    [Theory]
    [InlineData("pickup", "상차지도착", false)]
    [InlineData("dropoff", "하차지도착", true)]
    public async Task 운송_정본확인후에만_완료안내를_한번_보여준다(string action, string state, bool final)
    {
        var api = new CargoApi(state);
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, action);
        Assert.Null(model.CompletionFeedback.Current);
        Ready(model);
        await model.SubmitAsync();
        var notice = Assert.IsType<Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Completion.WorkflowCompletionNotice>(model.CompletionFeedback.Current);
        Assert.Equal(final, notice.IsFinal);
        Assert.Equal($"cargo:1:{action}", notice.Key);
        Assert.Contains(final ? "하차" : "상차", notice.Title);
        Assert.Equal(1, api.Writes);
        model.CompletionFeedback.Dismiss();
        await model.LoadAsync(1, action);
        await model.SubmitAsync();
        Assert.Null(model.CompletionFeedback.Current);
        Assert.Equal(1, api.Writes);
    }

    [Theory]
    [InlineData("pickup", "상차지도착")]
    [InlineData("dropoff", "하차지도착")]
    public async Task 운송_응답유실중에는_성공창이없고_같은업무_재조회로_복구한다(string action, string state)
    {
        var api = new CargoApi(state) { LoseResponse = true };
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, action); Ready(model);
        await model.SubmitAsync();
        Assert.True(model.AwaitingResult);
        Assert.Null(model.CompletionFeedback.Current);
        await model.SubmitAsync();
        Assert.Equal(1, api.Writes);
        await model.LoadAsync(1, action);
        Assert.False(model.AwaitingResult);
        Assert.NotNull(model.CompletionFeedback.Current);
        Assert.Equal(1, api.Writes);
    }

    [Fact]
    public async Task 이미완료된운송_첫조회는_새완료안내가아니다()
    {
        var api = new CargoApi("인수완료");
        using var model = new CargoTransportActionViewModel(new(api), api, new Access());
        await model.LoadAsync(1, "dropoff");
        Assert.True(model.CompletionConfirmed);
        Assert.Null(model.CompletionFeedback.Current);
        Assert.Equal(0, api.Writes);
    }

    [Fact]
    public async Task 운송_계정과대상전환은_이전완료안내를_지운다()
    {
        var api = new CargoApi("상차지도착"); var access = new Access();
        using var model = new CargoTransportActionViewModel(new(api), api, access);
        await model.LoadAsync(1, "pickup"); Ready(model); await model.SubmitAsync();
        Assert.NotNull(model.CompletionFeedback.Current);
        await model.LoadAsync(2, "pickup");
        Assert.Null(model.CompletionFeedback.Current);
        api.State = "상차지도착";
        await model.LoadAsync(1, "pickup"); Ready(model); await model.SubmitAsync();
        Assert.NotNull(model.CompletionFeedback.Current);
        access.Switch("other");
        Assert.Null(model.CompletionFeedback.Current);
    }

    [Theory]
    [InlineData("put-away", "적재")]
    [InlineData("picking", "피킹")]
    [InlineData("packing", "포장")]
    public async Task 창고_완료와_같은대상재조회가_모두성공해야_완료안내를_보인다(string kind, string label)
    {
        var api = new WarehouseApi(kind);
        using var model = new WarehouseTaskActionViewModel(new(api), new Access());
        await model.LoadAsync(kind, "8"); Confirm(model);
        Assert.Null(model.CompletionFeedback.Current);
        await model.SubmitAsync();
        Assert.True(model.Saved); Assert.False(model.AwaitingResult);
        Assert.Contains(label, model.CompletionFeedback.Current!.Title);
        Assert.False(model.CompletionFeedback.Current.IsFinal);
        Assert.Equal(1, api.Writes);
        model.CompletionFeedback.Dismiss();
        await model.LoadAsync(kind, "8"); await model.SubmitAsync();
        Assert.Null(model.CompletionFeedback.Current);
        Assert.Equal(1, api.Writes);
    }

    [Theory]
    [InlineData("put-away")]
    [InlineData("picking")]
    [InlineData("packing")]
    public async Task 창고_응답유실은_같은완료의_재조회만하며_명령을_반복하지않는다(string kind)
    {
        var api = new WarehouseApi(kind) { LoseResponse = true };
        using var model = new WarehouseTaskActionViewModel(new(api), new Access());
        await model.LoadAsync(kind, "8"); Confirm(model); await model.SubmitAsync();
        Assert.True(model.AwaitingResult); Assert.Null(model.CompletionFeedback.Current);
        await model.SubmitAsync(); Assert.Equal(1, api.Writes);
        await model.LoadAsync(kind, "8");
        Assert.True(model.Saved); Assert.False(model.AwaitingResult);
        Assert.NotNull(model.CompletionFeedback.Current);
        Assert.Equal(1, api.Writes);
    }

    [Fact]
    public async Task 창고_후속조회가실패하면_완료응답만으로_성공을표시하지않는다()
    {
        var api = new WarehouseApi("picking") { FailReadAfterWrite = true };
        using var model = new WarehouseTaskActionViewModel(new(api), new Access());
        await model.LoadAsync("picking", "8"); Confirm(model); await model.SubmitAsync();
        Assert.True(model.AwaitingResult); Assert.Null(model.CompletionFeedback.Current);
        api.FailReadAfterWrite = false;
        await model.LoadAsync("picking", "8");
        Assert.NotNull(model.CompletionFeedback.Current); Assert.Equal(1, api.Writes);
    }

    [Fact]
    public async Task 피킹시작을_피킹완료로_안내하지않고_계정전환시_안내를지운다()
    {
        var api = new WarehouseApi("picking") { CanStart = true }; var access = new Access();
        using var model = new WarehouseTaskActionViewModel(new(api), access);
        await model.LoadAsync("picking", "8"); model.SaveConfirmed = true;
        await model.SubmitAsync();
        Assert.Null(model.CompletionFeedback.Current); Assert.False(model.Saved);
        Confirm(model); await model.SubmitAsync();
        Assert.NotNull(model.CompletionFeedback.Current);
        access.Switch("other");
        Assert.Null(model.CompletionFeedback.Current);
    }

    private static void Ready(CargoTransportActionViewModel model)
    {
        model.SetPhoto(new Photo());
        model.CargoConfirmed = model.LocationConfirmed = model.RecipientConfirmed = model.PaymentConfirmed = model.SaveConfirmed = true;
    }
    private static void Confirm(WarehouseTaskActionViewModel model)
    { model.StorageLocation = "A-1"; model.FirstConfirmed = model.SecondConfirmed = model.SaveConfirmed = true; }
    private sealed class Photo : IBrowserFile
    {
        public string Name => "photo.jpg";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 3;
        public string ContentType => "image/jpeg";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) => new MemoryStream([1, 2, 3]);
    }
    private abstract class Api : IRoleWorkspaceApi
    {
        public int Writes { get; protected set; }
        public abstract Task<T> GetAsync<T>(string role, string path, CancellationToken ct);
        public abstract Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct);
        public Task<T?> PutAsync<T>(string role, string path, object body, CancellationToken ct) => PostAsync<T>(role, path, body, ct);
        public Task<T?> UploadAsync<T>(string role, string path, HttpContent body, CancellationToken ct)
            => Task.FromResult((T?)(object)new CargoEvidenceUploadResponse("photo", null));
    }
    private sealed class CargoApi(string state) : Api
    {
        public string State { get; set; } = state;
        public bool LoseResponse { get; set; }
        public override Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
            => Task.FromResult((T)(object)new 기사운송상세응답 { Id = path.EndsWith("/2", StringComparison.Ordinal) ? 2 : 1, 상태 = State });
        public override Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct) where T : default
        {
            Writes++; State = path.EndsWith("/pickup-complete", StringComparison.Ordinal) ? "상차완료" : "인수완료";
            if (LoseResponse) throw new HttpRequestException("Synthetic response loss");
            return Task.FromResult((T?)(object)new 기사운송상태변경응답 { Id = 1, 상태 = State });
        }
    }
    private sealed class WarehouseApi(string kind) : Api
    {
        private readonly DateTime completedAt = new(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc);
        public bool LoseResponse { get; set; }
        public bool FailReadAfterWrite { get; set; }
        public bool CanStart { get; set; }
        private bool completed;
        public override Task<T> GetAsync<T>(string role, string path, CancellationToken ct)
        {
            if (FailReadAfterWrite && Writes > 0) throw new HttpRequestException("Synthetic read loss");
            object response = kind switch
            {
                "put-away" => new 적재작업상세응답 { InboundItemId = 8, CanPutAway = !completed, StorageLocation = "A-1", PutAwayAtUtc = completed ? completedAt : null },
                "packing" => new 포장작업상세응답 { InboundItemId = 8, CanPack = !completed, AvailableQuantity = 1,
                    PackingType = 포장유형코드.일반포장, PackedAtUtc = completed ? completedAt : null },
                _ => new 피킹작업상세응답 { TaskKey = "8", CanStart = CanStart, CanComplete = !completed && !CanStart, RackCode = "A-1",
                    Status = completed ? 피킹작업조회상태코드.완료 : 피킹작업조회상태코드.진행중, StartedAtUtc = completedAt, CompletedAtUtc = completed ? completedAt : null }
            };
            return Task.FromResult((T)response);
        }
        public override Task<T?> PostAsync<T>(string role, string path, object body, CancellationToken ct) where T : default
        {
            Writes++;
            if (CanStart)
            {
                CanStart = false;
                return Task.FromResult((T?)(object)new 피킹작업결과응답 { TaskKey = "8", Status = 피킹작업조회상태코드.진행중, StartedAtUtc = completedAt });
            }
            completed = true;
            if (LoseResponse) throw new HttpRequestException("Synthetic response loss");
            object response = kind switch
            {
                "put-away" => new 적재작업결과응답 { InboundItemId = 8, InventoryStatus = "적재완료", StorageLocation = "A-1", PutAwayAtUtc = completedAt },
                "packing" => new 포장작업결과응답 { InboundItemId = 8, InventoryStatus = "포장완료-" + 포장유형코드.일반포장, PackagingQuantity = 1, PackagingType = 포장유형코드.일반포장, PackedAtUtc = completedAt },
                _ => new 피킹작업결과응답 { TaskKey = "8", Status = 피킹작업조회상태코드.완료, CompletedAtUtc = completedAt }
            };
            return Task.FromResult((T?)response);
        }
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        private RoleWorkspaceIdentity identity = new("worker", 1, true);
        public event Action? Changed;
        public RoleWorkspaceIdentity GetIdentity(string role) => identity;
        public Task EnsureInitializedAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignInAsync(string role, string name, string password, CancellationToken ct = default) => Task.CompletedTask;
        public Task SignOutAsync(string role, CancellationToken ct = default) { Switch(null); return Task.CompletedTask; }
        public void Switch(string? owner) { identity = new(owner, identity.Revision + 1, owner is not null); Changed?.Invoke(); }
    }
}
