using System.Text.Json;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

public sealed class 공간공동운영변화Tests
{
    [Fact]
    public async Task 비공개_변화는_기존상세권한과_조회자의전문권한을_유지한다()
    {
        var f = new Fixture();
        f.Record.Participants.Add(new() { UserId = "member", RoleCode = 공간공동운영역할Codes.Picker, StateCode = "interested" });
        f.Record.Cycles.Add(Cycle("one", 1, Reference("InboundPlan", "inbound")));
        f.Bridge.Resolve = (actor, references) => references.Select(reference => Source(reference,
            verified: actor == "owner", completed: actor == "owner")).ToArray();

        Assert.Null(await f.Service(null).상세Async(f.Record.LedgerId));
        Assert.Null(await f.Service("stranger").상세Async(f.Record.LedgerId));
        Assert.Empty(f.Bridge.Calls);
        var owner = await f.Detail();
        var member = await f.Detail("member");
        Assert.True(Assert.Single(Assert.Single(owner.SharedOutcomes!.Cycles).Sources).Verified);
        var hidden = Assert.Single(Assert.Single(member.SharedOutcomes!.Cycles).Sources);
        Assert.False(hidden.Verified); Assert.False(hidden.Completed); Assert.True(hidden.RequiresAttention);
        Assert.Equal(new[] { "owner", "member" }, f.Bridge.Calls.Select(call => call.Actor).ToArray());
        Assert.Equal(0, f.Store.WriteCount);
    }

