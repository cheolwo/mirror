using System.Text.Json;
using RestaurantDeskApp.Services;
using Ssalddel.Client.Infrastructure.Security;

namespace Ssalddel.Tests.Clients;

public sealed partial class RestaurantOrderProgressRecoveryTests
{
    [Fact]
    public async Task Restart_UnconfirmedProgress_RestoresOriginalIdPayloadRevision_AndNeverPostsOnRead()
    {
        var store = new DurableProgressStore();
        var api = new Orders { Command = _ => throw new HttpRequestException("lost") };
        using (var first = Desk(api, await OwnedAuth(), store))
        {
            await first.주문조회Async("A");
            await Assert.ThrowsAsync<HttpRequestException>(() => first.조리시간변경Async("A", 20));
        }
        var original = Assert.Single(api.Requests);
        api.Command = null;
        using var restarted = Desk(api, await OwnedAuth(), store);
        await restarted.주문조회Async("A");
        Assert.Single(api.Requests);
        await restarted.조리시간변경Async("A", 45);
        Assert.Equal(2, api.Requests.Count);
        AssertSameRequest(original, api.Requests[1]);
        Assert.Equal(20, api.Requests[1].조리예상분);
        Assert.Equal(5, api.Requests[1].예상Revision);
        Assert.Empty((await store.LoadAsync())!.Requests);
    }

    [Fact]
    public async Task Restart_AcceptedProgress_IsConfirmedByGetWithoutResubmission()
    {
        var store = new DurableProgressStore();
        var api = new Orders();
        api.Command = request => { api.Apply(request); throw new HttpRequestException("lost"); };
        using (var first = Desk(api, await OwnedAuth(), store))
        {
            await first.주문조회Async("A");
            await Assert.ThrowsAsync<HttpRequestException>(() => first.조리시간변경Async("A", 20));
        }
        using var restarted = Desk(api, await OwnedAuth(), store);
        var detail = await restarted.주문조회Async("A");
        Assert.Equal(20, detail!.상세주문!.조리예상분);
        Assert.Single(api.Requests);
        Assert.Empty((await store.LoadAsync())!.Requests);
    }

    [Fact]
    public async Task Restart_FailedCanonicalRead_KeepsSnapshotAndDoesNotPost()
    {
        var store = new DurableProgressStore();
        var api = new Orders { Command = _ => throw new HttpRequestException("lost") };
        using (var first = Desk(api, await OwnedAuth(), store))
        {
            await first.주문조회Async("A");
            await Assert.ThrowsAsync<HttpRequestException>(() => first.조리시간변경Async("A", 20));
        }
        api.Read = _ => throw new HttpRequestException("offline");
        using var restarted = Desk(api, await OwnedAuth(), store);
        await Assert.ThrowsAsync<HttpRequestException>(() => restarted.조리시간변경Async("A", 30));
        Assert.Single(api.Requests);
        Assert.Equal(api.Requests[0].클라이언트요청Id, Assert.Single((await store.LoadAsync())!.Requests).RequestId);
    }

    [Theory]
    [InlineData("load")]
    [InlineData("save")]
    public async Task StorageFailure_BlocksProtectedReadOrWrite_WithoutSyntheticSuccess(string failure)
    {
        var store = new DurableProgressStore();
        var api = new Orders();
        using var desk = Desk(api, await OwnedAuth(), store);
        if (failure == "load") store.LoadFailure = new JsonException("corrupt");
        else { await desk.주문조회Async("A"); store.SaveFailure = new IOException("secure store unavailable"); }
        await Assert.ThrowsAsync<RestaurantPendingStorageException>(() => desk.조리시간변경Async("A", 20));
        Assert.Empty(api.Requests);
        Assert.Equal(failure == "load" ? 0 : 1, api.Reads);
    }

    [Fact]
    public async Task ExplicitLogout_WaitsForDurableClear_AndRemovalFailureIsNotHidden()
    {
        var store = new DurableProgressStore();
        var auth = await OwnedAuth();
        var api = new Orders { Command = _ => throw new HttpRequestException("lost") };
        using var desk = Desk(api, auth, store);
        await desk.주문조회Async("A");
        await Assert.ThrowsAsync<HttpRequestException>(() => desk.조리시간변경Async("A", 20));
        store.ClearFailure = new IOException("removal failed");
        await Assert.ThrowsAsync<RestaurantPendingStorageException>(() => auth.LogoutAsync());
        Assert.False(auth.Session.IsAuthenticated);
        Assert.Contains("이전 요청", auth.ConsumeSessionCleanupNotice());
        Assert.Null(auth.ConsumeSessionCleanupNotice());
        Assert.Single((await store.LoadAsync())!.Requests);
    }

    [Fact]
    public async Task DifferentOwnerDuringDelayedSave_CannotSendOrRestorePreviousRequest()
    {
        var store = new DurableProgressStore();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        store.BeforeSave = async () => { entered.TrySetResult(); await release.Task; };
        var auth = await OwnedAuth();
        var api = new Orders();
        using var desk = Desk(api, auth, store);
        await desk.주문조회Async("A");
        var command = desk.조리시간변경Async("A", 20);
        await entered.Task;
        await auth.Session.ApplyAsync(Token("other-owner"));
        release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command);
        Assert.Empty(api.Requests);
        Assert.Null(await store.LoadAsync());
        store.BeforeSave = null;
        await desk.주문조회Async("A");
        await desk.조리시간변경Async("A", 30);
        Assert.Equal(30, Assert.Single(api.Requests).조리예상분);
    }

    [Fact]
    public async Task SameOwnerAuthenticationExpiry_RestartKeepsOriginalRequest()
    {
        var store = new DurableProgressStore();
        var auth = await OwnedAuth();
        var api = new Orders { Command = _ => throw new HttpRequestException("lost") };
        using (var first = Desk(api, auth, store))
        {
            await first.주문조회Async("A");
            await Assert.ThrowsAsync<HttpRequestException>(() => first.조리시간변경Async("A", 20));
            await auth.InvalidateRejectedSessionAsync(CancellationToken.None);
        }
        api.Command = null;
        using var restarted = Desk(api, await OwnedAuth(), store);
        await restarted.조리시간변경Async("A", 45);
        AssertSameRequest(api.Requests[0], api.Requests[1]);
    }

    private static async Task<RestaurantAuthService> OwnedAuth()
    {
        var session = new ClientAuthSession(new TokenStore(), new ClientSessionGuard());
        await session.ApplyAsync(Token("owner"));
        return new(new HttpClient(), session);
    }

    private sealed class DurableProgressStore : IRestaurantProgressPendingStore
    {
        private string? json;
        public Exception? LoadFailure, SaveFailure, ClearFailure;
        public Func<Task>? BeforeSave;
        public Task<RestaurantProgressPendingSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (LoadFailure is not null) throw LoadFailure;
            return Task.FromResult(json is null ? null : JsonSerializer.Deserialize<RestaurantProgressPendingSnapshot>(json));
        }
        public async Task SaveAsync(RestaurantProgressPendingSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (BeforeSave is not null) await BeforeSave();
            if (SaveFailure is not null) throw SaveFailure;
            json = JsonSerializer.Serialize(snapshot);
        }
        public async Task ClearAsync(string ownerId, CancellationToken cancellationToken = default)
        {
            if (ClearFailure is not null) throw ClearFailure;
            if ((await LoadAsync(cancellationToken))?.OwnerId == ownerId) json = null;
        }
    }
}
