using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영업무연결Tests
{
    [Fact]
    public void 초기모델과_생산단계는_확인하지않은_작업이나완료를_만들지않는다()
    {
        var empty = new 공간공동운영업무연결Model();
        Assert.False(empty.Available); Assert.Null(empty.Cycle); Assert.Empty(empty.Nodes); Assert.Empty(empty.Relations);
        var f = new Fixture(); var flow = f.Build();
        Assert.True(flow.Available); Assert.Equal(7, flow.Nodes.Count);
        var production = Assert.Single(flow.Nodes, node => node.Stage.Key == "production");
        Assert.Empty(production.Sources); Assert.Empty(production.Orders);
        Assert.Contains("실제 생산·입고 완료가 아니", production.Stage.Boundary);
        Assert.Contains("절차 안내", flow.Message);
    }

    [Fact]
    public void 주문은_공급절차와병렬로_출고에서만만나며_같은회차생산으로_자동순환하지않는다()
    {
        var stages = 공간공동운영업무연결Presentation.Stages;
        Assert.Empty(stages.Single(stage => stage.Key == "order").PreviousKeys);
        Assert.Equal(new[] { "outbound" }, stages.Single(stage => stage.Key == "storage").NextKeys);
        Assert.Equal(new[] { "storage", "order" }, stages.Single(stage => stage.Key == "outbound").PreviousKeys);
        Assert.Empty(stages.Single(stage => stage.Key == "settlement").NextKeys);
    }

    [Fact]
    public void 기본선택은_배열마지막이아닌_회차번호가가장큰_현재회차다()
    {
        var f = new Fixture(); var latest = new 공간공동운영통합회차Dto
        { NodeKey = "cycle:latest", CycleId = "latest", Number = 2, TermsRevision = 1, StatusCode = "prepared" };
        f.Snapshot.Cycles = [latest, f.Cycle];
        f.Detail.Cycles = [new() { CycleId = latest.CycleId, Number = 2, TermsRevision = 1, StatusCode = "prepared" }, f.Detail.Cycles[0]];
        Assert.Equal("latest", f.Build().Cycle!.CycleId);
    }

    [Theory]
    [InlineData("ledger")]
    [InlineData("revision")]
    [InlineData("mode")]
    public void 서로다른_원장이나판본이나모드의_자료를_합치지않는다(string mismatch)
    {
        var f = new Fixture(); f.Source("InboundPlan", "private-inbound");
        if (mismatch == "ledger") f.Snapshot.LedgerId = "another";
        if (mismatch == "revision") f.Snapshot.Revision++;
        if (mismatch == "mode") f.Snapshot.ExecutionMode = "Operational";
        var flow = f.Build();
        Assert.False(flow.Available); Assert.Null(flow.Cycle); Assert.Empty(flow.Nodes); Assert.Empty(flow.UnmappedSources);
        Assert.DoesNotContain("private-inbound", JsonSerializer.Serialize(flow));
    }

    [Fact]
    public void 없는회차나_메타데이터가다른회차는_빈업무로확정하지않는다()
    {
        var f = new Fixture();
        Assert.False(f.Build("missing-cycle").Available);
        f.Cycle.TermsRevision++;
        Assert.False(f.Build().Available);
        f.Cycle.TermsRevision--; f.Snapshot.Cycles = [f.Cycle, f.Cycle];
        Assert.False(f.Build().Available);
    }

    [Fact]
    public void 원천종류는_관련절차의_참고자료에만_연결하고_생산이나피킹완료로_전파하지않는다()
    {
        var f = new Fixture();
        var expected = new Dictionary<string, string>
        {
            ["InboundPlan"] = "inbound", ["DistributedSite"] = "storage", ["StorageAgreement"] = "storage",
            ["RootOrderLedger"] = "order", ["DistributedWave"] = "outbound", ["DistributedOrder"] = "delivery",
            ["MartOrder"] = "delivery", ["Settlement"] = "settlement"
        };
        foreach (var kind in expected.Keys) f.Source(kind, "reference-" + kind, completed: true);
        var flow = f.Build();
        foreach (var pair in expected)
            Assert.Single(flow.Nodes.Single(node => node.Stage.Key == pair.Value).Sources, source => source.KindCode == pair.Key);
        Assert.Empty(flow.Nodes.Single(node => node.Stage.Key == "production").Sources);
        Assert.Contains("개별 피킹·포장이 완료", flow.Nodes.Single(node => node.Stage.Key == "outbound").Stage.Boundary);
        Assert.Empty(flow.UnmappedSources);
    }

    [Fact]
    public void 중복원천은_한개로_보이되_확인됨과업무링크를_승계하지않는다()
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound", completed: true);
        f.Snapshot.SharedSources = [source, source];
        var flow = f.Build(); var item = Assert.Single(flow.Nodes.Single(node => node.Stage.Key == "inbound").Sources);
        Assert.True(flow.Available); Assert.False(item.Verified); Assert.False(item.Completed); Assert.True(item.RequiresAttention);
        Assert.Empty(item.Href); Assert.Equal("현재 자료 확인 필요", 공간공동운영업무연결Presentation.SourceStatus(item));
        Assert.True(source.Verified); Assert.True(source.Completed);
    }

    [Fact]
    public void 현재원천_누락이나회차별충돌은_정상완료로보이지않는다()
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound", completed: true);
        f.Snapshot.SharedSources = [];
        Assert.False(Assert.Single(f.Build().Nodes.Single(node => node.Stage.Key == "inbound").Sources).Verified);
        source.StatusCode = "source-context-conflict"; f.Snapshot.SharedSources = [source];
        var conflict = Assert.Single(f.Build().Nodes.Single(node => node.Stage.Key == "inbound").Sources);
        Assert.False(conflict.Verified); Assert.False(conflict.Completed); Assert.Empty(conflict.Href);
    }

    [Fact]
    public void 다른회차의_원천과주문과관계는_선택회차에_나오지않는다()
    {
        var f = new Fixture(); var own = f.Source("InboundPlan", "own-inbound");
        var secret = new 공간공동운영통합원천Dto { NodeKey = "source:InboundPlan:secret-inbound", KindCode = "InboundPlan",
            ReferenceId = "secret-inbound", Label = "private-label", Verified = true };
        f.Snapshot.SharedSources = [own, secret];
        f.Snapshot.Cycles = [f.Cycle, new() { CycleId = "other", NodeKey = "cycle:other", Number = 2, TermsRevision = 1,
            Orders = [new() { NodeKey = "order:secret-order", ProductName = "private-product" }], SharedSourceKeys = [secret.NodeKey] }];
        f.Detail.Cycles = [f.Detail.Cycles[0], new() { CycleId = "other", Number = 2, TermsRevision = 1 }];
        f.Snapshot.Relations = [new() { FromKey = "cycle:other", ToKey = secret.NodeKey, RelationCode = "cycle-source-reference" }];
        var flow = f.Build("cycle-one"); var json = JsonSerializer.Serialize(flow);
        Assert.True(flow.Available); Assert.DoesNotContain("secret-inbound", json); Assert.DoesNotContain("private-product", json);
        Assert.DoesNotContain("private-label", json); Assert.Empty(flow.Relations);
    }

    [Fact]
    public void 상세회차에없는원천키를_추가한경우_다른회차자료로_보충하지않는다()
    {
        var f = new Fixture(); f.Cycle.SharedSourceKeys = ["source:InboundPlan:other-cycle"];
        Assert.False(f.Build().Available);
    }

    [Fact]
    public void 모르는종류는_분류대기하며_원시이름과상태와주소를_제품문구로쓰지않는다()
    {
        var f = new Fixture(); var original = f.Source("FuturePrivateKind", "private-id", completed: true);
        original.Label = "raw-secret-label"; original.StatusCode = "raw-secret-status"; original.Href = "https://example.invalid/private";
        var flow = f.Build(); var item = Assert.Single(flow.UnmappedSources);
        Assert.Empty(flow.Nodes.SelectMany(node => node.Sources)); Assert.False(item.Verified); Assert.False(item.Completed); Assert.Empty(item.Href);
        Assert.Equal("분류 확인이 필요한 연결 자료", item.Label);
        Assert.Equal("현재 자료 확인 필요", 공간공동운영업무연결Presentation.SourceStatus(item));
        Assert.DoesNotContain("raw-secret", 공간공동운영업무연결Presentation.OrderStatus(original.StatusCode));
        Assert.DoesNotContain("raw-secret", 공간공동운영업무연결Presentation.RootStatus(original.StatusCode));
    }

    [Fact]
    public void 실제참고관계는_정확한_선택주문과원천의_검증된계보만_유지한다()
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound");
        var order = f.Order(); order.Children = [source];
        f.Snapshot.Relations =
        [
            new() { FromKey = order.NodeKey, ToKey = source.NodeKey, RelationCode = "source-lineage" },
            new() { FromKey = order.NodeKey, ToKey = source.NodeKey, RelationCode = "source-lineage" },
            new() { FromKey = "order:other", ToKey = source.NodeKey, RelationCode = "source-lineage" },
            new() { FromKey = order.NodeKey, ToKey = "source:InboundPlan:other", RelationCode = "source-lineage" },
            new() { FromKey = order.NodeKey, ToKey = source.NodeKey, RelationCode = "execute-next" }
        ];
        var flow = f.Build(); var relation = Assert.Single(flow.Relations);
        Assert.Equal(order.NodeKey, relation.FromKey); Assert.Equal(source.NodeKey, relation.ToKey);
        Assert.Equal("참고 자료 연결", 공간공동운영업무연결Presentation.RelationLabel(relation.RelationCode));
        order.RootReadStatusCode = "lineage-unverified";
        Assert.Empty(f.Build().Relations); Assert.Empty(f.Build().Cycle!.Orders.Single().Children);
    }

    [Fact]
    public void 주문조회가차단되면_이전주문목록을_비우고_절차안내만유지한다()
    {
        var f = new Fixture(); f.Order(); f.Snapshot.OrdersReadStatusCode = "feature-disabled";
        var flow = f.Build(); Assert.True(flow.Available); Assert.Empty(flow.Cycle!.Orders);
        Assert.Empty(flow.Nodes.SelectMany(node => node.Orders)); Assert.Contains("현재 다시 확인", flow.Message);
    }

    [Fact]
    public void 자료상태는_미확인과주의를우선하며_정산준비를_실제지급으로_바꾸지않는다()
    {
        var source = new 공간공동운영통합원천Dto { KindCode = "Settlement", Completed = true, RequiresAttention = true };
        Assert.Equal("현재 자료 확인 필요", 공간공동운영업무연결Presentation.SourceStatus(source));
        source.Verified = true;
        Assert.Equal("문제 확인 필요", 공간공동운영업무연결Presentation.SourceStatus(source));
        source.RequiresAttention = false;
        Assert.Equal("지급 준비 확인 · 실제 지급 확인 별도", 공간공동운영업무연결Presentation.SourceStatus(source));
        source.KindCode = "RootOrderLedger";
        Assert.Contains("실물 완료와 별도", 공간공동운영업무연결Presentation.SourceStatus(source));
    }

    private sealed class Fixture
    {
        public 공간공동운영상세Response Detail { get; } = new()
        {
            LedgerId = "cooperation", Revision = 5, ExecutionMode = "Simulation",
            Cycles = [new() { CycleId = "cycle-one", Number = 1, TermsRevision = 1, StatusCode = "prepared" }]
        };
        public 공간공동운영통합회차Dto Cycle { get; } = new()
        { CycleId = "cycle-one", NodeKey = "cycle:cycle-one", Number = 1, TermsRevision = 1, StatusCode = "prepared" };
        public 공간공동운영통합Response Snapshot { get; }
        public Fixture() => Snapshot = new() { LedgerId = Detail.LedgerId, Revision = Detail.Revision,
            ExecutionMode = Detail.ExecutionMode, Cycles = [Cycle] };
        public 공간공동운영업무연결Model Build(string? cycleId = null)
            => 공간공동운영업무연결Presentation.Build(Detail, Snapshot, cycleId);
        public 공간공동운영통합원천Dto Source(string kind, string id, bool completed = false)
        {
            var source = new 공간공동운영통합원천Dto
            {
                NodeKey = "source:" + kind + ":" + id, KindCode = kind, ReferenceId = id, Label = "원천 표시명",
                Verified = true, Completed = completed, StatusCode = "Active", Href = "/workspace/shipper/distributed/inbound"
            };
            Detail.Cycles[0].References = Detail.Cycles[0].References.Append(new 공간공동운영전문참조Dto { KindCode = kind, ReferenceId = id }).ToArray();
            Cycle.SharedSourceKeys = Cycle.SharedSourceKeys.Append(source.NodeKey).ToArray();
            Snapshot.SharedSources = Snapshot.SharedSources.Append(source).ToArray();
            return source;
        }
        public 공간공동운영통합주문Dto Order()
        {
            var id = Guid.NewGuid(); var order = new 공간공동운영통합주문Dto
            { OrderRequestId = id, NodeKey = "order:" + id.ToString("N"), ProductName = "허용된 상품", RootReadStatusCode = "verified-link" };
            Cycle.Orders = Cycle.Orders.Append(order).ToArray(); return order;
        }
    }
}
