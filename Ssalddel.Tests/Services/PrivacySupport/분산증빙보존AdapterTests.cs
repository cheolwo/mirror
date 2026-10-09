using Ssalddel.Application.Warehouse;
using Ssalddel.Contracts.Common.PrivacyRetention;
using Ssalddel.Contracts.Common.PrivacySupport;
using Ssalddel.Services.PrivacyRetention;
using Ssalddel.Services.PrivacySupport;

namespace Ssalddel.Tests.Services.PrivacySupport;

public sealed class 분산증빙보존AdapterTests
{
    [Theory]
    [InlineData(보호지원출처Codes.분산입고, "lot-a")]
    [InlineData(보호지원출처Codes.분산배송, "order-a")]
    public async Task 분산증빙은기존음식파기원장으로매핑하지않고동일사건순번과당사자원천으로위임한다(string kind, string id)
    {
        var legacy = new Legacy(); var distributed = new Distributed(); var adapter = new 보호지원보존Adapter(legacy, distributed);
        var until = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
        await adapter.보존정지Async("case", kind, id, "basis", until);
        await adapter.보존정지해제Async("case", kind, id);
        Assert.Equal(until, await adapter.보존정지조회Async("case", kind, id));
        Assert.True(await adapter.보존정지해제확인Async("case", kind, id));
        await adapter.보존상태적용Async("case", kind, id, 7, true, "basis", until);
        Assert.Equal(new 보호지원보존적용(7, true, until), await adapter.보존적용조회Async("case", kind, id));
        Assert.Equal(new[] { "hold", "release", "read-hold", "read-release", "apply", "read-receipt" }, distributed.Calls.Select(x => x.Action));
        Assert.All(distributed.Calls, x => { Assert.Equal("case", x.Case); Assert.Equal(kind, x.Kind); Assert.Equal(id, x.Id); });
        Assert.Equal(0, legacy.Calls);
    }

    [Fact]
    public async Task 기존음식과생활배송매핑은보존하고미지원화물을분산증빙으로승격하지않는다()
    {
        var legacy = new Legacy(); var distributed = new Distributed(); var adapter = new 보호지원보존Adapter(legacy, distributed);
        await adapter.보존정지Async("case", 보호지원출처Codes.음식주문, "food", "basis", DateTime.UtcNow.AddDays(1));
        Assert.Equal("FoodOrder", legacy.LastSource);
        await adapter.보존정지Async("case", 보호지원출처Codes.생활배송, "delivery", "basis", DateTime.UtcNow.AddDays(1));
        Assert.Equal("NeighborhoodDelivery", legacy.LastSource);
        Assert.Equal("RetentionSourceUnsupported", (await Assert.ThrowsAsync<보호지원Exception>(() =>
            adapter.보존정지Async("case", 보호지원출처Codes.화물의뢰, "cargo", "basis", DateTime.UtcNow.AddDays(1)))).Code);
        Assert.Empty(distributed.Calls);
        Assert.Equal(2, legacy.Calls);
    }

    private sealed class Distributed : I분산사진보존연결
    {
        public Task AcquireOpenCaseAsync(string caseId, string sourceKind, string sourceId, CancellationToken ct = default)
            => throw new InvalidOperationException("Not expected in adapter routing tests.");
        public List<(string Action, string Case, string Kind, string Id)> Calls { get; } = [];
        private DateTime? until;
        public Task 보존정지Async(string c, string k, string i, string b, DateTime u, CancellationToken ct = default)
        { Calls.Add(("hold", c, k, i)); until = u; return Task.CompletedTask; }
        public Task 보존정지해제Async(string c, string k, string i, CancellationToken ct = default)
        { Calls.Add(("release", c, k, i)); return Task.CompletedTask; }
        public Task<DateTime?> 보존정지조회Async(string c, string k, string i, CancellationToken ct = default)
        { Calls.Add(("read-hold", c, k, i)); return Task.FromResult(until); }
        public Task<bool> 보존정지해제확인Async(string c, string k, string i, CancellationToken ct = default)
        { Calls.Add(("read-release", c, k, i)); return Task.FromResult(true); }
        public Task 보존상태적용Async(string c, string k, string i, long sequence, bool hold, string basis, DateTime? u, CancellationToken ct = default)
        { Assert.Equal(7, sequence); Assert.True(hold); Assert.Equal("basis", basis); Assert.Equal(until, u); Calls.Add(("apply", c, k, i)); return Task.CompletedTask; }
        public Task<보호지원보존적용?> 보존적용조회Async(string c, string k, string i, CancellationToken ct = default)
        { Calls.Add(("read-receipt", c, k, i)); return Task.FromResult<보호지원보존적용?>(new(7, true, until)); }
    }
    private sealed class Legacy : I개인정보보존조정Service
    {
        public int Calls { get; private set; }
        public string LastSource { get; private set; } = "";
        public Task 보존정지Async(string sourceCode, string recordId, string caseId, string reasonCode, DateTime reviewAtUtc, CancellationToken ct = default)
        { Calls++; LastSource = sourceCode; return Task.CompletedTask; }
        public Task 보존정지해제Async(string sourceCode, string recordId, string caseId, CancellationToken ct = default) => throw new InvalidOperationException("Not expected.");
        public Task<개인정보파기결과Dto?> 결과Async(string sourceCode, string recordId, string ownerUserId, CancellationToken ct = default) => throw new InvalidOperationException("Not expected.");
        public Task<int> 실행Async(CancellationToken ct = default) => throw new InvalidOperationException("Not expected.");
    }
}
