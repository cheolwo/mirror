using SellerApp.Services;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Contracts.Common.Orderer;
using Ssalddel.Ui.Common.Areas.App.Services;

namespace Ssalddel.Tests.Ui.Seller;

public sealed class 주문자수요ReadSessionTests
{
    [Fact]
    public async Task 두주문조회는먼저시작하고_응답은개별공동집단수출순서로반영한다()
    {
        await using var fixture = new Fixture();
        var individual = Pending<개별주문관점페이지응답>();
        var group = Pending<공동주문관점페이지응답>();
        var clustered = Pending<IReadOnlyList<공동구매자동집단응답>>();
        var clusterStarted = Pending<bool>();
        fixture.Individual = _ => individual.Task;
        fixture.Group = _ => group.Task;
        fixture.Clustered = () => { clusterStarted.SetResult(true); return clustered.Task; };
        var loading = fixture.LoadAsync();
        Assert.Equal(new[] { "individual:", "group:" }, fixture.Calls);
        Assert.True(fixture.Session.Loading);
        group.SetResult(fixture.NewGroup);
        Assert.Equal(0, fixture.Session.GroupOrders.TotalCount);
        individual.SetResult(fixture.NewIndividual);
        await clusterStarted.Task;
        Assert.Same(fixture.NewIndividual, fixture.Session.IndividualOrders);
        Assert.Same(fixture.NewGroup, fixture.Session.GroupOrders);
        Assert.Equal(new[] { "individual:", "group:", "cluster" }, fixture.Calls);
        clustered.SetResult(fixture.NewClustered);
        await loading;

        Assert.Same(fixture.NewClustered, fixture.Session.ClusteredDemands);
        Assert.Same(fixture.NewExport, fixture.Session.ExportLedgers);
        Assert.Equal(new[] { "individual:", "group:", "cluster", "export:" }, fixture.Calls);
        Assert.False(fixture.Session.Loading);
    }

    [Theory]
    [InlineData("individual", true)]
    [InlineData("individual", false)]
    [InlineData("group", true)]
    [InlineData("group", false)]
    [InlineData("cluster", true)]
    [InlineData("cluster", false)]
    [InlineData("export", true)]
    [InlineData("export", false)]
    public async Task 일반실패는_호출시점과await시점의_현재부분반영만남긴다(string stage, bool synchronous)
    {
        await using var fixture = new Fixture();
        await fixture.LoadAsync();
        var oldIndividual = fixture.Session.IndividualOrders;
        var oldGroup = fixture.Session.GroupOrders;
        var oldCluster = fixture.Session.ClusteredDemands;
        var oldExport = fixture.Session.ExportLedgers;
        fixture.NewResponses();
        fixture.Calls.Clear();
        fixture.Fail(stage, new InvalidOperationException("failed at " + stage), synchronous);

        await fixture.LoadAsync();

        Assert.Equal("failed at " + stage, fixture.Session.ErrorMessage);
        Assert.Same(stage != "individual" && !(stage == "group" && synchronous)
            ? fixture.NewIndividual : oldIndividual, fixture.Session.IndividualOrders);
        Assert.Same(stage is "cluster" or "export" ? fixture.NewGroup : oldGroup, fixture.Session.GroupOrders);
        Assert.Same(stage == "export" ? fixture.NewClustered : oldCluster, fixture.Session.ClusteredDemands);
        Assert.Same(oldExport, fixture.Session.ExportLedgers);
        Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
        Assert.Null(fixture.Session.ExportUnavailableMessage);
        Assert.Equal(stage switch { "individual" when synchronous => 1, "individual" or "group" => 2, "cluster" => 3, _ => 4 }, fixture.Calls.Count);
        Assert.False(fixture.Session.Loading);
    }

