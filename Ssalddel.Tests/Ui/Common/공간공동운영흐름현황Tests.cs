using System.Text.Json;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Ui.Common.Areas.App.Components.Community;

namespace Ssalddel.Tests.Ui.Common;

public sealed class 공간공동운영흐름현황Tests
{
    [Fact]
    public void 같은회차의_조건판본과_실제점검시각이_있는_서버시작확인만_표시한다()
    {
        var f = new Fixture(); var result = f.Build();
        Assert.True(result.Flow.Available); Assert.True(result.GateIsCurrent); Assert.Equal(f.CheckedAt, result.CheckedAtUtc);
        Assert.Equal("ready", result.GateTone); Assert.Equal("회차 시작 조건 확인", result.GateTitle);
        Assert.Contains("실제 시작은 운영 담당자가 선택", result.GateMessage);
        Assert.Empty(result.Issues); Assert.Equal(7, result.Nodes.Count);
        Assert.All(result.Nodes, node => { Assert.Equal("neutral", node.Tone); Assert.Equal("연결 전", node.Label); });
    }

    [Fact]
    public void 보류근거는_원문순서와_일대일_대응을_보존하고_역할은_회차요약에만_표시한다()
    {
        var f = new Fixture();
        f.Block(Issue("RequiredRoleAcceptanceMissing", "cooperation", "필수 역할 수락 확인", roles: [공간공동운영역할Codes.Driver]),
            Issue("InboundPlanReferenceRequired", "source", "입고 계획 연결 확인", stages: ["inbound"]),
            Issue("RequiredAppointmentReviewRequired", "required-appointment", "시간 약속 수락 확인"));
        var result = f.Build();
        Assert.True(result.GateIsCurrent); Assert.Equal("blocked", result.GateTone);
        Assert.Equal(f.Detail.Readiness.Blockers, result.Issues.Select(issue => issue.Message));
        Assert.Equal(new[] { "RequiredRoleAcceptanceMissing", "InboundPlanReferenceRequired", "RequiredAppointmentReviewRequired" }, result.Issues.Select(issue => issue.Code));
        Assert.Equal(new[] { 공간공동운영역할Codes.Driver }, result.MissingRoleCodes);
        var inbound = Node(result, "inbound"); Assert.Equal("blocked", inbound.Tone);
        Assert.Equal("InboundPlanReferenceRequired", Assert.Single(inbound.Issues).Code);
        Assert.All(result.Nodes.Where(node => node.StageKey != "inbound"), node =>
        { Assert.NotEqual("blocked", node.Tone); Assert.Empty(node.Issues); });
    }

    [Theory]
    [InlineData("reversed")]
    [InlineData("missing")]
    [InlineData("extra")]
    public void 문구가같은집합이어도_보류목록의_순서나수가다르면_미확인으로_남긴다(string mismatch)
    {
        var f = new Fixture(); f.Block(Issue("first", "source", "입고 연결 확인", ["inbound"]), Issue("second", "cycle", "회차 준비 확인"));
        f.Detail.Readiness.Blockers = mismatch switch
        {
            "reversed" => f.Detail.Readiness.Blockers.Reverse().ToArray(),
            "missing" => f.Detail.Readiness.Blockers.Take(1).ToArray(),
            _ => f.Detail.Readiness.Blockers.Append("다른 판본 보류").ToArray()
        };
        AssertUnknownGate(f.Build());
    }

    [Fact]
    public void 동일한보류문구도_개별근거로_존재하면_임의로_중복제거하지않는다()
    {
        var f = new Fixture(); f.Block(Issue("first", "cycle", "시간 약속 확인"), Issue("second", "required-appointment", "시간 약속 확인"));
        var result = f.Build(); Assert.Equal("blocked", result.GateTone); Assert.Equal(2, result.Issues.Count);
        Assert.Equal(new[] { "first", "second" }, result.Issues.Select(issue => issue.Code));
    }

