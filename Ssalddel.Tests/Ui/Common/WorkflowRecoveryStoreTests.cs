using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.RoleWorkspace.Core;

namespace Ssalddel.Tests.Ui.Common;

public sealed class WorkflowRecoveryStoreTests
{
    [Fact]
    public async Task 계정과업무별미확정요청은_다른번호로덮어쓰거나제거하지않는다()
    {
        var store = new VolatileWorkflowRecoveryStore(); var record = Record(); await store.SaveAsync(record, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(record with { RequestId = Guid.NewGuid() }, default));
        await store.RemoveAsync(record.Owner, record.Scope, Guid.NewGuid(), default);
        Assert.Equal(record, await store.LoadAsync(record.Owner, record.Scope, default));
        Assert.Null(await store.LoadAsync("another-owner", record.Scope, default));
        Assert.Null(await store.LoadAsync(record.Owner, "another-scope", default));
    }

    [Fact]
    public async Task 진행정보갱신은최초보관시각을연장하지않고_만료후원문을되살리지않는다()
    {
        var clock = new RecoveryClock(); var store = new VolatileWorkflowRecoveryStore(clock); var record = Record(clock.Now.UtcDateTime);
        await store.SaveAsync(record, default); clock.Now = clock.Now.AddHours(71);
        await store.SaveAsync(record with { CreatedAtUtc = clock.Now.UtcDateTime, PayloadJson = "{\"progress\":true}" }, default);
        Assert.Equal(record.CreatedAtUtc, (await store.LoadAsync(record.Owner, record.Scope, default))!.CreatedAtUtc);
        clock.Now = clock.Now.AddHours(1); Assert.Null((await store.LoadAsync(record.Owner, record.Scope, default))!.PayloadJson);
        clock.Now = clock.Now.AddDays(-5);
        await store.SaveAsync(record with { CreatedAtUtc = clock.Now.UtcDateTime }, default);
        Assert.Null((await store.LoadAsync(record.Owner, record.Scope, default))!.PayloadJson);
        Assert.Equal(record.RequestId, (await store.LoadAsync(record.Owner, record.Scope, default))!.RequestId);
    }

    [Theory]
    [InlineData("{\"ImageBase64\":\"secret\"}")]
    [InlineData("{\"photo\":{\"Bytes\":[1,2,3]}}")]
    [InlineData("{\"Bytes\":\"raw\"}")]
    public async Task 사진원본은_중첩된복구원문에도저장할수없다(string payload)
    {
        var store = new VolatileWorkflowRecoveryStore(); var record = Record() with { PayloadJson = payload };
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(record, default));
        Assert.Null(await store.LoadAsync(record.Owner, record.Scope, default));
    }

    [Fact]
    public async Task 보호저장은_분산역할scope의실제계정에결속하고새인스턴스도복원한다()
    {
        var storage = new Dictionary<string, string>(); var scopes = new List<string>();
        var owner = new RoleWorkspaceIdentity("owner", 1, true);
        ProtectedWorkflowRecoveryStore Store() => new(scope => { scopes.Add(scope); return owner; },
            key => Task.FromResult(storage.GetValueOrDefault(key)), (key, value) => { storage[key] = value; return Task.CompletedTask; }, key => storage.Remove(key));
        var record = Record() with { Scope = "distributed.cargo-driver.round.v1" };
        await Store().SaveAsync(record, default); Assert.Equal(record, await Store().LoadAsync(record.Owner, record.Scope, default));
        Assert.All(scopes, scope => Assert.Equal(record.Scope, scope));
        owner = owner with { OwnerId = "other", Revision = 2 };
        await Assert.ThrowsAsync<OperationCanceledException>(() => Store().LoadAsync(record.Owner, record.Scope, default));
        Assert.Null(await Store().LoadAsync("other", record.Scope, default)); Assert.Single(storage);
    }

    [Fact]
    public async Task 보호저장인스턴스가달라도_동시저장은미확정번호를덮지않는다()
    {
        var storage = new Dictionary<string, string>(); var record = Record();
        var release = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstRead = true;
        Task Write(string key, string value) { storage[key] = value; return Task.CompletedTask; }
        var first = new ProtectedWorkflowRecoveryStore(_ => new("owner", 1, true), key =>
        {
            if (!firstRead) return Task.FromResult(storage.GetValueOrDefault(key));
            firstRead = false; started.SetResult(); return release.Task;
        }, Write, key => storage.Remove(key));
        var second = new ProtectedWorkflowRecoveryStore(_ => new("owner", 1, true), key => Task.FromResult(storage.GetValueOrDefault(key)), Write, key => storage.Remove(key));
        var saving = first.SaveAsync(record, default); await started.Task;
        var conflicting = second.SaveAsync(record with { RequestId = Guid.NewGuid() }, default);
        release.SetResult(null); await saving;
        await Assert.ThrowsAsync<InvalidOperationException>(() => conflicting);
        Assert.Equal(record, await second.LoadAsync(record.Owner, record.Scope, default));
    }

    [Fact]
    public async Task 보호IO중동일계정판본변경은_저장완료응답으로승격하지않는다()
    {
        var identity = new RoleWorkspaceIdentity("owner", 1, true); var record = Record();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ProtectedWorkflowRecoveryStore(_ => identity, _ => Task.FromResult<string?>(null),
            async (_, _) => { started.SetResult(); await release.Task; }, _ => { });
        var saving = store.SaveAsync(record, default); await started.Task;
        identity = identity with { Revision = 2 }; release.SetResult();
        await Assert.ThrowsAsync<OperationCanceledException>(() => saving);
    }
    private static WorkflowRecoveryRecord Record(DateTime? created = null)
        => new("owner", "test.v1", Guid.NewGuid(), "{\"address\":\"private\"}", created ?? DateTime.UtcNow, "target");
}
