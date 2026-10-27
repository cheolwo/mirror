using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

public sealed class 공간공동운영UseCaseTests
{
    [Fact]
    public async Task 생성은_로그인과_비구속동의가_필요하고_동일요청은_한번만_저장한다()
    {
        var f = new Fixture(); var request = f.Request();
        Code("AuthenticationRequired", await f.Service(null).생성Async(request));
        request.ConfirmNonBinding = false; Code("InvalidProposal", await f.Service().생성Async(request));
        request.ConfirmNonBinding = true;
        var first = Ok(await f.Service().생성Async(request));
        var replay = Ok(await f.Service().생성Async(request));
        Assert.Equal(first.LedgerId, replay.LedgerId); Assert.True(replay.IdempotentReplay); Assert.Single(f.Store.Rows);
        request.Terms.Purpose = "다른 목적"; Code("IdempotencyConflict", await f.Service().생성Async(request));
    }

    [Fact]
    public async Task 공개는_명시소개동의만_내보내며_철회즉시_소개와집계에서_빠진다()
    {
        var f = new Fixture(); var item = Ok(await f.Service().생성Async(f.Request()));
        var before = await f.Service(null).공개Async(); Assert.Single(before.Items); Assert.Equal(0, before.PublicParticipantCount);
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.IntroductionConsent, consent: true, name: "면목 이웃"));
        var visible = await f.Service(null).공개Async(); Assert.Equal(1, visible.PublicParticipantCount); Assert.Equal("면목 이웃", Assert.Single(visible.Items.Single().Introductions).DisplayName);
        Assert.DoesNotContain("owner", JsonSerializer.Serialize(visible), StringComparison.Ordinal);
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.IntroductionConsent, consent: false));
        Assert.Empty((await f.Service(null).공개Async()).Items.Single().Introductions);
        Assert.Equal(0, (await f.Service(null).공개Async()).PublicParticipantCount);
    }

    [Fact]
    public async Task Simulation제안은_운영공개집계에서_제외하고_진짜0과_다른상태를쓴다()
    {
        var f = new Fixture(); f.Mode.Mode = SsalddelExecutionMode.Simulation;
        Ok(await f.Service().생성Async(f.Request()));
        var sim = await f.Service(null).공개Async(); Assert.Equal("simulation-excluded", sim.StatusCode); Assert.Single(sim.Items); Assert.Equal(0, sim.RecruitmentCount);
        f.Mode.Mode = SsalddelExecutionMode.Operational;
        var real = await f.Service(null).공개Async(); Assert.Equal("recorded", real.StatusCode); Assert.Empty(real.Items); Assert.Equal(0, real.RecruitmentCount);
    }

    [Theory]
    [InlineData("용마산로 377", "InvalidTerms")]
    [InlineData("010-1234-5678", "InvalidTerms")]
    public async Task 공개문구의_상세주소와연락처는_거절한다(string text, string code)
    {
        var f = new Fixture(); var request = f.Request(); request.Terms.Title = text;
        Code(code, await f.Service().생성Async(request)); Assert.Empty(f.Store.Rows);
    }

    [Fact]
    public async Task 참여자아닌계정은_상세와요청결과를_조회못하고_공개제안에만_본인참여한다()
    {
        var f = new Fixture(); var item = Ok(await f.Service().생성Async(f.Request()));
        Assert.Null(await f.Service("stranger").상세Async(item.LedgerId));
        Assert.Null(await f.Service("stranger").요청결과Async(Guid.NewGuid(), item.LedgerId));
        item = Ok(await f.Command(item, "stranger", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Driver));
        Assert.NotNull(await f.Service("stranger").상세Async(item.LedgerId));
        Code("ActionNotAllowed", await f.Command(item, "stranger", 공간공동운영명령Codes.UpdateTerms, terms: f.Terms()));
    }

    [Fact]
    public async Task 하나의계정이_복수역할을_선택하되_역할별수락과동의는_독립이다()
    {
        var f = new Fixture(); var item = Ok(await f.Service().생성Async(f.Request()));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Supplier));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.AcceptRole, role: 공간공동운영역할Codes.Supplier));
        Assert.Equal(2, item.Participants.Count); Assert.Equal("interested", item.Participants.Single(x => x.RoleCode == 공간공동운영역할Codes.SpaceProvider).ParticipationStateCode);
        Assert.Equal("accepted", item.Participants.Single(x => x.RoleCode == 공간공동운영역할Codes.Supplier).ParticipationStateCode);
        Code("RoleRequired", await f.Command(item, "owner", 공간공동운영명령Codes.IntroductionConsent, consent: true, name: "이웃"));
    }

    [Fact]
    public async Task 조건이바뀌면_합의와소개와운영수락을_새판본에승계하지않는다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner();
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.IntroductionConsent, consent: true, name: "이웃"));
        var terms = f.Terms(); terms.Capacity = "보관 수량 변경";
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.UpdateTerms, terms: terms));
        Assert.Equal(2, item.TermsRevision); Assert.Null(item.OperatorUserId);
        Assert.All(item.Participants, member => { Assert.Equal(0, member.AgreedTermsRevision); Assert.False(member.IntroductionConsented); });
        Code("TermsRevisionChanged", await f.Command(item, "owner", 공간공동운영명령Codes.AgreeTerms, consent: true, termsRevision: 1));
    }

    [Fact]
    public async Task 운영담당은_공간제공자의_본인수락으로시작하고_양측인계수락전에는_바뀌지않는다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner();
        item = Ok(await f.Command(item, "coordinator", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Coordinator));
        item = Ok(await f.Command(item, "coordinator", 공간공동운영명령Codes.AcceptRole));
        Code("ActionNotAllowed", await f.Command(item, "coordinator", 공간공동운영명령Codes.AcceptOperator, consent: true));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.ProposeOperatorHandover, consent: true, target: "coordinator"));
        Assert.Equal("owner", item.OperatorUserId); Assert.Equal("coordinator", item.PendingOperatorUserId);
        Code("ActionNotAllowed", await f.Command(item, "owner", 공간공동운영명령Codes.AcceptOperatorHandover, consent: true));
        item = Ok(await f.Command(item, "coordinator", 공간공동운영명령Codes.AcceptOperatorHandover, consent: true));
        Assert.Equal("coordinator", item.OperatorUserId); Assert.Null(item.PendingOperatorUserId);
    }

    [Fact]
    public async Task 공급역할만철회하면_수락한공간제공역할의_운영담당과_인계제안은_보존한다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner();
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Supplier));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.AcceptRole, role: 공간공동운영역할Codes.Supplier));
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Coordinator));
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.AcceptRole));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.ProposeOperatorHandover, consent: true, target: "next"));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Withdraw, role: 공간공동운영역할Codes.Supplier));
        Assert.Equal("owner", item.OperatorUserId); Assert.Equal("next", item.PendingOperatorUserId);
        Assert.Equal("accepted", item.Participants.Single(x => x.UserId == "owner" && x.RoleCode == 공간공동운영역할Codes.SpaceProvider).ParticipationStateCode);
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Withdraw, role: 공간공동운영역할Codes.SpaceProvider));
        Assert.Null(item.OperatorUserId); Assert.Null(item.PendingOperatorUserId);
        Assert.Equal(0, f.Store.Rows[item.LedgerId].PendingOperatorTermsRevision);
    }

    [Fact]
    public async Task 수락한조율역할이남으면_공간제공역할철회도_운영자선택을_자동취소하지않는다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner();
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Coordinator));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.AcceptRole, role: 공간공동운영역할Codes.Coordinator));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Withdraw, role: 공간공동운영역할Codes.SpaceProvider));
        Assert.Equal("owner", item.OperatorUserId);
        Assert.Equal("accepted", item.Participants.Single(x => x.RoleCode == 공간공동운영역할Codes.Coordinator).ParticipationStateCode);
    }

    [Fact]
    public async Task 인계받을사람의_다른역할철회는_대기제안을보존하고_마지막운영자격철회만_해제한다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner();
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Coordinator));
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.AcceptRole));
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.Join, role: 공간공동운영역할Codes.Supplier));
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.AcceptRole, role: 공간공동운영역할Codes.Supplier));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.ProposeOperatorHandover, consent: true, target: "next"));
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.Withdraw, role: 공간공동운영역할Codes.Supplier));
        Assert.Equal("next", item.PendingOperatorUserId); Assert.Equal(item.TermsRevision, f.Store.Rows[item.LedgerId].PendingOperatorTermsRevision);
        item = Ok(await f.Command(item, "next", 공간공동운영명령Codes.Withdraw, role: 공간공동운영역할Codes.Coordinator));
        Assert.Equal("owner", item.OperatorUserId); Assert.Null(item.PendingOperatorUserId);
        Assert.Equal(0, f.Store.Rows[item.LedgerId].PendingOperatorTermsRevision);
    }

    [Fact]
    public async Task 요청영수증은_프로세스객체가바뀌어도_복구되고_다른payload는_거절한다()
    {
        var f = new Fixture(); var item = Ok(await f.Service().생성Async(f.Request()));
        var command = f.NewCommand(item, 공간공동운영명령Codes.AcceptRole);
        Ok(await f.Service().변경Async(item.LedgerId, command));
        var replay = Ok(await f.Service().변경Async(item.LedgerId, command)); Assert.True(replay.IdempotentReplay);
        Assert.NotNull(await f.Service().요청결과Async(command.ClientRequestId, item.LedgerId));
        command.Action = 공간공동운영명령Codes.Withdraw; Code("IdempotencyConflict", await f.Service().변경Async(item.LedgerId, command));
        Code("RevisionConflict", await f.Command(item, "owner", 공간공동운영명령Codes.AcceptRole));
    }

    [Fact]
    public async Task 전문원천을못읽거나_무권한참조면_시작과완료를_만들지않는다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner(); item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.CreateCycle));
        Code("ReadinessRequired", await f.Command(item, "owner", 공간공동운영명령Codes.StartCycle, cycle: item.Cycles.Single().CycleId));
        f.Bridge.Verify = false;
        Code("ReferenceNotVerified", await f.Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Single().CycleId,
            reference: new() { KindCode = "DistributedSite", ReferenceId = "other-site" }));
        Assert.Empty((await f.Service().상세Async(item.LedgerId))!.Cycles.Single().References);
    }

    [Fact]
    public async Task 실제입고와주문수령결과로_한회차를마친뒤_다음회차를_빈참조로준비한다()
    {
        var f = new Fixture(); var item = await f.PreparedCycle(); var cycleId = item.Cycles.Single().CycleId;
        f.Bridge.Revision = 2; // 연결 뒤 합법적인 전문 업무 진행은 예전 연결 시점 판본으로 막지 않습니다.
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.StartCycle, cycle: cycleId));
        Code("SourceCompletionRequired", await f.Command(item, "owner", 공간공동운영명령Codes.CompleteCycle, cycle: cycleId));
        f.Bridge.Complete = true;
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.CompleteCycle, cycle: cycleId));
        Assert.True(item.Cycles.Single().SourceCompletionVerified);
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.CreateCycle));
        Assert.Equal(2, item.Cycles.Count); Assert.Empty(item.Cycles.Last().References); Assert.Equal("prepared", item.Cycles.Last().StatusCode);
        Code("SourceAlreadyUsedInCycle", await f.Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Last().CycleId,
            reference: new() { KindCode = "DistributedOrder", ReferenceId = "DistributedOrder-actual" }));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Last().CycleId,
            reference: new() { KindCode = "DistributedSite", ReferenceId = "DistributedSite-actual" }));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Last().CycleId,
            reference: new() { KindCode = "InboundPlan", ReferenceId = "InboundPlan-actual" }));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Last().CycleId,
            reference: new() { KindCode = "DistributedOrder", ReferenceId = "next-order-from-remaining-stock" }));
        Assert.Equal(3, item.Cycles.Last().References.Count);
        Assert.Contains(item.Cycles.Last().References, reference => reference.KindCode == "InboundPlan" && reference.ReferenceId == "InboundPlan-actual");
    }

    [Fact]
    public async Task 새참조를연결할때는_선택시점의_전문판본이_최신과_일치해야한다()
    {
        var f = new Fixture(); var item = await f.ReadyOwner(); item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.CreateCycle));
        f.Bridge.Revision = 2;
        Code("ReferenceRevisionChanged", await f.Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Single().CycleId,
            reference: new() { KindCode = "DistributedSite", ReferenceId = "site", ExpectedRevision = 1 }));
        Assert.Empty((await f.Service().상세Async(item.LedgerId))!.Cycles.Single().References);
    }

    [Fact]
    public async Task 진행중철회는_소개를즉시숨기고_전문업무원천과_주문부록은_보존한다()
    {
        var f = new Fixture(); var item = await f.PreparedCycle(); var id = item.Cycles.Single().CycleId;
        var record = f.Store.Rows[item.LedgerId]; record.OrderExtensionJson = "original-order-authority";
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.IntroductionConsent, consent: true, name: "이웃"));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.StartCycle, cycle: id));
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.Withdraw));
        Assert.Equal("running", item.Cycles.Single().StatusCode); Assert.NotEmpty(item.Cycles.Single().References);
        Assert.Empty((await f.Service(null).공개Async()).Items.Single().Introductions);
        Assert.Equal("original-order-authority", f.Store.Rows[item.LedgerId].OrderExtensionJson);
    }

    [Fact]
    public async Task 공간철회는_신규시작을막지만_실제수령완료된_기존회차기록을_지우지않는다()
    {
        var f = new Fixture(); var item = await f.PreparedCycle(); var cycle = item.Cycles.Single().CycleId;
        f.Bridge.SpaceRevoked = true;
        Code("ReadinessRequired", await f.Command(item, "owner", 공간공동운영명령Codes.StartCycle, cycle: cycle));
        f.Bridge.SpaceRevoked = false;
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.StartCycle, cycle: cycle));
        f.Bridge.SpaceRevoked = true; f.Bridge.Complete = true;
        item = Ok(await f.Command(item, "owner", 공간공동운영명령Codes.CompleteCycle, cycle: cycle));
        Assert.True(item.Cycles.Single().SourceCompletionVerified);
        Assert.Contains(item.Readiness.Sources, source => source.KindCode == "DistributedSite" && source.RequiresAttention);
    }

    [Fact]
    public async Task 공개집계는_200개_저장소페이지이후의_실제자료도_빠뜨리지않는다()
    {
        var f = new Fixture();
        for (var i = 0; i < 205; i++) Ok(await f.Service("user-" + i).생성Async(f.Request()));
        var response = await f.Service(null).공개Async(page: 11);
        Assert.Equal(205, response.RecruitmentCount); Assert.Equal(5, response.Items.Count); Assert.False(response.HasMore); Assert.False(response.CountsArePartial);
    }

    [Fact]
    public async Task 게시동의철회는_소개동의와독립이며_제안전체를_즉시숨긴다()
    {
        var f = new Fixture(); var item = Ok(await f.Service().생성Async(f.Request()));
        Ok(await f.Command(item, "owner", 공간공동운영명령Codes.ProposalVisibility, consent: false));
        Assert.Empty((await f.Service(null).공개Async()).Items);
        Assert.NotNull(await f.Service().상세Async(item.LedgerId));
    }

    private static 공간공동운영상세Response Ok(Result<공간공동운영상세Response> result)
    { Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(x => x.Message))); return result.Value; }
    private static void Code(string code, Result<공간공동운영상세Response> result)
    { Assert.True(result.IsFailed, "Expected failure: " + code); Assert.Contains(result.Errors, error => error.Metadata.GetValueOrDefault("Code")?.ToString() == code); }

    private sealed class Fixture
    {
        public MemoryStore Store { get; } = new();
        public FakeMode Mode { get; } = new();
        public FakeBridge Bridge { get; } = new();
        public 공간공동운영UseCase Service(string? actor = "owner") => new(Store, new Actor(actor), Mode, new Official생활교류공개지역Source(), TimeProvider.System, Bridge);
        public 공간공동운영조건Dto Terms() => new() { Title = "동네 상온 물품", Purpose = "작은 공간을 함께 활용합니다.", PublicNeighborhoodRegionKey = "region:kr:hjd:1126056500", RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider] };
        public 공간공동운영생성Request Request() => new() { ClientRequestId = Guid.NewGuid(), Terms = Terms(), RoleCode = 공간공동운영역할Codes.SpaceProvider, ConfirmNonBinding = true, PublishProposal = true };
        public 공간공동운영명령Request NewCommand(공간공동운영상세Response item, string action) => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = item.Revision, ExpectedTermsRevision = item.TermsRevision, Action = action, ConfirmNonBinding = true };
        public Task<Result<공간공동운영상세Response>> Command(공간공동운영상세Response item, string actor, string action, string? role = null,
            bool? consent = null, string? name = null, 공간공동운영조건Dto? terms = null, long? termsRevision = null,
            string? target = null, string? cycle = null, 공간공동운영전문참조Dto? reference = null)
        {
            var command = NewCommand(item, action); command.RoleCode = role; command.Consented = consent; command.PublicDisplayName = name;
            command.Terms = terms; command.ExpectedTermsRevision = termsRevision ?? item.TermsRevision; command.TargetUserId = target; command.CycleId = cycle; command.Reference = reference;
            return Service(actor).변경Async(item.LedgerId, command);
        }
        public async Task<공간공동운영상세Response> ReadyOwner()
        {
            var item = Ok(await Service().생성Async(Request()));
            item = Ok(await Command(item, "owner", 공간공동운영명령Codes.AcceptRole));
            item = Ok(await Command(item, "owner", 공간공동운영명령Codes.AgreeTerms, consent: true));
            return Ok(await Command(item, "owner", 공간공동운영명령Codes.AcceptOperator, consent: true));
        }
        public async Task<공간공동운영상세Response> PreparedCycle()
        {
            var item = await ReadyOwner(); item = Ok(await Command(item, "owner", 공간공동운영명령Codes.CreateCycle));
            foreach (var kind in new[] { "DistributedSite", "StorageAgreement", "InboundPlan", "DistributedOrder", "RootOrderLedger" })
                item = Ok(await Command(item, "owner", 공간공동운영명령Codes.LinkReference, cycle: item.Cycles.Single().CycleId,
                    reference: new() { KindCode = kind, ReferenceId = kind + "-actual", ExpectedRevision = 1 }));
            return item;
        }
    }

    private sealed record Actor(string? UserId) : ICurrentUserAccessor { public string? Role => "community"; }
    private sealed class FakeMode : ISsalddelExecutionModePolicy
    {
        public SsalddelExecutionMode Mode { get; set; } = SsalddelExecutionMode.Operational;
        public bool IsSimulation => Mode == SsalddelExecutionMode.Simulation;
        public bool IsOperational => !IsSimulation;
    }
    private sealed class FakeBridge : I공간공동운영전문업무Bridge
    {
        public bool Verify { get; set; } = true;
        public bool Complete { get; set; }
        public long Revision { get; set; } = 1;
        public bool SpaceRevoked { get; set; }
        public Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId, IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영원천상태Dto>>(references.Select(item => new 공간공동운영원천상태Dto
            { KindCode = item.KindCode, ReferenceId = item.ReferenceId, Verified = Verify, VerifiedRevision = Complete ? Math.Max(2, Revision) : Revision,
                RequiresAttention = SpaceRevoked && item.KindCode is "DistributedSite" or "StorageAgreement",
                Completed = item.KindCode switch { "DistributedSite" or "StorageAgreement" => !SpaceRevoked, "RootOrderLedger" => false, _ => Complete } }).ToArray());
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId, IReadOnlyList<공간공동운영전문참조Dto> currentRefs, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영연결후보Dto>>([]);
    }
    private sealed class MemoryStore : I공간공동운영Store
    {
        public Dictionary<string, 공간공동운영Record> Rows { get; } = new(StringComparer.Ordinal);
        private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        public Task<공간공동운영Record?> 조회Async(string ledgerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Rows.TryGetValue(ledgerId, out var row) ? Copy(row) : null);
        public Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영Record>>(Rows.Values.Skip(skip).Take(take).Select(Copy).ToArray());
        public Task<bool> 생성Async(공간공동운영Record record, CancellationToken cancellationToken = default)
            => Task.FromResult(Rows.TryAdd(record.LedgerId, Copy(record)));
        public Task<bool> 교체Async(공간공동운영Record record, long expectedRevision, string actorUserId, CancellationToken cancellationToken = default)
        {
            if (!Rows.TryGetValue(record.LedgerId, out var prior) || prior.Revision != expectedRevision) return Task.FromResult(false);
            Assert.Equal(expectedRevision + 1, record.Revision); Rows[record.LedgerId] = Copy(record); return Task.FromResult(true);
        }
    }
}
