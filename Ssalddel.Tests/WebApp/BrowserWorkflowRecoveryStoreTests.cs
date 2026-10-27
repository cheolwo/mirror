using System.Text.Json;
using Microsoft.JSInterop;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.WebApp.Services;

namespace Ssalddel.Tests.WebApp;

/// <summary>브라우저 어댑터의 계정·서버·늦은 IO 계약. 실제 IndexedDB·암호화 검증과 구분합니다.</summary>
public sealed class BrowserWorkflowRecoveryStoreTests
{
    [Fact]
    public async Task 새어댑터는동일계정과서버의번호를복원하고_다른요청을덮지않는다()
    {
        var context = new Context(); var js = new Browser(); var identity = context.Capture();
        var scope = identity.Scope("web.shipper.receipt.target");
        var record = new WorkflowRecoveryRecord(identity.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow, "target-a");
        await new BrowserWorkflowRecoveryStore(js, context).SaveAsync(record, default);
        var restarted = new BrowserWorkflowRecoveryStore(js, context);
        Assert.Equal(record, await restarted.LoadAsync(identity.Owner, scope, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.SaveAsync(record with { RequestId = Guid.NewGuid() }, default));
        await restarted.RemoveAsync(identity.Owner, scope, Guid.NewGuid(), default);
        Assert.Equal(record, await restarted.LoadAsync(identity.Owner, scope, default));
        await restarted.RemoveAsync(identity.Owner, scope, record.RequestId, default);
        Assert.Null(await restarted.LoadAsync(identity.Owner, scope, default));
        Assert.All(js.Keys, key => { Assert.DoesNotContain(identity.Owner, key); Assert.DoesNotContain("target-a", key); });
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("endpoint")]
    [InlineData("revision")]
    [InlineData("logout")]
    public async Task 늦은IO사이계정서버판본전환은다른화면에기록을적용하지않는다(string change)
    {
        var context = new Context(); var js = new Browser(); var identity = context.Capture();
        var scope = identity.Scope("web.shipper.receipt.target");
        var record = new WorkflowRecoveryRecord(identity.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow, "target-a");
        var store = new BrowserWorkflowRecoveryStore(js, context); await store.SaveAsync(record, default);
        js.AfterRead = () =>
        {
            if (change == "owner") context.Owner = "another";
            else if (change == "endpoint") context.Endpoint = new string('b', 32);
            else if (change == "revision") context.Revision++;
            else context.Authenticated = false;
        };
        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadAsync(identity.Owner, scope, default));
        Assert.Single(js.Records);
    }

    [Fact]
    public async Task 보호IO실패는빈기록으로숨기지않고_서버없는기존업무는영속화하지않는다()
    {
        var context = new Context(); var js = new Browser(); var store = new BrowserWorkflowRecoveryStore(js, context);
        var identity = context.Capture(); var scope = identity.Scope("web.shipper.receipt.target");
        var record = new WorkflowRecoveryRecord(identity.Owner, scope, Guid.NewGuid(), null, DateTime.UtcNow, "target-a");
        js.Fail = true;
        await Assert.ThrowsAsync<JSException>(() => store.SaveAsync(record, default));
        await Assert.ThrowsAsync<JSException>(() => store.LoadAsync(record.Owner, scope, default));
        var old = record with { Scope = "legacy.non-server.v1" };
        await store.SaveAsync(old, default); Assert.Equal(old, await store.LoadAsync(old.Owner, old.Scope, default));
        Assert.Null(await new BrowserWorkflowRecoveryStore(js, context).LoadAsync(old.Owner, old.Scope, default));
        Assert.Empty(js.Records);
    }

    private sealed class Context : IWorkflowRecoveryContext
    {
        public string Owner = "owner-a", Endpoint = new('a', 32);
        public long Revision = 1; public bool Authenticated = true;
        public WorkflowRecoverySnapshot Capture() => Authenticated ? new(Owner, Revision, Endpoint) : throw new InvalidOperationException();
        public bool IsCurrent(WorkflowRecoverySnapshot snapshot) => Authenticated && snapshot == Capture();
    }
    private sealed class Browser : IJSRuntime, IJSObjectReference
    {
        public Dictionary<string, string> Records { get; } = [];
        public List<string> Keys { get; } = [];
        public Action? AfterRead; public bool Fail;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, default, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args)
        {
            ct.ThrowIfCancellationRequested(); if (Fail) throw new JSException("protected browser unavailable");
            if (identifier == "import") return ValueTask.FromResult((TValue)(object)this);
            var key = (string)args![0]!; Keys.Add(key); string? result = null;
            if (identifier == "read") { result = Records.GetValueOrDefault(key); AfterRead?.Invoke(); }
            else if (identifier == "save")
            {
                var candidate = (string)args[1]!;
                var value = JsonSerializer.Deserialize<WorkflowRecoveryRecord>(candidate, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                if (Records.TryGetValue(key, out var oldJson)
                    && JsonSerializer.Deserialize<WorkflowRecoveryRecord>(oldJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.RequestId != value.RequestId)
                    throw new InvalidOperationException();
                Records[key] = candidate; result = candidate;
            }
            else if (identifier == "remove")
            {
                if (Records.TryGetValue(key, out var oldJson)
                    && JsonSerializer.Deserialize<WorkflowRecoveryRecord>(oldJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.RequestId.ToString() == (string)args[1]!)
                    Records.Remove(key);
            }
            else throw new NotSupportedException(identifier);
            return ValueTask.FromResult(result is null ? default! : (TValue)(object)result);
        }
    }
}
