using System.Text.Json;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

/// <summary>기존 UseCase의 준비 판정과 구조화된 조회 근거를 대조합니다. DB·전문 업무·단말 실행 시험은 아닙니다.</summary>
public sealed class 공간공동운영준비근거Tests
{
    [Fact]
    public async Task 실제로점검한_회차판본과시각을_붙이고_준비된_기존판정은_유지한다()
    {
        var f = new Fixture(); var readiness = (await f.Read()).Readiness;
        Assert.Equal("current-cycle", readiness.CycleId); Assert.Equal(2, readiness.CycleTermsRevision);
        Assert.Equal(f.Clock.GetUtcNow(), readiness.AsOfUtc);
        Assert.True(readiness.CanStart); Assert.False(readiness.CanComplete);
        Assert.Empty(readiness.Blockers); Assert.Empty(readiness.Issues); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task 이전회차나_큰회차번호로_점검대상을_자동치환하지않는다()
    {
        var f = new Fixture();
        var previous = new 공간공동운영회차Dto { CycleId = "previous-cycle", Number = 1, TermsRevision = 1,
            StatusCode = "completed", References = f.Store.Current.Cycles[0].References };
        f.Store.Current.Cycles.Add(previous);
        var response = await f.Read();
        Assert.Equal("previous-cycle", response.Readiness.CycleId); Assert.Equal(1, response.Readiness.CycleTermsRevision);
        Assert.False(response.Readiness.CanStart); Assert.False(response.Readiness.CanComplete);
        Assert.Equal("CycleTermsMismatch", Assert.Single(response.Readiness.Issues).Code);
        Assert.Contains(response.SharedOutcomes!.Cycles, cycle => cycle.CycleId == "current-cycle" && cycle.TermsRevision == 2);
        Assert.DoesNotContain(response.Readiness.Issues, issue => issue.StageKeys.Length > 0);
        Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task 회차가없으면_빈회차와0판본을_명시하고_다른회차근거를_만들지않는다()
    {
        var f = new Fixture(); f.Store.Current.Cycles.Clear();
        var readiness = (await f.Read()).Readiness;
        Assert.Equal("", readiness.CycleId); Assert.Equal(0, readiness.CycleTermsRevision);
        Assert.Equal(f.Clock.GetUtcNow(), readiness.AsOfUtc);
        Assert.Contains(readiness.Issues, issue => issue.Code == "CycleTermsMismatch" && issue.ScopeCode == "cycle");
        Assert.False(readiness.CanStart); Assert.False(readiness.CanComplete);
        Assert.Equal(readiness.Blockers, readiness.Issues.Select(issue => issue.Message));
    }

    [Fact]
    public async Task 필수역할_수락부족은_모임범위와역할만_알리고_실제단계담당부족으로_추정하지않는다()
    {
        var f = new Fixture(); f.Store.Current.Terms.RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider, 공간공동운영역할Codes.Driver];
        var readiness = (await f.Read()).Readiness; var issue = Assert.Single(readiness.Issues);
        Assert.Equal("RequiredRoleAcceptanceMissing", issue.Code); Assert.Equal("cooperation", issue.ScopeCode);
        Assert.Equal(new[] { 공간공동운영역할Codes.Driver }, issue.RoleCodes); Assert.Empty(issue.StageKeys);
        Assert.Equal("필수 역할의 본인 수락이 필요합니다.", issue.Message);
        Assert.False(readiness.CanStart); Assert.False(readiness.CanComplete);
        Assert.Equal(readiness.Blockers, readiness.Issues.Select(value => value.Message));
    }

    [Fact]
    public async Task 과거자료에_알수없는역할이있어도_기존보류는유지하되_새역할코드를_만들지않는다()
    {
        var f = new Fixture(); f.Store.Current.Terms.RequiredRoleCodes = ["legacy-unknown-role"];
        var readiness = (await f.Read()).Readiness; var issue = Assert.Single(readiness.Issues);
        Assert.Equal("RequiredRoleAcceptanceMissing", issue.Code); Assert.Empty(issue.RoleCodes); Assert.Empty(issue.StageKeys);
        Assert.Equal("필수 역할의 본인 수락이 필요합니다.", Assert.Single(readiness.Blockers)); Assert.False(readiness.CanStart);
    }

    [Fact]
    public async Task 기존필수원천_부재의문구와순서를_유지하고_입증된단계만_지정한다()
    {
        var f = new Fixture(); f.Store.Current.Cycles[0].References = [];
        var readiness = (await f.Read()).Readiness;
        Assert.Equal(new[] { "유효한 공간 거점 연결이 필요합니다.", "현재 보관 합의 연결이 필요합니다.", "실제 입고 계획 연결이 필요합니다.",
            "공간 거점의 현재 사용 동의를 확인해 주세요.", "보관 합의의 양측 동의와 유효 기간을 확인해 주세요.", "현재 주문 원장 연결이 필요합니다." }, readiness.Blockers);
        Assert.Equal(new[] { "DistributedSiteReferenceRequired", "StorageAgreementReferenceRequired", "InboundPlanReferenceRequired",
            "DistributedSiteConsentRequired", "StorageAgreementConsentOrValidityRequired", "OrderReferenceRequired" }, readiness.Issues.Select(issue => issue.Code));
        Assert.Equal(new[] { "storage", "storage", "inbound", "storage", "storage", "order" }, readiness.Issues.Select(issue => Assert.Single(issue.StageKeys)));
        Assert.All(readiness.Issues, issue => { Assert.Equal("source", issue.ScopeCode); Assert.Empty(issue.RoleCodes); });
        Assert.False(readiness.CanStart); Assert.False(readiness.CanComplete);
        Assert.Equal(readiness.Blockers, readiness.Issues.Select(issue => issue.Message));
    }

    [Fact]
    public async Task 모임권한조건모드중단_원천관계문제를_문구순서그대로_회차전체근거로_보존한다()
    {
        var f = new Fixture(); f.Store.Current.ExecutionMode = "Operational"; f.Store.Current.StatusCode = "paused";
        f.Store.Current.OperatorUserId = null; f.Store.Current.Participants[0].AgreedTermsRevision = 1;
        f.Store.Current.Terms.RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider, 공간공동운영역할Codes.Driver];
        f.Store.Current.Cycles[0].TermsRevision = 1; f.Bridge.Verify = false;
        var readiness = (await f.Read()).Readiness;
        Assert.Equal(new[] { "현재 실행 모드와 제안의 모드가 다릅니다.", "모임이 중지 또는 종료되었습니다.",
            "공간 제공자의 운영 담당 수락 또는 쌍방 인계 수락이 필요합니다.", "필수 역할의 본인 수락이 필요합니다.",
            "역할을 수락한 참여자의 현재 조건 동의가 필요합니다.", "현재 조건 판본의 회차를 준비해 주세요.",
            "전문 원장의 현재 권한·판본·관계를 확인해야 합니다." }, readiness.Blockers.Take(7));
        Assert.Equal(new[] { "ExecutionModeMismatch", "CooperationStopped", "OperatorAcceptanceRequired", "RequiredRoleAcceptanceMissing",
            "CurrentTermsAgreementRequired", "CycleTermsMismatch", "ProfessionalSourcesUnverified" }, readiness.Issues.Take(7).Select(issue => issue.Code));
        Assert.All(readiness.Issues.Take(7), issue => Assert.Empty(issue.StageKeys));
        Assert.Equal(readiness.Blockers, readiness.Issues.Select(issue => issue.Message));
        Assert.All(readiness.Issues.SelectMany(issue => issue.StageKeys), key => Assert.True(공간공동운영업무Routes.IsKnownStage(key)));
        Assert.All(readiness.Issues.SelectMany(issue => issue.RoleCodes), code => Assert.True(공간공동운영역할Codes.IsKnown(code)));
        Assert.False(readiness.CanStart); Assert.False(readiness.CanComplete); Assert.Equal(0, f.Store.Writes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task 필수약속만_기존시작관문에따라_회차전체준비근거로_표시한다(bool required)
    {
        var f = new Fixture(); f.AddAppointment(required);
        var readiness = (await f.Read()).Readiness;
        Assert.Equal(!required, readiness.CanStart); Assert.False(readiness.CanComplete);
        if (required)
        {
            var issue = Assert.Single(readiness.Issues);
            Assert.Equal("RequiredAppointmentReviewRequired", issue.Code); Assert.Equal("required-appointment", issue.ScopeCode);
            Assert.Empty(issue.StageKeys); Assert.Empty(issue.RoleCodes);
            Assert.Equal("이 회차 시간 약속의 필수 당사자 수락이 모두 필요합니다.", issue.Message);
        }
        else { Assert.Empty(readiness.Issues); Assert.Empty(readiness.Blockers); }
        Assert.Equal(readiness.Blockers, readiness.Issues.Select(issue => issue.Message)); Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task 시작준비와_이미진행중인_실물결과완료의_기존판정은_합치지않는다()
    {
        var f = new Fixture(); f.Store.Current.Cycles[0].StatusCode = "running"; f.Bridge.PhysicalCompleted = true;
        var before = (await f.Read()).Readiness;
        Assert.False(before.CanStart); Assert.True(before.CanComplete); Assert.Empty(before.Issues);
        f.Store.Current.StatusCode = "paused"; f.Bridge.SpaceAvailable = false;
        var after = (await f.Read()).Readiness;
        Assert.False(after.CanStart); Assert.True(after.CanComplete);
        Assert.Contains(after.Issues, issue => issue.Code == "CooperationStopped" && issue.StageKeys.Length == 0);
        Assert.Contains(after.Issues, issue => issue.Code == "DistributedSiteConsentRequired" && issue.StageKeys.SequenceEqual(new[] { "storage" }));
        Assert.Equal(after.Blockers, after.Issues.Select(issue => issue.Message)); Assert.Equal(0, f.Store.Writes);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Bridge Bridge { get; } = new();
        public Clock Clock { get; } = new();
        public Fixture()
        {
            Store.Current = new() { LedgerId = "hub", Revision = 4, TermsRevision = 2,
                OwnerUserId = "owner", OperatorUserId = "owner", ExecutionMode = "Simulation",
                CreateRequestId = Guid.NewGuid(), CreateFingerprint = "creation",
                Terms = new() { RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider] },
                Participants = [new() { UserId = "owner", RoleCode = 공간공동운영역할Codes.SpaceProvider, StateCode = "accepted", AgreedTermsRevision = 2 }],
                Cycles = [new() { CycleId = "current-cycle", Number = 2, TermsRevision = 2, References =
                    [Ref("DistributedSite"), Ref("StorageAgreement"), Ref("InboundPlan"), Ref("DistributedOrder")] }] };
        }
        private static 공간공동운영전문참조Dto Ref(string kind) => new() { KindCode = kind, ReferenceId = kind + "-source", ExpectedRevision = 1 };
        public 공간공동운영UseCase Service() => new(Store, new Actor(), new TestExecutionPolicy(), new Official생활교류공개지역Source(), Clock, Bridge);
        public async Task<공간공동운영상세Response> Read() => Assert.IsType<공간공동운영상세Response>(await Service().상세Async("hub"));
        public void AddAppointment(bool required)
        {
            Store.Current.Participants.Add(new() { UserId = "worker", RoleCode = 공간공동운영역할Codes.InboundInspector, StateCode = "accepted", AgreedTermsRevision = 2 });
            var terms = new 공간공동운영예약조건Dto { CycleId = "current-cycle", KindCode = "inbound", RequiredForCycle = required,
                Capacity = 2, StartsAtUtc = Clock.GetUtcNow().AddHours(1), EndsAtUtc = Clock.GetUtcNow().AddHours(2), References = [Ref("InboundPlan")],
                Parties = [new() { UserId = "owner", RoleCode = 공간공동운영역할Codes.SpaceProvider }, new() { UserId = "worker", RoleCode = 공간공동운영역할Codes.InboundInspector }] };
            var extension = new 공간공동운영예약UseCase.ReservationExtension
            {
                Bookings = [new() { BookingId = Guid.NewGuid(), Revision = 1, CooperationTermsRevision = 2, CreatorUserId = "owner", Terms = terms,
                    UpdatedAtUtc = Clock.GetUtcNow(), Parties = terms.Parties.Select(p => new 공간공동운영예약UseCase.BookingParty { UserId = p.UserId, RoleCode = p.RoleCode }).ToList() }]
            };
            Store.Current.WorkReservationExtensionJson = JsonSerializer.Serialize(extension, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
    }
    private sealed class Actor : ICurrentUserAccessor { public string? UserId => "owner"; public string? Role => "community"; }
    private sealed class TestExecutionPolicy : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Clock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 24, 3, 0, 0, TimeSpan.Zero); }
    private sealed class Bridge : I공간공동운영전문업무Bridge
    {
        public bool Verify = true, PhysicalCompleted, SpaceAvailable = true;
        public Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId,
            IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영원천상태Dto>>(references.Select(reference => new 공간공동운영원천상태Dto
            { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId, Verified = Verify, VerifiedRevision = Verify ? 1 : null,
                RequiresAttention = !SpaceAvailable && reference.KindCode is "DistributedSite" or "StorageAgreement",
                Completed = reference.KindCode is "DistributedSite" or "StorageAgreement" ? SpaceAvailable : PhysicalCompleted }).ToArray());
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId,
            IReadOnlyList<공간공동운영전문참조Dto> references, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영연결후보Dto>>([]);
    }
    private sealed class Store : I공간공동운영Store
    {
        public 공간공동운영Record Current = new();
        public int Writes;
        public Task<공간공동운영Record?> 조회Async(string ledgerId, CancellationToken cancellationToken = default)
            => Task.FromResult(ledgerId == Current.LedgerId ? JsonSerializer.Deserialize<공간공동운영Record>(JsonSerializer.Serialize(Current)) : null);
        public Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> 생성Async(공간공동운영Record record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> 교체Async(공간공동운영Record record, long expectedRevision, string actorUserId, CancellationToken cancellationToken = default)
        { Writes++; throw new NotSupportedException("조회 근거 시험에서는 저장하지 않습니다."); }
    }
}