    [Fact]
    public async Task 현재관심과수락은_고유계정으로세고_역할수락과_현조건동의를_구분한다()
    {
        var f = new Fixture(); f.Record.TermsRevision = 2;
        f.Record.Terms.RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider, 공간공동운영역할Codes.Supplier, 공간공동운영역할Codes.Picker];
        f.Record.Participants =
        [
            new() { UserId = "owner", RoleCode = 공간공동운영역할Codes.SpaceProvider, StateCode = "accepted", AgreedTermsRevision = 2 },
            new() { UserId = "owner", RoleCode = 공간공동운영역할Codes.Supplier, StateCode = "accepted", AgreedTermsRevision = 2 },
            new() { UserId = "owner", RoleCode = 공간공동운영역할Codes.Driver, StateCode = "interested" },
            new() { UserId = "supplier", RoleCode = 공간공동운영역할Codes.Supplier, StateCode = "accepted", AgreedTermsRevision = 1 },
            new() { UserId = "interested", RoleCode = 공간공동운영역할Codes.Driver, StateCode = "interested" },
            new() { UserId = "interested", RoleCode = 공간공동운영역할Codes.Picker, StateCode = "interested" },
            new() { UserId = "withdrawn", RoleCode = 공간공동운영역할Codes.Picker, StateCode = "withdrawn" }
        ];
        var value = (await f.Detail()).SharedOutcomes!;
        Assert.Equal(2, value.TermsRevision);
        Assert.Equal(2, value.AcceptedParticipantCount);
        Assert.Equal(1, value.InterestedParticipantCount);
        Assert.Equal(new[] { 공간공동운영역할Codes.SpaceProvider, 공간공동운영역할Codes.Supplier }, value.AcceptedRoleCodes);
        Assert.Equal(new[] { 공간공동운영역할Codes.SpaceProvider }, value.CurrentTermsAgreedRoleCodes);
        Assert.Equal(new[] { 공간공동운영역할Codes.Supplier }, value.PendingTermsAgreementRoleCodes);
        Assert.Equal(new[] { 공간공동운영역할Codes.Picker }, value.MissingRoleCodes);
    }

    [Fact]
    public async Task 회차별_현재원천을_다시읽고_마지막회차의_readiness결과만_재사용한다()
    {
        var f = new Fixture();
        var old = Cycle("old", 1, Reference("InboundPlan", "old-inbound"));
        old.StatusCode = "completed"; old.SourceCompletionVerified = true;
        old.StartedAtUtc = f.Clock.GetUtcNow().AddHours(-2); old.CompletedAtUtc = f.Clock.GetUtcNow().AddHours(-1);
        f.Record.Cycles = [old, Cycle("current", 2, Reference("DistributedOrder", "current-order"))];
        f.Bridge.Resolve = (_, references) => references.Select(reference => Source(reference,
            verified: reference.ReferenceId == "current-order", completed: reference.ReferenceId == "current-order")).ToArray();
        var before = JsonSerializer.Serialize(f.Record);
        var detail = await f.Detail(); var value = detail.SharedOutcomes!;
        Assert.Equal(f.Clock.GetUtcNow(), value.AsOfUtc);
        Assert.Equal(2, f.Bridge.Calls.Count);
        Assert.Equal("current-order", Assert.Single(f.Bridge.Calls[0].References).ReferenceId);
        Assert.Equal("old-inbound", Assert.Single(f.Bridge.Calls[1].References).ReferenceId);
        var prior = value.Cycles[0]; var current = value.Cycles[1];
        Assert.True(prior.SourceCompletionRecorded); Assert.Equal("completed", prior.StatusCode);
        Assert.Equal(old.StartedAtUtc, prior.StartedAtUtc); Assert.Equal(old.CompletedAtUtc, prior.CompletedAtUtc);
        Assert.False(Assert.Single(prior.Sources).Verified); Assert.Equal(1, prior.UnverifiedSourceCount);
        Assert.True(Assert.Single(current.Sources).Completed); Assert.Equal(0, current.UnverifiedSourceCount);
        Assert.Equal(detail.Readiness.Sources.Single().ReferenceId, current.Sources.Single().ReferenceId);
        Assert.Equal(before, JsonSerializer.Serialize(f.Record)); Assert.Equal(0, f.Store.WriteCount);
    }

    [Fact]
    public async Task 연결없는회차와_검증누락을_구분하고_중복이나_다른원천으로_실적을늘리지않는다()
    {
        var f = new Fixture();
        f.Record.Cycles =
        [
            Cycle("empty", 1),
            Cycle("connected", 2, Reference("InboundPlan", "repeated"), Reference("InboundPlan", "repeated"),
                Reference("DistributedOrder", "missing"), Reference("MartOrder", "duplicate-result"), Reference("Settlement", "settlement"))
        ];
        f.Bridge.Resolve = (_, _) =>
        [
            Source(Reference("InboundPlan", "repeated"), true, true),
            Source(Reference("MartOrder", "duplicate-result"), true, true),
            Source(Reference("MartOrder", "duplicate-result"), true, true),
            Source(Reference("Settlement", "settlement"), true, true),
            Source(Reference("DistributedOrder", "not-linked"), true, true)
        ];
        var value = (await f.Detail()).SharedOutcomes!;
        var empty = value.Cycles[0];
        Assert.Equal(0, empty.LinkedSourceCount); Assert.Equal(0, empty.UnverifiedSourceCount); Assert.Empty(empty.Sources);
        var connected = value.Cycles[1];
        Assert.Equal(4, connected.LinkedSourceCount); Assert.Equal(1, connected.DuplicateReferenceCount);
        Assert.Equal(3, connected.UnverifiedSourceCount); Assert.Equal(3, connected.AttentionSourceCount);
        Assert.Equal(4, connected.Sources.Count);
        Assert.DoesNotContain(connected.Sources, source => source.ReferenceId == "not-linked");
        Assert.Equal("duplicate-reference", connected.Sources.Single(source => source.ReferenceId == "repeated").StatusCode);
        Assert.Equal("source-result-missing", connected.Sources.Single(source => source.ReferenceId == "missing").StatusCode);
        Assert.Equal("source-result-duplicate", connected.Sources.Single(source => source.ReferenceId == "duplicate-result").StatusCode);
        Assert.Equal("Settlement", Assert.Single(connected.Sources.Where(source => source.Completed)).KindCode);
    }

    [Fact]
    public async Task Simulation은_명시하고_다른실행모드의_원천은_확인완료로승계하지않는다()
    {
        var f = new Fixture(); f.Record.ExecutionMode = SsalddelExecutionMode.Simulation.ToString();
        f.Mode.Mode = SsalddelExecutionMode.Simulation;
        f.Record.Cycles = [Cycle("old", 1, Reference("InboundPlan", "old")), Cycle("latest", 2, Reference("DistributedOrder", "latest"))];
        f.Bridge.Resolve = (_, references) => references.Select(reference => Source(reference, true, true)).ToArray();
        var simulation = (await f.Detail()).SharedOutcomes!;
        Assert.Equal("Simulation", simulation.ExecutionMode); Assert.True(simulation.MatchesCurrentExecutionMode);
        Assert.All(simulation.Cycles, cycle => Assert.True(Assert.Single(cycle.Sources).Verified));
        f.Bridge.Calls.Clear(); f.Mode.Mode = SsalddelExecutionMode.Operational;
        var differentMode = (await f.Detail()).SharedOutcomes!;
        Assert.Equal("Simulation", differentMode.ExecutionMode); Assert.False(differentMode.MatchesCurrentExecutionMode);
        Assert.Single(f.Bridge.Calls); // 기존 readiness 호출 외에 과거 회차를 다른 모드로 조회하지 않습니다.
        Assert.All(differentMode.Cycles, cycle =>
        {
            var source = Assert.Single(cycle.Sources);
            Assert.Equal("execution-mode-mismatch", source.StatusCode);
            Assert.False(source.Verified); Assert.False(source.Completed); Assert.True(source.RequiresAttention);
        });
    }

    [Fact]
    public async Task 공개제안과_초대에는_비공개변화를_추가하지않고_이전상세응답도_읽는다()
    {
        var f = new Fixture(); f.Record.PublishProposal = true;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var publicResult = await f.Service(null).공개Async();
        var invitation = await f.Service(null).초대조회Async(f.Record.LedgerId);
        Assert.Single(publicResult.Items); Assert.NotNull(invitation);
        Assert.DoesNotContain("sharedOutcomes", JsonSerializer.Serialize(publicResult, options), StringComparison.Ordinal);
        Assert.DoesNotContain("sharedOutcomes", JsonSerializer.Serialize(invitation, options), StringComparison.Ordinal);
        Assert.Empty(f.Bridge.Calls);
        Assert.Null(JsonSerializer.Deserialize<공간공동운영상세Response>("{\"ledgerId\":\"legacy\"}", options)!.SharedOutcomes);
    }

    private static 공간공동운영전문참조Dto Reference(string kind, string id) => new() { KindCode = kind, ReferenceId = id };
    private static 공간공동운영회차Dto Cycle(string id, int number, params 공간공동운영전문참조Dto[] references)
        => new() { CycleId = id, Number = number, TermsRevision = 1, StatusCode = "prepared", References = references };
    private static 공간공동운영원천상태Dto Source(공간공동운영전문참조Dto reference, bool verified, bool completed)
        => new() { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId, StatusCode = verified ? "confirmed" : "not-visible",
            Label = "본인에게 허용된 전문 원장", Verified = verified, Completed = completed, VerifiedRevision = verified ? 2 : null };

    private sealed class Fixture
    {
        public 공간공동운영Record Record { get; } = new()
        {
            LedgerId = "private-cooperation", OwnerUserId = "owner", Revision = 3, TermsRevision = 1, ExecutionMode = "Operational",
            Terms = new() { Title = "동네 공동운영", Purpose = "일을 함께 이어갑니다.", PublicNeighborhoodRegionKey = "region:kr:hjd:1126056500" },
            Participants = [new() { UserId = "owner", RoleCode = 공간공동운영역할Codes.SpaceProvider, StateCode = "interested" }]
        };
        public ReadOnlyStore Store { get; }
        public FakeMode Mode { get; } = new();
        public RecordingBridge Bridge { get; } = new();
        public TimeProvider Clock { get; } = new FixedClock();
        public Fixture() => Store = new(Record);
        public 공간공동운영UseCase Service(string? actor = "owner") => new(Store, new Actor(actor), Mode,
            new Official생활교류공개지역Source(), Clock, Bridge);
        public async Task<공간공동운영상세Response> Detail(string actor = "owner")
            => Assert.IsType<공간공동운영상세Response>(await Service(actor).상세Async(Record.LedgerId));
    }
    private sealed record Actor(string? UserId) : ICurrentUserAccessor { public string? Role => "community"; }
    private sealed class FixedClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 24, 1, 0, 0, TimeSpan.Zero); }
    private sealed class FakeMode : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode { get; set; } = SsalddelExecutionMode.Operational;
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => !IsSimulation;
    }
    private sealed class RecordingBridge : I공간공동운영전문업무Bridge
    {
        public List<(string Actor, IReadOnlyList<공간공동운영전문참조Dto> References)> Calls { get; } = [];
        public Func<string, IReadOnlyList<공간공동운영전문참조Dto>, IReadOnlyList<공간공동운영원천상태Dto>> Resolve { get; set; }
            = (_, references) => references.Select(reference => Source(reference, true, false)).ToArray();
        private bool reading;
        public async Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId,
            IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default)
        {
            Assert.False(reading); reading = true;
            try { Calls.Add((actorUserId, references)); await Task.Yield(); cancellationToken.ThrowIfCancellationRequested(); return Resolve(actorUserId, references); }
            finally { reading = false; }
        }
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId,
            IReadOnlyList<공간공동운영전문참조Dto> currentRefs, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영연결후보Dto>>([]);
    }
    private sealed class ReadOnlyStore(공간공동운영Record record) : I공간공동운영Store
    {
        public int WriteCount { get; private set; }
        private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        public Task<공간공동운영Record?> 조회Async(string ledgerId, CancellationToken cancellationToken = default)
            => Task.FromResult(ledgerId == record.LedgerId ? Copy(record) : null);
        public Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영Record>>(new[] { record }.Skip(skip).Take(take).Select(Copy).ToArray());
        public Task<bool> 생성Async(공간공동운영Record value, CancellationToken cancellationToken = default)
        { WriteCount++; throw new InvalidOperationException("조회에서 원장을 생성할 수 없습니다."); }
        public Task<bool> 교체Async(공간공동운영Record value, long expectedRevision, string actorUserId, CancellationToken cancellationToken = default)
        { WriteCount++; throw new InvalidOperationException("조회에서 원장을 변경할 수 없습니다."); }
    }
}
