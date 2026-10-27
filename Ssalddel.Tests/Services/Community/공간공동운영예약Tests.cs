using System.Text.Json;
using FluentResults;
using Ssalddel.Application.CommandProcessing;
using Ssalddel.Contracts.Common.Community;
using Ssalddel.Services.Community;
using 살뜰.Services.Options;

namespace Ssalddel.Tests.Services.Community;

public sealed class 공간공동운영예약Tests
{
    [Fact]
    public async Task 제안과_양측의_시간약속수락은_별개이며_실근무와_금전권한을_만들지_않는다()
    {
        var f = new Fixture(); var row = await f.Propose();
        Assert.Equal("proposed", row.StatusCode); Assert.All(row.Parties, party => Assert.Equal("invited", party.PartyStateCode));
        row = Ok(await f.Command("producer", row, "accept"));
        Assert.Equal("proposed", row.StatusCode);
        row = Ok(await f.Command("customer", row, "accept"));
        Assert.Equal("confirmed", row.StatusCode); Assert.All(row.Parties, party => Assert.Equal("accepted", party.PartyStateCode));
        Assert.True(row.NonBinding); Assert.False(row.GrantsHrAuthority); Assert.False(row.WorkCompleted);
        Assert.False(row.InventoryReserved); Assert.False(row.FinancialObligationConfirmed); Assert.False(row.ExternalSchedulesVerified);
        Assert.False(row.ProfessionalReviewReady); Assert.Contains(row.ReadinessBlockers, value => value.Contains("연결한 전문 업무가 없어", StringComparison.Ordinal));
        Assert.Equal("this-integrated-ledger", row.ConflictScope);
        Assert.Equal("original-order-extension", f.Store.Current.OrderExtensionJson);
        var list = await f.Service("customer").목록Async("hub");
        Assert.Single(list.Items); Assert.Equal(3, list.Notices.Count); Assert.Equal(3, row.History.Count);
        Assert.Equal(3, f.Store.Writes); Assert.Empty(f.Bridge.Requests);
    }

    [Fact]
    public async Task 익명과_외부인은_저장하지_못하며_운영담당도_다른당사자를_대신수락하거나취소하지_못한다()
    {
        var f = new Fixture(); var row = await f.Propose();
        Code("SpaceBookingAuthenticationRequired", await f.Service(null).변경Async("hub", row.BookingId, f.Request(row, "accept")));
        Code("SpaceBookingCooperationNotFound", await f.Service("stranger").변경Async("hub", row.BookingId, f.Request(row, "accept")));
        var operatorView = await f.Service("operator").조회Async("hub", row.BookingId);
        Assert.NotNull(operatorView); Assert.Empty(operatorView.AllowedActions);
        var preview = await f.Service("operator").취소영향Async("hub", row.BookingId); Assert.False(preview.CanCancel);
        var cancel = f.Request(row, "cancel"); cancel.ImpactDigest = preview.Digest;
        Code("SpaceBookingPartyRequired", await f.Service("operator").변경Async("hub", row.BookingId, cancel));
        Assert.Equal(1, f.Store.Writes);
    }

    [Fact]
    public async Task 같은원장의_비당사자는_다른약속과_알림과_영수증을_보지_못한다()
    {
        var f = new Fixture(); var request = f.Proposal(); var id = Guid.NewGuid();
        Ok(await f.Service("producer").변경Async("hub", id, request));
        var outsider = await f.Service("other").목록Async("hub");
        Assert.Empty(outsider.Items); Assert.Empty(outsider.Notices);
        Assert.Null(await f.Service("other").조회Async("hub", id));
        Assert.Null(await f.Service("customer").요청결과Async("hub", request.ClientRequestId));
        Assert.NotNull(await f.Service("producer").요청결과Async("hub", request.ClientRequestId));
        Assert.Equal(1, f.Store.Writes);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("capacity")]
    [InlineData("past")]
    [InlineData("offset")]
    [InlineData("role")]
    [InlineData("unknown-user")]
    [InlineData("actor-missing")]
    public async Task 잘못된_정원_당사자_역할_시간은_제안에_저장하지_않는다(string invalid)
    {
        var f = new Fixture(); var request = f.Proposal(); var terms = request.Terms!;
        switch (invalid)
        {
            case "duplicate": terms.Parties = [terms.Parties[0], terms.Parties[0]]; break;
            case "capacity": terms.Capacity = 1; break;
            case "past": terms.StartsAtUtc = f.Clock.Now.AddHours(-1); break;
            case "offset": terms.StartsAtUtc = terms.StartsAtUtc.ToOffset(TimeSpan.FromHours(9)); break;
            case "role": terms.Parties[1].RoleCode = "invented-role"; break;
            case "unknown-user": terms.Parties[1].UserId = "unregistered"; break;
            case "actor-missing": terms.Parties = [new() { UserId = "customer", RoleCode = 공간공동운영역할Codes.Resident }, new() { UserId = "other", RoleCode = 공간공동운영역할Codes.Picker }]; break;
        }
        Assert.True((await f.Service("producer").변경Async("hub", Guid.NewGuid(), request)).IsFailed);
        Assert.Equal(0, f.Store.Writes); Assert.Null(f.Store.Current.WorkReservationExtensionJson);
    }