    [Theory]
    [InlineData("cluster", 403)]
    [InlineData("cluster", 404)]
    [InlineData("cluster", 401)]
    [InlineData("cluster", 500)]
    [InlineData("export", 403)]
    [InlineData("export", 404)]
    [InlineData("export", 401)]
    [InlineData("export", 500)]
    public async Task 집단수요와수출의_정확한403과404만_기능미활성으로계속진행한다(string stage, int status)
    {
        await using var fixture = new Fixture();
        fixture.Fail(stage, new SsalddelApiException("api failed", status, "test", "", null), false);
        await fixture.LoadAsync();

        if (status is 403 or 404)
        {
            Assert.Null(fixture.Session.ErrorMessage);
            Assert.Equal(4, fixture.Calls.Count);
            if (stage == "cluster")
            {
                Assert.Empty(fixture.Session.ClusteredDemands);
                Assert.Equal("집단 수요는 1.0 같이 주문·주문자 집단화 기능이 활성화된 환경에서 확인할 수 있습니다.", fixture.Session.ClusteredDemandUnavailableMessage);
                Assert.Same(fixture.NewExport, fixture.Session.ExportLedgers);
            }
            else
            {
                Assert.Equal(0, fixture.Session.ExportLedgers.TotalCount);
                Assert.Equal("수출 판매는 1.5 무역 준비 기능과 판매자 권한이 활성화된 환경에서 확인할 수 있습니다.", fixture.Session.ExportUnavailableMessage);
            }
        }
        else
        {
            Assert.Equal("api failed", fixture.Session.ErrorMessage);
            Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
            Assert.Null(fixture.Session.ExportUnavailableMessage);
            Assert.Equal(stage == "cluster" ? 3 : 4, fixture.Calls.Count);
        }
    }

    [Fact]
    public async Task 개별주문의403과_다른예외형식은_기능미활성으로변환하지않는다()
    {
        await using var fixture = new Fixture();
        fixture.Fail("individual", new SsalddelApiException("individual forbidden", 403, "test", "", null), false);
        await fixture.LoadAsync();
        Assert.Equal("individual forbidden", fixture.Session.ErrorMessage);
        Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
        fixture.Individual = _ => Task.FromResult(fixture.NewIndividual);
        fixture.Fail("cluster", new HttpRequestException("http forbidden", null, System.Net.HttpStatusCode.Forbidden), false);
        await fixture.LoadAsync();
        Assert.Equal("http forbidden", fixture.Session.ErrorMessage);
        Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
    }

    [Fact]
    public async Task 검색은_조회시작시한번만읽고_성공한재조회는이전오류와안내를지운다()
    {
        await using var fixture = new Fixture();
        fixture.Fail("cluster", new SsalddelApiException("off", 404, "test", "", null), false);
        fixture.Fail("export", new InvalidOperationException("export failed"), false);
        await fixture.LoadAsync();
        Assert.NotNull(fixture.Session.ClusteredDemandUnavailableMessage);
        Assert.NotNull(fixture.Session.ErrorMessage);
        var individual = Pending<개별주문관점페이지응답>();
        fixture.Individual = _ => individual.Task;
        fixture.Clustered = () => Task.FromResult(fixture.NewClustered);
        fixture.Export = _ => Task.FromResult(fixture.NewExport);
        fixture.Calls.Clear();
        fixture.Search = "first";
        var reads = fixture.SearchReads;
        var loading = fixture.LoadAsync();
        Assert.Null(fixture.Session.ErrorMessage);
        Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
        fixture.Search = "after";
        individual.SetResult(fixture.NewIndividual);
        await loading;

        Assert.Equal(new[] { "individual:first", "group:first", "cluster", "export:first" }, fixture.Calls);
        Assert.Equal(reads + 1, fixture.SearchReads);
        Assert.Null(fixture.Session.ExportUnavailableMessage);
    }

    [Fact]
    public async Task 두번째조회가먼저완료되면_첫늦은응답과후속조회는버린다()
    {
        await using var fixture = new Fixture();
        var pending = Pending<개별주문관점페이지응답>();
        var second = new 개별주문관점페이지응답 { TotalCount = 12 };
        var calls = 0;
        fixture.Individual = _ => ++calls == 1 ? pending.Task : Task.FromResult(second);
        var first = fixture.LoadAsync();
        await fixture.LoadAsync();
        Assert.Equal(2, calls);
        Assert.Same(second, fixture.Session.IndividualOrders);
        Assert.False(fixture.Session.Loading);
        pending.SetResult(fixture.NewIndividual);
        await first;
        Assert.Same(second, fixture.Session.IndividualOrders);
        Assert.Equal(6, fixture.Calls.Count);
        Assert.False(fixture.Session.Loading);
    }

    [Fact]
    public async Task 이전조회가먼저끝나도_현재조회가끝날때까지loading을유지한다()
    {
        await using var fixture = new Fixture();
        var previous = Pending<개별주문관점페이지응답>();
        var current = Pending<개별주문관점페이지응답>();
        var calls = 0;
        fixture.Individual = _ => ++calls == 1 ? previous.Task : current.Task;
        var first = fixture.LoadAsync();
        var second = fixture.LoadAsync();
        previous.SetResult(fixture.NewIndividual);
        await first;
        Assert.True(fixture.Session.Loading);
        Assert.Equal(0, fixture.Session.IndividualOrders.TotalCount);
        current.SetResult(fixture.NewIndividual);
        await second;
        Assert.False(fixture.Session.Loading);
        Assert.Same(fixture.NewIndividual, fixture.Session.IndividualOrders);
    }

