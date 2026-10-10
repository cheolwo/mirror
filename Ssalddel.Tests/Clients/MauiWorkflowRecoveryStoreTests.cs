using System.Text.Json;
using Ssalddel.Client.Infrastructure.Security;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;
using Ssalddel.Ui.Common.Areas.App.Services;
using SsalddelApp.Services;

namespace Ssalddel.Tests.Clients;

public sealed class MauiWorkflowRecoveryStoreTests
{
    [Fact]
    public async Task 일반앱의분산업무는_화주로그인이아닌_선택역할의계정과판본으로복원한다()
    {
        var auth = await Session(); var storage = new Storage(); var access = new Access();
        var store = new MauiWorkflowRecoveryStore(auth, storage.Read, storage.Write, storage.Remove, access: access);
        var record = Record() with { Owner = "driver-a", Scope = "distributed.cargo-driver.round.v1" };
        await store.SaveAsync(record, default); Assert.Equal(record, await store.LoadAsync(record.Owner, record.Scope, default));
        access.Identity = new("driver-b", 2, true);
        await Assert.ThrowsAsync<OperationCanceledException>(() => store.LoadAsync(record.Owner, record.Scope, default));
        Assert.Null(await store.LoadAsync("driver-b", record.Scope, default));
    }

    [Fact]
    public async Task 새adapter도_같은계정의원요청만복원하고_다른계정은조회할수없다()
    {
        var auth = await Session(); var storage = new Storage(); var first = Store(auth, storage); var record = Record();
        await first.SaveAsync(record, default);
        var reopened = Store(auth, storage); Assert.Equal(record, await reopened.LoadAsync("shipper-a", record.Scope, default));
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot(user: "shipper-b", token: "other"));
        Assert.Null(await reopened.LoadAsync("shipper-b", record.Scope, default));
        await Assert.ThrowsAsync<OperationCanceledException>(() => reopened.LoadAsync("shipper-a", record.Scope, default));
        Assert.Single(storage.Values);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 저장소조회중_로그아웃이나동일계정재인증이면_늦은원문을전달하지않는다(bool sameOwner)
    {
        var auth = await Session(); var record = Record(); var storage = new Storage(); await Store(auth, storage).SaveAsync(record, default);
        var waiting = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new MauiWorkflowRecoveryStore(auth, _ => waiting.Task, storage.Write, storage.Remove);
        var pending = store.LoadAsync(record.Owner, record.Scope, default);
        if (sameOwner) await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot(token: "reauthenticated")); else await auth.ClearAsync();
        waiting.SetResult(storage.Values.Values.Single());
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task 다른미확정번호와조건없는삭제는_기존보호기록을바꾸지않는다()
    {
        var auth = await Session(); var storage = new Storage(); var store = Store(auth, storage); var record = Record();
        await store.SaveAsync(record, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(record with { RequestId = Guid.NewGuid() }, default));
        await store.RemoveAsync(record.Owner, record.Scope, Guid.NewGuid(), default);
        Assert.Equal(record, await store.LoadAsync(record.Owner, record.Scope, default));
        await store.RemoveAsync(record.Owner, record.Scope, record.RequestId, default); Assert.Empty(storage.Values);
    }

    [Fact]
    public async Task 만료삭제는보호저장에도반영되며_후속진행갱신으로원문을되살리지않는다()
    {
        var auth = await Session(); var storage = new Storage(); var clock = new Clock(); var record = Record(clock.Now.UtcDateTime);
        var store = Store(auth, storage, clock); await store.SaveAsync(record, default);
        clock.Now = clock.Now.AddHours(71);
        await store.SaveAsync(record with { CreatedAtUtc = clock.Now.UtcDateTime, PayloadJson = "{\"progress\":true}" }, default);
        clock.Now = clock.Now.AddHours(1);
        Assert.Null((await store.LoadAsync(record.Owner, record.Scope, default))!.PayloadJson);
        Assert.Null(JsonSerializer.Deserialize<WorkflowRecoveryRecord>(storage.Values.Values.Single())!.PayloadJson);
        clock.Now = clock.Now.AddDays(-5); await store.SaveAsync(record, default);
        var reopened = Store(auth, storage, clock); var tombstone = await reopened.LoadAsync(record.Owner, record.Scope, default);
        Assert.Null(tombstone!.PayloadJson); Assert.Equal(record.RequestId, tombstone.RequestId); Assert.Equal(record.CreatedAtUtc, tombstone.CreatedAtUtc);
    }

    [Fact]
    public async Task 해시경로에다른계정원문이있어도_주소를복원하지않는다()
    {
        var auth = await Session(); var storage = new Storage(); var store = Store(auth, storage); var record = Record();
        await store.SaveAsync(record, default); var key = storage.Values.Keys.Single();
        storage.Values[key] = JsonSerializer.Serialize(record with { Owner = "foreign" });
        await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(record.Owner, record.Scope, default));
    }

    [Fact]
    public async Task 사진raw나실제보호저장실패는_성공한복구기록으로표시하지않는다()
    {
        var auth = await Session(); var storage = new Storage(); var store = Store(auth, storage);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(Record() with { PayloadJson = "{\"ImageBase64\":\"raw\"}" }, default));
        Assert.Empty(storage.Values);
        var failing = new MauiWorkflowRecoveryStore(auth, storage.Read, (_, _) => throw new IOException("device storage failure"), storage.Remove);
        await Assert.ThrowsAsync<IOException>(() => failing.SaveAsync(Record(), default)); Assert.Empty(storage.Values);
    }

    private static async Task<AuthSession> Session()
    {
        var auth = new AuthSession(new ShipperRecoveryTokenStore(), new ClientSessionGuard());
        await auth.ApplyAsync(ShipperRecoveryFixture.Snapshot()); return auth;
    }
    private static WorkflowRecoveryRecord Record(DateTime? created = null)
        => new("shipper-a", "test.v1", Guid.NewGuid(), "{\"address\":\"private\"}", created ?? DateTime.UtcNow, "source");
    private static MauiWorkflowRecoveryStore Store(AuthSession auth, Storage storage, TimeProvider? time = null)
        => new(auth, storage.Read, storage.Write, storage.Remove, time);
    private sealed class Storage
    {
        public Dictionary<string, string> Values { get; } = [];
        public Task<string?> Read(string key) => Task.FromResult(Values.GetValueOrDefault(key));
        public Task Write(string key, string value) { Values[key] = value; return Task.CompletedTask; }
        public void Remove(string key) => Values.Remove(key);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Access : IRoleWorkspaceAccess
    {
        public RoleWorkspaceIdentity Identity { get; set; } = new("driver-a", 1, true);
        public event Action? Changed { add { } remove { } }
        public RoleWorkspaceIdentity GetIdentity(string roleKey) => roleKey == "cargo-driver" ? Identity : new(null, 0, false);
        public Task EnsureInitializedAsync(string roleKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SignInAsync(string roleKey, string name, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SignOutAsync(string roleKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