    [Fact]
    public async Task 종류별_고정역할쌍을_강제하지_않되_각당사자의_현역할과_동의는_검사한다()
    {
        var f = new Fixture(); var request = f.Proposal(); request.Terms!.KindCode = "inbound";
        request.Terms.Parties = [new() { UserId = "producer", RoleCode = 공간공동운영역할Codes.Supplier },
            new() { UserId = "operator", RoleCode = 공간공동운영역할Codes.SpaceProvider }];
        Assert.True((await f.Service("producer").변경Async("hub", Guid.NewGuid(), request)).IsSuccess);
        f = new(); f.Store.Current.Participants.Single(p => p.UserId == "customer").AgreedTermsRevision = 0;
        Code("SpaceBookingParticipantsChanged", await f.Service("producer").변경Async("hub", Guid.NewGuid(), f.Proposal()));
        Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task 회차필수약속은_운영담당자만_본인이_당사자로_참여해_제안한다()
    {
        var f = new Fixture();
        Code("SpaceBookingRequiredOperatorOnly", await f.Service("producer").변경Async("hub", Guid.NewGuid(), f.Proposal(requiredForCycle: true)));
        var request = f.Proposal(); request.Terms!.RequiredForCycle = true;
        Code("SpaceBookingTermsInvalid", await f.Service("operator").변경Async("hub", Guid.NewGuid(), request));
        Assert.Equal(0, f.Store.Writes);
        var row = await f.Propose(withReference: true, requiredForCycle: true);
        Assert.True(row.Terms.RequiredForCycle); Assert.Contains(row.Parties, p => p.UserId == "operator");
    }

    [Theory]
    [InlineData("proposed")]
    [InlineData("declined")]
    [InlineData("cancelled")]
    public async Task 일반참여자의_선택시간약속_제안거절취소는_회차시작을_막지않는다(string state)
    {
        var f = new Fixture(); var row = await f.Propose();
        if (state == "declined") row = Ok(await f.Command("customer", row, "decline"));
        else if (state == "cancelled") row = await f.Cancel(row);
        Assert.Equal(state, row.StatusCode); Assert.False(row.Terms.RequiredForCycle);
        Assert.Empty(f.Blockers()); Assert.True((await f.StartCycle()).IsSuccess);
    }

    [Fact]
    public async Task 수락한_시간은_같은원장의_다른회차에도_겹쳐예약하지_못한다()
    {
        var f = new Fixture(); var first = await f.Propose(); first = Ok(await f.Command("producer", first, "accept"));
        var next = f.Proposal(); next.Terms!.CycleId = "cycle-two";
        Code("SpaceBookingTimeConflict", await f.Service("producer").변경Async("hub", Guid.NewGuid(), next));
        Assert.Equal(2, f.Store.Writes);
        next = f.Proposal(); next.Terms!.CycleId = "cycle-two";
        next.Terms.StartsAtUtc = first.Terms.EndsAtUtc; next.Terms.EndsAtUtc = first.Terms.EndsAtUtc.AddHours(1);
        Assert.True((await f.Service("producer").변경Async("hub", Guid.NewGuid(), next)).IsSuccess);
    }

    [Fact]
    public async Task 동시에_다른약속을_수락하면_부모CAS로_한번만_확정되고_정원을_넘기지_않는다()
    {
        var f = new Fixture(); var first = await f.Propose(); var second = await f.Propose();
        var one = f.Request(first, "accept"); var two = f.Request(second, "accept");
        f.Store.RaceNextWrites = 2;
        var results = await Task.WhenAll(f.Service("producer").변경Async("hub", first.BookingId, one),
            f.Service("producer").변경Async("hub", second.BookingId, two));
        Assert.Single(results, result => result.IsSuccess);
        Code("SpaceBookingRevisionConflict", Assert.Single(results, result => result.IsFailed));
        var list = await f.Service("producer").목록Async("hub");
        Assert.Single(list.Items.Where(item => item.Parties.Any(p => p.IsMe && p.PartyStateCode == "accepted")));
        Assert.Equal(3, f.Store.Writes);
    }

    [Fact]
    public async Task 같은요청은_한번만_저장하고_내용을바꾼_재사용은_거절한다()
    {
        var f = new Fixture(); var id = Guid.NewGuid(); var request = f.Proposal();
        var first = Ok(await f.Service("producer").변경Async("hub", id, request));
        var replay = Ok(await f.Service("producer").변경Async("hub", id, request));
        Assert.True(replay.Replayed); Assert.Equal(first.BookingId, replay.BookingId); Assert.Equal(1, f.Store.Writes);
        request.Terms!.EndsAtUtc = request.Terms.EndsAtUtc.AddMinutes(10);
        Code("SpaceBookingIdempotencyConflict", await f.Service("producer").변경Async("hub", id, request));
        Code("SpaceBookingIdempotencyConflict", await f.Service("producer").변경Async("hub", Guid.NewGuid(), request));
        Assert.Equal(1, f.Store.Writes);
    }

    [Fact]
    public async Task 역할철회와_조건변경은_수락과_회차시작을_다시_보류시킨다()
    {
        var f = new Fixture(); var row = await f.Propose(requiredForCycle: true);
        f.Store.Current.Participants.Single(p => p.UserId == "customer").StateCode = "withdrawn";
        Code("SpaceBookingParticipantsChanged", await f.Command("producer", row, "accept"));
        Assert.NotEmpty(공간공동운영예약UseCase.회차준비보류사유(f.Store.Current, "cycle", f.Clock.Now));
        f.Store.Current.Participants.Single(p => p.UserId == "customer").StateCode = "accepted";
        f.Store.Current.TermsRevision++;
        var changed = await f.Service("producer").조회Async("hub", row.BookingId);
        Assert.False(changed!.ProfessionalReviewReady); Assert.DoesNotContain("accept", changed.AllowedActions);
        Code("SpaceBookingTermsChanged", await f.Command("producer", row, "accept"));
    }

    [Fact]
    public async Task 기한이_지난_약속은_실행확인으로_바꾸지_않는다()
    {
        var f = new Fixture(); var row = await f.Propose(requiredForCycle: true); f.Clock.Now = row.Terms.EndsAtUtc;
        Code("SpaceBookingWindowExpired", await f.Command("producer", row, "accept"));
        Assert.Contains(공간공동운영예약UseCase.회차준비보류사유(f.Store.Current, "cycle", f.Clock.Now), reason => reason.Contains("만료", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 거절은_본인의_미수락약속만_바꾸고_다른사람의_수락과_보수를_건드리지_않는다()
    {
        var f = new Fixture(); var row = await f.Propose(); row = Ok(await f.Command("producer", row, "accept"));
        row = Ok(await f.Command("customer", row, "decline"));
        Assert.Equal("declined", row.StatusCode);
        Assert.Equal("accepted", row.Parties.Single(p => p.UserId == "producer").PartyStateCode);
        Assert.Equal("declined", row.Parties.Single(p => p.UserId == "customer").PartyStateCode);
        Assert.False(row.FinancialObligationConfirmed); Assert.False(row.WorkCompleted);
        Assert.Equal("original-order-extension", f.Store.Current.OrderExtensionJson);
    }

    [Fact]
    public async Task 취소영향은_조회만으로_저장하지않고_명시확인뒤_본인약속만_취소한다()
    {
        var f = new Fixture(); var row = await f.Confirm(requiredForCycle: true); var writes = f.Store.Writes;
        var impact = await f.Service("customer").취소영향Async("hub", row.BookingId);
        Assert.Equal(writes, f.Store.Writes); Assert.True(impact.CanCancel); Assert.False(impact.CostsVerified);
        Assert.False(impact.ExpectedLossVerified); Assert.False(impact.FinancialObligationConfirmed); Assert.False(impact.ProfessionalMutationPerformed);
        Code("SpaceBookingImpactChanged", await f.Command("customer", row, "cancel"));
        var request = f.Request(row, "cancel"); request.ImpactDigest = impact.Digest;
        row = Ok(await f.Service("customer").변경Async("hub", row.BookingId, request));
        Assert.Equal("cancelled", row.StatusCode);
        Assert.Equal("accepted", row.Parties.Single(p => p.UserId == "producer").PartyStateCode);
        Assert.Equal("cancelled", row.Parties.Single(p => p.UserId == "customer").PartyStateCode);
        Assert.Equal(writes + 1, f.Store.Writes);
        Assert.Contains(공간공동운영예약UseCase.회차준비보류사유(f.Store.Current, "cycle", f.Clock.Now), reason => reason.Contains("자동 대체하지", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 영향미리보기의_부모예약원천판본이_바뀌면_이전digest로_취소하지_못한다()
    {
        var f = new Fixture(); var row = await f.Confirm(withReference: true);
        var impact = await f.Service("producer").취소영향Async("hub", row.BookingId);
        f.Bridge.SourceRevision = 2;
        var request = f.Request(row, "cancel"); request.ImpactDigest = impact.Digest;
        Code("SpaceBookingImpactChanged", await f.Service("producer").변경Async("hub", row.BookingId, request));
        var fresh = await f.Service("producer").취소영향Async("hub", row.BookingId);
        Assert.NotEqual(impact.Digest, fresh.Digest); Assert.True(fresh.ProfessionalCleanupRequired);
        request.ImpactDigest = fresh.Digest;
        row = Ok(await f.Service("producer").변경Async("hub", row.BookingId, request));
        Assert.True(row.ProfessionalCleanupRequired); Assert.Equal("cancelled", row.StatusCode);
        Assert.Equal("original-order-extension", f.Store.Current.OrderExtensionJson);
    }

    [Fact]
    public async Task 취소된필수약속을_같은종류의_새약속으로_자동대체하지_않는다()
    {
        var f = new Fixture(); var old = await f.Confirm(requiredForCycle: true);
        var impact = await f.Service("customer").취소영향Async("hub", old.BookingId);
        var request = f.Request(old, "cancel"); request.ImpactDigest = impact.Digest;
        Ok(await f.Service("customer").변경Async("hub", old.BookingId, request));
        await f.Confirm();
        Assert.Contains(공간공동운영예약UseCase.회차준비보류사유(f.Store.Current, "cycle", f.Clock.Now), reason => reason.Contains("거절·취소", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 명시한_대체약속의_전원수락과_현재전문연결확인후에만_원약속보류를_해제한다()
    {
        var f = new Fixture(); var original = await f.Confirm(withReference: true, requiredForCycle: true);
        original = await f.Cancel(original);
        var proposal = f.Proposal(requiredForCycle: true); proposal.Terms!.ReplacesBookingId = original.BookingId; proposal.Terms.References = [f.Reference()];
        var replacement = Ok(await f.Service("operator").변경Async("hub", Guid.NewGuid(), proposal));
        Assert.Equal(original.BookingId, replacement.Terms.ReplacesBookingId);
        Assert.Contains(f.Blockers(), reason => reason.Contains("거절·취소", StringComparison.Ordinal));
        replacement = Ok(await f.Command("producer", replacement, "accept"));
        Assert.Contains(f.Blockers(), reason => reason.Contains("거절·취소", StringComparison.Ordinal));
        replacement = Ok(await f.Command("customer", replacement, "accept"));
        replacement = Ok(await f.Command("operator", replacement, "accept"));
        Assert.Empty(f.Blockers());
        var prior = await f.Service("producer").조회Async("hub", original.BookingId);
        Assert.Equal("cancelled", prior!.StatusCode); Assert.True(prior.ProfessionalCleanupRequired);
        Assert.Equal("confirmed", replacement.StatusCode); Assert.False(replacement.WorkCompleted);
        Assert.True((await f.StartCycle()).IsSuccess);
    }

    [Theory]
    [InlineData("other-cycle")]
    [InlineData("other-kind")]
    [InlineData("still-open")]
    [InlineData("not-a-party")]
    [InlineData("unknown")]
    [InlineData("required-changed")]
    public async Task 대체는_같은회차종류의_닫힌약속_당사자만_제안할수_있다(string invalid)
    {
        var f = new Fixture(); var original = await f.Confirm();
        if (invalid != "still-open") original = await f.Cancel(original);
        var request = f.Proposal(); request.Terms!.ReplacesBookingId = original.BookingId; var actor = "producer";
        switch (invalid)
        {
            case "other-cycle": request.Terms.CycleId = "cycle-two"; break;
            case "other-kind": request.Terms.KindCode = "inbound"; break;
            case "not-a-party":
                actor = "operator"; request.Terms.Parties = [new() { UserId = "operator", RoleCode = 공간공동운영역할Codes.SpaceProvider },
                    new() { UserId = "customer", RoleCode = 공간공동운영역할Codes.Resident }]; break;
            case "unknown": request.Terms.ReplacesBookingId = Guid.NewGuid(); break;
            case "required-changed":
                actor = "operator"; request = f.Proposal(requiredForCycle: true); request.Terms!.ReplacesBookingId = original.BookingId; break;
        }
        var writes = f.Store.Writes;
        Code(invalid == "not-a-party" ? "SpaceBookingReplacementPartyRequired" : "SpaceBookingReplacementInvalid",
            await f.Service(actor).변경Async("hub", Guid.NewGuid(), request));
        Assert.Equal(writes, f.Store.Writes);
    }

    [Fact]
    public async Task 동시대체제안은_부모CAS로_하나만_저장하고_기존대체가있으면_명시마지막연결을_요구한다()
    {
        var f = new Fixture(); var original = await f.Cancel(await f.Confirm(withReference: true, requiredForCycle: true));
        var first = f.Proposal(requiredForCycle: true); first.Terms!.ReplacesBookingId = original.BookingId; first.Terms.References = [f.Reference()];
        var second = f.Proposal(requiredForCycle: true); second.Terms!.ReplacesBookingId = original.BookingId; second.Terms.References = [f.Reference()];
        f.Store.RaceNextWrites = 2;
        var results = await Task.WhenAll(f.Service("operator").변경Async("hub", Guid.NewGuid(), first),
            f.Service("operator").변경Async("hub", Guid.NewGuid(), second));
        var next = Ok(Assert.Single(results, result => result.IsSuccess));
        Code("SpaceBookingRevisionConflict", Assert.Single(results, result => result.IsFailed));
        var duplicate = f.Proposal(requiredForCycle: true); duplicate.Terms!.ReplacesBookingId = original.BookingId;
        Code("SpaceBookingReplacementAlreadyExists", await f.Service("operator").변경Async("hub", Guid.NewGuid(), duplicate));
        next = Ok(await f.Command("customer", next, "decline"));
        var chain = f.Proposal(requiredForCycle: true); chain.Terms!.ReplacesBookingId = next.BookingId; chain.Terms.References = [f.Reference()];
        var final = Ok(await f.Service("operator").변경Async("hub", Guid.NewGuid(), chain));
        final = Ok(await f.Command("producer", final, "accept")); final = Ok(await f.Command("customer", final, "accept"));
        final = Ok(await f.Command("operator", final, "accept"));
        Assert.Empty(f.Blockers());
        Assert.Equal(3, (await f.Service("producer").목록Async("hub")).Items.Count);
    }

    [Fact]
    public async Task 전문연결없는_대체약속은_전원수락해도_원취소와_실제시작보류를_해제하지않는다()
    {
        var f = new Fixture(); var original = await f.Cancel(await f.Confirm(withReference: true, requiredForCycle: true));
        var request = f.Proposal(requiredForCycle: true); request.Terms!.ReplacesBookingId = original.BookingId;
        var next = Ok(await f.Service("operator").변경Async("hub", Guid.NewGuid(), request));
        next = Ok(await f.Command("producer", next, "accept")); next = Ok(await f.Command("customer", next, "accept"));
        next = Ok(await f.Command("operator", next, "accept"));
        Assert.False(next.ProfessionalReviewReady); Assert.NotEmpty(f.Blockers());
        Code("ReadinessRequired", await f.StartCycle());
    }

    [Fact]
    public async Task 인계받은_운영담당자는_닫힌필수약속만_본인포함_명시대체할수_있다()
    {
        var f = new Fixture(); var original = await f.Cancel(await f.Confirm(withReference: true, requiredForCycle: true));
        f.Store.Current.OperatorUserId = "other";
        f.Store.Current.Participants.Single(p => p.UserId == "other").RoleCode = 공간공동운영역할Codes.Coordinator;
        var request = f.Proposal(requiredForCycle: true); request.Terms!.ReplacesBookingId = original.BookingId;
        request.Terms.Parties = [new() { UserId = "producer", RoleCode = 공간공동운영역할Codes.Supplier },
            new() { UserId = "customer", RoleCode = 공간공동운영역할Codes.Resident },
            new() { UserId = "other", RoleCode = 공간공동운영역할Codes.Coordinator }];
        request.Terms.References = [f.Reference()];
        var next = Ok(await f.Service("other").변경Async("hub", Guid.NewGuid(), request));
        next = Ok(await f.Command("producer", next, "accept")); next = Ok(await f.Command("customer", next, "accept"));
        next = Ok(await f.Command("other", next, "accept"));
        Assert.Empty(f.Blockers()); Assert.True(next.Terms.RequiredForCycle);
        Assert.Equal("cancelled", (await f.Service("producer").조회Async("hub", original.BookingId))!.StatusCode);
        f = new(); original = await f.Confirm(withReference: true, requiredForCycle: true);
        f.Store.Current.OperatorUserId = "other";
        f.Store.Current.Participants.Single(p => p.UserId == "other").RoleCode = 공간공동운영역할Codes.Coordinator;
        request = f.Proposal(requiredForCycle: true); request.Terms!.ReplacesBookingId = original.BookingId;
        request.Terms.Parties = [new() { UserId = "other", RoleCode = 공간공동운영역할Codes.Coordinator },
            new() { UserId = "customer", RoleCode = 공간공동운영역할Codes.Resident }]; request.Terms.Capacity = 2;
        Code("SpaceBookingReplacementInvalid", await f.Service("other").변경Async("hub", Guid.NewGuid(), request));
    }

    [Fact]
    public async Task 회차에없는_임의참조와_권한미확인원천은_제안에서_거절한다()
    {
        var f = new Fixture(); var request = f.Proposal();
        request.Terms!.References = [new() { KindCode = "InboundPlan", ReferenceId = "private-other" }];
        Code("SpaceBookingReferencesInvalid", await f.Service("producer").변경Async("hub", Guid.NewGuid(), request));
        request = f.Proposal(); request.Terms!.References = [f.Reference()]; f.Bridge.Visible = false;
        Code("SpaceBookingSourceChanged", await f.Service("producer").변경Async("hub", Guid.NewGuid(), request));
        Assert.Equal(0, f.Store.Writes);
    }

    [Fact]
    public async Task 수락명령은_최신_원천상태를_다시_읽는다()
    {
        var f = new Fixture(); var row = await f.Propose(withReference: true, requiredForCycle: true); f.Bridge.SourceRevision = 2;
        Code("SpaceBookingSourceChanged", await f.Command("producer", row, "accept"));
        Assert.Equal("invited", (await f.Service("producer").조회Async("hub", row.BookingId))!.Parties.Single(p => p.IsMe).PartyStateCode);
        Assert.NotEmpty(공간공동운영예약UseCase.회차준비보류사유(f.Store.Current, "cycle", f.Clock.Now, f.Bridge.States([f.Reference()])));
    }

    [Fact]
    public async Task 클라이언트가_원천판본을_생략해도_확인판본을_저장하고_회차연결변경시_수락보류한다()
    {
        var f = new Fixture(); var request = f.Proposal(); var reference = f.Reference(); reference.ExpectedRevision = null;
        request.Terms!.References = [reference];
        var row = Ok(await f.Service("producer").변경Async("hub", Guid.NewGuid(), request));
        Assert.Equal(1, Assert.Single(row.Terms.References).ExpectedRevision);
        f.Store.Current.Cycles.Single(c => c.CycleId == "cycle").References = [];
        Code("SpaceBookingReferencesChanged", await f.Command("producer", row, "accept"));
        Assert.False((await f.Service("producer").조회Async("hub", row.BookingId))!.ProfessionalReviewReady);
    }

    [Fact]
    public async Task 후보는_현재수락역할과조건과_회차의_권한있는참조만_보여준다()
    {
        var f = new Fixture(); f.Store.Current.Participants.Single(p => p.UserId == "other").AgreedTermsRevision = 0;
        f.Bridge.SourceRevision = 3;
        var candidates = await f.Service("producer").후보Async("hub");
        Assert.DoesNotContain(candidates.Participants, p => p.UserId == "other");
        Assert.Contains(candidates.Participants, p => p.UserId == "producer" && p.IsMe);
        Assert.All(candidates.Cycles.SelectMany(c => c.References), reference => Assert.Equal(3, reference.Reference.ExpectedRevision));
        Assert.DoesNotContain(candidates.Participants, p => p.DisplayName == p.UserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":2}")]
    [InlineData("{\"bookings\":null}")]
    public async Task 새부록_손상은_빈약속으로_대체하거나_저장하지_않는다(string broken)
    {
        var f = new Fixture(); f.Store.Current.WorkReservationExtensionJson = broken;
        var error = await Assert.ThrowsAsync<공간공동운영예약Exception>(() => f.Service("producer").목록Async("hub"));
        Assert.Equal("SpaceBookingStoredDataInvalid", error.Code); Assert.Equal(0, f.Store.Writes);
        Assert.Equal(broken, f.Store.Current.WorkReservationExtensionJson);
    }

    [Fact]
    public void 예약없는_이전회차는_새관문으로_막지_않는다()
    {
        var f = new Fixture();
        Assert.Empty(공간공동운영예약UseCase.회차준비보류사유(f.Store.Current, "cycle", f.Clock.Now));
    }

    [Fact]
    public async Task 상위회차시작은_미수락약속을_우회하지못하고_모두확인후에만_진행한다()
    {
        var f = new Fixture(); var row = await f.Propose(withReference: true, requiredForCycle: true);
        var before = await f.StartCycle(); Code("ReadinessRequired", before);
        row = Ok(await f.Command("producer", row, "accept")); row = Ok(await f.Command("customer", row, "accept"));
        row = Ok(await f.Command("operator", row, "accept"));
        var after = await f.StartCycle(); Assert.True(after.IsSuccess);
        Assert.Equal("running", Assert.Single(after.Value.Cycles.Where(c => c.CycleId == "cycle")).StatusCode);
    }

    [Fact]
    public async Task 전문업무연결없는_시간합의는_상위회차시작의_준비완료로_승격하지_않는다()
    {
        var f = new Fixture(); await f.Confirm(requiredForCycle: true);
        Assert.Contains(f.Blockers(), reason => reason.Contains("연결한 전문 업무가 없어", StringComparison.Ordinal));
        Code("ReadinessRequired", await f.StartCycle());
    }

    private static 공간공동운영예약Dto Ok(Result<공간공동운영예약Dto> result)
    { Assert.True(result.IsSuccess, string.Join(";", result.Errors.Select(error => error.Message))); return result.Value; }
    private static void Code<T>(string code, Result<T> result)
    {
        Assert.True(result.IsFailed);
        Assert.Contains(result.Errors, error => error.Message == code || error.Metadata.GetValueOrDefault("code") as string == code
            || error.Metadata.GetValueOrDefault("Code") as string == code);
    }

    private sealed class Fixture
    {
        public Store Store { get; } = new();
        public Bridge Bridge { get; } = new();
        public Clock Clock { get; } = new();
        private readonly TestExecutionPolicy mode = new();
        public Fixture()
        {
            Store.Current = new()
            {
                LedgerId = "hub", Revision = 7, TermsRevision = 2, OwnerUserId = "operator", OperatorUserId = "operator", ExecutionMode = "Simulation",
                CreateFingerprint = "created", CreateRequestId = Guid.NewGuid(), OrderExtensionJson = "original-order-extension",
                Terms = new() { RequiredRoleCodes = [공간공동운영역할Codes.SpaceProvider, 공간공동운영역할Codes.Supplier, 공간공동운영역할Codes.Resident] },
                Participants = [Member("operator", 공간공동운영역할Codes.SpaceProvider), Member("producer", 공간공동운영역할Codes.Supplier),
                    Member("customer", 공간공동운영역할Codes.Resident), Member("other", 공간공동운영역할Codes.Picker)],
                Cycles = [new() { CycleId = "cycle", Number = 1, TermsRevision = 2, References =
                    [Reference(), new() { KindCode = "DistributedSite", ReferenceId = "site" }, new() { KindCode = "StorageAgreement", ReferenceId = "agreement" },
                        new() { KindCode = "DistributedOrder", ReferenceId = "order" }] },
                    new() { CycleId = "cycle-two", Number = 2, TermsRevision = 2 }]
            };
        }
        private static 공간공동운영참여Record Member(string user, string role)
            => new() { UserId = user, RoleCode = role, StateCode = "accepted", AgreedTermsRevision = 2 };
        public 공간공동운영전문참조Dto Reference() => new() { KindCode = "InboundPlan", ReferenceId = "inbound", ExpectedRevision = 1 };
        public 공간공동운영예약UseCase Service(string? actor) => new(Store, new Access(Store), new Actor(actor), mode, Bridge, Clock);
        public 공간공동운영예약명령Request Proposal(bool requiredForCycle = false)
            => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Store.Current.Revision, ExpectedTermsRevision = Store.Current.TermsRevision,
                Action = "propose", ConfirmNonBinding = true, Terms = new() { CycleId = "cycle", KindCode = "supply", Capacity = requiredForCycle ? 3 : 2,
                    RequiredForCycle = requiredForCycle,
                    StartsAtUtc = Clock.Now.AddHours(1), EndsAtUtc = Clock.Now.AddHours(2),
                    Parties = requiredForCycle
                        ? [new() { UserId = "producer", RoleCode = 공간공동운영역할Codes.Supplier }, new() { UserId = "customer", RoleCode = 공간공동운영역할Codes.Resident },
                            new() { UserId = "operator", RoleCode = 공간공동운영역할Codes.SpaceProvider }]
                        : [new() { UserId = "producer", RoleCode = 공간공동운영역할Codes.Supplier }, new() { UserId = "customer", RoleCode = 공간공동운영역할Codes.Resident }] } };
        public async Task<공간공동운영예약Dto> Propose(bool withReference = false, bool requiredForCycle = false)
        {
            var request = Proposal(requiredForCycle); if (withReference) request.Terms!.References = [Reference()];
            return Ok(await Service(requiredForCycle ? "operator" : "producer").변경Async("hub", Guid.NewGuid(), request));
        }
        public async Task<공간공동운영예약Dto> Confirm(bool withReference = false, bool requiredForCycle = false)
        {
            var row = await Propose(withReference, requiredForCycle); row = Ok(await Command("producer", row, "accept"));
            row = Ok(await Command("customer", row, "accept")); return requiredForCycle ? Ok(await Command("operator", row, "accept")) : row;
        }
        public async Task<공간공동운영예약Dto> Cancel(공간공동운영예약Dto row)
        {
            var request = Request(row, "cancel"); request.ImpactDigest = (await Service("customer").취소영향Async("hub", row.BookingId)).Digest;
            return Ok(await Service("customer").변경Async("hub", row.BookingId, request));
        }
        public IReadOnlyList<string> Blockers() => 공간공동운영예약UseCase.회차준비보류사유(Store.Current, "cycle", Clock.Now,
            Bridge.States(Store.Current.Cycles.Single(c => c.CycleId == "cycle").References));
        public 공간공동운영예약명령Request Request(공간공동운영예약Dto row, string action)
            => new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Store.Current.Revision,
                ExpectedBookingRevision = row.Revision, ExpectedTermsRevision = Store.Current.TermsRevision, Action = action, ConfirmNonBinding = true };
        public Task<Result<공간공동운영예약Dto>> Command(string actor, 공간공동운영예약Dto row, string action)
            => Service(actor).변경Async("hub", row.BookingId, Request(row, action));
        public Task<Result<공간공동운영상세Response>> StartCycle()
        {
            var service = new 공간공동운영UseCase(Store, new Actor("operator"), mode, new Official생활교류공개지역Source(), Clock, Bridge);
            return service.변경Async("hub", new() { ClientRequestId = Guid.NewGuid(), ExpectedRevision = Store.Current.Revision,
                ExpectedTermsRevision = 2, CycleId = "cycle", Action = 공간공동운영명령Codes.StartCycle });
        }
    }
    private sealed class Actor(string? actor) : ICurrentUserAccessor { public string? UserId => actor; public string? Role => "community"; }
    private sealed class TestExecutionPolicy : ISsalddelExecutionModePolicy
    { public SsalddelExecutionMode Mode => SsalddelExecutionMode.Simulation; public bool IsSimulation => true; public bool IsOperational => false; }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = new(2026, 10, 24, 3, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Access(Store store) : I공간공동운영연결Query
    {
        public async Task<공간공동운영Record?> 조회Async(string ledgerId, string actorUserId, CancellationToken cancellationToken = default)
        { var record = await store.조회Async(ledgerId, cancellationToken); return record is not null && (record.OwnerUserId == actorUserId || record.Participants.Any(p => p.UserId == actorUserId)) ? record : null; }
    }
    private sealed class Bridge : I공간공동운영전문업무Bridge
    {
        public long SourceRevision = 1;
        public bool Visible = true;
        public List<IReadOnlyList<공간공동운영전문참조Dto>> Requests { get; } = [];
        public IReadOnlyList<공간공동운영원천상태Dto> States(IReadOnlyList<공간공동운영전문참조Dto> refs)
            => refs.Select(reference => new 공간공동운영원천상태Dto { KindCode = reference.KindCode, ReferenceId = reference.ReferenceId,
                Label = "전문 업무", Verified = Visible, VerifiedRevision = SourceRevision,
                Completed = reference.KindCode is "DistributedSite" or "StorageAgreement", StatusCode = "Active" }).ToArray();
        public Task<IReadOnlyList<공간공동운영원천상태Dto>> ValidateAsync(string actorUserId, IReadOnlyList<공간공동운영전문참조Dto> refs, CancellationToken cancellationToken = default)
        { Requests.Add(refs); return Task.FromResult(States(refs)); }
        public Task<IReadOnlyList<공간공동운영연결후보Dto>> CandidatesAsync(string actorUserId, IReadOnlyList<공간공동운영전문참조Dto> refs, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영연결후보Dto>>([]);
    }
    private sealed class Store : I공간공동운영Store
    {
        public 공간공동운영Record Current = new();
        public int Writes;
        public int RaceNextWrites;
        private readonly object gate = new();
        private TaskCompletionSource? race;
        private static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
        public Task<공간공동운영Record?> 조회Async(string ledgerId, CancellationToken cancellationToken = default)
        { lock (gate) return Task.FromResult(ledgerId == Current.LedgerId ? Copy(Current) : null); }
        public Task<IReadOnlyList<공간공동운영Record>> 목록Async(int skip = 0, int take = 200, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<공간공동운영Record>>(skip == 0 ? [Copy(Current)] : []);
        public Task<bool> 생성Async(공간공동운영Record record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<bool> 교체Async(공간공동운영Record record, long expectedRevision, string actorUserId, CancellationToken cancellationToken = default)
        {
            Task? wait = null;
            lock (gate)
            {
                if (RaceNextWrites > 0)
                { race ??= new(TaskCreationOptions.RunContinuationsAsynchronously); wait = race.Task; if (--RaceNextWrites == 0) race.TrySetResult(); }
            }
            if (wait is not null) await wait.WaitAsync(cancellationToken);
            lock (gate)
            { if (Current.Revision != expectedRevision) return false; Current = Copy(record); Writes++; return true; }
        }
    }
}