    [Theory]
    [InlineData("individual", 500)]
    [InlineData("group", 500)]
    [InlineData("cluster", 403)]
    [InlineData("cluster", 404)]
    [InlineData("cluster", 500)]
    [InlineData("export", 403)]
    [InlineData("export", 404)]
    [InlineData("export", 500)]
    public async Task 이전조회가늦게실패해도_현재응답과오류안내를바꾸지않는다(string stage, int status)
    {
        await using var fixture = new Fixture();
        var release = Pending<bool>();
        fixture.Delay(stage, release.Task, new SsalddelApiException("old failure", status, "test", "", null));
        var previous = fixture.LoadAsync();
        fixture.NewResponses();
        fixture.UseImmediateResponses();
        await fixture.LoadAsync();
        release.SetResult(true);
        await previous;

        Assert.Same(fixture.NewIndividual, fixture.Session.IndividualOrders);
        Assert.Same(fixture.NewGroup, fixture.Session.GroupOrders);
        Assert.Same(fixture.NewClustered, fixture.Session.ClusteredDemands);
        Assert.Same(fixture.NewExport, fixture.Session.ExportLedgers);
        Assert.Null(fixture.Session.ErrorMessage);
        Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
        Assert.Null(fixture.Session.ExportUnavailableMessage);
        Assert.False(fixture.Session.Loading);
    }

    [Theory]
    [InlineData("individual")]
    [InlineData("group")]
    [InlineData("cluster")]
    [InlineData("export")]
    public async Task 이전조회가각단계에서늦게완료돼도_현재결과를덮어쓰지않는다(string stage)
    {
        await using var fixture = new Fixture();
        var release = Pending<bool>();
        fixture.Delay(stage, release.Task);
        var previous = fixture.LoadAsync();
        fixture.NewResponses();
        fixture.UseImmediateResponses();
        await fixture.LoadAsync();
        release.SetResult(true);
        await previous;

        Assert.Same(fixture.NewIndividual, fixture.Session.IndividualOrders);
        Assert.Same(fixture.NewGroup, fixture.Session.GroupOrders);
        Assert.Same(fixture.NewClustered, fixture.Session.ClusteredDemands);
        Assert.Same(fixture.NewExport, fixture.Session.ExportLedgers);
        Assert.False(fixture.Session.Loading);
    }

    [Fact]
    public async Task 폐기는_30초보완조회대기와후속조회를끝내고_늦은응답을반영하지않는다()
    {
        var fixture = new Fixture();
        var pending = Pending<개별주문관점페이지응답>();
        fixture.Individual = _ => pending.Task;
        var loading = fixture.LoadAsync();
        var dispatched = 0;
        var monitor = fixture.Session.MonitorAsync(action => { dispatched++; return action(); }, fixture.LoadAsync);
        var oldIndividual = fixture.Session.IndividualOrders;
        var oldExport = fixture.Session.ExportLedgers;
        await fixture.DisposeAsync();
        await monitor;
        Assert.Equal(0, dispatched);
        Assert.False(loading.IsCompleted);
        pending.SetResult(fixture.NewIndividual);
        await loading;
        Assert.Same(oldIndividual, fixture.Session.IndividualOrders);
        Assert.Same(oldExport, fixture.Session.ExportLedgers);
        Assert.Equal(new[] { "individual:", "group:" }, fixture.Calls);
        Assert.False(fixture.Session.Loading);
        await fixture.DisposeAsync();
        await fixture.LoadAsync();
        await fixture.Session.MonitorAsync(action => { dispatched++; return action(); }, fixture.LoadAsync);
        Assert.Equal(0, dispatched);
        Assert.Equal(2, fixture.Calls.Count);
    }