    [Theory]
    [InlineData("cycle")]
    [InlineData("terms")]
    [InlineData("time")]
    public void 선택회차의_점검대상이나판본시각이없으면_다른점검을_가져오지않는다(string mismatch)
    {
        var f = new Fixture(); f.Block(Issue("source", "source", "다른 판본 입고 보류", ["inbound"]));
        if (mismatch == "cycle") f.Detail.Readiness.CycleId = "other";
        if (mismatch == "terms") f.Detail.Readiness.CycleTermsRevision++;
        if (mismatch == "time") f.Detail.Readiness.AsOfUtc = default;
        var result = f.Build(); Assert.True(result.Flow.Available); AssertUnknownGate(result);
        Assert.DoesNotContain("다른 판본 입고 보류", JsonSerializer.Serialize(result));
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("completed")]
    [InlineData("status")]
    [InlineData("attention")]
    public void 같은부모판본이어도_원천권한이나상태가_바뀌면_과거시작점검을_재사용하지않는다(string change)
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound");
        f.Block(Issue("source", "source", "이전 원천의 입고 보류", ["inbound"]));
        Assert.True(f.Build().GateIsCurrent);
        switch (change)
        {
            case "permission": source.Verified = false; break;
            case "completed": source.Completed = true; break;
            case "status": source.StatusCode = "Completed"; break;
            case "attention": source.RequiresAttention = true; break;
        }
        var earlier = Assert.Single(f.Detail.Readiness.Sources);
        Assert.True(earlier.Verified); Assert.False(earlier.Completed); Assert.False(earlier.RequiresAttention);
        Assert.Equal("Active", earlier.StatusCode);
        var result = f.Build(); Assert.True(result.Flow.Available); AssertUnknownGate(result);
        Assert.DoesNotContain("이전 원천의 입고 보류", JsonSerializer.Serialize(result));
        Assert.Equal(5, f.Detail.Revision); Assert.Equal(5, f.Snapshot.Revision);
    }

    [Theory]
    [InlineData("readiness-missing")]
    [InlineData("readiness-duplicate")]
    [InlineData("readiness-other-key")]
    [InlineData("snapshot-missing")]
    [InlineData("snapshot-duplicate")]
    public void 점검원천과_후속조회원천의_키별수가_정확히하나씩_일치하지않으면_미확인이다(string mismatch)
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound");
        f.Block(Issue("source", "source", "불일치 원천의 보류", ["inbound"]));
        switch (mismatch)
        {
            case "readiness-missing": f.Detail.Readiness.Sources = []; break;
            case "readiness-duplicate":
                f.Detail.Readiness.Sources = f.Detail.Readiness.Sources.Append(new 공간공동운영원천상태Dto
                { KindCode = source.KindCode, ReferenceId = source.ReferenceId, Verified = true, StatusCode = "Active" }).ToArray();
                break;
            case "readiness-other-key": Assert.Single(f.Detail.Readiness.Sources).ReferenceId = "other"; break;
            case "snapshot-missing": f.Snapshot.SharedSources = []; break;
            case "snapshot-duplicate":
                f.Snapshot.SharedSources = f.Snapshot.SharedSources.Append(new 공간공동운영통합원천Dto
                { NodeKey = source.NodeKey, KindCode = source.KindCode, ReferenceId = source.ReferenceId, Verified = true, StatusCode = "Active" }).ToArray();
                break;
        }
        var result = f.Build(); Assert.True(result.Flow.Available); AssertUnknownGate(result);
        Assert.DoesNotContain("불일치 원천의 보류", JsonSerializer.Serialize(result));
    }

    [Fact]
    public void 원천전체수가_같아도_하나의키가중복되고_다른키가빠졌으면_미확인이다()
    {
        var f = new Fixture(); f.Source("InboundPlan", "one"); f.Source("InboundPlan", "two");
        f.Detail.Readiness.Sources =
        [
            new() { KindCode = "InboundPlan", ReferenceId = "one", Verified = true, StatusCode = "Active" },
            new() { KindCode = "InboundPlan", ReferenceId = "one", Verified = true, StatusCode = "Active" }
        ];
        Assert.Equal(f.Cycle.SharedSourceKeys.Count, f.Detail.Readiness.Sources.Count);
        var result = f.Build(); Assert.True(result.Flow.Available); AssertUnknownGate(result);
    }

    [Theory]
    [InlineData("default")]
    [InlineData("reverse")]
    public void 후속조회시각이_없거나_시작점검보다_앞서면_빈원천이라도_최신으로_표시하지않는다(string mismatch)
    {
        var f = new Fixture();
        f.Snapshot.AsOfUtc = mismatch == "default" ? default : f.CheckedAt.AddTicks(-1);
        var result = f.Build(); Assert.True(result.Flow.Available); AssertUnknownGate(result);
    }

    [Fact]
    public void 점검시각과_후속조회시각이_같고_원천도같으면_현재점검으로_유지한다()
    {
        var f = new Fixture(); f.Source("InboundPlan", "inbound"); f.Snapshot.AsOfUtc = f.CheckedAt;
        var result = f.Build(); Assert.True(result.GateIsCurrent); Assert.Equal("ready", result.GateTone);
        Assert.Equal(f.CheckedAt, result.CheckedAtUtc);
    }

    [Fact]
    public void 기본최신회차와_명시선택한_이전회차의_점검을_섞지않는다()
    {
        var f = new Fixture(); f.Block(Issue("missing", "cooperation", "현재 회차 역할 확인", roles: [공간공동운영역할Codes.Driver]));
        f.Detail.Cycles = [new() { CycleId = "newer", Number = 9, TermsRevision = 2, StatusCode = "prepared" }, f.Detail.Cycles[0]];
        f.Snapshot.Cycles = [new() { NodeKey = "cycle:newer", CycleId = "newer", Number = 9, TermsRevision = 2, StatusCode = "prepared" }, f.Cycle];
        var latest = f.Build(); Assert.Equal("newer", latest.Flow.Cycle!.CycleId); AssertUnknownGate(latest);
        var selected = f.Build("cycle"); Assert.Equal("blocked", selected.GateTone);
        Assert.Equal(new[] { 공간공동운영역할Codes.Driver }, selected.MissingRoleCodes);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("message")]
    [InlineData("scope")]
    [InlineData("stage")]
    [InlineData("role")]
    [InlineData("global-stage")]
    public void 근거의_역할과단계경계가_잘못되면_자유문구로_막힌단계를_만들지않는다(string invalid)
    {
        var f = new Fixture(); var issue = Issue("source", "source", "입고 담당 확인", ["inbound"]);
        switch (invalid)
        {
            case "code": issue.Code = " "; break;
            case "message": issue.Message = " "; break;
            case "scope": issue.ScopeCode = "guessed"; break;
            case "stage": issue.StageKeys = ["not-a-stage"]; break;
            case "role": issue.RoleCodes = ["guessed-worker"]; break;
            case "global-stage": issue.ScopeCode = "cooperation"; break;
        }
        f.Block(issue); AssertUnknownGate(f.Build());
    }

    [Fact]
    public void 필수역할_부족을_배송이나생산의_담당배정실패로_자동전파하지않는다()
    {
        var f = new Fixture(); f.Source("DistributedOrder", "order-source", completed: true);
        f.Block(Issue("RequiredRoleAcceptanceMissing", "cooperation", "역할 수락 확인", roles:
            [공간공동운영역할Codes.Driver, 공간공동운영역할Codes.Supplier, 공간공동운영역할Codes.Driver]));
        var result = f.Build(); Assert.Equal("blocked", result.GateTone);
        Assert.Equal(2, result.MissingRoleCodes.Count); Assert.All(result.Nodes, node => Assert.Empty(node.Issues));
        Assert.Equal("linked", Node(result, "delivery").Tone); Assert.Equal("neutral", Node(result, "production").Tone);
        Assert.DoesNotContain(result.Nodes, node => node.Tone == "blocked");
    }

    [Theory]
    [InlineData("running", false)]
    [InlineData("running", true)]
    [InlineData("completed", false)]
    public void 진행중이거나_마친회차는_시작보류로_되돌리거나_선행단계를_차단하지않는다(string state, bool canComplete)
    {
        var f = new Fixture(); f.Detail.Cycles[0].StatusCode = f.Cycle.StatusCode = state;
        f.Source("InboundPlan", "inbound", completed: true);
        f.Block(Issue("source", "source", "입고 상태 확인", ["inbound"])); f.Detail.Readiness.CanComplete = canComplete;
        var result = f.Build(); Assert.True(result.GateIsCurrent); Assert.Equal("neutral", result.GateTone);
        Assert.Empty(result.Issues); Assert.Empty(result.MissingRoleCodes); Assert.Equal("linked", Node(result, "inbound").Tone);
        Assert.DoesNotContain(result.Nodes, node => node.Tone == "blocked");
        if (state == "completed") Assert.Equal("마친 회차의 기록", result.GateTitle);
        else if (canComplete) Assert.Contains("회차 마침은 운영 담당자가 선택", result.GateMessage);
        else Assert.Contains("진행과 마감 근거", result.GateMessage);
    }

    [Fact]
    public void 현재원천을_확인못하면_마친회차에서도_자료주의는_별도로_유지한다()
    {
        var f = new Fixture(); f.Detail.Cycles[0].StatusCode = f.Cycle.StatusCode = "completed";
        var source = f.Source("InboundPlan", "inbound", completed: true); source.Verified = false;
        var result = f.Build(); Assert.Equal("neutral", result.GateTone);
        Assert.Equal("attention", Node(result, "inbound").Tone); Assert.Equal("현재 자료 확인 필요", Node(result, "inbound").Label);
        Assert.Empty(Node(result, "inbound").Issues); Assert.Empty(Node(result, "production").SourceLabels);
    }

    [Fact]
    public void 실제계보의_고유주문수만_해당자료단계에_집계하고_다른절차에_전파하지않는다()
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound");
        var one = f.Order(source); var two = f.Order(source);
        f.Snapshot.Relations =
        [
            Relation(one.NodeKey, source.NodeKey), Relation(one.NodeKey, source.NodeKey), Relation(two.NodeKey, source.NodeKey),
            Relation("order:other-cycle", source.NodeKey), Relation(one.NodeKey, "source:InboundPlan:other"),
            new() { FromKey = one.NodeKey, ToKey = source.NodeKey, RelationCode = "execute-next" }
        ];
        var result = f.Build(); Assert.Equal(1, result.LinkedSourceCount); Assert.Equal(2, result.LinkedOrderCount);
        Assert.Equal(2, Node(result, "inbound").RelatedOrderCount);
        Assert.All(result.Nodes.Where(node => node.StageKey != "inbound"), node => Assert.Equal(0, node.RelatedOrderCount));
        one.RootReadStatusCode = "lineage-unverified";
        Assert.Equal(1, Node(f.Build(), "inbound").RelatedOrderCount);
    }

    [Fact]
    public void 주문조회가_미확인이면_과거주문수나_자료관계를_진행실적으로_재사용하지않는다()
    {
        var f = new Fixture(); var source = f.Source("InboundPlan", "inbound"); var order = f.Order(source);
        f.Snapshot.Relations = [Relation(order.NodeKey, source.NodeKey)]; f.Snapshot.OrdersReadStatusCode = "temporarily-unavailable";
        var result = f.Build(); Assert.Equal(0, result.LinkedOrderCount); Assert.Equal(0, Node(result, "inbound").RelatedOrderCount);
        Assert.Equal("attention", Node(result, "order").Tone); Assert.Empty(Node(result, "order").SourceLabels);
    }

    [Fact]
    public void 제안중인주문만_있고_전문원장이_아직없으면_주문협의연결로_구분한다()
    {
        var f = new Fixture(); var order = f.Order();
        order.AgreementStatusCode = "proposed"; order.RootReadStatusCode = "not-created";
        var result = f.Build(); var node = Node(result, "order");
        Assert.Equal("linked", node.Tone); Assert.Equal("주문 협의 연결", node.Label);
        Assert.Empty(node.SourceLabels); Assert.Equal(1, result.LinkedOrderCount); Assert.Equal(0, result.LinkedSourceCount);
        Assert.Equal(0, result.PaymentReadyEvidenceCount); Assert.Equal(0, node.RelatedOrderCount);
    }

    [Fact]
    public void 실제원주문_연결이확인된_주문은_협의중으로_낮추어표시하지않는다()
    {
        var f = new Fixture(); f.Order();
        var result = f.Build(); Assert.Equal("원장 연결 확인", Node(result, "order").Label);
        Assert.Equal("linked", Node(result, "order").Tone);
    }

    [Theory]
    [InlineData(false, false, true, 0, "정산 근거 재확인 필요")]
    [InlineData(true, true, true, 0, "정산 근거 재확인 필요")]
    [InlineData(true, false, false, 0, "정산 원장 연결 · 지급 준비 확인 전")]
    [InlineData(true, false, true, 1, "연결된 정산의 지급 준비 확인")]
    public void 정산의_원천확인과문제를_먼저보고_Completed는_지급준비근거로만_집계한다(
        bool verified, bool attention, bool completed, int evidenceCount, string expected)
    {
        var f = new Fixture(); var settlement = f.Source("Settlement", "settlement", completed);
        settlement.Verified = verified; settlement.RequiresAttention = attention;
        var result = f.Build(); Assert.Equal(expected, result.SettlementLabel); Assert.Equal(evidenceCount, result.PaymentReadyEvidenceCount);
        Assert.NotEqual("completed", Node(result, "settlement").Tone);
        Assert.DoesNotContain("입금 완료", result.SettlementLabel); Assert.DoesNotContain("실지급 완료", result.SettlementLabel);
    }

    [Fact]
    public void 자유조건의_금액이나시간을_이익실수령액으로_추정하지않는다()
    {
        var f = new Fixture(); f.Detail.Terms.CostAndCompensation = "999999원 이익과 777시간 절감";
        var source = f.Source("Settlement", "settlement", completed: true); source.Label = "999999원 지급 완료";
        var result = f.Build(); var json = JsonSerializer.Serialize(result);
        Assert.Equal(1, result.PaymentReadyEvidenceCount); Assert.Equal("linked", Node(result, "settlement").Tone);
        Assert.DoesNotContain("999999", json); Assert.DoesNotContain("777", json); Assert.DoesNotContain("0원", json);
        var empty = new Fixture().Build(); Assert.Equal("정산 원장 미연결", empty.SettlementLabel);
        Assert.Equal(0, empty.PaymentReadyEvidenceCount); Assert.Equal("연결 전", Node(empty, "settlement").Label);
    }

    [Fact]
    public void 투영은_원자료를_수정하지않고_보류배열은_현재결과에_별도로_보존한다()
    {
        var f = new Fixture(); var issue = Issue("missing", "cooperation", "필수 역할 확인", roles: [공간공동운영역할Codes.Driver]);
        f.Block(issue); f.Source("InboundPlan", "inbound");
        var before = JsonSerializer.Serialize(new { f.Detail, f.Snapshot }); var result = f.Build();
        Assert.Equal(before, JsonSerializer.Serialize(new { f.Detail, f.Snapshot }));
        issue.Message = "나중에 바뀐 문구"; issue.RoleCodes[0] = 공간공동운영역할Codes.Supplier;
        Assert.Equal("필수 역할 확인", Assert.Single(result.Issues).Message);
        Assert.Equal(new[] { 공간공동운영역할Codes.Driver }, result.MissingRoleCodes);
    }

    private static 공간공동운영단계표시 Node(공간공동운영흐름현황 value, string key) => Assert.Single(value.Nodes, node => node.StageKey == key);
    private static void AssertUnknownGate(공간공동운영흐름현황 value)
    {
        Assert.False(value.GateIsCurrent); Assert.Equal("unknown", value.GateTone); Assert.Null(value.CheckedAtUtc);
        Assert.Empty(value.Issues); Assert.Empty(value.MissingRoleCodes);
        Assert.All(value.Nodes, node => { Assert.NotEqual("blocked", node.Tone); Assert.Empty(node.Issues); });
    }
    private static 공간공동운영준비보류Dto Issue(string code, string scope, string message, string[]? stages = null, string[]? roles = null)
        => new() { Code = code, ScopeCode = scope, Message = message, StageKeys = stages ?? [], RoleCodes = roles ?? [] };
    private static 공간공동운영통합관계Dto Relation(string from, string to) => new() { FromKey = from, ToKey = to, RelationCode = "source-lineage" };

    private sealed class Fixture
    {
        public DateTimeOffset CheckedAt { get; } = new(2026, 10, 24, 8, 0, 0, TimeSpan.Zero);
        public 공간공동운영상세Response Detail { get; } = new()
        {
            LedgerId = "hub", Revision = 5, TermsRevision = 2, ExecutionMode = "Simulation",
            Cycles = [new() { CycleId = "cycle", Number = 1, TermsRevision = 2, StatusCode = "prepared" }]
        };
        public 공간공동운영통합회차Dto Cycle { get; } = new()
            { NodeKey = "cycle:cycle", CycleId = "cycle", Number = 1, TermsRevision = 2, StatusCode = "prepared" };
        public 공간공동운영통합Response Snapshot { get; }
        public Fixture()
        {
            Detail.Readiness = new() { CycleId = "cycle", CycleTermsRevision = 2, AsOfUtc = CheckedAt, CanStart = true };
            Snapshot = new() { LedgerId = "hub", Revision = 5, ExecutionMode = "Simulation", AsOfUtc = CheckedAt.AddMinutes(1), Cycles = [Cycle] };
        }
        public 공간공동운영흐름현황 Build(string? cycleId = null) => 공간공동운영흐름현황Presentation.Build(Detail, Snapshot, cycleId);
        public void Block(params 공간공동운영준비보류Dto[] issues)
        { Detail.Readiness.CanStart = false; Detail.Readiness.Issues = issues; Detail.Readiness.Blockers = issues.Select(issue => issue.Message).ToArray(); }
        public 공간공동운영통합원천Dto Source(string kind, string id, bool completed = false)
        {
            var source = new 공간공동운영통합원천Dto { NodeKey = $"source:{kind}:{id}", KindCode = kind, ReferenceId = id,
                Label = "원 업무 표시", Verified = true, Completed = completed, StatusCode = "Active", Href = "/workspace/shipper" };
            Detail.Cycles[0].References = Detail.Cycles[0].References.Append(new 공간공동운영전문참조Dto { KindCode = kind, ReferenceId = id }).ToArray();
            Cycle.SharedSourceKeys = Cycle.SharedSourceKeys.Append(source.NodeKey).ToArray();
            Detail.Readiness.Sources = Detail.Readiness.Sources.Append(new 공간공동운영원천상태Dto
            {
                KindCode = kind, ReferenceId = id, Label = source.Label, Verified = source.Verified,
                Completed = source.Completed, RequiresAttention = source.RequiresAttention,
                StatusCode = source.StatusCode, Href = source.Href
            }).ToArray();
            Snapshot.SharedSources = Snapshot.SharedSources.Append(source).ToArray(); return source;
        }
        public 공간공동운영통합주문Dto Order(params 공간공동운영통합원천Dto[] children)
        {
            var id = Guid.NewGuid(); var order = new 공간공동운영통합주문Dto { NodeKey = "order:" + id.ToString("N"), OrderRequestId = id,
                ProductName = "상온 포장 상품", RootReadStatusCode = "verified-link", Children = children };
            Cycle.Orders = Cycle.Orders.Append(order).ToArray(); return order;
        }
    }
}
