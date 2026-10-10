using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.App.Services;
using Ssalddel.Ui.Common.Areas.App.ViewModels;

namespace Ssalddel.Tests.Ui.Common;

public sealed class OrdererGroupImportReadinessStateTests
{
    [Fact]
    public async Task 이전선택의늦은성공은_새원장을덮지않는다()
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var first = state.LoadAsync("a", "group-a"); var second = state.LoadAsync("b", "group-b");
        client.Complete(1, "B"); await second; client.Complete(0, "A"); await first;
        Assert.Equal("B", state.Readiness!.상품명); Assert.Null(state.Error); Assert.False(state.IsLoading);
        Assert.Equal(("a", "group-a"), client.Calls[0]); Assert.Equal(("b", "group-b"), client.Calls[1]);
    }
    [Fact]
    public async Task 이전선택의늦은오류는_현재원장오류가아니다()
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var first = state.LoadAsync("a", "group"); var second = state.LoadAsync("b", "group");
        client.Complete(1, "B"); await second; client.Fail(0); await first;
        Assert.Equal("B", state.Readiness!.상품명); Assert.Null(state.Error); Assert.Null(state.BackgroundWarning);
    }
    [Fact]
    public async Task 이전오류가먼저돌아와도_새원장의읽기중상태를종료하지않는다()
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var first = state.LoadAsync("a", "group"); var second = state.LoadAsync("b", "group");
        client.Fail(0); await first; Assert.True(state.IsLoading); Assert.Null(state.Error);
        client.Complete(1, "B"); await second; Assert.False(state.IsLoading);
    }
    [Fact]
    public async Task 같은원장재조회실패는_확인한내용과시각을유지한다()
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var first = state.LoadAsync("a", "group"); client.Complete(0, "A"); await first;
        var confirmedAt = state.LastLoadedAtUtc;
        var refresh = state.LoadAsync("a", "group", force: true, preserveCurrentContent: true);
        Assert.False(state.IsLoading); client.Fail(1); await refresh;
        Assert.Equal("A", state.Readiness!.상품명); Assert.Equal(confirmedAt, state.LastLoadedAtUtc);
        Assert.Contains("이전에 확인한 내용을 유지합니다", state.BackgroundWarning); Assert.Null(state.Error);
    }
    [Fact]
    public async Task 보존옵션이있어도_다른집단에는이전내용을남기지않는다()
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var first = state.LoadAsync("a", "group-a"); client.Complete(0, "A"); await first;
        var next = state.LoadAsync("a", "group-b", force: true, preserveCurrentContent: true);
        Assert.Null(state.Readiness); Assert.Null(state.LastLoadedAtUtc); Assert.True(state.IsLoading);
        client.Fail(1); await next; Assert.NotNull(state.Error); Assert.Null(state.BackgroundWarning);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task 종료후늦은응답은_자료와오류를복구하지않는다(bool success)
    {
        var client = new Client(); var state = new OrdererGroupImportReadinessState(client);
        var pending = state.LoadAsync("a", "group"); state.Dispose();
        if (success) client.Complete(0, "A"); else client.Fail(0);
        await pending; await state.LoadAsync("b", "group");
        Assert.Null(state.Readiness); Assert.Null(state.Error); Assert.False(state.IsLoading); Assert.Single(client.Calls);
    }
    [Fact]
    public async Task 빈집단선택은_이전읽기를무효화하고로딩을종료한다()
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var pending = state.LoadAsync("a", "group"); await state.LoadAsync("a", null);
        Assert.False(state.IsLoading); client.Complete(0, "A"); await pending; Assert.Null(state.Readiness);
    }
    [Theory]
    [InlineData(401)] [InlineData(403)]
    public async Task 권한거절은_이전확인자료도숨긴다(int status)
    {
        var client = new Client(); using var state = new OrdererGroupImportReadinessState(client);
        var first = state.LoadAsync("a", "group"); client.Complete(0, "A"); await first;
        var refresh = state.LoadAsync("a", "group", force: true, preserveCurrentContent: true);
        client.Fail(1, new SsalddelApiException("denied", status, "read", "", null)); await refresh;
        Assert.Null(state.Readiness); Assert.Null(state.LastLoadedAtUtc); Assert.Null(state.BackgroundWarning);
        Assert.NotNull(state.Error); Assert.False(state.IsLoading);
    }
    [Fact]
    public async Task 같은원장도_계정변경후이전응답을버리고_새계정으로조회한다()
    {
        string? owner = "a"; var client = new Client(); using var state = new OrdererGroupImportReadinessState(client, () => owner);
        var old = state.LoadAsync("ledger", "group"); owner = "b";
        client.Complete(0, "A"); await old; Assert.Null(state.Readiness); Assert.False(state.IsLoading);
        var next = state.LoadAsync("ledger", "group"); client.Complete(1, "B"); await next;
        Assert.Equal("B", state.Readiness!.상품명);
        owner = null; state.SynchronizeOwner(); await state.LoadAsync("ledger", "group");
        Assert.Null(state.Readiness); Assert.Equal(2, client.Calls.Count);
    }
    private sealed class Client : I같이수입준비주문자Client
    {
        internal readonly List<(string, string)> Calls = [];
        private readonly List<TaskCompletionSource<같이수입준비주문자조회응답?>> results = [];
        public Task<같이수입준비주문자조회응답?> 조회Async(string ledger, string group, CancellationToken ct)
        { Calls.Add((ledger, group)); var result = new TaskCompletionSource<같이수입준비주문자조회응답?>(TaskCreationOptions.RunContinuationsAsynchronously); results.Add(result); return result.Task; }
        internal void Complete(int index, string name) => results[index].SetResult(new() { 상품명 = name });
        internal void Fail(int index, Exception? exception = null) => results[index].SetException(exception ?? new HttpRequestException("offline"));
    }
}