    [Theory]
    [InlineData("group", false)]
    [InlineData("cluster", false)]
    [InlineData("export", false)]
    [InlineData("individual", true)]
    [InlineData("cluster", true)]
    [InlineData("export", true)]
    public async Task 폐기이후에는_진행중단계의성공이나실패가상태를바꾸지않는다(string stage, bool failure)
    {
        var fixture = new Fixture();
        var release = Pending<bool>();
        fixture.Delay(stage, release.Task, failure ? new SsalddelApiException("late failure", 403, "test", "", null) : null);
        var loading = fixture.LoadAsync();
        await fixture.DisposeAsync();
        var individual = fixture.Session.IndividualOrders;
        var group = fixture.Session.GroupOrders;
        var clustered = fixture.Session.ClusteredDemands;
        var export = fixture.Session.ExportLedgers;
        var calls = fixture.Calls.Count;
        release.SetResult(true);
        await loading;

        Assert.Same(individual, fixture.Session.IndividualOrders);
        Assert.Same(group, fixture.Session.GroupOrders);
        Assert.Same(clustered, fixture.Session.ClusteredDemands);
        Assert.Same(export, fixture.Session.ExportLedgers);
        Assert.Equal(calls, fixture.Calls.Count);
        Assert.Null(fixture.Session.ErrorMessage);
        Assert.Null(fixture.Session.ClusteredDemandUnavailableMessage);
        Assert.Null(fixture.Session.ExportUnavailableMessage);
        Assert.False(fixture.Session.Loading);
    }

    private static TaskCompletionSource<T> Pending<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class Fixture : IAsyncDisposable
    {
        public 주문자수요ReadSession Session { get; } = new();
        public List<string> Calls { get; } = [];
        public string Search { get; set; } = "";
        public int SearchReads { get; private set; }
        public 개별주문관점페이지응답 NewIndividual { get; private set; } = new() { TotalCount = 1 };
        public 공동주문관점페이지응답 NewGroup { get; private set; } = new() { TotalCount = 2 };
        public IReadOnlyList<공동구매자동집단응답> NewClustered { get; private set; } = [new() { 자동집단Id = "cluster" }];
        public 판매자수출원장목록응답 NewExport { get; private set; } = new() { TotalCount = 3 };
        public Func<string, Task<개별주문관점페이지응답>> Individual { get; set; }
        public Func<string, Task<공동주문관점페이지응답>> Group { get; set; }
        public Func<Task<IReadOnlyList<공동구매자동집단응답>>> Clustered { get; set; }
        public Func<string, Task<판매자수출원장목록응답>> Export { get; set; }

        public Fixture() => UseImmediateResponses();

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Individual), nameof(Group), nameof(Clustered), nameof(Export))]
        public void UseImmediateResponses()
        {
            Individual = _ => Task.FromResult(NewIndividual);
            Group = _ => Task.FromResult(NewGroup);
            Clustered = () => Task.FromResult(NewClustered);
            Export = _ => Task.FromResult(NewExport);
        }

        public void Delay(string stage, Task release, Exception? error = null)
        {
            async Task<T> Delayed<T>(T response)
            {
                await release;
                if (error is not null) throw error;
                return response;
            }

            switch (stage)
            {
                case "individual": Individual = _ => Delayed(NewIndividual); break;
                case "group": Group = _ => Delayed(NewGroup); break;
                case "cluster": Clustered = () => Delayed(NewClustered); break;
                case "export": Export = _ => Delayed(NewExport); break;
                default: throw new ArgumentOutOfRangeException(nameof(stage));
            }
        }

        public Task LoadAsync() => Session.LoadAsync(
            search => { Calls.Add("individual:" + search); return Individual(search); },
            search => { Calls.Add("group:" + search); return Group(search); },
            () => { Calls.Add("cluster"); return Clustered(); },
            search => { Calls.Add("export:" + search); return Export(search); },
            () => { SearchReads++; return Search; });

        public void Fail(string stage, Exception error, bool synchronous)
        {
            Task<T> Failure<T>() => synchronous ? throw error : Task.FromException<T>(error);
            switch (stage)
            {
                case "individual": Individual = _ => Failure<개별주문관점페이지응답>(); break;
                case "group": Group = _ => Failure<공동주문관점페이지응답>(); break;
                case "cluster": Clustered = () => Failure<IReadOnlyList<공동구매자동집단응답>>(); break;
                case "export": Export = _ => Failure<판매자수출원장목록응답>(); break;
            }
        }

        public void NewResponses()
        {
            NewIndividual = new() { TotalCount = 11 };
            NewGroup = new() { TotalCount = 22 };
            NewClustered = [new() { 자동집단Id = "new-cluster" }];
            NewExport = new() { TotalCount = 33 };
        }

        public ValueTask DisposeAsync() => Session.DisposeAsync();
    }
}
