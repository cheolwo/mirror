using System.Text.Json;
using Microsoft.Extensions.Options;
using Ssalddel.Services.PrivacySupport;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 보호지원파기Tests
{
    [Fact]
    public async Task 정책확인이없으면폐쇄삼년을지났어도파기를실행하지않는다()
    {
        var store = new Store(); var options = new 보호지원Options { ClosedDisputePurgeEnabled = true };
        var service = new 보호지원파기Service(store, Options.Create(options), TimeProvider.System);
        Assert.Equal(0, await service.실행Async());
        Assert.False(store.Deleted);
        Assert.NotNull(store.Row);
    }

    [Fact]
    public async Task 보존정지와재개된사건은파기하지않고현재폐쇄사건만CAS로삭제한다()
    {
        var store = new Store(); var service = Service(store);
        store.Row!.SourceHoldActive = true;
        Assert.Equal(0, await service.실행Async());
        store.Row.SourceHoldActive = false; store.Row.StatusCode = "reopened";
        Assert.Equal(0, await service.실행Async());
        store.Row.StatusCode = "closed";
        Assert.Equal(1, await service.실행Async());
        Assert.True(store.Deleted);
        Assert.Null(store.Row);
    }

    [Fact]
    public async Task 파기쓰기경쟁에서재개판본이먼저저장되면삭제하지않는다()
    {
        var store = new Store { ConflictOnce = true };
        Assert.Equal(0, await Service(store).실행Async());
        Assert.False(store.Deleted);
        Assert.Equal("reopened", store.Row!.StatusCode);
    }

    [Fact]
    public async Task 파기Claim뒤일시실패는복구시같은상태를이어받아삭제한다()
    {
        var store = new Store { FailDeleteOnce = true }; var service = Service(store);
        await Assert.ThrowsAsync<IOException>(() => service.실행Async());
        Assert.Equal("claimed", store.Row!.PurgeStateCode);
        Assert.Equal(1, await service.실행Async());
        Assert.True(store.Deleted);
    }

    [Theory]
    [InlineData("privacy-rights")]
    [InlineData("privacy-incident")]
    public async Task 미확정권리사고보존정책에분쟁삼년기준을자동적용하지않는다(string kind)
    {
        var store = new Store(); store.Row!.Kind = kind;
        Assert.Equal(0, await Service(store).실행Async());
        Assert.NotNull(store.Row);
    }

    private static 보호지원파기Service Service(Store store) => new(store, Options.Create(new 보호지원Options
    {
        ClosedDisputePurgeEnabled = true, ClosedDisputeRetentionPolicyConfirmed = true,
        ClosedDisputeRetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion
    }), TimeProvider.System);

    private sealed class Store : I보호지원Store
    {
        public 보호지원Record? Row { get; set; } = new()
        {
            CaseId = "test-expired", Kind = "transaction-dispute", StatusCode = "closed", Revision = 10,
            ClosedAtUtc = DateTime.UtcNow.AddYears(-4), PurgeAfterAtUtc = DateTime.UtcNow.AddYears(-1),
            RetentionPolicyVersion = 보호지원파기Service.DisputeRetentionPolicyVersion
        };
        public bool ConflictOnce { get; set; }
        public bool FailDeleteOnce { get; set; }
        public bool Deleted { get; set; }
        public Task<보호지원Record?> 조회Async(string id, CancellationToken ct = default) => Task.FromResult(Row is null ? null : Clone(Row));
        public Task<bool> 생성Async(보호지원Record row, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(보호지원Record row, long expected, CancellationToken ct = default)
        {
            if (ConflictOnce)
            {
                ConflictOnce = false; Row!.Revision++; Row.StatusCode = "reopened"; Row.PurgeAfterAtUtc = null;
                return Task.FromResult(false);
            }
            if (Row?.Revision != expected) return Task.FromResult(false);
            Row = Clone(row); return Task.FromResult(true);
        }
        public Task<IReadOnlyList<보호지원Record>> 목록Async(string? user, string? kind, int skip, int take, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<보호지원Record>> 파기후보Async(DateTime now, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>(Row is { } row ? [Clone(row)] : []);
        public Task<IReadOnlyList<보호지원Record>> 보존조정후보Async(int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<보호지원Record>>(Row is { RetentionIntent: not null } row ? [Clone(row)] : []);
        public Task<bool> 파기Async(보호지원Record claimed, DateTime now, CancellationToken ct = default)
        {
            if (FailDeleteOnce) { FailDeleteOnce = false; throw new IOException("temporary storage interruption"); }
            if (Row?.Revision != claimed.Revision || claimed.PurgeStateCode != "claimed") return Task.FromResult(false);
            Deleted = true; Row = null; return Task.FromResult(true);
        }
        private static 보호지원Record Clone(보호지원Record row) => JsonSerializer.Deserialize<보호지원Record>(JsonSerializer.Serialize(row))!;
    }
}
