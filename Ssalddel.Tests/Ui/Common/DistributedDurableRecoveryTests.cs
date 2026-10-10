using System.Text.Json;
using Ssalddel.Contracts.Common.Warehouse;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.DistributedFulfillment;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Common;

public sealed class DistributedDurableRecoveryTests
{
    private const string Scope = "distributed.cargo-driver.round.v1";

    [Fact]
    public async Task 사진응답유실후_세션재시작은_업로드GET만하고_명시완료에서_같은번호를쓴다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.AfterPhotoPost = _ => throw new IOException("saved photo reply lost");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); var id = first.Photo.CompletionRequestId;
        var record = await store.LoadAsync("actor", Scope, default);
        Assert.NotNull(record); Assert.DoesNotContain("ImageBase64", record.PayloadJson);
        Assert.DoesNotContain(Convert.ToBase64String(new byte[] { 255, 216, 255, 224, 0, 1 }), record.PayloadJson);
        Assert.Empty(t.Posts); first.Dispose(); t.AfterPhotoPost = null;
        using var restored = Model(t, store); await restored.LoadAsync("a-different-wave");
        Assert.Equal("wave-a", restored.SelectedId); Assert.Equal(id, restored.Photo.CompletionRequestId);
        Assert.Empty(restored.Photo.Photos); Assert.True(restored.Photo.FullyUploaded);
        Assert.Empty(t.Posts); Assert.Single(t.PhotoPosts); Assert.Contains(t.Gets, path => path.Contains("/evidence/uploads/"));
        await restored.RetryPhotoAsync();
        Assert.Equal(id, Assert.Single(t.Posts).Body.RequestId); Assert.Single(t.PhotoPosts);
        Assert.False(restored.HasPendingPhoto); Assert.Null(await store.LoadAsync("actor", Scope, default));
    }

    [Fact]
    public async Task 미저장사진은_raw를복원하거나_재전송하지않고_미완료GET후_현재업무에서재촬영한다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.BeforePhotoPost = _ => throw new IOException("photo was not stored");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); first.Dispose();
        using var restored = Model(t, store); await restored.LoadAsync("wave-a");
        Assert.True(restored.Photo.NeedsRecapture); Assert.Empty(restored.Photo.Photos); Assert.True(restored.PhotoInputsLocked);
        await restored.RetryPhotoAsync(); Assert.Single(t.PhotoPosts); Assert.Empty(t.Posts);
        await restored.CancelPhotoWorkAsync();
        Assert.False(restored.HasPendingPhoto); Assert.True(restored.CanEdit);
        Assert.Null(await store.LoadAsync("actor", Scope, default)); Assert.Empty(t.Posts);
    }

    [Fact]
    public async Task 완료응답유실후_재진입은_원영수증과정본만확인하고_사진과완료를재전송하지않는다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.AfterPost = _ => throw new IOException("completion committed, reply lost");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); var id = first.RequestId; first.Dispose();
        using var restored = Model(t, store); await restored.LoadAsync("wave-a");
        Assert.Single(t.PhotoPosts); Assert.Single(t.Posts); Assert.Equal(id, t.Posts[0].Body.RequestId);
        Assert.False(restored.HasPendingCommand); Assert.False(restored.HasPendingPhoto); Assert.NotNull(restored.Message);
        Assert.Null(await store.LoadAsync("actor", Scope, default));
    }

    [Fact]
    public async Task 미접수완료원문은_페이지재시작후에도_그대로보존하고_사용자재시도만POST한다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.BeforePost = _ => throw new IOException("before command commit");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); var id = first.RequestId;
        var original = JsonSerializer.Serialize(t.Posts[0].Body, t.Posts[0].Body.GetType()); first.Dispose(); t.BeforePost = null;
        using var restored = Model(t, store); await restored.LoadAsync("wave-a");
        Assert.True(restored.HasPendingCommand); Assert.Equal(id, restored.RequestId); Assert.Single(t.Posts);
        await restored.ChooseAsync("another-wave"); Assert.Equal("wave-a", restored.SelectedId);
        await restored.RetryPendingAsync(); Assert.Equal(2, t.Posts.Count); Assert.Single(t.PhotoPosts);
        Assert.All(t.Posts, post => Assert.Equal(original, JsonSerializer.Serialize(post.Body, post.Body.GetType())));
        Assert.False(restored.HasPendingCommand);
    }

    [Fact]
    public async Task 보호저장실패는_사진과완료POST를모두차단한다()
    {
        var t = DistributedUiTransport.Round(); using var model = await Ready(t, new FailingRecoveryStore());
        await model.CompletePhotoAsync(true); Assert.Empty(t.PhotoPosts); Assert.Empty(t.Posts); Assert.NotNull(model.Error);
    }

    [Fact]
    public async Task 다른계정은_미확정사진과업무원문을볼수없다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.BeforePhotoPost = _ => throw new IOException("unknown");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); first.Dispose();
        t.Gets.Clear(); t.SetIdentity(new("other", 2, true)); t.Workspace.CurrentUserId = "other";
        using var other = Model(t, store); await other.LoadAsync("wave-a");
        Assert.False(other.HasPendingPhoto); Assert.Null(other.Photo.Work);
        Assert.DoesNotContain(t.Gets, path => path.Contains("/receipts/") || path.Contains("/evidence/uploads/"));
    }

    [Fact]
    public async Task 원문만료는_원번호조회만허용하고_확인실패중에는_새업무로전환하지않는다()
    {
        var clock = new RecoveryClock(); var store = new VolatileWorkflowRecoveryStore(clock); var t = DistributedUiTransport.Round();
        t.BeforePhotoPost = _ => throw new IOException("unknown");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); var id = first.Photo.CompletionRequestId; first.Dispose();
        clock.Now = DateTimeOffset.UtcNow.AddDays(4);
        t.BeforeGet = path => { if (path.Contains("/receipts/")) throw new IOException("offline"); };
        using var restored = Model(t, store); await restored.LoadAsync("wave-a");
        Assert.True(restored.RecoveryExpired); Assert.Null(restored.Photo.Work); Assert.False(restored.CanEdit);
        Assert.Equal(id, restored.RequestId); Assert.Null((await store.LoadAsync("actor", Scope, default))!.PayloadJson);
        await restored.DiscardExpiredRecoveryAsync(); Assert.True(restored.RecoveryExpired); Assert.Single(t.PhotoPosts); Assert.Empty(t.Posts);
        t.BeforeGet = null; await restored.DiscardExpiredRecoveryAsync();
        Assert.False(restored.RecoveryExpired); Assert.True(restored.CanEdit); Assert.Null(await store.LoadAsync("actor", Scope, default));
    }

    [Theory]
    [InlineData("aggregate")]
    [InlineData("revision")]
    public async Task 완료명령복원GET은_원대상과원판본을대조한뒤에만_보호기록을지운다(string mismatch)
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.BeforePost = _ => throw new IOException("command result unknown");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); var id = first.RequestId!.Value; first.Dispose();
        t.Workspace.Waves[0].Revision = 2;
        t.Workspace.Waves = [.. t.Workspace.Waves, new() { WaveId = "wave-b", Revision = 2 }];
        t.Receipts[id] = new(id, "PickBatch", mismatch == "aggregate" ? "wave-b" : "wave-a", null, 1,
            mismatch == "revision" ? 1 : 2, DateTime.UtcNow);
        using var restored = Model(t, store); await restored.LoadAsync("wave-a");
        Assert.True(restored.HasPendingCommand); Assert.NotNull(restored.Error); Assert.Null(restored.Message);
        Assert.NotNull(await store.LoadAsync("actor", Scope, default)); Assert.Single(t.Posts); Assert.Single(t.PhotoPosts);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("quantity")]
    [InlineData("aggregate")]
    public async Task 사진만보관된복원에서_다른업무영수증은_완료로승격하지않는다(string mismatch)
    {
        var store = new VolatileWorkflowRecoveryStore(); var t = DistributedUiTransport.Round();
        t.AfterPhotoPost = _ => throw new IOException("photo reply lost");
        var first = await Ready(t, store); await first.CompletePhotoAsync(true); var id = first.Photo.CompletionRequestId!.Value; first.Dispose();
        t.Workspace.Waves[0].Revision = 2;
        t.Receipts[id] = new(id, mismatch == "kind" ? "DeliverBatch" : "PickBatch",
            mismatch == "aggregate" ? "another-wave" : "wave-a", null, mismatch == "quantity" ? 2 : 1, 2, DateTime.UtcNow);
        using var restored = Model(t, store); await restored.LoadAsync("wave-a");
        Assert.NotNull(restored.Error); Assert.Null(restored.Message); Assert.True(restored.HasPendingPhoto);
        Assert.NotNull(await store.LoadAsync("actor", Scope, default)); Assert.Empty(t.Posts);
    }

    private static 분산이행PageViewModel Model(DistributedUiTransport transport, IWorkflowRecoveryStore store)
        => new(new(transport, "cargo-driver"), "round", new Test업무사진촬영Adapter(), store);
    private static async Task<분산이행PageViewModel> Ready(DistributedUiTransport transport, IWorkflowRecoveryStore store)
    {
        var model = Model(transport, store); await model.LoadAsync("wave-a");
        await model.CapturePhotoAsync(DistributedPhotoWorkflowTests.PickWork(), "피킹", "pick", "waves/wave-a/pick-batches", ["pick"], 1);
        return model;
    }
}
